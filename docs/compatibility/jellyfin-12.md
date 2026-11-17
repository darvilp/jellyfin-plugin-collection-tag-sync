# Jellyfin 12 migration

## Build target and evidence

This is an unreleased migration for plugin version `0.3.0.0`. It targets
Jellyfin `12.0.0`, ABI `12.0.0.0`, and `net10.0`. The SDK baseline is
`10.0.100` with `latestMinor` roll-forward. Cloud validation used SDK `10.0.404`.
The minimum 12.0.0 package line is intentional; later 12.x runtime compatibility
still needs server testing.

The official NuGet packages [Jellyfin.Controller](https://www.nuget.org/packages/Jellyfin.Controller/12.0.0),
[Jellyfin.Model](https://www.nuget.org/packages/Jellyfin.Model/12.0.0) and
[Jellyfin.Common](https://www.nuget.org/packages/Jellyfin.Common/12.0.0) target .NET 10.
The downloaded Controller package identifies upstream commit
`6c073e19ddf604b2369c638716164fdab4c952dc` in its repository metadata.

The upstream contracts inspected at that commit retain the APIs used here:

- [ILibraryManager](https://github.com/jellyfin/jellyfin/blob/6c073e19ddf604b2369c638716164fdab4c952dc/MediaBrowser.Controller/Library/ILibraryManager.cs):
  `GetItemById`, `GetItemList`, `UpdateItemAsync` and `ItemUpdated`.
- [ICollectionManager](https://github.com/jellyfin/jellyfin/blob/6c073e19ddf604b2369c638716164fdab4c952dc/MediaBrowser.Controller/Collections/ICollectionManager.cs):
  collection creation, membership writes and collection events.
- [Folder](https://github.com/jellyfin/jellyfin/blob/6c073e19ddf604b2369c638716164fdab4c952dc/MediaBrowser.Controller/Entities/Folder.cs):
  `GetLinkedChildren` for direct collection membership.
- [IPluginServiceRegistrator](https://github.com/jellyfin/jellyfin/blob/6c073e19ddf604b2369c638716164fdab4c952dc/MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs):
  the existing service registration signature.

The existing adapters compile against the released assemblies without source
changes. Compilation and mock-based tests do not prove database persistence,
event ordering, catalog selection or runtime service registration.

## Legacy support and release boundary

The 10.11/.NET 9 source is preserved at commit
`9c1f55079c43634ceadc73a43ca8387d54a34136` on
[legacy/jellyfin-10.11](https://github.com/darvilp/jellyfin-plugin-collection-tag-sync/tree/legacy/jellyfin-10.11).
Its [compatibility report](jellyfin-10.11.11.md) and historical release notes
remain historical evidence, not validation of this migration.

The plugin GUID remains `04920eee-c499-4b13-890f-7af0175f28f0` and the
configuration schema remains unchanged. Existing release ZIPs and historical
manifest entries must remain unchanged. A distinct candidate version avoids
reusing the published 0.2.0.0 identity for a different ABI.

`upgradeFrom` is absent because there is no public same-ABI predecessor.
`scripts/test-manifest-upgrade.sh` assumes both packages run on one server and
checks both against the candidate ABI. Do not point it at a 10.11 package on
Jellyfin 12. Add `upgradeFrom` when a public Jellyfin 12 predecessor exists.
Cross-ABI configuration retention requires the separate server-upgrade test below.

## Cloud validation

- Restore and Release build passed, zero warnings and errors.
- All 184 .NET unit tests passed on net10.0, including the existing Jellyfin
  adapter, event, configuration and reconciliation tests.
- All 45 JavaScript UI tests and JavaScript syntax checks passed on Node 24.
  CI uses the existing package.json module declaration without the removed
  experimental default-type flag.
- Bash syntax, ShellCheck 0.11.0, actionlint 1.7.12 and release tooling tests passed.
- Release tests reject an old-ABI candidate and preserve historical 10.11
  manifest fixtures beside the new 12.0.0.0 entry.
- Pinned JPRM built a local ZIP; package contents and metadata verification passed.

The initial migration did not run a server. Follow-up CI debugging used only
the isolated Compose fixture. Catalog installation and cross-ABI upgrade checks
remain pending; the owner's live server has not been accessed.
The .NET test runner emitted an informational VSTEST_ARTIFACTS_DIRECTORY fallback
message after all tests passed. JPRM warned that no plugin image is configured.

## Owner handoff before merge or release

- [ ] Run the PR's GitHub build/package checks and review their results.
- [ ] Use an isolated Jellyfin 12 test server, with no production data mounts.
      Build with `scripts/package.sh`, then run `scripts/test-browser-e2e.sh`
      with the candidate ZIP. The Compose fixture pins the official 12.0 image by digest.
- [ ] Run the server scripts listed in [local development](../DEVELOPMENT.md),
      including clean temporary-catalog installation. Confirm the plugin loads,
      its administrator page works and non-administrators cannot mutate state.
- [ ] Check Movie and Series tag writes, case-equivalent tags, direct collection
      membership, event-driven reconciliation, Full Reconcile, run-once preview
      and execution, and destructive confirmation/circuit-breaker behavior.
- [ ] On a disposable copy of a backed-up 10.11 server, record mappings, run-once
      groups, revision and safety settings. Upgrade the server to 12, replace
      the old plugin with the candidate, and verify those settings and collection
      GUIDs survive restart. Do not install the net10.0 build on a 10.11 server.
- [ ] Test the owner's intended 12.x server version as well as the minimum
      version, recording exact versions and observed results here.
- [ ] Review the candidate version and release notes, retain all old assets and
      manifest entries, then obtain separate release authorization. Pushing a
      `v*.*.*.*` tag publishes assets and updates the live manifest automatically.

Rollback of a server migration uses the complete pre-upgrade data/config backup
and the matching old server/plugin versions. Changing only the plugin DLL does
not reverse Jellyfin database migrations.

## Docker image correction

The initial CI package job built and verified the ZIP but could not start its
browser fixture because Docker Hub has no `jellyfin/jellyfin:12.0.0` tag.
The official registry's `12.0` tag was independently resolved to manifest-list
digest `sha256:baba630419915985442f315f08b0cf46d9f4c8a0cc4bd38e94a6d35751dd5ef5`.
Its amd64 image labels identify Jellyfin version `12.0` and the official
`jellyfin/jellyfin-packaging` source. Both fixture references pin that digest.
Release verification removes the digest and normalizes `12.0` to `12.0.0`
before comparing it with the package ABI. A regression assertion checks that
the Compose server and media generator use the same pinned image.

After fixing the image, the isolated server reported version `12.0.0` but
rejected the fixtures' legacy `X-Emby-Token` header with HTTP 401. The same token
with `Authorization: MediaBrowser Token="..."` returned HTTP 200. Shell and
Playwright fixtures now use that supported authorization format.
