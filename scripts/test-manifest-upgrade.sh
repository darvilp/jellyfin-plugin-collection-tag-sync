#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
jprm="${JPRM_BIN:-${project_root}/.testenv/jprm/bin/jprm}"
candidate_version="$("${script_dir}/read-build-metadata.sh" version)"
previous_version="$("${script_dir}/read-build-metadata.sh" upgradeFrom)"
target_abi="$("${script_dir}/read-build-metadata.sh" targetAbi)"
candidate_package="${1:-${project_root}/artifacts/collection-tag-sync_${candidate_version}.zip}"
plugin_id="04920eee-c499-4b13-890f-7af0175f28f0"
repository_slug="${GITHUB_REPOSITORY:-darvilp/jellyfin-plugin-collection-tag-sync}"
previous_tag="v${previous_version}"
previous_asset_name="Jellyfin.Plugin.CollectionTagSync_${previous_version}.zip"
previous_asset_url="https://github.com/${repository_slug}/releases/download/${previous_tag}/${previous_asset_name}"
manifest_port="18098"
test_root="$(mktemp -d /tmp/collection-tag-sync-upgrade.XXXXXX)"
manifest_dir="${test_root}/repository"
manifest_log="${test_root}/http-server.log"
previous_package="${test_root}/${previous_asset_name}"
previous_checksum="${previous_package}.sha256"
original_repositories=''
manifest_server_pid=''

if [[ ! -f "${token_file}" || ! -x "${jprm}" || ! -f "${candidate_package}" ]]; then
    printf 'Configure a clean test server, install JPRM, and build the candidate before the upgrade smoke test.\n' >&2
    exit 2
fi

if [[ -z "${previous_version}" || "${previous_version}" == "${candidate_version}" ]]; then
    printf 'build.yaml upgradeFrom must name a distinct prior public package.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"
container_gateway="$(docker inspect collection-tag-sync-jellyfin --format '{{range .NetworkSettings.Networks}}{{.Gateway}}{{end}}')"
manifest_base_url="http://${container_gateway}:${manifest_port}"
manifest_url="${manifest_base_url}/manifest.json"

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

    rm -rf -- "${test_root}"
}

trap restore_environment EXIT

restart_server() {
    local health=''
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

    printf 'Jellyfin did not become healthy during the manifest upgrade smoke test.\n' >&2
    exit 3
}

assert_active_version() {
    local expected_version="$1"
    local plugins
    plugins="$(curl --fail --silent \
        --header "X-Emby-Token: ${access_token}" \
        "${server_url}/Plugins")"
    if ! jq --exit-status \
        --arg plugin_id "${plugin_id}" \
        --arg version "${expected_version}" \
        'any(.[];
          ((.Id | ascii_downcase | gsub("-"; "")) == ($plugin_id | gsub("-"; "")))
          and .Version == $version
          and .Status == "Active")' \
        <<<"${plugins}" >/dev/null; then
        printf 'Collection Tag Sync %s was not active after restart: %s\n' \
            "${expected_version}" "${plugins}" >&2
        exit 4
    fi
}

install_catalog_version() {
    local version="$1"
    curl --fail --silent --get --request POST \
        --header "X-Emby-Token: ${access_token}" \
        --data-urlencode "assemblyGuid=${plugin_id}" \
        --data-urlencode "version=${version}" \
        --data-urlencode "repositoryUrl=${manifest_url}" \
        "${server_url}/Packages/Installed/Collection%20Tag%20Sync"
    restart_server
    assert_active_version "${version}"
}

assert_catalog_version() {
    local version="$1"
    local packages
    packages="$(curl --fail --silent \
        --header "X-Emby-Token: ${access_token}" \
        "${server_url}/Packages")"
    if ! jq --exit-status \
        --arg plugin_id "${plugin_id}" \
        --arg version "${version}" \
        --arg target_abi "${target_abi}" \
        'any(.[];
          ((.guid | ascii_downcase | gsub("-"; "")) == ($plugin_id | gsub("-"; "")))
          and any(.versions[]; .version == $version and .targetAbi == $target_abi))' \
        <<<"${packages}" >/dev/null; then
        printf 'Collection Tag Sync %s was not available from the temporary catalog.\n' \
            "${version}" >&2
        exit 5
    fi
}

"${script_dir}/verify-package.sh" "${candidate_package}" "${candidate_version}" "${target_abi}"
curl --fail --location --retry 5 --retry-all-errors --retry-delay 2 \
    "${previous_asset_url}" --output "${previous_package}"
curl --fail --location --retry 5 --retry-all-errors --retry-delay 2 \
    "${previous_asset_url}.sha256" --output "${previous_checksum}"
(
    cd -- "${test_root}"
    sha256sum --check "$(basename -- "${previous_checksum}")"
)
"${script_dir}/verify-package.sh" "${previous_package}" "${previous_version}" "${target_abi}"

mkdir -p "${manifest_dir}"
"${jprm}" repo init "${manifest_dir}"
"${jprm}" repo add \
    --plugin-url "${previous_asset_url}" \
    "${manifest_dir}" \
    "${previous_package}"

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
docker exec collection-tag-sync-jellyfin curl --fail --silent "${manifest_url}" >/dev/null

original_repositories="$(curl --fail --silent \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Repositories")"
temporary_repository="$(jq --arg url "${manifest_url}" \
    '[{"Name":"Collection Tag Sync Upgrade Test","Url":$url,"Enabled":true}]' \
    <<<"${original_repositories}")"
curl --fail --silent --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --header 'Content-Type: application/json' \
    --data "${temporary_repository}" \
    "${server_url}/Repositories"

assert_catalog_version "${previous_version}"

plugins_before="$(curl --fail --silent \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Plugins")"
if jq --exit-status \
    --arg plugin_id "${plugin_id}" \
    'any(.[]; ((.Id | ascii_downcase | gsub("-"; "")) == ($plugin_id | gsub("-"; ""))))' \
    <<<"${plugins_before}" >/dev/null; then
    printf 'Manifest upgrade smoke requires a clean server without Collection Tag Sync installed.\n' >&2
    exit 5
fi

install_catalog_version "${previous_version}"

sentinel_collection_name="Upgrade Sentinel Target"
sentinel_collection_id="$(curl --fail --silent --get --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --data-urlencode "name=${sentinel_collection_name}" \
    "${server_url}/Collections" | jq --raw-output .Id)"
sentinel_configuration="$(jq --null-input \
    --arg collection_id "${sentinel_collection_id}" \
    --arg collection_name "${sentinel_collection_name}" \
    '{
      SchemaVersion: 1,
      StartupReconcileDelayMinutes: 17,
      DestructiveCircuitBreakerEnabled: true,
      DestructiveMaximumAffectedItems: 13,
      DestructiveMaximumRemovalPercentage: 37,
      DestructiveMinimumAssignmentPopulation: 14,
      DestructiveCircuitBreakerDisableAcknowledged: false,
      MappingGroups: [
        {
          Target: {
            Kind: 1,
            CollectionId: $collection_id,
            CollectionDisplayName: $collection_name
          },
          Sources: [
            {Kind: 0, TagValue: "Upgrade-Sentinel-Source-A"},
            {Kind: 0, TagValue: "Upgrade-Sentinel-Source-B"}
          ],
          Policy: 1,
          IsEnabled: false
        }
      ]
    }')"
activation="$(curl --fail --silent --request POST \
    --header "X-Emby-Token: ${access_token}" \
    --header 'Content-Type: application/json' \
    --data "${sentinel_configuration}" \
    "${server_url}/CollectionTagSync/Configuration")"
active_revision="$(jq --raw-output '.ActiveRevision // .activeRevision // 0' <<<"${activation}")"
if [[ "${active_revision}" -le 0 ]]; then
    printf 'Prior package did not accept the sentinel configuration: %s\n' "${activation}" >&2
    exit 6
fi

assert_sentinel_configuration() {
    local configuration
    configuration="$(curl --fail --silent \
        --header "X-Emby-Token: ${access_token}" \
        "${server_url}/Plugins/${plugin_id}/Configuration")"
    if ! jq --exit-status \
        --argjson revision "${active_revision}" \
        --arg collection_id "${sentinel_collection_id}" \
        --arg collection_name "${sentinel_collection_name}" \
        '.SchemaVersion == 1
         and .Revision == $revision
         and .StartupReconcileDelayMinutes == 17
         and .DestructiveCircuitBreakerEnabled == true
         and .DestructiveMaximumAffectedItems == 13
         and .DestructiveMaximumRemovalPercentage == 37
         and .DestructiveMinimumAssignmentPopulation == 14
         and .DestructiveCircuitBreakerDisableAcknowledged == false
         and ((.MappingGroups // []) | length == 1)
         and .MappingGroups[0].Target.Kind == "Collection"
         and .MappingGroups[0].Target.CollectionId == $collection_id
         and .MappingGroups[0].Target.CollectionDisplayName == $collection_name
         and ((.MappingGroups[0].Sources // []) | length == 2)
         and .MappingGroups[0].Sources[0].Kind == "Tag"
         and .MappingGroups[0].Sources[0].TagValue == "Upgrade-Sentinel-Source-A"
         and .MappingGroups[0].Sources[1].Kind == "Tag"
         and .MappingGroups[0].Sources[1].TagValue == "Upgrade-Sentinel-Source-B"
         and .MappingGroups[0].Policy == "Authoritative"
         and .MappingGroups[0].IsEnabled == false' \
        <<<"${configuration}" >/dev/null; then
        printf 'Sentinel configuration was not retained: %s\n' "${configuration}" >&2
        exit 7
    fi
}

assert_sentinel_configuration
"${jprm}" repo add \
    --url "${manifest_base_url}" \
    "${manifest_dir}" \
    "${candidate_package}"
assert_catalog_version "${candidate_version}"
install_catalog_version "${candidate_version}"
assert_sentinel_configuration

printf 'Upgraded Collection Tag Sync %s to %s through the Jellyfin catalog with configuration revision %s retained.\n' \
    "${previous_version}" "${candidate_version}" "${active_revision}"
