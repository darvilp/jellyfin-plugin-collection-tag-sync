#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"

if [[ ! -f "${token_file}" ]]; then
    printf 'Missing test-server token. Run scripts/configure-test-server.sh first.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"
event_start="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"

api_get() {
    curl --fail --silent --header "X-Emby-Token: ${access_token}" "$1"
}

items="$(api_get "${server_url}/Items?Recursive=true&IncludeItemTypes=Movie,Series&Fields=Path,Tags")"
movie_id="$(jq --raw-output '.Items[] | select(.Type == "Movie") | .Id' <<<"${items}" | head -n 1)"
series_id="$(jq --raw-output '.Items[] | select(.Type == "Series") | .Id' <<<"${items}" | head -n 1)"

movie="$(api_get "${server_url}/Items/${movie_id}")"
updated_movie="$(jq '.Tags = ["Kid-Approved"]' <<<"${movie}")"
curl --fail --silent --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --header 'Content-Type: application/json' \
    --data "${updated_movie}" \
    "${server_url}/Items/${movie_id}"

collection_name="Waltney Picks $(date -u +'%Y%m%d%H%M%S')"
collection="$(curl --fail --silent --get --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --data-urlencode "name=${collection_name}" \
    --data-urlencode "ids=${movie_id}" \
    "${server_url}/Collections")"
collection_id="$(jq --raw-output .Id <<<"${collection}")"

curl --fail --silent --request POST \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Collections/${collection_id}/Items?ids=${series_id}"
curl --fail --silent --request DELETE \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Collections/${collection_id}/Items?ids=${series_id}"

persisted_movie="$(api_get "${server_url}/Items/${movie_id}")"
jq --exit-status '.Tags | index("Kid-Approved") != null' <<<"${persisted_movie}" >/dev/null

sleep 1
event_log="$(docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" \
    logs --since "${event_start}" jellyfin)"

for expected_event in \
    'Collection Tag Sync event: ItemUpdated' \
    'Collection Tag Sync event: CollectionCreated' \
    'Collection Tag Sync event: ItemsAddedToCollection' \
    'Collection Tag Sync event: ItemsRemovedFromCollection'; do
    if ! rg --fixed-strings "${expected_event}" <<<"${event_log}" >/dev/null; then
        printf 'Missing plugin event log: %s\n' "${expected_event}" >&2
        exit 3
    fi
done

printf 'Observed item-update and collection add/remove events for collection %s (%s).\n' \
    "${collection_name}" \
    "${collection_id}"
