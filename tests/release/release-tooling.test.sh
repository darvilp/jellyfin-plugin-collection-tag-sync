#!/usr/bin/env bash
# shellcheck disable=SC2016 # GitHub and shell expressions are intentional static workflow assertions.

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/../.." && pwd)"
verify_release="${project_root}/scripts/verify-release-contract.sh"
prepare_assets="${project_root}/scripts/prepare-release-assets.sh"
verify_history="${project_root}/scripts/verify-manifest-history.sh"
workflow_path="${project_root}/.github/workflows/release.yml"
read_build_metadata="${project_root}/scripts/read-build-metadata.sh"
temp_root="$(mktemp -d /tmp/collection-tag-sync-release-test.XXXXXX)"
trap 'rm -rf -- "${temp_root}"' EXIT

version="$("${read_build_metadata}" version)"
upgrade_from="$("${read_build_metadata}" upgradeFrom)"
release_notes="${project_root}/docs/releases/v${version}-alpha.md"
tag="v${version}"
target_abi="10.11.11.0"
asset_name="Jellyfin.Plugin.CollectionTagSync_${version}.zip"
source_url="https://github.com/darvilp/jellyfin-plugin-collection-tag-sync/releases/download/${tag}/${asset_name}"
source_ref_literal='ref: ${{ env.SOURCE_REF }}'
build_output_literal='source-commit: ${{ steps.source.outputs.commit }}'
publish_ref_literal='ref: ${{ needs.build.outputs.source-commit }}'
plugin_url_literal='--plugin-url "${ASSET_URL}"'
manifest_install_literal='scripts/test-manifest-install.sh "${PACKAGE}"'
manifest_upgrade_literal='scripts/test-manifest-upgrade.sh "${PACKAGE}"'
upgrade_gate_literal="if: steps.upgrade-contract.outputs.enabled == 'true'"

test -f "${workflow_path}"
grep --fixed-strings -- '- "v*.*.*.*"' "${workflow_path}" >/dev/null
grep --fixed-strings 'group: collection-tag-sync-release' "${workflow_path}" >/dev/null
grep --fixed-strings 'contents: read' "${workflow_path}" >/dev/null
grep --fixed-strings 'contents: write' "${workflow_path}" >/dev/null
grep --fixed-strings "${source_ref_literal}" "${workflow_path}" >/dev/null
grep --fixed-strings 'release-bundle/source-commit.txt' "${workflow_path}" >/dev/null
grep --fixed-strings "${build_output_literal}" "${workflow_path}" >/dev/null
grep --fixed-strings "${publish_ref_literal}" "${workflow_path}" >/dev/null
grep --fixed-strings 'Reject a moved release tag' "${workflow_path}" >/dev/null
grep --fixed-strings -- "${plugin_url_literal}" "${workflow_path}" >/dev/null
grep --fixed-strings "${manifest_install_literal}" "${workflow_path}" >/dev/null
grep --fixed-strings "${manifest_upgrade_literal}" "${workflow_path}" >/dev/null
grep --fixed-strings 'id: upgrade-contract' "${workflow_path}" >/dev/null
grep --fixed-strings "${upgrade_gate_literal}" "${workflow_path}" >/dev/null
grep --fixed-strings 'scripts/test-env.sh reset --confirm' "${workflow_path}" >/dev/null
grep --fixed-strings 'export JFTS_UID JFTS_GID' "${workflow_path}" >/dev/null
test "$(grep --count --fixed-strings 'export JFTS_UID JFTS_GID' "${workflow_path}")" -eq 2
grep --fixed-strings 'gh release create' "${workflow_path}" >/dev/null
grep --fixed-strings -- '--draft' "${workflow_path}" >/dev/null
if grep --extended-regexp --line-number 'uses: [^ ]+@(main|master|v[0-9]+)' "${workflow_path}"; then
    printf 'Release workflow actions must be pinned to immutable commit SHAs.\n' >&2
    exit 1
fi
publish_checkout_line="$(grep --line-number --fixed-strings \
    "${publish_ref_literal}" \
    "${workflow_path}" | cut -d: -f1)"
bundle_download_line="$(grep --line-number --fixed-strings \
    'name: Download validated release bundle' \
    "${workflow_path}" | cut -d: -f1)"
if ((publish_checkout_line >= bundle_download_line)); then
    printf 'Publish must check out before downloading the validated release bundle.\n' >&2
    exit 1
fi
for heading in '## Compatibility' '## Included behavior' '## Configuration and upgrade notes' '## Known limitations'; do
    grep --fixed-strings "${heading}" "${release_notes}" >/dev/null
done
test -x "${project_root}/scripts/test-manifest-upgrade.sh"
test "${upgrade_from}" != "${version}"
test -f "${project_root}/docs/releases/v${upgrade_from}-alpha.md"
shellcheck "${project_root}/scripts/test-manifest-upgrade.sh"
if grep --extended-regexp --line-number '0\.1\.0\.0|10\.11\.11\.0' \
    "${project_root}/scripts/package.sh" \
    "${project_root}/scripts/install-local-plugin.sh" \
    "${project_root}/scripts/test-manifest-install.sh" \
    "${project_root}/scripts/test-manifest-upgrade.sh"; then
    printf 'Package/install scripts must derive version and ABI from build.yaml.\n' >&2
    exit 1
fi

make_package() {
    local package_version="$1"
    local package_abi="$2"
    local output_path="$3"
    local staging="${temp_root}/package-${package_version}-${package_abi}"

    mkdir -p "${staging}"
    printf 'test assembly\n' >"${staging}/Jellyfin.Plugin.CollectionTagSync.dll"
    jq --null-input \
        --arg version "${package_version}" \
        --arg target_abi "${package_abi}" \
        '{
            name: "Collection Tag Sync",
            guid: "04920eee-c499-4b13-890f-7af0175f28f0",
            version: $version,
            targetAbi: $target_abi
        }' >"${staging}/meta.json"
    (
        cd -- "${staging}"
        zip --quiet "${output_path}" Jellyfin.Plugin.CollectionTagSync.dll meta.json
    )
}

expect_failure() {
    local expected_message="$1"
    shift
    local output

    if output="$("$@" 2>&1)"; then
        printf 'Expected command to fail: %s\n' "$*" >&2
        exit 1
    fi

    if ! grep --fixed-strings "${expected_message}" <<<"${output}" >/dev/null; then
        printf 'Expected failure containing %q, got:\n%s\n' "${expected_message}" "${output}" >&2
        exit 1
    fi
}

package_path="${temp_root}/${asset_name}"
make_package "${version}" "${target_abi}" "${package_path}"

"${verify_release}" "${tag}" "${package_path}"

mismatched_version="${version%.*}.$((10#${version##*.} + 1))"
expect_failure \
    'does not match build.yaml version' \
    "${verify_release}" "v${mismatched_version}" "${package_path}"

mismatched_package="${temp_root}/mismatched.zip"
make_package "${mismatched_version}" "${target_abi}" "${mismatched_package}"
expect_failure \
    "Package version ${mismatched_version} does not match build.yaml version ${version}." \
    "${verify_release}" "${tag}" "${mismatched_package}"

assets_dir="${temp_root}/assets"
"${prepare_assets}" "${tag}" "${package_path}" "${assets_dir}"
test -f "${assets_dir}/${asset_name}"
test -f "${assets_dir}/${asset_name}.sha256"
(
    cd -- "${assets_dir}"
    sha256sum --check "${asset_name}.sha256"
)

package_md5="$(md5sum "${package_path}" | awk '{print $1}')"
manifest_path="${temp_root}/manifest.json"
before_manifest="${temp_root}/manifest-before.json"
jq --null-input \
    '[{
        guid: "04920eee-c499-4b13-890f-7af0175f28f0",
        name: "Collection Tag Sync",
        versions: [{
            version: "0.0.9.0",
            targetAbi: "10.11.11.0",
            sourceUrl: "https://example.invalid/prior.zip",
            checksum: "00000000000000000000000000000000"
        }]
    }]' >"${before_manifest}"
jq --null-input \
    --arg version "${version}" \
    --arg target_abi "${target_abi}" \
    --arg source_url "${source_url}" \
    --arg checksum "${package_md5}" \
    '[{
        guid: "04920eee-c499-4b13-890f-7af0175f28f0",
        name: "Collection Tag Sync",
        versions: [
            {
                version: "0.0.9.0",
                targetAbi: "10.11.11.0",
                sourceUrl: "https://example.invalid/prior.zip",
                checksum: "00000000000000000000000000000000"
            },
            {
                version: $version,
                targetAbi: $target_abi,
                sourceUrl: $source_url,
                checksum: $checksum
            }
        ]
    }]' >"${manifest_path}"

"${verify_release}" "${tag}" "${package_path}" "${manifest_path}" "${source_url}"
"${verify_history}" "${before_manifest}" "${manifest_path}"

jq '.[0].versions = [.[0].versions[-1]]' \
    "${manifest_path}" >"${temp_root}/lost-history-manifest.json"
expect_failure \
    'Generated manifest changed or removed a previously published version entry.' \
    "${verify_history}" "${before_manifest}" "${temp_root}/lost-history-manifest.json"

jq '.[0].versions[-1].sourceUrl = "https://example.invalid/wrong.zip"' \
    "${manifest_path}" >"${temp_root}/wrong-manifest.json"
expect_failure \
    'Manifest sourceUrl does not match the immutable release asset URL.' \
    "${verify_release}" "${tag}" "${package_path}" \
    "${temp_root}/wrong-manifest.json" "${source_url}"

printf 'Release tooling contract tests passed.\n'
