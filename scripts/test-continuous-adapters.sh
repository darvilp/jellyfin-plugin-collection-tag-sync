#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
plugin_id="04920eee-c499-4b13-890f-7af0175f28f0"
activation_url="${server_url}/CollectionTagSync/Configuration"

if [[ ! -f "${token_file}" ]]; then
    printf 'Missing test-server token. Run scripts/configure-test-server.sh first.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"
source_tag="Adapter-Source-$(date -u +'%Y%m%d%H%M%S%N')"
target_tag="Adapter-Target-$(date -u +'%Y%m%d%H%M%S%N')"
preserved_tag="Adapter-Preserved-$(date -u +'%Y%m%d%H%M%S%N')"
movie_id=''
series_id=''
movie_original_tags=''
series_original_tags=''
original_configuration=''
event_log=''

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

set_configuration() {
    local response
    local request_id
    local state=''
    response="$(api_post_json "${activation_url}" "$1")"
    request_id="$(jq --raw-output '.ReconciliationId // .reconciliationId' <<<"${response}")"
    for _ in {1..30}; do
        state="$(api_get "${activation_url}/Reconciliations/${request_id}" \
            | jq --raw-output '.State // .state')"
        if [[ "${state}" == "2" || "${state}" == "Completed" ]]; then
            return
        fi

        if [[ "${state}" == "3" || "${state}" == "4" \
            || "${state}" == "PartiallyFailed" || "${state}" == "Failed" ]]; then
            break
        fi

        sleep 1
    done

    printf 'Configuration activation %s ended in unexpected state %s.\n' \
        "${request_id}" "${state}" >&2
    return 1
}

create_collection() {
    local name="$1"
    curl --fail --silent --get --request POST \
        --header "X-Emby-Token: ${access_token}" \
        --data-urlencode "name=${name}" \
        "${server_url}/Collections" \
        | jq --raw-output .Id
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

format_guid() {
    sed -E 's/^(.{8})(.{4})(.{4})(.{4})(.{12})$/\1-\2-\3-\4-\5/' <<<"$1"
}

count_log_lines() {
    local expected="$1"
    { rg --ignore-case --fixed-strings "${expected}" <<<"${event_log}" || true; } | wc -l
}

refresh_logs() {
    event_log="$(docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" \
        logs --since "${event_start}" jellyfin)"
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

    printf 'Jellyfin did not become healthy after the persistence restart.\n' >&2
    exit 8
}

wait_for_log_counts() {
    local expected_movie_applied="$1"
    local expected_series_applied="$2"
    local minimum_movie_settled="$3"
    local minimum_series_settled="$4"
    for _ in {1..30}; do
        refresh_logs
        movie_applied="$(count_log_lines "reconciliation applied ItemId=${movie_guid} MutationCount=1")"
        series_applied="$(count_log_lines "reconciliation applied ItemId=${series_guid} MutationCount=1")"
        movie_settled="$(count_log_lines "reconciliation settled ItemId=${movie_guid} MutationCount=0")"
        series_settled="$(count_log_lines "reconciliation settled ItemId=${series_guid} MutationCount=0")"
        if [[ "${movie_applied}" -eq "${expected_movie_applied}" \
            && "${series_applied}" -eq "${expected_series_applied}" \
            && "${movie_settled}" -ge "${minimum_movie_settled}" \
            && "${series_settled}" -ge "${minimum_series_settled}" ]]; then
            return
        fi

        sleep 1
    done

    printf 'Timed out waiting for reconciliation logs; Movie applied/settled=%s/%s, Series=%s/%s.\n' \
        "${movie_applied:-0}" \
        "${movie_settled:-0}" \
        "${series_applied:-0}" \
        "${series_settled:-0}" >&2
    exit 5
}

wait_for_collection_state() {
    local collection_id="$1"
    local item_id="$2"
    local expected="$3"
    local members
    for _ in {1..30}; do
        members="$(api_get "${server_url}/Items?ParentId=${collection_id}&Recursive=true")"
        present="$(jq --arg item_id "${item_id}" '.Items | any(.Id == $item_id)' <<<"${members}")"
        if [[ "${present}" == "${expected}" ]]; then
            return
        fi

        sleep 1
    done

    printf 'Collection %s membership for item %s did not become %s.\n' \
        "${collection_id}" \
        "${item_id}" \
        "${expected}" >&2
    exit 3
}

wait_for_tag_state() {
    local item_id="$1"
    local tag="$2"
    local expected="$3"
    local item
    for _ in {1..30}; do
        item="$(api_get "${server_url}/Items/${item_id}")"
        present="$(jq --arg tag "${tag}" \
            '.Tags | any(ascii_downcase == ($tag | ascii_downcase))' \
            <<<"${item}")"
        if [[ "${present}" == "${expected}" ]]; then
            return
        fi

        sleep 1
    done

    printf 'Tag %s on item %s did not become %s.\n' "${tag}" "${item_id}" "${expected}" >&2
    exit 4
}

restore_test_state() {
    set +e
    set_configuration '{"SchemaVersion":1,"MappingGroups":[]}' >/dev/null
    if [[ -n "${movie_id}" && -n "${movie_original_tags}" ]]; then
        set_item_tags "${movie_id}" "${movie_original_tags}" >/dev/null
    fi

    if [[ -n "${series_id}" && -n "${series_original_tags}" ]]; then
        set_item_tags "${series_id}" "${series_original_tags}" >/dev/null
    fi

    if [[ -n "${original_configuration}" ]]; then
        set_configuration "${original_configuration}" >/dev/null
    fi
}

trap restore_test_state EXIT

original_configuration="$(api_get "${server_url}/Plugins/${plugin_id}/Configuration")"
items="$(api_get "${server_url}/Items?Recursive=true&IncludeItemTypes=Movie,Series&Fields=Tags")"
movie_id="$(jq --raw-output '.Items[] | select(.Type == "Movie") | .Id' <<<"${items}" | head -n 1)"
series_id="$(jq --raw-output '.Items[] | select(.Type == "Series") | .Id' <<<"${items}" | head -n 1)"
if [[ -z "${movie_id}" || -z "${series_id}" ]]; then
    printf 'The synthetic Movie and Series fixtures are required.\n' >&2
    exit 2
fi

movie_original_tags="$(jq --compact-output --arg movie_id "${movie_id}" \
    '.Items[] | select(.Id == $movie_id) | (.Tags // [])' <<<"${items}")"
series_original_tags="$(jq --compact-output --arg series_id "${series_id}" \
    '.Items[] | select(.Id == $series_id) | (.Tags // [])' <<<"${items}")"
source_collection_id="$(create_collection "Adapter Source $(date -u +'%Y%m%d%H%M%S%N')")"
target_collection_id="$(create_collection "Adapter Target $(date -u +'%Y%m%d%H%M%S%N')")"
movie_guid="$(format_guid "${movie_id}")"
series_guid="$(format_guid "${series_id}")"
source_collection_guid="$(format_guid "${source_collection_id}")"

additive_configuration="$(jq --null-input \
    --arg source_tag "${source_tag}" \
    --arg target_tag "${target_tag}" \
    --arg source_collection_id "${source_collection_id}" \
    --arg target_collection_id "${target_collection_id}" \
    '{
        SchemaVersion: 1,
        MappingGroups: [
            {
                Target: {Kind: 1, CollectionId: $target_collection_id, CollectionDisplayName: "Adapter Target"},
                Sources: [{Kind: 0, TagValue: $source_tag}],
                Policy: 0,
                IsEnabled: true
            },
            {
                Target: {Kind: 0, TagValue: $target_tag},
                Sources: [{Kind: 1, CollectionId: $source_collection_id, CollectionDisplayName: "Adapter Source"}],
                Policy: 0,
                IsEnabled: true
            }
        ]
    }')"
event_start="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
set_configuration "${additive_configuration}"

movie_source_tags="$(jq --arg tag "${source_tag}" '. + [$tag] | unique' <<<"${movie_original_tags}")"
set_item_tags "${movie_id}" "${movie_source_tags}"
set_item_tags "${movie_id}" "${movie_source_tags}"
curl --fail --silent --request POST \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Collections/${source_collection_id}/Items?ids=${series_id}"

wait_for_collection_state "${target_collection_id}" "${movie_id}" true
wait_for_tag_state "${series_id}" "${target_tag}" true
wait_for_log_counts 1 1 1 1

authoritative_configuration="$(jq '.MappingGroups[].Policy = 1' <<<"${additive_configuration}")"
set_configuration "${authoritative_configuration}"
set_item_tags "${movie_id}" "${movie_original_tags}"
set_item_tags "${movie_id}" "${movie_original_tags}"
curl --fail --silent --request DELETE \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Collections/${source_collection_id}/Items?ids=${series_id}"

wait_for_collection_state "${target_collection_id}" "${movie_id}" false
wait_for_tag_state "${series_id}" "${target_tag}" false
wait_for_log_counts 2 2 2 2

movie_preserved_tags="$(jq --arg tag "${preserved_tag}" '. + [$tag] | unique' <<<"${movie_original_tags}")"
unresolved_configuration="$(jq --null-input \
    --arg preserved_tag "${preserved_tag}" \
    --arg source_collection_id "${source_collection_id}" \
    --arg target_collection_id "${target_collection_id}" \
    '{
        SchemaVersion: 1,
        MappingGroups: [
            {
                Target: {Kind: 0, TagValue: $preserved_tag},
                Sources: [
                    {Kind: 1, CollectionId: $source_collection_id, CollectionDisplayName: "Adapter Source"},
                    {Kind: 0, TagValue: "Also absent"}
                ],
                Policy: 1,
                IsEnabled: true
            },
            {
                Target: {Kind: 1, CollectionId: $target_collection_id, CollectionDisplayName: "Adapter Target"},
                Sources: [{Kind: 0, TagValue: $preserved_tag}],
                Policy: 0,
                IsEnabled: true
            }
        ]
    }')"
set_configuration "${unresolved_configuration}"
curl --fail --silent --request DELETE \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Items/${source_collection_id}"
set_item_tags "${movie_id}" "${movie_preserved_tags}"
set_item_tags "${movie_id}" "${movie_preserved_tags}"

wait_for_collection_state "${target_collection_id}" "${movie_id}" true
wait_for_tag_state "${movie_id}" "${preserved_tag}" true
wait_for_log_counts 3 2 3 2
refresh_logs
unresolved_count="$(count_log_lines 'mapping group unresolved GroupIndex=0')"
if [[ "${unresolved_count}" -ne 1 ]] \
    || ! rg --ignore-case --fixed-strings "MissingCollectionIds=${source_collection_guid}" \
        <<<"${event_log}" >/dev/null; then
    printf 'Expected one persistent unresolved-group warning for %s; found %s.\n' \
        "${source_collection_guid}" \
        "${unresolved_count}" >&2
    exit 6
fi

restart_server
for _ in {1..20}; do
    refresh_logs
    unresolved_count="$(count_log_lines 'mapping group unresolved GroupIndex=0')"
    if [[ "${unresolved_count}" -eq 2 ]]; then
        break
    fi

    sleep 1
done

if [[ "${unresolved_count}" -ne 2 ]]; then
    printf 'The unresolved-group warning did not rehydrate after restart; found %s total warnings.\n' \
        "${unresolved_count}" >&2
    exit 9
fi

set_configuration '{"SchemaVersion":1,"MappingGroups":[]}'
set_item_tags "${movie_id}" "${movie_preserved_tags}"
for _ in {1..20}; do
    refresh_logs
    if rg --fixed-strings 'unresolved mapping diagnostics cleared' <<<"${event_log}" >/dev/null; then
        break
    fi

    sleep 1
done

if ! rg --fixed-strings 'unresolved mapping diagnostics cleared' <<<"${event_log}" >/dev/null; then
    printf 'Disabling the unresolved group did not clear its persistent diagnostic.\n' >&2
    exit 7
fi

printf 'Verified continuous Tag <-> Collection adapters for Movie and Series.\n'
printf 'Verified Additive adds, Authoritative removals, case-preserving tag writes, and fail-closed missing-collection pass-through.\n'
