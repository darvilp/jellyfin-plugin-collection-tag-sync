#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
artifact_path="${1:-}"
export DOCKER_CONFIG="${project_root}/.testenv/docker"
compose_arguments=(
    --project-directory "${project_root}"
    -f "${project_root}/compose.yaml"
    -f "${project_root}/compose.e2e.yaml"
)

cleanup() {
    if [[ "${JFTS_E2E_KEEP_SERVER:-0}" != "1" ]]; then
        docker compose "${compose_arguments[@]}" down
    fi
}
trap cleanup EXIT

mkdir -p "${DOCKER_CONFIG}"
bash "${script_dir}/test-env.sh" prepare
bash "${script_dir}/test-env.sh" up
bash "${script_dir}/configure-test-server.sh"

if [[ -z "${artifact_path}" ]]; then
    artifact_path="$(bash "${script_dir}/package.sh" | tail -n 1)"
fi

bash "${script_dir}/install-local-plugin.sh" "${artifact_path}"
mkdir -p "${project_root}/artifacts/playwright"

docker compose "${compose_arguments[@]}" run --rm --build browser-e2e
