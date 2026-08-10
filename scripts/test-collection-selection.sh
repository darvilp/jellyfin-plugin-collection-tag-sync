#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
collections_url="${server_url}/CollectionTagSync/Collections"
run_once_url="${server_url}/CollectionTagSync/RunOnce"

if [[ ! -f "${token_file}" ]]; then
    printf 'Missing test-server token. Run scripts/configure-test-server.sh first.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"
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

api_delete() {
    curl --fail --silent --request DELETE \
        --header "X-Emby-Token: ${access_token}" \
        "$1" >/dev/null
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

    printf 'Jellyfin did not become healthy during collection-selection validation.\n' >&2
    exit 3
}

request_with_status GET "${collections_url}/Picker" '' false
if [[ "${response_code}" != "401" ]]; then
    printf 'Unauthenticated collection picker returned HTTP %s instead of 401.\n' \
        "${response_code}" >&2
    exit 4
fi

request_with_status POST "${collections_url}/Create" '{"Name":"   "}'
if [[ "${response_code}" != "400" ]] \
    || ! jq --exit-status \
        '((.Outcome // .outcome) == 1) or ((.Outcome // .outcome) == "InvalidName")' \
        <<<"${response_body}" >/dev/null; then
    printf 'Empty Add New request did not return InvalidName: HTTP %s, %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 5
fi

preservation_collection_id=''
cleanup_preservation_collection() {
    if [[ -n "${preservation_collection_id}" ]]; then
        api_delete "${server_url}/Items/${preservation_collection_id}" || true
    fi
}
trap cleanup_preservation_collection EXIT

existing_cass_ids="$(api_get "${server_url}/Items?IncludeItemTypes=BoxSet&Recursive=true&Fields=Path" \
    | jq --raw-output '.Items[]
        | select((.Path // "") | endswith("/Cass [boxset]"))
        | .Id')"
if [[ -n "${existing_cass_ids}" ]]; then
    printf 'A pre-existing Cass collection blocks the disposable name-preservation probe: %s\n' \
        "${existing_cass_ids//$'\n'/, }" >&2
    printf 'Remove that test artifact explicitly or reset the isolated Jellyfin environment, then rerun.\n' >&2
    exit 13
fi

request_with_status POST "${collections_url}/Create" '{"Name":"Cass"}'
if [[ "${response_code}" != "201" ]]; then
    printf 'Name-preservation collection returned HTTP %s: %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 14
fi

preservation_collection_id="$(jq --raw-output \
    '.SelectedCollection.Id // .selectedCollection.id' <<<"${response_body}")"
for _ in {1..5}; do
    preservation_item="$(api_get "${server_url}/Items/${preservation_collection_id}")"
    if ! jq --exit-status \
        '.Name == "Cass"
         and .LockData == true
         and ((.ProviderIds // {}) | length == 0)' \
        <<<"${preservation_item}" >/dev/null; then
        printf 'Plugin-created Cass did not remain locked, provider-free, and administrator-named: %s\n' \
            "${preservation_item}" >&2
        exit 15
    fi

    sleep 1
done

api_delete "${server_url}/Items/${preservation_collection_id}"
preservation_collection_id=''

created_name="Waltney Picker $(date -u +'%Y%m%d%H%M%S%N')"
create_request="$(jq --null-input --arg name "  ${created_name}  " '{Name: $name}')"
request_with_status POST "${collections_url}/Create" "${create_request}"
if [[ "${response_code}" != "201" ]]; then
    printf 'Add New collection returned HTTP %s: %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 6
fi

collection_id="$(jq --raw-output \
    '.SelectedCollection.Id // .selectedCollection.id' <<<"${response_body}")"
selected_name="$(jq --raw-output \
    '.SelectedCollection.DisplayName // .selectedCollection.displayName' <<<"${response_body}")"
if [[ -z "${collection_id}" || "${selected_name}" != "${created_name}" ]]; then
    printf 'Add New did not return the created GUID as the trimmed selected value: %s\n' \
        "${response_body}" >&2
    exit 7
fi

picker="$(api_get "${collections_url}/Picker")"
if ! jq --exit-status \
    --arg collection_id "${collection_id}" \
    --arg name "${created_name}" \
    'any((.Id // .id) == $collection_id
        and (.DisplayName // .displayName) == $name)' \
    <<<"${picker}" >/dev/null; then
    printf 'The picker did not expose the created GUID and display name.\n' >&2
    exit 8
fi

renamed_name="Blooth Picker $(date -u +'%Y%m%d%H%M%S%N')"
collection_item="$(api_get "${server_url}/Items/${collection_id}")"
renamed_item="$(jq --arg name "${renamed_name}" '.Name = $name' <<<"${collection_item}")"
api_post_json "${server_url}/Items/${collection_id}" "${renamed_item}" >/dev/null

for _ in {1..20}; do
    picker="$(api_get "${collections_url}/Picker")"
    if jq --exit-status \
        --arg collection_id "${collection_id}" \
        --arg name "${renamed_name}" \
        'any((.Id // .id) == $collection_id
            and (.DisplayName // .displayName) == $name)' \
        <<<"${picker}" >/dev/null; then
        break
    fi

    sleep 1
done

if ! jq --exit-status \
    --arg collection_id "${collection_id}" \
    --arg name "${renamed_name}" \
    'any((.Id // .id) == $collection_id
        and (.DisplayName // .displayName) == $name)' \
    <<<"${picker}" >/dev/null; then
    printf 'Picker did not retain GUID identity after a collection rename.\n' >&2
    exit 9
fi

duplicate_name="$(tr '[:upper:]' '[:lower:]' <<<"${renamed_name}")"
duplicate_request="$(jq --null-input --arg name "  ${duplicate_name}  " '{Name: $name}')"
request_with_status POST "${collections_url}/Create" "${duplicate_request}"
if [[ "${response_code}" != "409" ]] \
    || ! jq --exit-status \
        --arg collection_id "${collection_id}" \
        '(((.Outcome // .outcome) == 2) or ((.Outcome // .outcome) == "DuplicateName"))
         and ((.MatchingCollections // .matchingCollections)
             | any((.Id // .id) == $collection_id))' \
        <<<"${response_body}" >/dev/null; then
    printf 'Normalized duplicate did not return matching GUID picker recovery: HTTP %s, %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 10
fi

self_source_group="$(jq --null-input \
    --arg collection_id "${collection_id}" \
    --arg name "${renamed_name}" \
    '{
        Target: {Kind: 1, CollectionId: $collection_id, CollectionDisplayName: $name},
        Sources: [{Kind: 1, CollectionId: $collection_id, CollectionDisplayName: $name}],
        Policy: 0
    }')"
request_with_status POST "${run_once_url}/Groups" "${self_source_group}"
if [[ "${response_code}" != "400" ]]; then
    printf 'The intentionally invalid surrounding run-once returned HTTP %s instead of 400.\n' \
        "${response_code}" >&2
    exit 11
fi

restart_server
picker="$(api_get "${collections_url}/Picker")"
if ! jq --exit-status --arg collection_id "${collection_id}" \
    'any((.Id // .id) == $collection_id)' <<<"${picker}" >/dev/null; then
    printf 'Created collection was rolled back after surrounding workflow failure/restart.\n' >&2
    exit 12
fi

printf 'Verified elevated GUID picker, trimmed Add New selection, rename-safe identity, and duplicate recovery.\n'
printf 'Verified plugin-created collection name protection across Jellyfin metadata refresh.\n'
printf 'Verified empty-name rejection and independent collection persistence after surrounding failure.\n'
