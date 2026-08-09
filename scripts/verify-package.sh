#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
artifact_path="${1:-}"
expected_version="${2:-$("${script_dir}/read-build-metadata.sh" version)}"
expected_target_abi="${3:-$("${script_dir}/read-build-metadata.sh" targetAbi)}"
if [[ -z "${artifact_path}" || ! -f "${artifact_path}" ]]; then
    printf 'Package does not exist: %s\n' "${artifact_path}" >&2
    exit 2
fi

mapfile -t package_entries < <(unzip -Z1 "${artifact_path}" | sort)
if [[ "${#package_entries[@]}" -ne 2 ]]; then
    printf 'Expected two package entries, found %s:\n' "${#package_entries[@]}" >&2
    printf '  %s\n' "${package_entries[@]}" >&2
    exit 3
fi

if [[ "${package_entries[0]}" != "Jellyfin.Plugin.CollectionTagSync.dll" || "${package_entries[1]}" != "meta.json" ]]; then
    printf 'Unexpected package entries:\n' >&2
    printf '  %s\n' "${package_entries[@]}" >&2
    exit 4
fi

if ! unzip -p "${artifact_path}" meta.json | jq --exit-status \
    --arg version "${expected_version}" \
    --arg target_abi "${expected_target_abi}" \
    '.name == "Collection Tag Sync"
     and .guid == "04920eee-c499-4b13-890f-7af0175f28f0"
     and .version == $version
     and .targetAbi == $target_abi' >/dev/null; then
    printf 'Package metadata does not match the pinned plugin contract.\n' >&2
    exit 5
fi

printf 'Verified package contents and metadata: %s\n' "${artifact_path}"
