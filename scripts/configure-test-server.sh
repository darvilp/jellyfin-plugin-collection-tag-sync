#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
admin_name="jfts-admin"
admin_password="jfts-local-only"
client_authorization='MediaBrowser Client="Collection Tag Sync Tests", Device="WSL", DeviceId="collection-tag-sync-tests", Version="0.1.0.0"'

wizard_complete="$(curl --fail --silent "${server_url}/System/Info/Public" | jq --raw-output .StartupWizardCompleted)"
if [[ "${wizard_complete}" != "true" ]]; then
    curl --fail --silent --request POST \
        --header 'Content-Type: application/json' \
        --data '{"ServerName":"Collection Tag Sync Test","UICulture":"en-US","MetadataCountryCode":"US","PreferredMetadataLanguage":"en"}' \
        "${server_url}/Startup/Configuration"

    curl --fail --silent "${server_url}/Startup/User" >/dev/null

    curl --fail --silent --request POST \
        --header 'Content-Type: application/json' \
        --data "{\"Name\":\"${admin_name}\",\"Password\":\"${admin_password}\"}" \
        "${server_url}/Startup/User"

    curl --fail --silent --request POST \
        --header 'Content-Type: application/json' \
        --data '{"EnableRemoteAccess":false,"EnableAutomaticPortMapping":false}' \
        "${server_url}/Startup/RemoteAccess"

    curl --fail --silent --request POST "${server_url}/Startup/Complete"
fi

authentication_result="$(curl --fail --silent --request POST \
    --header "Authorization: ${client_authorization}" \
    --header 'Content-Type: application/json' \
    --data "{\"Username\":\"${admin_name}\",\"Pw\":\"${admin_password}\"}" \
    "${server_url}/Users/AuthenticateByName")"
access_token="$(jq --raw-output .AccessToken <<<"${authentication_result}")"

umask 077
printf '%s\n' "${access_token}" >"${token_file}"

api_get() {
    curl --fail --silent --header "X-Emby-Token: ${access_token}" "$1"
}

api_post() {
    curl --fail --silent --request POST \
        --header "X-Emby-Token: ${access_token}" \
        --header 'Content-Type: application/json' \
        --data "${2:-{}}" \
        "$1"
}

virtual_folders="$(api_get "${server_url}/Library/VirtualFolders")"
if ! jq --exit-status '.[] | select(.Name == "Movies")' <<<"${virtual_folders}" >/dev/null; then
    api_post "${server_url}/Library/VirtualFolders?name=Movies&collectionType=movies&paths=%2Fmedia%2FMovies&refreshLibrary=false"
fi

if ! jq --exit-status '.[] | select(.Name == "Series")' <<<"${virtual_folders}" >/dev/null; then
    api_post "${server_url}/Library/VirtualFolders?name=Series&collectionType=tvshows&paths=%2Fmedia%2FSeries&refreshLibrary=false"
fi

api_post "${server_url}/Library/Refresh"

items='{"Items":[]}'
for _ in {1..20}; do
    items="$(api_get "${server_url}/Items?Recursive=true&IncludeItemTypes=Movie,Series&Fields=Path,Tags")"
    movie_count="$(jq '[.Items[] | select(.Type == "Movie")] | length' <<<"${items}")"
    series_count="$(jq '[.Items[] | select(.Type == "Series")] | length' <<<"${items}")"
    if [[ "${movie_count}" -ge 1 && "${series_count}" -ge 1 ]]; then
        break
    fi
    sleep 2
done

if [[ "${movie_count:-0}" -lt 1 || "${series_count:-0}" -lt 1 ]]; then
    printf 'Synthetic Movie and Series did not appear before the timeout.\n' >&2
    jq '{TotalRecordCount, Items: [.Items[] | {Id, Name, Type, Path}]}' <<<"${items}" >&2
    exit 2
fi

jq '{TotalRecordCount, Items: [.Items[] | {Id, Name, Type, Path, Tags}]}' <<<"${items}"
printf 'Stored the isolated test-server access token at %s.\n' "${token_file}"
