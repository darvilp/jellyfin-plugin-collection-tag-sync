# Collection Tag Sync — GitHub Packaging and Release Plan

## 1. Distribution strategy

Distribute through the author’s GitHub repository:

```text
main branch
  → source, tests, docs, build metadata

GitHub Releases
  → immutable plugin ZIP and SHA-256 asset

manifest branch
  → generated Jellyfin manifest.json
```

Users add the raw `manifest.json` URL as a third-party Jellyfin plugin repository, then install from the Jellyfin catalog.

Do not reuse Jellyfin’s official deployment workflow or its deployment-host secrets. Build a third-party release workflow owned entirely by this repository.

---

## 2. Compatibility strategy

V1 supports one explicitly tested Jellyfin ABI line.

At implementation kickoff, pin together:

- `targetAbi` in `build.yaml`;
- `Jellyfin.Controller` package version;
- `Jellyfin.Model` package version;
- .NET target framework;
- JPRM version;
- tested Jellyfin server version range.

Do not copy template versions mechanically. Confirm them in the integration spike.

If later supporting incompatible Jellyfin ABI lines, publish separate compatible plugin releases rather than assuming one binary works everywhere.

---

## 3. Repository layout

```text
/
├── .github/workflows/
│   ├── ci.yml
│   └── release.yml
├── Jellyfin.Plugin.CollectionTagSync/
├── tests/
│   ├── Jellyfin.Plugin.CollectionTagSync.Tests/
│   └── fixtures/
├── docs/
│   ├── DESIGN.md
│   ├── PLAN.md
│   ├── TESTING.md
│   ├── RELEASE.md
│   ├── compatibility/
│   └── adr/
├── build.yaml
├── Jellyfin.Plugin.CollectionTagSync.sln
├── LICENSE
└── README.md
```

The generated `manifest.json` lives on the dedicated `manifest` branch rather than being manually edited on `main`.

The implemented workflow is `.github/workflows/release.yml`. A four-component
version-tag push runs the build with read-only repository permissions. Only
after that job passes does a separate `contents: write` job create a draft,
upload and verify assets, prepare the manifest commit, publish the prerelease,
and expose the generated manifest. A manual dispatch revalidates an existing
tag without running the publishing job.

---

## 4. Plugin metadata

Illustrative `build.yaml`:

```yaml
name: "Collection Tag Sync"
guid: "<GENERATE-ONCE-AND-NEVER-CHANGE>"
version: "0.1.0.0"
targetAbi: "<CONFIRMED-JELLYFIN-ABI>"
framework: "<CONFIRMED-TARGET-FRAMEWORK>"
overview: "Synchronize explicit Jellyfin tag and collection mappings."
description: >
  Provides explicit many-to-many synchronization between direct
  Movie/Series tags and collection membership, including continuous
  reconciliation and run-once operations.
category: "General"
owner: "<GITHUB-OWNER>"
artifacts:
  - "Jellyfin.Plugin.CollectionTagSync.dll"
changelog: >
  Initial release.
```

Rules:

- Generate the GUID once.
- Never change the GUID after publication.
- Keep tag, package, `build.yaml`, and manifest versions identical.
- Do not package Jellyfin server assemblies as plugin runtime dependencies.

---

## 5. Versioning

Use four-component plugin versions:

```text
0.1.0.0
0.1.1.0
1.0.0.0
```

Git tags:

```text
v0.1.0.0
v0.1.1.0
v1.0.0.0
```

The release workflow must fail if:

```text
Git tag version != build.yaml version
```

---

## 6. Pull-request and main CI

Required jobs:

### Test

```text
checkout
setup pinned .NET SDK
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

### Package validation

```text
install/use pinned JPRM
jprm plugin build
inspect generated ZIP
upload ZIP as CI artifact
```

CI should verify:

- tests pass;
- package is generated;
- intended DLLs/resources are present;
- Jellyfin server assemblies are not bundled unexpectedly;
- `build.yaml` parses correctly.

Pin GitHub Actions and reusable workflow revisions rather than tracking moving branches when practical.

---

## 7. Release trigger and permissions

Trigger on a version tag or a published GitHub Release.

Recommended workflow permissions:

```yaml
permissions:
  contents: write
```

Use a release concurrency group so two releases cannot update the manifest branch simultaneously.

Example intent:

```yaml
concurrency:
  group: collection-tag-sync-release
  cancel-in-progress: false
```

---

## 8. Release workflow

### Step 1 — Validate source and version

- Checkout the exact tag.
- Read `build.yaml` version.
- Verify it equals the tag without the `v` prefix.
- Verify working tree is clean and tag points to the intended commit.

### Step 2 — Build and test

```text
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
```

### Step 3 — Build plugin package

Use a pinned JPRM version.

Illustrative command:

```bash
jprm plugin build . \
  --output artifacts \
  --version "$VERSION"
```

Capture the generated ZIP path.

### Step 4 — Inspect artifact

Verify:

- expected plugin DLL exists;
- embedded configuration page/resources exist;
- dependency DLLs are intentional;
- Jellyfin server runtime assemblies are not bundled;
- no test binaries, secrets, or temporary files are present.

### Step 5 — Generate human-verification checksum

```bash
sha256sum "$ZIP" > "$ZIP.sha256"
```

JPRM should manage the checksum required by the Jellyfin manifest format. The SHA-256 asset is an additional release-verification aid.

### Step 6 — Publish GitHub Release assets

Create or update:

```text
Jellyfin.Plugin.CollectionTagSync_<VERSION>.zip
Jellyfin.Plugin.CollectionTagSync_<VERSION>.zip.sha256
```

Release notes include:

- supported Jellyfin ABI/version range;
- behavior changes;
- configuration migration notes;
- upgrade notes;
- known limitations.

### Step 7 — Update manifest branch

Recommended manifest URL:

```text
https://raw.githubusercontent.com/<owner>/<repo>/manifest/manifest.json
```

For the first release:

```bash
jprm repo init <manifest-worktree>
```

For every release, use the complete immutable GitHub Release asset URL:

```text
https://github.com/<owner>/<repo>/releases/download/v<VERSION>/Jellyfin.Plugin.CollectionTagSync_<VERSION>.zip
```

Then add the package:

```bash
jprm repo add \
  --plugin-url="$RELEASE_ASSET_URL" \
  <manifest-worktree> \
  "$ZIP"
```

`--plugin-url` is required for GitHub Release assets. JPRM's repository-base
`--url` mode appends its normal plugin subdirectory, but GitHub Release assets
live directly below the tag URL. The release contract rejects any generated
`sourceUrl` other than the exact immutable asset URL.

Commit and push the updated generated manifest to the `manifest` branch.

Do not hand-edit release entries unless repairing a known generator problem.

### Step 8 — Smoke test

On a clean/reproducible Jellyfin instance:

1. Add the raw manifest URL.
2. Confirm Collection Tag Sync appears in the catalog.
3. Install the release.
4. Restart Jellyfin if required.
5. Open the configuration page.
6. Run a small Movie/Series mapping test.
7. Upgrade from the prior release when one exists.
8. Confirm configuration is retained.

The temporary-catalog installation is automated. A true cross-version
upgrade/configuration-retention smoke test becomes possible when a second
package version exists; the first public release records that boundary instead
of claiming an upgrade that cannot yet occur.

---

## 9. Manual developer installation

Document a local development path:

1. Build the plugin in Release mode.
2. Copy/install the generated plugin package or DLLs into a test Jellyfin plugin directory according to the target server’s plugin layout.
3. Restart the test server.
4. Inspect Jellyfin logs for load/ABI errors.

Prefer testing the JPRM-generated ZIP before release because it matches the distributed artifact.

---

## 10. User installation documentation

README instructions:

```text
Dashboard
→ Plugins
→ Repositories / Manage Repositories
→ Add repository
```

Repository URL:

```text
https://raw.githubusercontent.com/<owner>/<repo>/manifest/manifest.json
```

Then:

```text
Catalog
→ Collection Tag Sync
→ Install
→ Restart if prompted
```

Also document:

- supported Jellyfin versions;
- configuration backup/upgrade notes;
- uninstall preserves synchronized metadata;
- Full Reconcile purpose.

---

## 11. Licensing

Use a license compatible with Jellyfin plugin distribution and linked Jellyfin libraries. The official plugin template uses GPLv3 guidance; select and document the project license before the first public binary release.

Include:

- `LICENSE`;
- source corresponding to each distributed release;
- SPDX identifiers where adopted by the project.

---

## 12. Release checklist

```text
[ ] ADR-affecting behavior is documented
[ ] Configuration schema migration is tested
[ ] Full unit/application test suite passes
[ ] Jellyfin contract tests pass for target ABI
[ ] Clean build from tagged checkout succeeds
[ ] Tag equals build.yaml version
[ ] targetAbi/framework/package versions are correct
[ ] JPRM ZIP is generated
[ ] ZIP contents are inspected
[ ] No server assemblies or secrets are bundled unexpectedly
[ ] SHA-256 asset is generated
[ ] GitHub Release notes are complete
[ ] ZIP and SHA-256 are uploaded
[ ] manifest branch is updated by JPRM
[ ] manifest points to immutable release asset
[ ] Clean catalog installation succeeds
[ ] Upgrade from previous release succeeds
[ ] Plugin configuration survives upgrade
[ ] Movie and Series smoke mappings succeed
[ ] Uninstall behavior remains metadata-preserving
```
