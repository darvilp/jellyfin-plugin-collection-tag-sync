#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
jprm="${project_root}/.testenv/jprm/bin/jprm"
artifact="${project_root}/artifacts/collection-tag-sync_0.1.0.0.zip"
plugin_id="04920eee-c499-4b13-890f-7af0175f28f0"
manifest_port="18097"
container_gateway="$(docker inspect collection-tag-sync-jellyfin --format '{{range .NetworkSettings.Networks}}{{.Gateway}}{{end}}')"
manifest_base_url="http://${container_gateway}:${manifest_port}"
manifest_url="${manifest_base_url}/manifest.json"
manifest_dir="${project_root}/.testenv/manifest-repository-$(date -u +'%Y%m%d%H%M%S')"
manifest_log="${manifest_dir}/http-server.log"
original_repositories=''
manifest_server_pid=''

if [[ ! -f "${token_file}" || ! -x "${jprm}" || ! -f "${artifact}" ]]; then
    printf 'Configure the test server and run scripts/package.sh before the manifest smoke test.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"

restore_environment() {
    set +e

    if [[ -n "${original_repositories}" ]]; then
        curl --silent --request POST \
            --header "X-Emby-Token: ${access_token}" \
            --header 'Content-Type: application/json' \
            --data "${original_repositories}" \
            "${server_url}/Repositories" >/dev/null
    fi

    if [[ -n "${manifest_server_pid}" ]]; then
        kill "${manifest_server_pid}" 2>/dev/null
        wait "${manifest_server_pid}" 2>/dev/null
    fi
}

trap restore_environment EXIT

mkdir -p "${manifest_dir}"
"${jprm}" repo init "${manifest_dir}"
"${jprm}" repo add \
    --url "${manifest_base_url}" \
    "${manifest_dir}" \
    "${artifact}"

python3 -m http.server "${manifest_port}" \
    --bind 0.0.0.0 \
    --directory "${manifest_dir}" \
    >"${manifest_log}" 2>&1 &
manifest_server_pid="$!"

for _ in {1..20}; do
    if curl --fail --silent "http://127.0.0.1:${manifest_port}/manifest.json" >/dev/null; then
        break
    fi

    sleep 1
done

docker exec collection-tag-sync-jellyfin \
    curl --fail --silent "${manifest_url}" >/dev/null

original_repositories="$(curl --fail --silent \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Repositories")"
temporary_repository="$(jq --arg url "${manifest_url}" \
    '[{"Name":"Collection Tag Sync Temporary","Url":$url,"Enabled":true}]' \
    <<<"${original_repositories}")"

curl --fail --silent --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --header 'Content-Type: application/json' \
    --data "${temporary_repository}" \
    "${server_url}/Repositories"

packages="$(curl --fail --silent \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Packages")"
jq --exit-status --arg plugin_id "${plugin_id}" \
    'any(.[]; ((.guid | ascii_downcase | gsub("-"; "")) == ($plugin_id | gsub("-"; ""))) and any(.versions[]; .version == "0.1.0.0" and .targetAbi == "10.11.11.0"))' \
    <<<"${packages}" >/dev/null

curl --fail --silent --get --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --data-urlencode "assemblyGuid=${plugin_id}" \
    --data-urlencode 'version=0.1.0.0' \
    --data-urlencode "repositoryUrl=${manifest_url}" \
    "${server_url}/Packages/Installed/Collection%20Tag%20Sync"

docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" restart jellyfin >/dev/null
for _ in {1..30}; do
    health="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' collection-tag-sync-jellyfin)"
    if [[ "${health}" == "healthy" ]]; then
        break
    fi

    sleep 1
done

if [[ "${health:-}" != "healthy" ]]; then
    printf 'The isolated Jellyfin server did not become healthy after manifest installation.\n' >&2
    exit 3
fi

plugins="$(curl --fail --silent \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Plugins")"
jq --exit-status --arg plugin_id "${plugin_id}" \
    'any(.[]; ((.Id | ascii_downcase | gsub("-"; "")) == ($plugin_id | gsub("-"; ""))) and .Version == "0.1.0.0" and .Status == "Active")' \
    <<<"${plugins}" >/dev/null

printf 'Loaded Collection Tag Sync 0.1.0.0 from temporary manifest %s.\n' "${manifest_url}"
