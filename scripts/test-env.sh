#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"
state_root="${project_root}/.testenv/jellyfin"
media_root="${state_root}/media"
image="jellyfin/jellyfin:10.11.11"

compose() {
    docker compose --project-directory "${project_root}" -f "${project_root}/compose.yaml" "$@"
}

prepare_directories() {
    mkdir -p "${state_root}/config" "${state_root}/cache" "${media_root}"
}

generate_video() {
    local output_path="$1"
    local color="$2"
    local frequency="$3"

    if [[ -f "${output_path}" ]]; then
        return
    fi

    mkdir -p "$(dirname -- "${output_path}")"
    docker run --rm \
        --network none \
        --user "$(id -u):$(id -g)" \
        --volume "${media_root}:/media" \
        --entrypoint /usr/lib/jellyfin-ffmpeg/ffmpeg \
        "${image}" \
        -hide_banner \
        -loglevel error \
        -f lavfi \
        -i "color=c=${color}:s=320x180:d=1" \
        -f lavfi \
        -i "sine=frequency=${frequency}:duration=1" \
        -shortest \
        -c:v libx264 \
        -preset ultrafast \
        -pix_fmt yuv420p \
        -c:a aac \
        "/media/${output_path#"${media_root}/"}"
}

prepare_fixtures() {
    prepare_directories
    cp -a "${project_root}/tests/fixtures/media/." "${media_root}/"

    generate_video \
        "${media_root}/Movies/Waltney Adventure (2024)/Waltney Adventure (2024).mkv" \
        "0x355c7d" \
        "440"
    generate_video \
        "${media_root}/Series/The Blooth Household (2024)/Season 01/The Blooth Household S01E01.mkv" \
        "0x7d4f35" \
        "554"
}

usage() {
    printf 'Usage: %s {prepare|up|down|status|logs|reset --confirm}\n' "$0"
}

command_name="${1:-}"
case "${command_name}" in
    prepare)
        prepare_fixtures
        ;;
    up)
        prepare_fixtures
        compose up --detach --wait
        printf 'Jellyfin is ready at http://127.0.0.1:18096\n'
        ;;
    down)
        compose down --remove-orphans
        ;;
    status)
        compose ps
        ;;
    logs)
        compose logs --follow jellyfin
        ;;
    reset)
        if [[ "${2:-}" != "--confirm" ]]; then
            printf 'Refusing to reset without --confirm.\n' >&2
            exit 2
        fi

        compose down --remove-orphans
        if [[ "${state_root}" != "${project_root}/.testenv/jellyfin" ]]; then
            printf 'Refusing to reset unexpected path: %s\n' "${state_root}" >&2
            exit 3
        fi

        find "${state_root}/config" "${state_root}/cache" -mindepth 1 -delete
        printf 'Reset Jellyfin config and cache; synthetic media was preserved.\n'
        ;;
    *)
        usage
        exit 2
        ;;
esac
