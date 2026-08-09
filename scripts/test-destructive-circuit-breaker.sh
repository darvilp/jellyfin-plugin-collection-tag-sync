#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
plugin_version="$("${script_dir}/read-build-metadata.sh" version)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
plugin_id="04920eee-c499-4b13-890f-7af0175f28f0"
activation_url="${server_url}/CollectionTagSync/Configuration"
full_reconcile_url="${server_url}/CollectionTagSync/FullReconcile"
plugin_directory="${project_root}/.testenv/jellyfin/config/plugins/Collection Tag Sync_${plugin_version}"
plugin_dll="${plugin_directory}/Jellyfin.Plugin.CollectionTagSync.dll"
disabled_dll="${project_root}/.testenv/jellyfin/Jellyfin.Plugin.CollectionTagSync.disabled"

if [[ ! -f "${token_file}" ]]; then
    printf 'Missing test-server token. Run scripts/configure-test-server.sh first.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"
source_tag="Waltney-Circuit-Source-$(date -u +'%Y%m%d%H%M%S%N')"
target_tag="Blooth-Circuit-Target-$(date -u +'%Y%m%d%H%M%S%N')"
movie_id=''
series_id=''
movie_title=''
series_title=''
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

api_post_json_with_status() {
    curl --silent --request POST \
        --header "X-Emby-Token: ${access_token}" \
        --header 'Content-Type: application/json' \
        --data "$2" \
        --write-out $'\n%{http_code}' \
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

    printf 'Jellyfin did not become healthy during destructive circuit-breaker validation.\n' >&2
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
if ! jq --exit-status \
    '((.MappingGroups // []) | length == 0) and (.PausedFullReconcile == null)' \
    <<<"${original_configuration}" >/dev/null; then
    printf 'Circuit-breaker validation requires no mappings or prior paused preview.\n' >&2
    exit 2
fi

items="$(api_get "${server_url}/Items?Recursive=true&IncludeItemTypes=Movie,Series&Fields=Tags")"
movie_id="$(jq --raw-output '.Items[] | select(.Type == "Movie") | .Id' <<<"${items}" | head -n 1)"
series_id="$(jq --raw-output '.Items[] | select(.Type == "Series") | .Id' <<<"${items}" | head -n 1)"
movie_title="$(jq --raw-output --arg item_id "${movie_id}" \
    '.Items[] | select(.Id == $item_id) | .Name' <<<"${items}")"
series_title="$(jq --raw-output --arg item_id "${series_id}" \
    '.Items[] | select(.Id == $item_id) | .Name' <<<"${items}")"
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
        DestructiveCircuitBreakerEnabled: true,
        DestructiveMaximumAffectedItems: 1,
        DestructiveMaximumRemovalPercentage: 100,
        DestructiveMinimumAssignmentPopulation: 10,
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

mv "${plugin_dll}" "${disabled_dll}"
plugin_disabled=true
restart_server

movie_drift_tags="$(jq --arg tag "${target_tag}" '. + [$tag] | unique' <<<"${movie_original_tags}")"
series_drift_tags="$(jq --arg tag "${target_tag}" '. + [$tag] | unique' <<<"${series_original_tags}")"
set_item_tags "${movie_id}" "${movie_drift_tags}"
set_item_tags "${series_id}" "${series_drift_tags}"

mv "${disabled_dll}" "${plugin_dll}"
plugin_disabled=false
restart_server

tasks="$(api_get "${server_url}/ScheduledTasks")"
task_id="$(jq --raw-output '.[] | select(.Key == "CollectionTagSyncFullReconcile") | .Id' <<<"${tasks}")"
prior_end="$(jq --raw-output \
    '.[] | select(.Key == "CollectionTagSyncFullReconcile") | (.LastExecutionResult.EndTimeUtc // "")' \
    <<<"${tasks}")"
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

full_status="$(api_get "${full_reconcile_url}/Status")"
run_id="$(jq --raw-output '.Id // .id' <<<"${full_status}")"
run_state="$(jq --raw-output '.State // .state' <<<"${full_status}")"
if [[ "${run_state}" != "3" && "${run_state}" != "AwaitingApproval" ]]; then
    printf 'Expected Full Reconcile to await approval; status=%s.\n' "${full_status}" >&2
    exit 4
fi

movie_paused="$(api_get "${server_url}/Items/${movie_id}")"
series_paused="$(api_get "${server_url}/Items/${series_id}")"
if ! jq --exit-status --arg target_tag "${target_tag}" \
    '.Tags | any(. == $target_tag)' <<<"${movie_paused}" >/dev/null \
    || ! jq --exit-status --arg target_tag "${target_tag}" \
        '.Tags | any(. == $target_tag)' <<<"${series_paused}" >/dev/null; then
    printf 'The paused Full Reconcile applied a destructive mutation before confirmation.\n' >&2
    exit 5
fi

preview="$(api_post_json "${full_reconcile_url}/${run_id}/Preview" '{}')"
authorization="$(jq --raw-output '.Authorization // .authorization' <<<"${preview}")"
if ! jq --exit-status \
    --arg movie_id "${movie_id}" \
    --arg movie_title "${movie_title}" \
    --arg series_id "${series_id}" \
    --arg series_title "${series_title}" \
    '(.Preview.UniqueAffectedItemCount // .preview.uniqueAffectedItemCount) == 2
     and ((.Preview.Removals // .preview.removals) | length == 2)
     and ((.Preview.Items // .preview.items) | length == 2)
     and ((.Preview.Items // .preview.items)
          | all(
              ((.ItemId // .itemId) == $movie_id
                  and (.ItemTitle // .itemTitle) == $movie_title)
              or ((.ItemId // .itemId) == $series_id
                  and (.ItemTitle // .itemTitle) == $series_title)))
     and ((.Preview.Items // .preview.items)
          | all((.Mutations // .mutations) | length == 1))' \
    <<<"${preview}" >/dev/null; then
    printf 'Paused preview did not expose the expected item titles and diagnostics.\n' >&2
    exit 6
fi

restart_server
rehydrated="$(api_get "${full_reconcile_url}/Status")"
rehydrated_state="$(jq --raw-output '.State // .state' <<<"${rehydrated}")"
if [[ "${rehydrated_state}" != "3" && "${rehydrated_state}" != "AwaitingApproval" ]]; then
    printf 'Restart did not rehydrate the persisted paused status.\n' >&2
    exit 7
fi

confirmation_body="$(jq --null-input --arg authorization "${authorization}" \
    '{Authorization: $authorization}')"
old_confirmation="$(api_post_json_with_status \
    "${full_reconcile_url}/${run_id}/Confirm" "${confirmation_body}")"
old_status="${old_confirmation##*$'\n'}"
if [[ "${old_status}" != "409" ]]; then
    printf 'A pre-restart authorization was not rejected; HTTP %s.\n' "${old_status}" >&2
    exit 8
fi

fresh_preview="$(api_post_json "${full_reconcile_url}/${run_id}/Preview" '{}')"
fresh_authorization="$(jq --raw-output '.Authorization // .authorization' <<<"${fresh_preview}")"
fresh_body="$(jq --null-input --arg authorization "${fresh_authorization}" \
    '{Authorization: $authorization}')"
fresh_confirmation="$(api_post_json_with_status \
    "${full_reconcile_url}/${run_id}/Confirm" "${fresh_body}")"
fresh_status="${fresh_confirmation##*$'\n'}"
fresh_response="${fresh_confirmation%$'\n'*}"
if [[ "${fresh_status}" != "200" ]] \
    || ! jq --exit-status \
        '((.Outcome // .outcome) == 0 or (.Outcome // .outcome) == "Accepted")
         and ((.RunResult.State // .runResult.state) == 4
              or (.RunResult.State // .runResult.state) == "Completed")' \
        <<<"${fresh_response}" >/dev/null; then
    printf 'Fresh equivalent confirmation did not complete: HTTP %s body=%s.\n' \
        "${fresh_status}" "${fresh_response}" >&2
    exit 9
fi

movie_after="$(api_get "${server_url}/Items/${movie_id}")"
series_after="$(api_get "${server_url}/Items/${series_id}")"
if jq --exit-status --arg target_tag "${target_tag}" \
    '.Tags | any(. == $target_tag)' <<<"${movie_after}" >/dev/null \
    || jq --exit-status --arg target_tag "${target_tag}" \
        '.Tags | any(. == $target_tag)' <<<"${series_after}" >/dev/null; then
    printf 'Confirmed equivalent Full Reconcile did not apply both removals.\n' >&2
    exit 10
fi

reused="$(api_post_json_with_status "${full_reconcile_url}/${run_id}/Confirm" "${fresh_body}")"
reused_status="${reused##*$'\n'}"
if [[ "${reused_status}" != "409" ]]; then
    printf 'A consumed authorization was not rejected; HTTP %s.\n' "${reused_status}" >&2
    exit 11
fi

printf 'Verified atomic destructive pause with item-level persisted diagnostics.\n'
printf 'Verified restart invalidates authorization while rehydrating paused status.\n'
printf 'Verified fresh equivalent confirmation recomputes, applies, and is single-use.\n'
