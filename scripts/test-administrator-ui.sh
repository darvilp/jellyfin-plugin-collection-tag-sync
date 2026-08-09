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
response_body=''
response_code=''

request_with_status() {
    local method="$1"
    local url="$2"
    local authenticated="${3:-true}"
    local -a arguments=(--silent --show-error --request "${method}" --write-out $'\n%{http_code}')
    if [[ "${authenticated}" == "true" ]]; then
        arguments+=(--header "X-Emby-Token: ${access_token}")
    fi

    response="$(curl "${arguments[@]}" "${url}")"
    response_code="${response##*$'\n'}"
    response_body="${response%$'\n'*}"
}

configuration_page="$(curl --fail --silent --get \
    --header "X-Emby-Token: ${access_token}" \
    --data-urlencode 'name=Collection Tag Sync' \
    "${server_url}/web/ConfigurationPage")"
if [[ "${configuration_page}" != *'data-controller="__plugin/Collection Tag Sync.js"'* \
    || "${configuration_page}" != *'id="collectionTagSyncMappings"'* \
    || "${configuration_page}" != *'id="collectionTagSyncRunOnce"'* \
    || "${configuration_page}" != *'id="collectionTagSyncFullReconcile"'* \
    || "${configuration_page}" != *'<span>Save configuration</span>'* \
    || "${configuration_page}" != *'<span>Preview changes</span>'* \
    || "${configuration_page}" != *'<span>Confirm removals and save</span>'* \
    || "${configuration_page}" == *'Validate and save'* \
    || "${configuration_page}" != *'aria-live="polite"'* ]]; then
    printf 'The embedded administrator page did not expose its required workflows.\n' >&2
    exit 3
fi

controller="$(curl --fail --silent --get \
    --header "X-Emby-Token: ${access_token}" \
    --data-urlencode 'name=Collection Tag Sync.js' \
    "${server_url}/web/ConfigurationPage")"
if [[ "${controller}" != *'export default function (view)'* \
    || "${controller}" != *'CollectionTagSync/Configuration/Preview'* \
    || "${controller}" != *'CollectionTagSync/RunOnce/Preview'* ]]; then
    printf 'The embedded thin UI controller was not served intact.\n' >&2
    exit 4
fi

for endpoint in \
    "${server_url}/CollectionTagSync/Tags/Picker" \
    "${server_url}/CollectionTagSync/Status"; do
    request_with_status GET "${endpoint}" false
    if [[ "${response_code}" != "401" ]]; then
        printf 'Unauthenticated UI support endpoint returned HTTP %s instead of 401: %s\n' \
            "${response_code}" "${endpoint}" >&2
        exit 5
    fi
done

request_with_status POST "${server_url}/CollectionTagSync/FullReconcile" false
if [[ "${response_code}" != "401" ]]; then
    printf 'Unauthenticated Full Reconcile queue returned HTTP %s instead of 401.\n' \
        "${response_code}" >&2
    exit 6
fi

request_with_status GET "${server_url}/CollectionTagSync/Tags/Picker"
if [[ "${response_code}" != "200" ]] \
    || ! jq --exit-status 'type == "array"' <<<"${response_body}" >/dev/null; then
    printf 'Elevated tag picker did not return an array: HTTP %s, %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 7
fi

request_with_status GET "${server_url}/CollectionTagSync/Status"
if [[ "${response_code}" != "200" ]] \
    || ! jq --exit-status \
        'has("Incremental") and has("FullReconcileRequest") and has("UnresolvedGroups")' \
        <<<"${response_body}" >/dev/null; then
    printf 'Elevated operational status did not return the privacy-safe UI snapshot: %s\n' \
        "${response_body}" >&2
    exit 8
fi

request_with_status POST "${server_url}/CollectionTagSync/FullReconcile"
if [[ "${response_code}" != "202" ]] \
    || ! jq --exit-status \
        '(.IsRequested // .isRequested) == true' <<<"${response_body}" >/dev/null; then
    printf 'Elevated Full Reconcile action did not queue background work: HTTP %s, %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 9
fi

full_reconcile_state=''
for _ in {1..30}; do
    request_with_status GET "${server_url}/CollectionTagSync/FullReconcile/Status"
    full_reconcile_state="$(jq --raw-output '.State // .state' <<<"${response_body}")"
    if [[ "${full_reconcile_state}" == "3" \
        || "${full_reconcile_state}" == "4" \
        || "${full_reconcile_state}" == "5" \
        || "${full_reconcile_state}" == "6" \
        || "${full_reconcile_state}" == "AwaitingApproval" \
        || "${full_reconcile_state}" == "Completed" \
        || "${full_reconcile_state}" == "CompletedWithFailures" \
        || "${full_reconcile_state}" == "Failed" ]]; then
        break
    fi

    sleep 1
done

if [[ "${full_reconcile_state}" != "3" \
    && "${full_reconcile_state}" != "4" \
    && "${full_reconcile_state}" != "5" \
    && "${full_reconcile_state}" != "AwaitingApproval" \
    && "${full_reconcile_state}" != "Completed" \
    && "${full_reconcile_state}" != "CompletedWithFailures" ]]; then
    printf 'Full Reconcile did not reach a safe terminal or paused state: %s\n' \
        "${response_body}" >&2
    exit 10
fi

printf 'Verified embedded administrator page/controller resources on Jellyfin 10.11.11.\n'
printf 'Verified elevated tag/status support and background Full Reconcile queueing.\n'
