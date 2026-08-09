#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
plugin_id="04920eee-c499-4b13-890f-7af0175f28f0"
activation_url="${server_url}/CollectionTagSync/Configuration"
plugin_directory="${project_root}/.testenv/jellyfin/config/plugins/Collection Tag Sync_0.1.0.0"
plugin_dll="${plugin_directory}/Jellyfin.Plugin.CollectionTagSync.dll"
disabled_dll="${project_root}/.testenv/jellyfin/Jellyfin.Plugin.CollectionTagSync.disabled"

if [[ ! -f "${token_file}" ]]; then
    printf 'Missing test-server token. Run scripts/configure-test-server.sh first.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"
source_tag="Full-Reconcile-Source-$(date -u +'%Y%m%d%H%M%S%N')"
target_tag="Full-Reconcile-Target-$(date -u +'%Y%m%d%H%M%S%N')"
movie_id=''
series_id=''
movie_original_tags=''
series_original_tags=''
original_configuration=''
plugin_disabled=false

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

    printf 'Jellyfin did not become healthy during Full Reconcile validation.\n' >&2
    exit 3
}

set_item_tags() {
    local item_id="$1"
    local tags="$2"
    local item
    local updated
    item="$(api_get "${server_url}/Items/${item_id}")"
    updated="$(jq --argjson tags "${tags}" '.Tags = $tags' <<<"${item}")"
    api_post_json "${server_url}/Items/${item_id}" "${updated}" >/dev/null
}

activate_configuration() {
    local response
    local request_id
    local state=''
    response="$(api_post_json "${activation_url}" "$1")"
    request_id="$(jq --raw-output '.ReconciliationId // .reconciliationId' <<<"${response}")"
    for _ in {1..30}; do
        state="$(api_get "${activation_url}/Reconciliations/${request_id}" \
            | jq --raw-output '.State // .state')"
        if [[ "${state}" == "2" || "${state}" == "Completed" ]]; then
            return
        fi

        sleep 1
    done

    printf 'Configuration activation %s did not complete; state=%s.\n' \
        "${request_id}" "${state}" >&2
    return 1
}

restore_test_state() {
    set +e
    if [[ -f "${disabled_dll}" ]]; then
        mv "${disabled_dll}" "${plugin_dll}"
        plugin_disabled=false
        restart_server >/dev/null
    fi

    if [[ "${plugin_disabled}" == "true" ]]; then
        restart_server >/dev/null
    fi

    if [[ -n "${original_configuration}" ]]; then
        activate_configuration '{"SchemaVersion":1,"StartupReconcileDelayMinutes":5,"MappingGroups":[]}' >/dev/null
    fi

    if [[ -n "${movie_id}" && -n "${movie_original_tags}" ]]; then
        set_item_tags "${movie_id}" "${movie_original_tags}" >/dev/null
    fi

    if [[ -n "${series_id}" && -n "${series_original_tags}" ]]; then
        set_item_tags "${series_id}" "${series_original_tags}" >/dev/null
    fi

    if [[ -n "${original_configuration}" ]]; then
        activate_configuration "${original_configuration}" >/dev/null
    fi
}

trap restore_test_state EXIT

original_configuration="$(api_get "${server_url}/Plugins/${plugin_id}/Configuration")"
if ! jq --exit-status '(.MappingGroups // []) | length == 0' <<<"${original_configuration}" >/dev/null; then
    printf 'Full Reconcile validation requires the isolated server to start with no mappings.\n' >&2
    exit 2
fi

items="$(api_get "${server_url}/Items?Recursive=true&IncludeItemTypes=Movie,Series&Fields=Tags")"
movie_id="$(jq --raw-output '.Items[] | select(.Type == "Movie") | .Id' <<<"${items}" | head -n 1)"
series_id="$(jq --raw-output '.Items[] | select(.Type == "Series") | .Id' <<<"${items}" | head -n 1)"
movie_original_tags="$(jq --compact-output --arg item_id "${movie_id}" \
    '.Items[] | select(.Id == $item_id) | (.Tags // [])' <<<"${items}")"
series_original_tags="$(jq --compact-output --arg item_id "${series_id}" \
    '.Items[] | select(.Id == $item_id) | (.Tags // [])' <<<"${items}")"

configuration="$(jq --null-input \
    --arg source_tag "${source_tag}" \
    --arg target_tag "${target_tag}" \
    '{
        SchemaVersion: 1,
        StartupReconcileDelayMinutes: 60,
        MappingGroups: [
            {
                Target: {Kind: 0, TagValue: $target_tag},
                Sources: [{Kind: 0, TagValue: $source_tag}],
                Policy: 1,
                IsEnabled: true
            }
        ]
    }')"
activate_configuration "${configuration}"

if [[ ! -f "${plugin_dll}" || -e "${disabled_dll}" ]]; then
    printf 'Expected exactly one installed test plugin DLL at %s.\n' "${plugin_dll}" >&2
    exit 4
fi

mv "${plugin_dll}" "${disabled_dll}"
plugin_disabled=true
restart_server

movie_drift_tags="$(jq --arg tag "${source_tag}" '. + [$tag] | unique' <<<"${movie_original_tags}")"
series_drift_tags="$(jq --arg tag "${target_tag}" '. + [$tag] | unique' <<<"${series_original_tags}")"
set_item_tags "${movie_id}" "${movie_drift_tags}"
set_item_tags "${series_id}" "${series_drift_tags}"

mv "${disabled_dll}" "${plugin_dll}"
plugin_disabled=false
event_start="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
restart_server

tasks="$(api_get "${server_url}/ScheduledTasks")"
task_id="$(jq --raw-output '.[] | select(.Key == "CollectionTagSyncFullReconcile") | .Id' <<<"${tasks}")"
prior_end="$(jq --raw-output \
    '.[] | select(.Key == "CollectionTagSyncFullReconcile") | (.LastExecutionResult.EndTimeUtc // "")' \
    <<<"${tasks}")"
if [[ -z "${task_id}" ]]; then
    printf 'Jellyfin did not discover the Collection Tag Sync Full Reconcile task.\n' >&2
    exit 5
fi

curl --fail --silent --request POST \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/ScheduledTasks/Running/${task_id}"

task=''
for _ in {1..60}; do
    task="$(api_get "${server_url}/ScheduledTasks" \
        | jq '.[] | select(.Key == "CollectionTagSyncFullReconcile")')"
    current_end="$(jq --raw-output '.LastExecutionResult.EndTimeUtc // ""' <<<"${task}")"
    status="$(jq --raw-output '.LastExecutionResult.Status // ""' <<<"${task}")"
    if [[ "${current_end}" != "${prior_end}" && "${status}" == "Completed" ]]; then
        break
    fi

    sleep 1
done

if [[ "${status:-}" != "Completed" || "${current_end:-}" == "${prior_end}" ]]; then
    printf 'Full Reconcile scheduled task did not complete: %s\n' "${task}" >&2
    exit 6
fi

movie_after="$(api_get "${server_url}/Items/${movie_id}")"
series_after="$(api_get "${server_url}/Items/${series_id}")"
if ! jq --exit-status --arg target_tag "${target_tag}" \
    '.Tags | any(. == $target_tag)' <<<"${movie_after}" >/dev/null; then
    printf 'Full Reconcile did not repair the missed Movie target tag.\n' >&2
    exit 7
fi

if jq --exit-status --arg target_tag "${target_tag}" \
    '.Tags | any(. == $target_tag)' <<<"${series_after}" >/dev/null; then
    printf 'Full Reconcile did not remove the unsupported Series target tag.\n' >&2
    exit 8
fi

logs="$(docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" \
    logs --since "${event_start}" jellyfin)"
if ! rg --fixed-strings \
    'Collection Tag Sync Full Reconcile finished' <<<"${logs}" >/dev/null \
    || ! rg --fixed-strings 'Succeeded=2 Failed=0' <<<"${logs}" >/dev/null; then
    printf 'Full Reconcile terminal summary was missing from Jellyfin logs.\n' >&2
    exit 9
fi

startup_configuration="$(jq '.StartupReconcileDelayMinutes = 0' <<<"${configuration}")"
activate_configuration "${startup_configuration}"
startup_event_start="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
restart_server

startup_logs=''
for _ in {1..60}; do
    startup_logs="$(docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" \
        logs --since "${startup_event_start}" jellyfin)"
    startup_request_count="$({ rg --fixed-strings \
        'queued startup Full Reconcile after DelayMinutes=0' <<<"${startup_logs}" || true; } | wc -l)"
    startup_run_count="$({ rg --fixed-strings \
        'Collection Tag Sync Full Reconcile finished' <<<"${startup_logs}" || true; } | wc -l)"
    if [[ "${startup_request_count}" -eq 1 && "${startup_run_count}" -eq 1 ]]; then
        break
    fi

    sleep 1
done

if [[ "${startup_request_count:-0}" -ne 1 || "${startup_run_count:-0}" -ne 1 ]]; then
    printf 'Zero-delay startup did not queue and complete exactly one Full Reconcile.\n' >&2
    exit 10
fi

printf 'Verified Jellyfin scheduled-task discovery and manual Full Reconcile completion.\n'
printf 'Verified offline missed addition and unsupported Authoritative target repair for Movie and Series.\n'
printf 'Verified one zero-delay startup Full Reconcile after Jellyfin core readiness.\n'
