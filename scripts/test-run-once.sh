#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
server_url="http://127.0.0.1:18096"
token_file="${project_root}/.testenv/jellyfin/access-token"
plugin_id="04920eee-c499-4b13-890f-7af0175f28f0"
activation_url="${server_url}/CollectionTagSync/Configuration"
run_once_url="${server_url}/CollectionTagSync/RunOnce"
plugin_configuration_url="${server_url}/Plugins/${plugin_id}/Configuration"

if [[ ! -f "${token_file}" ]]; then
    printf 'Missing test-server token. Run scripts/configure-test-server.sh first.\n' >&2
    exit 2
fi

access_token="$(<"${token_file}")"
source_tag="Waltney-RunOnce-$(date -u +'%Y%m%d%H%M%S%N')"
cascade_tag="Blooth-RunOnce-$(date -u +'%Y%m%d%H%M%S%N')"
movie_id=''
movie_title=''
original_tags=''
original_configuration=''
animation_id=''
kids_id=''
response_body=''
response_code=''

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

request_with_status() {
    local url="$1"
    local body="$2"
    local response
    response="$(curl --silent --show-error --request POST \
        --header "X-Emby-Token: ${access_token}" \
        --header 'Content-Type: application/json' \
        --data "${body}" \
        --write-out $'\n%{http_code}' \
        "${url}")"
    response_code="${response##*$'\n'}"
    response_body="${response%$'\n'*}"
}

create_collection() {
    local name="$1"
    curl --fail --silent --get --request POST \
        --header "X-Emby-Token: ${access_token}" \
        --data-urlencode "name=${name}" \
        "${server_url}/Collections" \
        | jq --raw-output .Id
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

wait_for_reconciliation() {
    local reconciliation_id="$1"
    local state=''
    for _ in {1..30}; do
        state="$(api_get "${run_once_url}/Reconciliations/${reconciliation_id}" \
            | jq --raw-output '.State // .state')"
        if [[ "${state}" == "2" || "${state}" == "Completed" ]]; then
            return
        fi

        if [[ "${state}" == "3" || "${state}" == "4" \
            || "${state}" == "PartiallyFailed" || "${state}" == "Failed" ]]; then
            printf 'Run-once reconciliation %s failed with state %s.\n' \
                "${reconciliation_id}" "${state}" >&2
            exit 3
        fi

        sleep 1
    done

    printf 'Run-once reconciliation %s did not finish; state=%s.\n' \
        "${reconciliation_id}" "${state}" >&2
    exit 3
}

wait_for_collection_state() {
    local collection_id="$1"
    local item_id="$2"
    local expected="$3"
    local present=''
    for _ in {1..30}; do
        present="$(api_get "${server_url}/Items?ParentId=${collection_id}&Recursive=true" \
            | jq --arg item_id "${item_id}" '.Items | any(.Id == $item_id)')"
        if [[ "${present}" == "${expected}" ]]; then
            return
        fi

        sleep 1
    done

    printf 'Collection %s membership for item %s did not become %s.\n' \
        "${collection_id}" "${item_id}" "${expected}" >&2
    exit 4
}

wait_for_tag_state() {
    local item_id="$1"
    local tag="$2"
    local expected="$3"
    local present=''
    for _ in {1..30}; do
        present="$(api_get "${server_url}/Items/${item_id}" \
            | jq --arg tag "${tag}" \
                '.Tags | any(ascii_downcase == ($tag | ascii_downcase))')"
        if [[ "${present}" == "${expected}" ]]; then
            return
        fi

        sleep 1
    done

    printf 'Tag %s on item %s did not become %s.\n' \
        "${tag}" "${item_id}" "${expected}" >&2
    exit 5
}

activate_configuration() {
    local configuration="$1"
    local response
    local reconciliation_id
    local state=''
    response="$(api_post_json "${activation_url}" "${configuration}")"
    reconciliation_id="$(jq --raw-output '.ReconciliationId // .reconciliationId' <<<"${response}")"
    for _ in {1..30}; do
        state="$(api_get "${activation_url}/Reconciliations/${reconciliation_id}" \
            | jq --raw-output '.State // .state')"
        if [[ "${state}" == "2" || "${state}" == "Completed" ]]; then
            return
        fi

        sleep 1
    done

    printf 'Configuration activation %s did not complete; state=%s.\n' \
        "${reconciliation_id}" "${state}" >&2
    exit 6
}

restore_test_state() {
    set +e
    if [[ -n "${original_configuration}" ]]; then
        activate_configuration '{"SchemaVersion":1,"MappingGroups":[]}' >/dev/null
    fi

    if [[ -n "${movie_id}" && -n "${original_tags}" ]]; then
        set_item_tags "${movie_id}" "${original_tags}" >/dev/null
    fi

    if [[ -n "${original_configuration}" ]]; then
        activate_configuration "${original_configuration}" >/dev/null
    fi
}

trap restore_test_state EXIT

original_configuration="$(api_get "${plugin_configuration_url}")"
if ! jq --exit-status '((.MappingGroups // []) | length == 0)' \
    <<<"${original_configuration}" >/dev/null; then
    printf 'Run-once validation requires the isolated server to start with no mappings.\n' >&2
    exit 2
fi

items="$(api_get "${server_url}/Items?Recursive=true&IncludeItemTypes=Movie,Series&Fields=Tags")"
movie_id="$(jq --raw-output '.Items[] | select(.Type == "Movie") | .Id' <<<"${items}" | head -n 1)"
movie_title="$(jq --raw-output --arg item_id "${movie_id}" \
    '.Items[] | select(.Id == $item_id) | .Name' <<<"${items}")"
original_tags="$(jq --compact-output --arg item_id "${movie_id}" \
    '.Items[] | select(.Id == $item_id) | (.Tags // [])' <<<"${items}")"
animation_name="Run Once Animation $(date -u +'%Y%m%d%H%M%S%N')"
kids_name="Run Once Kids $(date -u +'%Y%m%d%H%M%S%N')"
animation_id="$(create_collection "${animation_name}")"
kids_id="$(create_collection "${kids_name}")"
source_tags="$(jq --arg tag "${source_tag}" '. + [$tag] | unique' <<<"${original_tags}")"
set_item_tags "${movie_id}" "${source_tags}"

continuous_configuration="$(jq --null-input \
    --arg animation_id "${animation_id}" \
    --arg animation_name "${animation_name}" \
    --arg cascade_tag "${cascade_tag}" \
    --arg kids_id "${kids_id}" \
    --arg kids_name "${kids_name}" \
    '{
        SchemaVersion: 1,
        MappingGroups: [
            {
                Target: {Kind: 0, TagValue: $cascade_tag},
                Sources: [{Kind: 1, CollectionId: $animation_id, CollectionDisplayName: $animation_name}],
                Policy: 0,
                IsEnabled: true
            },
            {
                Target: {Kind: 1, CollectionId: $kids_id, CollectionDisplayName: $kids_name},
                Sources: [{Kind: 0, TagValue: $cascade_tag}],
                Policy: 0,
                IsEnabled: true
            }
        ]
    }')"
activate_configuration "${continuous_configuration}"
active_revision="$(jq --raw-output '.Revision' <<<"$(api_get "${plugin_configuration_url}")")"

bootstrap="$(jq --null-input \
    --arg animation_id "${animation_id}" \
    --arg animation_name "${animation_name}" \
    --arg source_tag "${source_tag}" \
    '{
        Target: {Kind: 1, CollectionId: $animation_id, CollectionDisplayName: $animation_name},
        Sources: [{Kind: 0, TagValue: $source_tag}],
        Policy: 0,
        ExcludedItemIds: []
    }')"
request_with_status "${run_once_url}/Preview" "${bootstrap}"
if [[ "${response_code}" != "200" ]]; then
    printf 'Run-once bootstrap preview returned HTTP %s: %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 7
fi

if ! jq --exit-status \
    --arg item_id "${movie_id}" \
    --arg item_title "${movie_title}" \
    --arg animation_id "${animation_id}" \
    --arg cascade_tag "${cascade_tag}" \
    --arg kids_id "${kids_id}" \
    '((.Authorization.ExcludableItemIds // .authorization.excludableItemIds) | index($item_id)) != null
     and ((.Authorization.Preview.Items // .authorization.preview.items)[]
        | select((.ItemId // .itemId) == $item_id)
        | ((.ItemTitle // .itemTitle) == $item_title)
          and ((.Mutations // .mutations) as $mutations
          | (($mutations | length) == 3)
          and ($mutations | any((.Target.CollectionId // .target.collectionId) == $animation_id))
          and ($mutations | any((.Target.TagValue // .target.tagValue) == $cascade_tag))
          and ($mutations | any((.Target.CollectionId // .target.collectionId) == $kids_id))))' \
    <<<"${response_body}" >/dev/null; then
    printf 'Run-once preview did not contain the item title, direct target, and two downstream cascades.\n' >&2
    exit 8
fi

authorization="$(jq --raw-output \
    '.Authorization.Authorization // .authorization.authorization' <<<"${response_body}")"
confirmation="$(jq --null-input \
    --argjson operation "${bootstrap}" \
    --arg authorization "${authorization}" \
    '{Operation: $operation, Authorization: $authorization}')"
request_with_status "${run_once_url}/Confirm" "${confirmation}"
if [[ "${response_code}" != "202" ]]; then
    printf 'Run-once bootstrap confirmation returned HTTP %s: %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 9
fi

reconciliation_id="$(jq --raw-output '.ReconciliationId // .reconciliationId' <<<"${response_body}")"
wait_for_reconciliation "${reconciliation_id}"
wait_for_collection_state "${animation_id}" "${movie_id}" true
wait_for_tag_state "${movie_id}" "${cascade_tag}" true
wait_for_collection_state "${kids_id}" "${movie_id}" true

persisted="$(api_get "${plugin_configuration_url}")"
if [[ "$(jq --raw-output '.Revision' <<<"${persisted}")" -ne "${active_revision}" ]] \
    || [[ "$(jq --raw-output '(.MappingGroups // []) | length' <<<"${persisted}")" -ne 2 ]] \
    || jq --exit-status --arg animation_id "${animation_id}" \
        '.MappingGroups | any(.Target.CollectionId == $animation_id)' \
        <<<"${persisted}" >/dev/null; then
    printf 'Run-once bootstrap changed persisted configuration or added its run-once edge.\n' >&2
    exit 10
fi

destructive="$(jq --null-input \
    --arg animation_id "${animation_id}" \
    --arg animation_name "${animation_name}" \
    '{
        Target: {Kind: 1, CollectionId: $animation_id, CollectionDisplayName: $animation_name},
        Sources: [{Kind: 0, TagValue: "Absent"}],
        Policy: 1,
        ExcludedItemIds: []
    }')"
request_with_status "${run_once_url}/Preview" "${destructive}"
if [[ "${response_code}" != "200" ]] \
    || ! jq --exit-status \
        --arg item_id "${movie_id}" \
        --arg animation_id "${animation_id}" \
        '(.Authorization.Preview.Items // .authorization.preview.items)[]
            | select((.ItemId // .itemId) == $item_id)
            | (.Mutations // .mutations)
            | any(((.Kind // .kind) == 3 or (.Kind // .kind) == "RemoveCollectionMembership")
                and ((.Target.CollectionId // .target.collectionId) == $animation_id))' \
        <<<"${response_body}" >/dev/null; then
    printf 'Destructive run-once preview did not authorize the expected collection removal.\n' >&2
    exit 11
fi

stale_authorization="$(jq --raw-output \
    '.Authorization.Authorization // .authorization.authorization' <<<"${response_body}")"
curl --fail --silent --request DELETE \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Collections/${animation_id}/Items?ids=${movie_id}"
wait_for_collection_state "${animation_id}" "${movie_id}" false
stale_confirmation="$(jq --null-input \
    --argjson operation "${destructive}" \
    --arg authorization "${stale_authorization}" \
    '{Operation: $operation, Authorization: $authorization}')"
request_with_status "${run_once_url}/Confirm" "${stale_confirmation}"
if [[ "${response_code}" != "409" ]] \
    || ! jq --exit-status \
        '((.Outcome // .outcome) == 2) or ((.Outcome // .outcome) == "RequiresPreview")' \
        <<<"${response_body}" >/dev/null; then
    printf 'Changed run-once removal returned HTTP %s instead of 409.\n' \
        "${response_code}" >&2
    exit 11
fi

curl --fail --silent --request POST \
    --header "X-Emby-Token: ${access_token}" \
    "${server_url}/Collections/${animation_id}/Items?ids=${movie_id}"
wait_for_collection_state "${animation_id}" "${movie_id}" true
excluded="$(jq --arg item_id "${movie_id}" '.ExcludedItemIds = [$item_id]' <<<"${destructive}")"
request_with_status "${run_once_url}/Preview" "${excluded}"
if [[ "${response_code}" != "200" ]] \
    || ! jq --exit-status --arg item_id "${movie_id}" \
        '(.Authorization.Preview.Items // .authorization.preview.items)[]
            | select((.ItemId // .itemId) == $item_id)
            | (.TargetEvaluations // .targetEvaluations)[0]
            | ((.ObservedState // .observedState) == true)
              and ((.EffectiveState // .effectiveState) == true)' \
        <<<"${response_body}" >/dev/null; then
    printf 'Excluded run-once preview did not retain the observed direct target state.\n' >&2
    exit 12
fi

excluded_authorization="$(jq --raw-output \
    '.Authorization.Authorization // .authorization.authorization' <<<"${response_body}")"
excluded_confirmation="$(jq --null-input \
    --argjson operation "${excluded}" \
    --arg authorization "${excluded_authorization}" \
    '{Operation: $operation, Authorization: $authorization}')"
request_with_status "${run_once_url}/Confirm" "${excluded_confirmation}"
if [[ "${response_code}" != "202" ]]; then
    printf 'Excluded run-once confirmation returned HTTP %s: %s\n' \
        "${response_code}" "${response_body}" >&2
    exit 13
fi

wait_for_reconciliation "$(jq --raw-output '.ReconciliationId // .reconciliationId' <<<"${response_body}")"
wait_for_collection_state "${animation_id}" "${movie_id}" true

final_configuration="$(api_get "${plugin_configuration_url}")"
if [[ "$(jq --raw-output '.Revision' <<<"${final_configuration}")" -ne "${active_revision}" ]]; then
    printf 'Run-once exclusion changed the active configuration revision.\n' >&2
    exit 14
fi

printf 'Verified previewed tag-to-collection bootstrap, downstream settling, and exact background execution.\n'
printf 'Verified no persisted edge, changed-removal rejection, and ephemeral direct-target exclusion.\n'
