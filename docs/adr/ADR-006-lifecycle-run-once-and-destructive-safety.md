# ADR-006 — Lifecycle, run-once, and destructive safety

**Status:** Accepted<br>
**Decision owner:** Project maintainer<br>
**Review gate:** Must be Accepted before production coding

**Amended 2026-08-09:** Persist reusable run-once groups while keeping every
preview and execution independent.

## Context

Mappings may be temporary, configuration changes may remove substantial metadata, and uninstall/disable behavior must be predictable.

## Decision

1. Continuous mappings are persisted and active until disabled/deleted.
2. Run-once groups persist the same target, ordered sources, and policy shape as
   mapping groups, but they never become active mappings and never participate
   in automatic reconciliation or the continuous graph. Each preview and
   execution operates on exactly one run-once group independently; groups are
   never combined into a batch and remain saved after successful execution.
   An operation evaluates the active continuous baseline, applies its one-time
   target in the plan, and then settles affected downstream continuous mappings.
3. Disabling or deleting a mapping preserves existing Jellyfin tags and collection memberships.
4. Plugin uninstall preserves existing Jellyfin tags and collection memberships.
   These lifecycle actions remove or deactivate management only; they do not
   infer or perform metadata cleanup.
5. Configuration changes are submitted as complete candidate configurations and
   validated without replacing the active configuration.
6. A removal-free candidate may be persisted without mandatory preview, but the
   server recomputes under the serialized coordinator before persistence. If a
   removal appears, the candidate is not saved and requires preview.
7. A candidate containing removals remains unsaved until its item-level preview
   is explicitly confirmed with a short-lived preview authorization.
8. On confirmation, the serialized coordinator revalidates and recomputes. If
   the removal set changed, it saves nothing and requires a new preview.
9. When the removal set still matches, persist the candidate and enqueue the
   recomputed plan. If a Jellyfin mutation fails, keep the accepted configuration
   active and let Full Reconcile repair partial state.
10. A successful removal-free save or confirmed destructive save persists the
    accepted configuration and enqueues affected reconciliation through the
    serialized background coordinator before the save response returns. The
    response includes the active configuration revision and a reconciliation
    status reference; it does not wait for all Jellyfin mutations to finish.
11. Preview includes item-level additions/removals, cascades, and final settled state.
12. Preview and execution use the same planner.
13. Authoritative changes that would remove state require current preview and explicit confirmation.
14. Run-once target collection selection supports existing collections and an
    explicit `Add new collection…` action in the picker. Existing collections
    cannot be targeted by free-text name entry. The add action opens a distinct
    creation workflow on the same screen and selects the returned Jellyfin GUID
    after successful creation.
    Creation rejects an empty name or a trimmed, case-insensitive match to an
    existing collection name, creates nothing, and shows the existing picker
    choices instead. Successful creation is an immediate, independent Jellyfin
    action: the collection remains if the administrator later cancels the
    mapping/run-once workflow or its save fails. The plugin never rolls that
    collection back automatically.
15. Authoritative run-once governs its target across the entire eligible Movie
    and Series library: supported items gain the target and unsupported items
    lose it. Evaluation may optimize to source matches plus existing target
    holders without changing those semantics.
16. Bulk reconciliation plans before writing and applies nothing when planned
    Authoritative removals exceed a configurable safety limit. The paused plan
    requires item-level preview and explicit confirmation.
17. The circuit breaker defaults to more than 25 unique affected items across a
    run or more than 20 percent of one group's currently observed target
    assignments when that group has at least 10 assignments. Disabling it
    requires an explicit warning and confirmation.
18. A run-once target already managed by an enabled continuous group is rejected.
    A disabled group does not block that target, and a staged reverse/bootstrap
    path is allowed because no run-once edge is persisted.
19. A preview authorization:
    - expires after 10 minutes;
    - is single-use;
    - is bound to the initiating administrator, canonical candidate
      configuration or operation request, run-once exclusion set,
      active-configuration revision, and exact removal tuples of item GUID,
      normalized target node, and removal type;
    - is invalidated by Jellyfin/plugin restart;
    - saves and applies nothing if expired, invalid, or presented after any
      removal tuple changes.
20. Addition-only changes do not invalidate preview authorization. They may be
    recomputed and applied because they introduce no newly authorized removal.
21. A run-once preview may exclude a planned direct target change for an item by
    choosing `Keep current target state`. The planner preserves that item's
    observed run-once target state and recomputes all downstream continuous
    effects before presenting a new preview. Exclusions are bound to the preview
    authorization and are never persisted.

## Consequences

- Temporarily running then disabling a mapping acts like an ad hoc conversion.
- Run-once is the V1 workflow for manual-only synchronization; V1 has no global
  mode for persisting continuous mappings while suppressing all automatic
  triggers.
- No provenance or cleanup database is needed.
- Metadata cleanup is always explicit rather than an accidental lifecycle side effect.
- Destructive configuration is visible before activation.
- Configuration saves remain responsive for large libraries, while status makes
  the distinction between accepted configuration and settled metadata explicit.
- Run-once operations can be tailored per item without introducing durable
  exceptions that continuous reconciliation would later need to honor.
- Reusable run-once groups survive restart without becoming active
  relationships. Independent execution keeps preview authorization, exclusions,
  diagnostics, and background status scoped to one group.
- Collection creation has an explicit lifecycle independent of mapping
  activation, avoiding surprising deletion of a collection that may already be
  in use.

## Required examples

```text
Enable Kid Approved → kid-approved.
Synchronize.
Disable mapping.
Existing kid-approved tags remain.
```

```text
Saved run-once group: kids-safe → Kids Safe collection.
Each execution runs that group independently. The result and reusable group
remain, but no continuous edge is stored.
```

```text
Save an accepted configuration.
Response: active revision plus background reconciliation status reference.
Metadata may still be settling when the response returns.
```

```text
Uninstall after Collection "Kid Approved" added Tag "kid-approved".
Result: the tag remains on every synchronized item.
```
