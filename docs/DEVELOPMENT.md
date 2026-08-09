# Local development

Collection Tag Sync is developed and tested in WSL. Its integration environment
is deliberately separate from any Jellyfin installation running on Windows.

## Prerequisites

- WSL 2 with systemd enabled
- Docker Engine 29 or newer with the Compose plugin
- .NET SDK 9
- `curl`, `git`, `jq`, `python3`, and `python3-venv`

The repository pins the .NET SDK feature band in `global.json`, all Jellyfin
NuGet packages in `Directory.Packages.props`, and the test server image in
`compose.yaml`.

If Docker cannot allocate its default bridge because WSL already owns broad
split routes such as `0.0.0.0/1` and `128.0.0.0/1`, select a non-overlapping
bridge in `/etc/docker/daemon.json`. The validated local setup used:

```json
{
  "bip": "172.30.0.1/24"
}
```

Restart Docker after changing daemon configuration. This is a host-specific
workaround, not a repository requirement. The Compose test network itself is
pinned to `172.31.252.0/24`.

## Build and unit tests

The wrapper keeps NuGet packages and the .NET CLI home under ignored
`.testenv/` state:

```bash
bash scripts/dotnet.sh restore Jellyfin.Plugin.CollectionTagSync.sln
bash scripts/dotnet.sh build Jellyfin.Plugin.CollectionTagSync.sln \
  --configuration Release \
  --no-restore
bash scripts/dotnet.sh test Jellyfin.Plugin.CollectionTagSync.sln \
  --configuration Release \
  --no-build \
  --no-restore
```

## Isolated Jellyfin server

Prepare and start the test server:

```bash
bash scripts/test-env.sh prepare
bash scripts/test-env.sh up
bash scripts/configure-test-server.sh
```

The server is reachable only at `http://127.0.0.1:18096`. Its config, cache,
plugins, access token, generated media, and logs stay under ignored
`.testenv/jellyfin/`. No Windows directories are mounted into the container.

Useful lifecycle commands:

```bash
bash scripts/test-env.sh status
bash scripts/test-env.sh logs
bash scripts/test-env.sh down
bash scripts/test-env.sh reset --confirm
```

`reset --confirm` removes only the repository's generated Jellyfin config and
cache. It preserves the deterministic synthetic media fixtures.

If Docker group membership has not reached the current shell yet, prefix a
Docker-dependent command with `sg docker -c`, for example:

```bash
sg docker -c 'bash scripts/test-env.sh up'
```

## Package and integration smoke tests

Build and inspect the JPRM package, install it manually, and run the live
contracts:

```bash
bash scripts/package.sh
bash scripts/install-local-plugin.sh
bash scripts/test-event-observation.sh
bash scripts/test-jellyfin-contracts.sh
bash scripts/test-walking-slice.sh
bash scripts/test-continuous-adapters.sh
bash scripts/test-manifest-install.sh
```

The last script creates an ignored temporary JPRM catalog, serves it only for
the duration of the test, asks Jellyfin to install from that catalog, restores
the server's original repository list, and stops the temporary HTTP server.

See [Jellyfin 10.11.11 compatibility](compatibility/jellyfin-10.11.11.md) for
the exact validated contract and known boundaries.
