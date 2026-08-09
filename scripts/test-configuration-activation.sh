#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
plugin_id="04920eee-c499-4b13-890f-7af0175f28f0"
activation_url="${server_url}/CollectionTagSync/Configuration"
plugin_configuration_url="${server_url}/Plugins/${plugin_id}/Configuration"

if [[ ! -f "${token_file}" ]]; then
    printf 'Missing test-server token. Run scripts/configure-test-server.sh first.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"
source_tag="Activation-Source-$(date -u +'%Y%m%d%H%M%S%N')"
danger_tag="Activation-Danger-$(date -u +'%Y%m%d%H%M%S%N')"
movie_id=''
series_id=''
movie_original_tags=''
series_original_tags=''
original_configuration=''
response_body=''
response_code=''

api_get() {
    curl --fail --silent --header "X-Emby-Token: ${access_token}" "$1"
}

api_post_json() {
    curl --fail --silent --request POST \
        --header "X-Emby-Token: ${access_token}" \
        --header 'Content-Type: application/json' \
        --data "$2" \
        "$1"
}

request_with_status() {
    local method="$1"
    local url="$2"
    local body="${3:-}"
    local authenticated="${4:-true}"
    local -a arguments=(--silent --show-error --request "${method}" --write-out $'\n%{http_code}')
    if [[ "${authenticated}" == "true" ]]; then
        arguments+=(--header "X-Emby-Token: ${access_token}")
    fi

    if [[ -n "${body}" ]]; then
        arguments+=(--header 'Content-Type: application/json' --data "${body}")
    fi

    response="$(curl "${arguments[@]}" "${url}")"
    response_code="${response##*$'\n'}"
    response_body="${response%$'\n'*}"
}

set_item_tags() {
    local item_id="$1"
    local tags="$2"
    local item
    local updated
    item="$(api_get "${server_url}/Items/${item_id}")"
    updated="$(jq --argjson tags "${tags}" '.Tags = $tags' <<<"${item}")"
    api_post_json "${server_url}/Items/${item_id}" "${updated}"
}

restart_server() {
    docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" \
        restart jellyfin >/dev/null
    for _ in {1..45}; do
        health="$(docker inspect collection-tag-sync-jellyfin \
            --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}')"
        if [[ "${health}" == "healthy" ]]; then
            return
        fi

        sleep 1
    done

    printf 'Jellyfin did not become healthy after configuration persistence restart.\n' >&2
    exit 8
}

restore_test_state() {
    set +e
    api_post_json \
        "${activation_url}" \
        '{"SchemaVersion":1,"MappingGroups":[]}' >/dev/null
    if [[ -n "${movie_id}" && -n "${movie_original_tags}" ]]; then
        set_item_tags "${movie_id}" "${movie_original_tags}" >/dev/null
    fi

    if [[ -n "${series_id}" && -n "${series_original_tags}" ]]; then
        set_item_tags "${series_id}" "${series_original_tags}" >/dev/null
    fi

}

trap restore_test_state EXIT

original_configuration="$(api_get "${plugin_configuration_url}")"
if ! jq --exit-status '(.MappingGroups // []) | length == 0' <<<"${original_configuration}" >/dev/null; then
    printf 'Configuration activation contract requires the isolated server to start with no mappings.\n' >&2
    exit 2
fi

starting_revision="$(jq --raw-output '.Revision // 0' <<<"${original_configuration}")"
items="$(api_get "${server_url}/Items?Recursive=true&IncludeItemTypes=Movie,Series&Fields=Tags")"
movie_id="$(jq --raw-output '.Items[] | select(.Type == "Movie") | .Id' <<<"${items}" | head -n 1)"
series_id="$(jq --raw-output '.Items[] | select(.Type == "Series") | .Id' <<<"${items}" | head -n 1)"
movie_original_tags="$(jq --compact-output --arg item_id "${movie_id}" \
    '.Items[] | select(.Id == $item_id) | (.Tags // [])' <<<"${items}")"
series_original_tags="$(jq --compact-output --arg item_id "${series_id}" \
    '.Items[] | select(.Id == $item_id) | (.Tags // [])' <<<"${items}")"

collection_name="Activation Target $(date -u +'%Y%m%d%H%M%S%N')"
collection_id="$(curl --fail --silent --get --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --data-urlencode "name=${collection_name}" \
    "${server_url}/Collections" | jq --raw-output .Id)"
movie_source_tags="$(jq --arg tag "${source_tag}" '. + [$tag] | unique' <<<"${movie_original_tags}")"
set_item_tags "${movie_id}" "${movie_source_tags}"

valid_candidate="$(jq --null-input \
    --arg source_tag "${source_tag}" \
    --arg collection_id "${collection_id}" \
    --arg collection_name "${collection_name}" \
    '{
        SchemaVersion: 1,
        Revision: 999,
        MappingGroups: [
            {
                Target: {Kind: 1, CollectionId: $collection_id, CollectionDisplayName: $collection_name},
                Sources: [{Kind: 0, TagValue: $source_tag}],
                Policy: 0,
                IsEnabled: true
            }
        ]
    }')"
cyclic_candidate='{"SchemaVersion":1,"MappingGroups":[{"Target":{"Kind":0,"TagValue":"A"},"Sources":[{"Kind":0,"TagValue":"B"}],"Policy":0,"IsEnabled":true},{"Target":{"Kind":0,"TagValue":"B"},"Sources":[{"Kind":0,"TagValue":"A"}],"Policy":0,"IsEnabled":true}]}'

request_with_status POST "${plugin_configuration_url}" "${cyclic_candidate}"
if [[ "${response_code}" =~ ^2 ]]; then
    printf 'Generic plugin configuration endpoint bypassed validated activation.\n' >&2
    exit 3
fi

persisted_after_bypass_attempt="$(api_get "${plugin_configuration_url}")"
if [[ "$(jq --raw-output '.Revision // 0' <<<"${persisted_after_bypass_attempt}")" \
    -ne "${starting_revision}" ]]; then
    printf 'Rejected generic configuration update changed the active revision.\n' >&2
    exit 3
fi

request_with_status POST "${activation_url}" '{}' false
if [[ "${response_code}" != "401" ]]; then
    printf 'Unauthenticated activation returned HTTP %s instead of 401.\n' "${response_code}" >&2
    exit 3
fi

request_with_status POST "${activation_url}" "${valid_candidate}"
if [[ "${response_code}" != "202" ]]; then
    printf 'Valid activation returned HTTP %s: %s\n' "${response_code}" "${response_body}" >&2
    exit 4
fi

accepted_revision="$(jq --raw-output '.ActiveRevision // .activeRevision' <<<"${response_body}")"
reconciliation_id="$(jq --raw-output '.ReconciliationId // .reconciliationId' <<<"${response_body}")"
if [[ "${accepted_revision}" -ne $((starting_revision + 1)) || -z "${reconciliation_id}" ]]; then
    printf 'Activation response did not contain the next revision and reconciliation identity: %s\n' \
        "${response_body}" >&2
    exit 5
fi

status_url="${activation_url}/Reconciliations/${reconciliation_id}"
status_state=''
for _ in {1..30}; do
    request_with_status GET "${status_url}"
    status_state="$(jq --raw-output '.State // .state' <<<"${response_body}")"
    if [[ "${status_state}" == "2" || "${status_state}" == "Completed" ]]; then
        break
    fi

    if [[ "${status_state}" == "3" || "${status_state}" == "4" \
        || "${status_state}" == "PartiallyFailed" || "${status_state}" == "Failed" ]]; then
        printf 'Background activation reconciliation failed: %s\n' "${response_body}" >&2
        exit 6
    fi

    sleep 1
done

if [[ "${status_state}" != "2" && "${status_state}" != "Completed" ]]; then
    printf 'Background activation reconciliation did not complete: %s\n' "${response_body}" >&2
    exit 7
fi

members="$(api_get "${server_url}/Items?ParentId=${collection_id}&Recursive=true")"
if ! jq --exit-status --arg movie_id "${movie_id}" '.Items | any(.Id == $movie_id)' \
    <<<"${members}" >/dev/null; then
    printf 'Accepted configuration did not reconcile the Movie in the background.\n' >&2
    exit 9
fi

request_with_status POST "${activation_url}" "${cyclic_candidate}"
if [[ "${response_code}" != "400" ]]; then
    printf 'Cyclic candidate returned HTTP %s instead of 400.\n' "${response_code}" >&2
    exit 10
fi

persisted_after_invalid="$(api_get "${plugin_configuration_url}")"
if [[ "$(jq --raw-output '.Revision' <<<"${persisted_after_invalid}")" -ne "${accepted_revision}" ]]; then
    printf 'Invalid candidate changed the active revision.\n' >&2
    exit 11
fi

series_danger_tags="$(jq --arg tag "${danger_tag}" '. + [$tag] | unique' <<<"${series_original_tags}")"
set_item_tags "${series_id}" "${series_danger_tags}"
destructive_candidate="$(jq --null-input --arg danger_tag "${danger_tag}" \
    '{SchemaVersion:1,MappingGroups:[{Target:{Kind:0,TagValue:$danger_tag},Sources:[{Kind:0,TagValue:"Absent"}],Policy:1,IsEnabled:true}]}')"
request_with_status POST "${activation_url}" "${destructive_candidate}"
if [[ "${response_code}" != "409" ]]; then
    printf 'Removal-bearing candidate returned HTTP %s instead of 409.\n' "${response_code}" >&2
    exit 12
fi

paused_id="$(jq --raw-output '.ReconciliationId // .reconciliationId' <<<"${response_body}")"
request_with_status GET "${activation_url}/Reconciliations/${paused_id}"
paused_state="$(jq --raw-output '.State // .state' <<<"${response_body}")"
if [[ "${paused_state}" != "5" && "${paused_state}" != "Paused" ]]; then
    printf 'Removal-bearing candidate did not expose paused status: %s\n' "${response_body}" >&2
    exit 13
fi

restart_server
persisted_after_restart="$(api_get "${plugin_configuration_url}")"
if [[ "$(jq --raw-output '.Revision' <<<"${persisted_after_restart}")" -ne "${accepted_revision}" ]] \
    || ! jq --exit-status --arg collection_id "${collection_id}" \
        '(.MappingGroups | length == 1) and .MappingGroups[0].Target.CollectionId == $collection_id' \
        <<<"${persisted_after_restart}" >/dev/null; then
    printf 'Accepted configuration did not survive restart: %s\n' "${persisted_after_restart}" >&2
    exit 14
fi

printf 'Verified elevation-only configuration activation, revision %s, and background request %s.\n' \
    "${accepted_revision}" \
    "${reconciliation_id}"
printf 'Verified invalid preservation, destructive pause, background settlement, and restart persistence.\n'
