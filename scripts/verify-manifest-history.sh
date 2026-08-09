#!/usr/bin/env bash

set -euo pipefail

before_manifest="${1:-}"
after_manifest="${2:-}"

if [[ ! -f "${before_manifest}" || ! -f "${after_manifest}" ]]; then
    printf 'Usage: %s BEFORE_MANIFEST AFTER_MANIFEST\n' "$0" >&2
    exit 2
fi

if ! jq --exit-status --slurp '
    def version_entries:
      [.[]
       | .guid as $guid
       | .versions[]
       | {
           guid: ($guid | ascii_downcase),
           version,
           targetAbi,
           sourceUrl,
           checksum
         }];
    (.[0] | version_entries) as $before
    | (.[1] | version_entries) as $after
    | all($before[]; . as $entry | any($after[]; . == $entry))
  ' "${before_manifest}" "${after_manifest}" >/dev/null; then
    printf 'Generated manifest changed or removed a previously published version entry.\n' >&2
    exit 3
fi

printf 'Verified preservation of all historical manifest version entries.\n'
