#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
plugin_id="04920eee-c499-4b13-890f-7af0175f28f0"

if [[ ! -f "${token_file}" ]]; then
    printf 'Missing test-server token. Run scripts/configure-test-server.sh first.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"
source_tag="Walking-Slice-$(date -u +'%Y%m%d%H%M%S%N')"
collection_name="Walking Slice $(date -u +'%Y%m%d%H%M%S%N')"
movie_id=''
series_id=''
movie_original_tags=''
series_original_tags=''

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

restore_test_state() {
    set +e
    api_post_json \
        "${server_url}/Plugins/${plugin_id}/Configuration" \
        '{"SchemaVersion":1,"MappingGroups":[]}' >/dev/null

    if [[ -n "${movie_id}" && -n "${movie_original_tags}" ]]; then
        current_movie="$(api_get "${server_url}/Items/${movie_id}")"
        restored_movie="$(jq --argjson tags "${movie_original_tags}" '.Tags = $tags' <<<"${current_movie}")"
        api_post_json "${server_url}/Items/${movie_id}" "${restored_movie}" >/dev/null
    fi

    if [[ -n "${series_id}" && -n "${series_original_tags}" ]]; then
        current_series="$(api_get "${server_url}/Items/${series_id}")"
        restored_series="$(jq --argjson tags "${series_original_tags}" '.Tags = $tags' <<<"${current_series}")"
        api_post_json "${server_url}/Items/${series_id}" "${restored_series}" >/dev/null
    fi
}

format_guid() {
    sed -E 's/^(.{8})(.{4})(.{4})(.{4})(.{12})$/\1-\2-\3-\4-\5/' <<<"$1"
}

count_log_lines() {
    local expected="$1"
    { rg --ignore-case --fixed-strings "${expected}" <<<"${event_log}" || true; } | wc -l
}

trap restore_test_state EXIT

items="$(api_get "${server_url}/Items?Recursive=true&IncludeItemTypes=Movie,Series&Fields=Path,Tags")"
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

collection="$(curl --fail --silent --get --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --data-urlencode "name=${collection_name}" \
    "${server_url}/Collections")"
collection_id="$(jq --raw-output .Id <<<"${collection}")"

api_post_json \
    "${server_url}/Plugins/${plugin_id}/Configuration" \
    "$(jq --null-input \
        --arg source_tag "${source_tag}" \
        --arg collection_id "${collection_id}" \
        --arg collection_name "${collection_name}" \
        '{
            SchemaVersion: 1,
            MappingGroups: [
                {
                    Target: {
                        Kind: 1,
                        CollectionId: $collection_id,
                        CollectionDisplayName: $collection_name
                    },
                    Sources: [{Kind: 0, TagValue: $source_tag}],
                    Policy: 0,
                    IsEnabled: true
                }
            ]
        }')"

event_start="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
for item_id in "${movie_id}" "${series_id}"; do
    item="$(api_get "${server_url}/Items/${item_id}")"
    tagged_item="$(jq --arg source_tag "${source_tag}" \
        '.Tags = ((.Tags // []) + [$source_tag] | unique)' \
        <<<"${item}")"
    api_post_json "${server_url}/Items/${item_id}" "${tagged_item}"
    api_post_json "${server_url}/Items/${item_id}" "${tagged_item}"
done

members='{"Items":[]}'
for _ in {1..30}; do
    members="$(api_get "${server_url}/Items?ParentId=${collection_id}&Recursive=true")"
    if jq --exit-status --arg movie_id "${movie_id}" --arg series_id "${series_id}" \
        '.Items | (any(.Id == $movie_id) and any(.Id == $series_id))' \
        <<<"${members}" >/dev/null; then
        break
    fi

    sleep 1
done

if ! jq --exit-status --arg movie_id "${movie_id}" --arg series_id "${series_id}" \
    '.Items | (any(.Id == $movie_id) and any(.Id == $series_id))' \
    <<<"${members}" >/dev/null; then
    printf 'The walking slice did not add both fixtures to collection %s.\n' "${collection_id}" >&2
    exit 3
fi

movie_guid="$(format_guid "${movie_id}")"
series_guid="$(format_guid "${series_id}")"
event_log=''
for _ in {1..20}; do
    event_log="$(docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" \
        logs --since "${event_start}" jellyfin)"
    movie_applied="$(count_log_lines "reconciliation applied ItemId=${movie_guid} MutationCount=1")"
    series_applied="$(count_log_lines "reconciliation applied ItemId=${series_guid} MutationCount=1")"
    movie_settled="$(count_log_lines "reconciliation settled ItemId=${movie_guid} MutationCount=0")"
    series_settled="$(count_log_lines "reconciliation settled ItemId=${series_guid} MutationCount=0")"
    if [[ "${movie_applied}" -eq 1 && "${series_applied}" -eq 1 \
        && "${movie_settled}" -ge 1 && "${series_settled}" -ge 1 ]]; then
        break
    fi

    sleep 1
done

if [[ "${movie_applied:-0}" -ne 1 || "${series_applied:-0}" -ne 1 ]]; then
    printf 'Expected exactly one effective collection write per fixture; Movie=%s Series=%s.\n' \
        "${movie_applied:-0}" \
        "${series_applied:-0}" >&2
    exit 4
fi

if [[ "${movie_settled:-0}" -lt 1 || "${series_settled:-0}" -lt 1 ]]; then
    printf 'Expected a zero-delta settling pass per fixture; Movie=%s Series=%s.\n' \
        "${movie_settled:-0}" \
        "${series_settled:-0}" >&2
    exit 5
fi

printf 'Verified Tag -> Collection Additive walking slice for Movie %s and Series %s.\n' \
    "${movie_id}" \
    "${series_id}"
printf 'Duplicate inputs produced one write each; self-events replanned to zero delta in collection %s.\n' \
    "${collection_id}"
