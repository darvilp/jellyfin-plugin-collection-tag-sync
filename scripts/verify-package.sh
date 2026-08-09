#!/usr/bin/env bash

set -euo pipefail

artifact_path="${1:-}"
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
    '.name == "Collection Tag Sync"
     and .guid == "04920eee-c499-4b13-890f-7af0175f28f0"
     and .version == "0.1.0.0"
     and .targetAbi == "10.11.11.0"' >/dev/null; then
    printf 'Package metadata does not match the pinned plugin contract.\n' >&2
    exit 5
fi

printf 'Verified package contents and metadata: %s\n' "${artifact_path}"
