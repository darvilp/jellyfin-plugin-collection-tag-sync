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

restart_server() {
    docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" restart jellyfin >/dev/null

    for _ in {1..30}; do
        health="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' collection-tag-sync-jellyfin)"
        if [[ "${health}" == "healthy" ]]; then
            return
        fi

        sleep 1
    done

    printf 'The isolated Jellyfin server did not become healthy after restart.\n' >&2
    exit 3
}

items="$(api_get "${server_url}/Items?Recursive=true&IncludeItemTypes=Movie,Series&Fields=Path,Tags")"
movie_id="$(jq --raw-output '.Items[] | select(.Type == "Movie") | .Id' <<<"${items}" | head -n 1)"
series_id="$(jq --raw-output '.Items[] | select(.Type == "Series") | .Id' <<<"${items}" | head -n 1)"

if [[ -z "${movie_id}" || -z "${series_id}" ]]; then
    printf 'The synthetic Movie and Series fixtures are required.\n' >&2
    exit 2
fi

movie="$(api_get "${server_url}/Items/${movie_id}")"
movie_with_case_variants="$(jq '.Tags = ["Kid-Approved", "kid-approved"]' <<<"${movie}")"
api_post_json "${server_url}/Items/${movie_id}" "${movie_with_case_variants}"

collection_name="Phase 1 Contract $(date -u +'%Y%m%d%H%M%S%N')"
first_collection="$(curl --fail --silent --get --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --data-urlencode "name=${collection_name}" \
    --data-urlencode "ids=${movie_id},${series_id}" \
    "${server_url}/Collections")"
first_collection_id="$(jq --raw-output .Id <<<"${first_collection}")"

second_collection="$(curl --fail --silent --get --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --data-urlencode "name=${collection_name}" \
    --data-urlencode "ids=${movie_id}" \
    "${server_url}/Collections")"
second_collection_id="$(jq --raw-output .Id <<<"${second_collection}")"

if [[ "${first_collection_id}" != "${second_collection_id}" ]]; then
    printf 'Jellyfin created two identities for same-name collection creates.\n' >&2
    exit 4
fi

curl --fail --silent --request POST \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Collections/${first_collection_id}/Items?ids=${series_id}"

api_post_json \
    "${server_url}/Plugins/${plugin_id}/Configuration" \
    '{"SchemaVersion":7}'

restart_server

persisted_movie="$(api_get "${server_url}/Items/${movie_id}")"
jq --exit-status \
    '.Tags | (index("Kid-Approved") != null and index("kid-approved") != null)' \
    <<<"${persisted_movie}" >/dev/null

filtered_items="$(curl --fail --silent --get \
    --header "X-Emby-Token: ${access_token}" \
    --data-urlencode 'Recursive=true' \
    --data-urlencode 'IncludeItemTypes=Movie' \
    --data-urlencode 'Tags=KID-APPROVED' \
    "${server_url}/Items")"
jq --exit-status --arg movie_id "${movie_id}" \
    '.Items | any(.Id == $movie_id)' \
    <<<"${filtered_items}" >/dev/null

first_members="$(api_get "${server_url}/Items?ParentId=${first_collection_id}&Recursive=true")"
jq --exit-status --arg movie_id "${movie_id}" --arg series_id "${series_id}" \
    '.Items | (any(.Id == $movie_id) and any(.Id == $series_id))' \
    <<<"${first_members}" >/dev/null

first_persisted="$(api_get "${server_url}/Items/${first_collection_id}")"
jq --exit-status --arg name "${collection_name}" '.Name == $name' <<<"${first_persisted}" >/dev/null

persisted_configuration="$(api_get "${server_url}/Plugins/${plugin_id}/Configuration")"
jq --exit-status '.SchemaVersion == 7' <<<"${persisted_configuration}" >/dev/null

movie_without_tags="$(jq '.Tags = []' <<<"${persisted_movie}")"
api_post_json "${server_url}/Items/${movie_id}" "${movie_without_tags}"
curl --fail --silent --request DELETE \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Collections/${first_collection_id}/Items?ids=${series_id}"
api_post_json \
    "${server_url}/Plugins/${plugin_id}/Configuration" \
    '{"SchemaVersion":1}'

restart_server

removed_movie="$(api_get "${server_url}/Items/${movie_id}")"
jq --exit-status \
    '[.Tags[] | ascii_downcase | select(. == "kid-approved")] | length == 0' \
    <<<"${removed_movie}" >/dev/null

remaining_members="$(api_get "${server_url}/Items?ParentId=${first_collection_id}&Recursive=true")"
jq --exit-status --arg movie_id "${movie_id}" --arg series_id "${series_id}" \
    '.Items | (any(.Id == $movie_id) and (any(.Id == $series_id) | not))' \
    <<<"${remaining_members}" >/dev/null

restored_configuration="$(api_get "${server_url}/Plugins/${plugin_id}/Configuration")"
jq --exit-status '.SchemaVersion == 1' <<<"${restored_configuration}" >/dev/null

printf 'Verified restart persistence, case-insensitive filtering, exact tag variants, Movie/Series membership, duplicate collection names, and plugin configuration serialization.\n'
printf 'Confirmed that two same-name create calls reused collection identity %s.\n' \
    "${first_collection_id}"
