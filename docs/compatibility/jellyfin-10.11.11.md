# Jellyfin 10.11.11 compatibility

**Validated:** 2026-08-09

**Scope:** Phase 1 integration spike through Phase 6A destructive configuration activation

## Pinned compatibility set

| Component | Validated value |
|---|---|
| Jellyfin server | `jellyfin/jellyfin:10.11.11` |
| Jellyfin source | `1fbd8739292cce610231be93daf43368733edf63` |
| Target framework | `net9.0` |
| .NET SDK | `9.0.119` through the `9.0.1xx` roll-forward policy |
| Jellyfin NuGet packages | `Jellyfin.Common`, `Jellyfin.Controller`, and `Jellyfin.Model` `10.11.11` |
| Plugin target ABI | `10.11.11.0` |
| Plugin GUID | `04920eee-c499-4b13-890f-7af0175f28f0` |
| JPRM | `9497a0a499416cc572ed2e07a391d9f943a37b4d` (`v1.1.1`) |

V1 intentionally targets the current Jellyfin release rather than claiming a
broader untested compatibility range.

## Live contract results

All checks ran against the isolated Compose server bound to
`127.0.0.1:18096`; the Windows Jellyfin installation and its default port were
not used.

- Jellyfin loaded the minimal JPRM package as `Collection Tag Sync 0.1.0.0` and
  reported it Active after restart.
- `ILibraryManager.ItemUpdated`, `ICollectionManager.CollectionCreated`,
  `ItemsAddedToCollection`, and `ItemsRemovedFromCollection` subscriptions
  observed the corresponding live API mutations. Initial members supplied to
  `CreateCollectionAsync` do not produce a separate added-items event, so the
  event contract explicitly adds a later member.
- Direct Movie tags and direct Movie/Series collection membership survived a
  server restart. Their removals also survived a second restart.
- `Kid-Approved` and `kid-approved` can coexist as exact stored tag values and
  both survived restart. A `Tags=KID-APPROVED` item query matched the Movie,
  confirming that tag filters are case-insensitive even though exact casing is
  preserved in storage.
- A second create call with the same collection name reused the same
  path-derived collection identity. The plugin must still reject a normalized
  duplicate before creation and identify existing collections by GUID.
- A collection created as a distinct API action survived restart without any
  active mapping. This supports the accepted rule that creation is not rolled
  back when a later mapping or run-once workflow is canceled or fails.
- `PluginConfiguration.SchemaVersion` and the server-assigned active revision
  serialized through Jellyfin's plugin configuration API and retained their
  values through restart.
- Jellyfin loaded the package through a temporary custom JPRM manifest, then
  restarted healthy with the plugin Active. The test restored the original
  Jellyfin repository list afterward.
- A bounded exact Tag to Collection Additive mapping synchronized both the
  synthetic Movie and Series through `ItemUpdated`, the pure planner, and
  `ICollectionManager`. Duplicate item updates produced exactly one effective
  write per item. Each plugin-generated collection event caused a second pass
  that logged zero mutations.
- Serializer-friendly mapping DTOs round-tripped through Jellyfin's plugin
  configuration API and activated GUID-bound collection references without
  name lookup.
- Continuous Tag to Collection and Collection to Tag mappings applied Additive
  additions and Authoritative removals for the synthetic Movie and Series.
  Direct tag writes used `ILibraryManager.UpdateItemAsync`; direct membership
  writes used `ICollectionManager` add/remove operations.
- A missing collection in a mixed-source Authoritative group skipped the whole
  group without removing its observed tag target. That observed target still
  supported a valid downstream collection mapping. One persistent warning was
  logged across duplicate and self-events, rehydrated from persisted
  configuration after restart, then cleared when mappings were explicitly
  disabled.
- Incremental work is serialized through one deduplicating coordinator. Its
  fine-grained queue accepts at most 1,000 unique pending item identities; the
  next unique identity activates storm fallback and raises one coalesced Full
  Reconcile request instead of growing the queue.
- An item write failure retains earlier successful mutations, stops the item,
  and quarantines it from later incremental events until Full Reconcile resets
  recovery state. Privacy-safe status snapshots expose only queued, running,
  and quarantined counts plus storm state, never item or library identities.
- The custom configuration activation and status endpoints required Jellyfin
  administrator elevation, and the generic plugin-configuration mutation route
  was rejected so it could not bypass server validation or revision assignment.
  A valid addition-only candidate returned HTTP 202 with revision and request
  identities before settlement, reconciled in the background, and remained
  active after restart. A cyclic candidate returned HTTP 400 without changing
  the active revision; an Authoritative removal-bearing candidate returned HTTP
  409 with paused status and was not saved.
- Concurrent accepted requests retain their own immutable revision snapshots,
  re-resolve collection availability fail closed at execution time, and yield
  the shared mutation boundary between items so a later save does not wait for
  the prior request's complete metadata settlement.
- Jellyfin discovered the public `IScheduledTask` implementation as
  `Collection Tag Sync: Full Reconcile`. It is visible, enabled, logged, and
  has no automatic default trigger; administrators can run it manually or add
  their own Jellyfin schedule. This matches Jellyfin's
  [`IScheduledTask` contract](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Tasks/IScheduledTask.cs)
  and runtime task discovery.
- Full Reconcile enumerated both eligible fixtures, calculated every item plan
  before writing, and repaired drift created while the plugin was offline: a
  missing Movie target was added and an unsupported Authoritative Series target
  was removed. The task completed with `Total=2 Succeeded=2 Failed=0`.
- A zero-minute startup delay still waited for Jellyfin core readiness, then
  queued exactly one coalesced startup run. Startup and storm recovery use
  Jellyfin's
  [`ILibraryManager.IsScanRunning`](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/Library/ILibraryManager.cs)
  plus a ten-second eligible-event settling window before claiming the pending
  bulk request.
- With the configured absolute limit lowered to one item, a two-item
  Authoritative-removal plan paused before either write. The elevated Full
  Reconcile API exposed both item-level removal diagnostics, while the target
  tags remained unchanged.
- Jellyfin restart retained the non-executable paused diagnostics and
  rehydrated `AwaitingApproval`, but invalidated the pre-restart authorization.
  A fresh administrator-bound authorization recomputed the equivalent plan,
  applied both removals, and was rejected when reused. Administrator identity
  uses Jellyfin's case-insensitive `Jellyfin-UserId` claim, matching the current
  [`ClaimsPrincipalExtensions` implementation](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Extensions/ClaimsPrincipalExtensions.cs).
- The elevated configuration-preview API exposed complete item-level additions,
  removals, supporting sources, and final settled target state without replacing
  the active revision. Changing one authorized removal tuple returned HTTP 409
  and saved nothing.
- Configuration preview authorization was invalidated by Jellyfin restart and
  rejected after use. A fresh confirmation tolerated addition-only drift,
  recomputed under the shared execution gate, returned HTTP 202 with the next
  active revision and queued status before settlement, then applied that exact
  recomputed removal-plus-addition plan in the background. A focused worker test
  proves accepted plans do not re-read changed state before execution, and a
  partial item-write failure leaves the accepted revision active for later Full
  Reconcile repair.

The package verifier confirmed that the ZIP contains exactly the plugin DLL
and JPRM `meta.json`, with version `0.1.0.0`, the permanent GUID, and target ABI
`10.11.11.0`.

## Reproducible validation

```bash
bash scripts/dotnet.sh restore Jellyfin.Plugin.CollectionTagSync.sln
bash scripts/dotnet.sh build Jellyfin.Plugin.CollectionTagSync.sln \
  --configuration Release --no-restore
bash scripts/dotnet.sh test Jellyfin.Plugin.CollectionTagSync.sln \
  --configuration Release --no-build --no-restore
bash scripts/package.sh
bash scripts/install-local-plugin.sh
bash scripts/test-event-observation.sh
bash scripts/test-jellyfin-contracts.sh
bash scripts/test-walking-slice.sh
bash scripts/test-continuous-adapters.sh
bash scripts/test-configuration-activation.sh
bash scripts/test-full-reconcile.sh
bash scripts/test-destructive-circuit-breaker.sh
bash scripts/test-configuration-preview.sh
bash scripts/test-manifest-install.sh
```

Validated results:

- build: zero warnings and zero errors;
- tests: 138 passed, 0 failed;
- event observation: all four subscribed event types observed;
- persistence/API contract: passed across two restarts;
- walking slice: Movie and Series added once each, then settled at zero delta;
- continuous adapters: both directions, both policies, Movie/Series scope, and
  fail-closed missing-collection behavior passed through the hardened
  coordinator;
- configuration activation: elevation-only HTTP access, server-assigned
  revision, authoritative-route enforcement, asynchronous settlement,
  invalid-candidate preservation, destructive pause, and restart persistence
  passed;
- Full Reconcile: Jellyfin scheduled-task discovery, manual invocation,
  offline addition/removal repair, terminal summary, and one zero-delay startup
  recovery passed;
- destructive circuit breaker: configurable threshold pause, zero pre-approval
  writes, persisted item-level diagnostics, restart invalidation and status
  rehydration, fresh-plan confirmation, and single-use enforcement passed;
- destructive configuration preview: complete settled item plan, candidate and
  removal binding, changed-removal rejection without persistence, restart
  invalidation, addition-only drift, background activation, and single-use
  enforcement passed;
- manual package install: passed;
- temporary-manifest catalog install: passed.

## Boundaries carried forward

- The Phase 3 walking slice proves that mutations made by the plugin's own
  collection writer emit self-events and settle to a zero-delta second pass.
  Phase 4A extends that proof to direct tag writes, collection removals, and
  fail-closed missing references. Phase 4B contains incremental failures and
  produces the bounded event-storm request. Phase 4C activates removal-free
  configurations and exposes background status. Phase 5A consumes coalesced
  manual, startup, and storm requests through Full Reconcile. Phase 5B pauses
  excessive Authoritative-removal plans atomically and executes only a freshly
  recomputed equivalent plan with current administrator confirmation. Phase 6A
  applies the same fresh-plan authorization rules before a destructive complete
  configuration candidate can become active.
- The create endpoint's independent persistence was proven. Cancellation and
  save-failure behavior around the future application workflow remains an
  application-level test for the run-once/configuration phase.
- This environment proves Jellyfin 10.11.11 on Linux containers in WSL. It does
  not claim native Windows-server or older-Jellyfin compatibility.
- Upgrade behavior from a prior released plugin version remains a release smoke
  test once a prior version exists.
