#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
plugin_id="04920eee-c499-4b13-890f-7af0175f28f0"
activation_url="${server_url}/CollectionTagSync/Configuration"
preview_url="${activation_url}/Preview"
confirm_url="${activation_url}/Confirm"
plugin_configuration_url="${server_url}/Plugins/${plugin_id}/Configuration"

if [[ ! -f "${token_file}" ]]; then
    printf 'Missing test-server token. Run scripts/configure-test-server.sh first.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"
remove_tag="Waltney-Preview-Remove-$(date -u +'%Y%m%d%H%M%S%N')"
source_tag="Blooth-Preview-Source-$(date -u +'%Y%m%d%H%M%S%N')"
add_tag="Blooth-Preview-Add-$(date -u +'%Y%m%d%H%M%S%N')"
item_id=''
original_tags=''
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
    local url="$1"
    local body="$2"
    local response
    response="$(curl --silent --show-error --request POST \
        --header "X-Emby-Token: ${access_token}" \
        --header 'Content-Type: application/json' \
        --data "${body}" \
        --write-out $'\n%{http_code}' \
        "${url}")"
    response_code="${response##*$'\n'}"
    response_body="${response%$'\n'*}"
}

set_item_tags() {
    local target_item_id="$1"
    local tags="$2"
    local item
    local updated
    item="$(api_get "${server_url}/Items/${target_item_id}")"
    updated="$(jq --argjson tags "${tags}" '.Tags = $tags' <<<"${item}")"
    api_post_json "${server_url}/Items/${target_item_id}" "${updated}" >/dev/null
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

    printf 'Jellyfin did not become healthy during configuration-preview validation.\n' >&2
    exit 3
}

wait_for_reconciliation() {
    local reconciliation_id="$1"
    local state=''
    for _ in {1..30}; do
        state="$(api_get "${activation_url}/Reconciliations/${reconciliation_id}" \
            | jq --raw-output '.State // .state')"
        if [[ "${state}" == "2" || "${state}" == "Completed" ]]; then
            return
        fi

        if [[ "${state}" == "3" || "${state}" == "4" \
            || "${state}" == "PartiallyFailed" || "${state}" == "Failed" ]]; then
            printf 'Configuration reconciliation %s failed with state %s.\n' \
                "${reconciliation_id}" "${state}" >&2
            exit 4
        fi

        sleep 1
    done

    printf 'Configuration reconciliation %s did not finish; state=%s.\n' \
        "${reconciliation_id}" "${state}" >&2
    exit 4
}

restore_test_state() {
    set +e
    if [[ -n "${original_configuration}" ]]; then
        api_post_json "${activation_url}" "${original_configuration}" >/dev/null
    fi
    if [[ -n "${item_id}" && -n "${original_tags}" ]]; then
        set_item_tags "${item_id}" "${original_tags}" >/dev/null
    fi
}

trap restore_test_state EXIT

original_configuration="$(api_get "${plugin_configuration_url}")"
if ! jq --exit-status '((.MappingGroups // []) | length == 0)' <<<"${original_configuration}" >/dev/null; then
    printf 'Configuration-preview validation requires the isolated server to start with no mappings.\n' >&2
    exit 2
fi

starting_revision="$(jq --raw-output '.Revision // 0' <<<"${original_configuration}")"
items="$(api_get "${server_url}/Items?Recursive=true&IncludeItemTypes=Movie,Series&Fields=Tags")"
item_id="$(jq --raw-output '.Items[0].Id' <<<"${items}")"
original_tags="$(jq --compact-output '.Items[0].Tags // []' <<<"${items}")"
initial_tags="$(jq --arg tag "${remove_tag}" '. + [$tag] | unique' <<<"${original_tags}")"
set_item_tags "${item_id}" "${initial_tags}"

candidate="$(jq --null-input \
    --arg remove_tag "${remove_tag}" \
    --arg source_tag "${source_tag}" \
    --arg add_tag "${add_tag}" \
    '{
        SchemaVersion: 1,
        StartupReconcileDelayMinutes: 5,
        DestructiveCircuitBreakerEnabled: true,
        DestructiveMaximumAffectedItems: 25,
        DestructiveMaximumRemovalPercentage: 20,
        DestructiveMinimumAssignmentPopulation: 10,
        MappingGroups: [
            {
                Target: {Kind: 0, TagValue: $remove_tag},
                Sources: [{Kind: 0, TagValue: "Absent"}],
                Policy: 1,
                IsEnabled: true
            },
            {
                Target: {Kind: 0, TagValue: $add_tag},
                Sources: [{Kind: 0, TagValue: $source_tag}],
                Policy: 0,
                IsEnabled: true
            }
        ]
    }')"

request_with_status "${preview_url}" "${candidate}"
if [[ "${response_code}" != "200" ]]; then
    printf 'Candidate preview returned HTTP %s: %s\n' "${response_code}" "${response_body}" >&2
    exit 5
fi

if ! jq --exit-status \
    --arg item_id "${item_id}" \
    --arg remove_tag "${remove_tag}" \
    '(.Authorization.Preview.Items // .authorization.preview.items)[]
        | select((.ItemId // .itemId) == $item_id)
        | (.Mutations // .mutations)
        | any(((.Kind // .kind) == 1 or (.Kind // .kind) == "RemoveTag")
            and ((.Target.TagValue // .target.tagValue) == $remove_tag))' \
    <<<"${response_body}" >/dev/null; then
    printf 'Preview did not expose the expected item-level removal: %s\n' "${response_body}" >&2
    exit 6
fi

stale_authorization="$(jq --raw-output \
    '.Authorization.Authorization // .authorization.authorization' <<<"${response_body}")"
without_removal="$(jq --arg tag "${remove_tag}" 'map(select(. != $tag))' <<<"${initial_tags}")"
set_item_tags "${item_id}" "${without_removal}"
confirmation="$(jq --null-input \
    --argjson candidate "${candidate}" \
    --arg authorization "${stale_authorization}" \
    '{Candidate: $candidate, Authorization: $authorization}')"
request_with_status "${confirm_url}" "${confirmation}"
if [[ "${response_code}" != "409" ]]; then
    printf 'Changed-removal confirmation returned HTTP %s instead of 409: %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 7
fi

after_stale="$(api_get "${plugin_configuration_url}")"
if [[ "$(jq --raw-output '.Revision' <<<"${after_stale}")" -ne "${starting_revision}" ]]; then
    printf 'Changed-removal confirmation persisted the candidate.\n' >&2
    exit 7
fi

set_item_tags "${item_id}" "${initial_tags}"
request_with_status "${preview_url}" "${candidate}"
restart_authorization="$(jq --raw-output \
    '.Authorization.Authorization // .authorization.authorization' <<<"${response_body}")"
restart_server
restart_confirmation="$(jq --null-input \
    --argjson candidate "${candidate}" \
    --arg authorization "${restart_authorization}" \
    '{Candidate: $candidate, Authorization: $authorization}')"
request_with_status "${confirm_url}" "${restart_confirmation}"
if [[ "${response_code}" != "409" ]]; then
    printf 'Pre-restart authorization returned HTTP %s instead of 409.\n' "${response_code}" >&2
    exit 8
fi

request_with_status "${preview_url}" "${candidate}"
fresh_authorization="$(jq --raw-output \
    '.Authorization.Authorization // .authorization.authorization' <<<"${response_body}")"
addition_drift_tags="$(jq --arg tag "${source_tag}" '. + [$tag] | unique' <<<"${initial_tags}")"
set_item_tags "${item_id}" "${addition_drift_tags}"
fresh_confirmation="$(jq --null-input \
    --argjson candidate "${candidate}" \
    --arg authorization "${fresh_authorization}" \
    '{Candidate: $candidate, Authorization: $authorization}')"
request_with_status "${confirm_url}" "${fresh_confirmation}"
if [[ "${response_code}" != "202" ]]; then
    printf 'Fresh equivalent confirmation returned HTTP %s: %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 9
fi

accepted_revision="$(jq --raw-output '.ActiveRevision // .activeRevision' <<<"${response_body}")"
reconciliation_id="$(jq --raw-output '.ReconciliationId // .reconciliationId' <<<"${response_body}")"
if [[ "${accepted_revision}" -ne $((starting_revision + 1)) || -z "${reconciliation_id}" ]]; then
    printf 'Confirmed activation did not return revision and background status: %s\n' \
        "${response_body}" >&2
    exit 9
fi

wait_for_reconciliation "${reconciliation_id}"
settled_item="$(api_get "${server_url}/Items/${item_id}")"
if jq --exit-status --arg tag "${remove_tag}" '.Tags | any(. == $tag)' \
    <<<"${settled_item}" >/dev/null \
    || ! jq --exit-status --arg tag "${add_tag}" '.Tags | any(. == $tag)' \
        <<<"${settled_item}" >/dev/null; then
    printf 'Confirmed candidate did not apply the fresh removal-plus-addition plan.\n' >&2
    exit 10
fi

request_with_status "${confirm_url}" "${fresh_confirmation}"
if [[ "${response_code}" != "409" ]]; then
    printf 'Reused authorization returned HTTP %s instead of 409.\n' "${response_code}" >&2
    exit 11
fi

printf 'Verified complete item-level preview and changed-removal rejection without persistence.\n'
printf 'Verified restart invalidation, addition-only drift, async activation revision %s, and single use.\n' \
    "${accepted_revision}"
