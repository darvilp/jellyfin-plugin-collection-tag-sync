#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
plugin_version="$("${script_dir}/read-build-metadata.sh" version)"
artifact_path="${1:-${project_root}/artifacts/collection-tag-sync_${plugin_version}.zip}"
plugin_root="${project_root}/.testenv/jellyfin/config/plugins"

bash "${script_dir}/verify-package.sh" "${artifact_path}"

plugin_version="$(unzip -p "${artifact_path}" meta.json | jq --raw-output .version)"
plugin_directory="${plugin_root}/Collection Tag Sync_${plugin_version}"

if [[ "${plugin_root}" != "${project_root}/.testenv/jellyfin/config/plugins" ]]; then
    printf 'Refusing to install into unexpected path: %s\n' "${plugin_root}" >&2
    exit 2
fi

mkdir -p "${plugin_directory}"
unzip -o "${artifact_path}" -d "${plugin_directory}" >/dev/null

docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" restart jellyfin

health_status=""
for _ in {1..45}; do
    health_status="$(docker inspect collection-tag-sync-jellyfin --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}')"
    if [[ "${health_status}" == "healthy" ]]; then
        break
    fi
    sleep 2
done

if [[ "${health_status}" != "healthy" ]]; then
    printf 'Jellyfin did not become healthy after plugin installation; status: %s.\n' "${health_status}" >&2
    exit 3
fi

curl --fail --silent http://127.0.0.1:18096/health | rg --fixed-strings "Healthy" >/dev/null

docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" \
    logs --since 2m jellyfin \
    | rg --fixed-strings "Loaded plugin: Collection Tag Sync"

printf 'Installed Collection Tag Sync %s into the isolated Jellyfin server.\n' "${plugin_version}"
