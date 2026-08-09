# Collection Tag Sync — Design Specification

## 1. Product statement

Collection Tag Sync is a Jellyfin server plugin that reconciles direct media tags and direct collection membership according to explicit administrator-defined mappings.

Typical use cases include:

```text
Collection: Kid Approved
        ↓
Tag: kid-approved
```

```text
Tag: Animation ──┐
Tag: Waltney ─────┼──→ Collection: Animation
Tag: Blooth ──────┘
```

```text
Tag: Waltney ──→ Collection: Animation
            ├──→ Collection: Kids
            └──→ Collection: American
```

The plugin is generic metadata synchronization. It does not directly manage Jellyfin users or access-control policies, though synchronized tags may be used by Jellyfin’s native user restrictions.

---

## 2. Goals

1. Support explicit Tag → Collection and Collection → Tag mappings.
2. Support many sources feeding one target and one source feeding many targets.
3. Provide near-real-time continuous synchronization for ordinary changes.
4. Provide Full Reconcile as the authoritative drift-repair mechanism.
5. Provide run-once operations for migration, bootstrapping, and ad hoc synchronization.
6. Prevent semantic synchronization loops before configuration is activated.
7. Make destructive behavior visible through preview and confirmation.
8. Keep core reconciliation logic pure and testable outside Jellyfin runtime code.
9. Package and distribute the plugin through the author’s GitHub repository.

## 3. Non-goals for v1

- Automatically creating a collection for every Jellyfin tag.
- Continuous bidirectional or cyclic synchronization.
- Boolean expressions such as AND, NOT, or nested rule groups.
- Seasons, episodes, music items, playlists, people, genres, studios, or Live TV items.
- Inherited-tag evaluation.
- Physically propagating Series tags to seasons or episodes.
- Direct NFO/sidecar editing.
- Direct Jellyfin database manipulation.
- Provenance-based “remove only what the plugin added” behavior.
- Transactional rollback of partial Jellyfin mutations.
- A global manual-only mode for persisted mappings; use run-once operations for
  manual-only synchronization.
- Durable per-item mapping exceptions or editable continuous/Full Reconcile
  previews.
- Publication into Jellyfin’s official plugin repository.

---

## 4. Terminology

### Node

A synchronization node is one of:

- `TagNode("Waltney")`
- `CollectionNode(<Jellyfin collection GUID>)`

### Tag identity

Configured tag values are trimmed and must remain non-empty. Tag identity uses
`StringComparer.OrdinalIgnoreCase` for matching, graph identity, target
uniqueness, and add/remove planning, following the
[Jellyfin 10.11.x casing research](research/jellyfin-tag-casing.md).

If an item already contains a case-equivalent tag, the node is present and the
existing spelling is preserved. When the plugin adds an absent tag, it writes
the administrator's trimmed configured spelling. When a plan makes the logical
tag absent, the writer removes every case-equivalent variant. Diacritic folding
is not part of tag identity.

### Mapping group

A mapping group has:

- exactly one target node;
- one or more source nodes;
- one policy;
- an enabled/disabled state.

Sources within one group may mix tag and collection nodes.

Example:

```text
Target: Collection "Animation"
Sources:
  - Tag "Animation"
  - Tag "Waltney"
  - Tag "Blooth"
Policy: Authoritative
```

### Observed state

Whether an eligible item currently has a direct tag or direct collection membership in Jellyfin.

### Effective state

The state produced by evaluating observed state and all upstream mapping groups in topological order.

### Continuous mapping

A persisted, enabled mapping group that remains active and participates in event-driven and Full Reconcile processing.

### Run-once operation

A non-persisted mapping-group-shaped command executed once. Its resulting Jellyfin metadata remains, but no active relationship remains afterward.

---

## 5. Mapping model

### 5.1 Explicit configuration only

Only nodes and edges selected by an administrator participate. Ordinary Jellyfin tags remain inert unless referenced in a mapping.

There is no v1 mode equivalent to:

```text
all tags → same-named collections
```

### 5.2 Many-to-many relationships

The user interface groups rules by target, but the internal graph contains individual directed edges.
One target group may combine tag and collection sources under the same OR
aggregation rule.

```text
Tag:Animation ──→ Collection:Animation
Tag:Waltney ─────→ Collection:Animation
Tag:Blooth ──────→ Collection:Animation

Tag:Waltney ─────→ Collection:Kids
Tag:Waltney ─────→ Collection:American
```

### 5.3 One persisted group per target

A normalized target may appear in at most one persisted mapping group,
regardless of whether that group is enabled or disabled.

Invalid:

```text
Waltney → Animation       [Additive]
Blooth  → Animation       [Authoritative]
```

Valid:

```text
Target: Animation
Sources: Waltney, Blooth
Policy: Authoritative
```

This avoids policy conflicts and makes target behavior easy to inspect.
Disabled groups continue to reserve their targets; alternate configurations are
made by editing the existing group rather than storing duplicate disabled
groups.

### 5.4 OR aggregation

A target is source-supported when any configured source is effectively true.

```text
supported(target) = OR(effective(source_1), ..., effective(source_n))
```

Removing one source does not remove a target while another source still supports it.

---

## 6. Formal state semantics

The graph is evaluated separately for each eligible item.

### 6.1 Unmapped node

If a node is not the target of an enabled mapping group:

```text
effective(node) = observed(node)
```

### 6.2 Additive target

```text
effective(target) = observed(target) OR any(effective(source))
```

Consequences:

- A matching source adds missing target state.
- Existing target state is preserved when no source matches.
- Existing manual target state is effective state and can feed downstream
  mappings.
- Additive planning never emits a removal.

### 6.3 Authoritative target

```text
effective(target) = any(effective(source))
```

Consequences:

- A matching source adds missing target state.
- Unsupported target state is removed.
- Manual or externally added target state is not preserved unless a configured
  source supports it.
- Authoritative changes can be destructive and therefore require preview/confirmation when removals are possible.

### 6.4 Topological evaluation

Because continuous mappings form a DAG, nodes are evaluated in topological order.

Example:

```text
Tag: Waltney
    ↓
Collection: Animation
    ↓
Tag: animated
    ↓
Collection: Kids
```

If `Waltney` is observed true, one planner pass derives:

```text
Animation effective = true
animated effective  = true
Kids effective      = true
```

The planner does not wait for several rounds of Jellyfin-generated events to discover downstream consequences.

### 6.5 Delta generation

Only mapped target nodes generate mutations.

```text
delta = effective(target) compared with observed(target)
```

Possible operations:

- Add tag
- Remove tag
- Add item to collection
- Remove item from collection

If effective and observed state match, no operation is emitted.

---

## 7. Eligible media and metadata scope

### 7.1 Supported item types

V1 supports:

- Movie
- Series

All other Jellyfin item types are ignored.

### 7.2 Direct state only

A tag source is true only when the eligible Movie or Series directly stores that tag.

A collection source is true only when the eligible Movie or Series is directly a member of that collection.

### 7.3 Series descendants

The plugin does not add tags or collection memberships to seasons or episodes. Any behavior Jellyfin applies to descendants based on Series metadata remains Jellyfin’s responsibility.

---

## 8. Continuous mappings

A continuous mapping is persisted in plugin configuration and participates in:

- event-driven incremental reconciliation;
- manual Full Reconcile;
- scheduled Full Reconcile;
- configuration-triggered background reconciliation enqueued before the valid
  save response returns.

Continuous mappings may point in either direction:

```text
Tag → Collection
Collection → Tag
```

They may form multi-hop chains, but the complete enabled graph must remain acyclic.

---

## 9. Run-once operations

A run-once operation uses the same source/target/policy concepts without persisting an active mapping.

Its target must not already be managed by an enabled continuous group. A
disabled persisted group does not block the operation.

Examples:

```text
Tag: kids-safe → Collection: Kids Safe
```

```text
Collection: Kid Approved → Tag: kid-approved
```

Use cases:

- Bootstrap a collection from existing tags.
- Apply an ad hoc metadata conversion.
- Run a continuous mapping temporarily, then disable it while preserving the result.

Run-once is the V1 manual-only synchronization workflow. Automatic continuous
mappings remain enabled while independent run-once operations are previewed and
executed. V1 does not provide a global mode that persists continuous mappings
while suppressing all automatic triggers.

### 9.1 Target collection selection

When the target is a collection, the UI must provide:

1. A picker for existing collections.
2. An `Add new collection…` action within the picker.
3. A distinct collection-creation workflow on the same screen.
4. Automatic selection of the returned Jellyfin GUID after successful creation.

Creation rejects an empty name or a trimmed, case-insensitive match to an
existing collection name. It creates nothing and presents the existing matches
for explicit selection. Pre-existing duplicate-named collections remain separate
GUID identities and the picker shows disambiguating details for each.

Successful creation takes effect immediately as an independent Jellyfin action.
The collection remains if the administrator later cancels the mapping or
run-once workflow, or if that workflow's save fails. The plugin does not roll
the collection back automatically, and the UI must disclose this lifecycle
before creation.

Existing collections cannot be targeted through free-text name entry. The
plugin does not silently match or rebind collections by name; entering a
collection name occurs only inside the explicit creation workflow.

### 9.2 Run-once final state

Preview and execution must include downstream effects of active continuous mappings.

Authoritative run-once governs its target across the entire eligible Movie and
Series library. Supported items gain the target and unsupported items lose it.
An implementation may optimize candidate discovery to source matches plus
existing target holders, but that must produce the same library-wide result.

Planning is staged:

1. Evaluate the active continuous DAG to establish baseline effective state.
2. Evaluate the one-time target.
3. Re-evaluate affected downstream continuous mappings to their final settled
   state.

This permits a reverse/bootstrap run-once path against an active continuous path
without adding the one-time edge to the persisted graph.

For each planned direct run-once target change, the preview may offer
`Keep current target state`. Selecting it creates a run-once exclusion for that
item: the planner preserves the observed target state and recomputes all
downstream continuous effects before presenting the updated preview. Users
cannot independently suppress cascaded operations. Exclusions are bound to one
preview authorization and are never persisted.

Example:

```text
RUN ONCE:
Tag: Waltney → Collection: Animation

CONTINUOUS:
Collection: Animation → Tag: animated
Tag: animated → Collection: Kids
```

Preview should show:

```text
Add to Animation
Add tag animated
Add to Kids
```

not merely the first direct mutation.

---

## 10. Continuous graph validation

### 10.1 Directed acyclic graph

Every source → target relationship is a directed edge.

Enabled continuous mappings must form a DAG.

### 10.2 Rejected cycles

Direct cycle:

```text
Tag: Waltney → Collection: Animation → Tag: Waltney
```

Indirect cycle:

```text
Tag: Waltney
  → Collection: Animation
  → Tag: animated
  → Collection: Kids
  → Tag: Waltney
```

Both are rejected before activation.

### 10.3 Diagnostics

Validation should return a human-readable cycle path, for example:

```text
Cannot enable mapping because it creates a synchronization cycle:

Tag "Waltney"
→ Collection "Animation"
→ Tag "animated"
→ Collection "Kids"
→ Tag "Waltney"
```

### 10.4 Validation points

Validate the complete proposed graph when:

- creating a mapping group;
- editing sources or target;
- enabling a disabled group;
- importing or migrating configuration.

Disabled groups do not participate in the active graph.

Run-once operations are not persisted and therefore do not create a continuous
graph cycle. A staged reverse/bootstrap path is allowed, but a run-once target
already managed by an enabled continuous group is rejected. A disabled group
does not block that target.

---

## 11. Preview and destructive safety

### 11.1 Preview contents

Preview returns:

- additions by operation type;
- removals by operation type;
- affected items;
- cascaded continuous changes;
- final settled state summary;
- unresolved references or validation errors.

Large results may be paginated, but item-level details must remain available.

### 11.2 Shared planner

Preview and execution use the same reconciliation planner. There is no separate approximation algorithm.

### 11.3 Mandatory confirmation

The following require preview and explicit confirmation:

- Authoritative run-once operations that remove state.
- Any candidate configuration whose reconciliation plan would remove state.

Preview operates on a complete candidate configuration without replacing the
active configuration. It returns a preview authorization for the exact
destructive removal set. The UI carries this opaque authorization between the
Preview and Confirm actions; administrators do not handle it manually.

Preview authorization:

- expires after 10 minutes;
- is single-use;
- is bound to the initiating administrator, canonical candidate configuration or
  operation request, run-once exclusion set, active-configuration revision, and
  exact removal tuples of item GUID, normalized target node, and removal type;
- is invalidated by Jellyfin/plugin restart;
- becomes invalid if any removal tuple changes, even when the count is unchanged.

Addition-only changes do not invalidate authorization and may be recomputed and
applied because they are non-destructive.

Preview editing is limited to per-item run-once exclusions on planned direct
target changes. Continuous mapping and Full Reconcile previews are not editable;
durable per-item mapping exceptions are outside V1.

On confirmation, the serialized coordinator revalidates and recomputes. If the
removal set changed, save nothing and require a new preview rather than silently
applying different removals. If it still matches, persist the candidate and
enqueue the recomputed plan through the background coordinator. The accepted
configuration remains active if a later Jellyfin mutation partially fails; Full
Reconcile repairs that state.

---

## 12. Configuration lifecycle

### 12.1 Save

Configuration is validated server-side as a complete candidate configuration.

A save succeeds only when:

- each group has a target and at least one source;
- all persisted target groups are unique by normalized node identity, including
  disabled groups;
- sources are unique after normalization;
- no self-edge exists;
- the enabled graph is acyclic;
- newly selected collection references resolve.

A removal-free candidate may save without mandatory preview, but the serialized
coordinator recomputes immediately before persistence. If a removal appears, it
saves nothing and requires preview and confirmation.

After a successful removal-free save or confirmed destructive save, affected
mappings reconcile in the background. The server persists the accepted
configuration and enqueues reconciliation through the serialized coordinator
before returning the save response. The response includes the active
configuration revision and a reconciliation status reference; it does not wait
for every Jellyfin mutation to finish. The status reports queued, running,
completed, partially failed, failed, or paused-for-approval outcomes. The
accepted configuration remains active if metadata application partially fails.

### 12.2 Disable or delete

Disabling or deleting a mapping:

- stops future management;
- preserves current Jellyfin tags and collection memberships;
- does not perform cleanup.

A later explicit run-once or cleanup feature may alter metadata, but lifecycle operations themselves are non-destructive.

### 12.3 Plugin uninstall

Uninstall leaves all Jellyfin tags and collection memberships untouched.

### 12.4 Missing collection after configuration

If a referenced collection is later deleted:

- keep the mapping configuration and enabled state;
- mark the entire group operationally unresolved;
- skip every source and perform no target mutations from that group;
- pass the target's current observed state through as effective state so valid
  downstream groups can still evaluate;
- do not partially evaluate a mixed-source group using only its remaining
  resolvable sources;
- log and display a persistent warning until repaired or disabled;
- do not auto-rebind by name.

This fail-closed behavior prevents a missing collection from being interpreted
as false and causing unintended Authoritative removals.

This is an invalid/broken configuration edge case and should use the simplest maintainable handling.

---

## 13. Event and reconciliation model

### 13.1 Event sources

Expected Jellyfin inputs include:

- collection item-added events;
- collection item-removed events;
- general item-updated events for possible tag changes;
- configuration changes;
- Jellyfin/plugin startup;
- manual or scheduled Full Reconcile requests.

External metadata providers, NFO imports, library scans, other plugins, and API
clients may produce the same observed source and target changes. Once an
Authoritative mapping is active, ordinary single-item changes from every writer
reconcile without a new confirmation; the activation confirmation grants
ongoing authority. Event storms and other bulk reconciliation paths remain
subject to the destructive circuit breaker before Authoritative removals.

The integration spike must verify exact event and persistence behavior against the selected Jellyfin ABI.

### 13.2 Event handlers enqueue only

Event handlers do not mutate Jellyfin state directly.

```text
Jellyfin event
    ↓
mark eligible item dirty
    ↓
signal worker
    ↓
return
```

### 13.3 Deduplicating dirty set

Use a deduplicating in-memory set keyed by item GUID. Repeated events for one item collapse into one pending reconciliation.

### 13.4 Serialized worker

One reconciliation worker owns mutations. Incremental reconciliation, configuration-triggered reconciliation, run-once execution, and Full Reconcile share the same serialization boundary.

### 13.5 Self-generated events

Plugin writes may produce Jellyfin events. Those events may enqueue the item again.

Correctness depends on idempotence:

```text
first pass: apply required delta
second pass: observed == effective, so emit no operations
```

Self-event suppression may be added as an optimization but is not a correctness requirement.

### 13.6 Event storms and library scans

Normal activity receives near-real-time per-item reconciliation.

During a large scan or excessive dirty-set growth:

- stop expanding fine-grained work indefinitely;
- set a Full Reconcile-required flag;
- coalesce events;
- execute broader reconciliation after the burst/scan.

The resulting bulk reconciliation is subject to the destructive circuit breaker
before it writes any mutations.

Exact thresholds and scan-detection mechanics are implementation details verified during the integration spike.

---

## 14. Full Reconcile

Full Reconcile is the canonical repair mechanism.

It should be available as:

- a manually runnable Jellyfin scheduled task;
- a configurable scheduled task;
- one delayed startup request when at least one continuous mapping is enabled.

The startup request coalesces with another pending bulk request, runs through the
serialized coordinator, and remains subject to the destructive circuit breaker.
Its settling delay is administrator-configurable from 0 through 60 minutes and
defaults to 5 minutes. Zero means as soon as Jellyfin is ready, not during plugin
construction. If a library scan or event storm is still active when the delay
expires, execution waits until that activity becomes quiet.

For v1, Full Reconcile may enumerate all Movies and Series, evaluate the graph, and apply deltas. Optimization to narrower candidate sets can follow later.

Full Reconcile calculates its complete plan before applying mutations. If
planned Authoritative removals exceed the configured destructive safety limit:

- apply none of the plan;
- mark the run as awaiting administrator approval;
- expose the item-level preview;
- recompute and require current confirmation before execution.

The circuit breaker is enabled by default and trips when either condition is
true:

- the bulk plan removes one or more target states from more than 25 unique
  items; or
- one mapping group removes more than 20 percent of its currently observed
  target assignments, when that group currently has at least 10 assignments.

An item with several planned removals counts once toward the absolute limit.
Cascaded removals count toward the group that owns each removed target. Both
limits are administrator-configurable. Disabling the circuit breaker entirely
requires an explicit warning and confirmation.

Full Reconcile repairs:

- missed events;
- partial writes;
- plugin downtime drift;
- external metadata changes;
- event-storm fallback;
- configuration changes that were not completely applied.

The default schedule is an implementation/configuration choice, not an ADR.

---

## 15. Failure and consistency model

### 15.1 Eventual consistency

The plugin is eventually consistent. A multi-operation item plan is not transactional.

### 15.2 Partial failure

If a mutation fails:

1. Keep already-applied mutations.
2. Stop the remaining operations for that item.
3. Log the failed operation and item.
4. Do not attempt rollback.
5. Defer that item until the next Full Reconcile.

### 15.3 No hot retry loop

Incremental processing should not immediately retry a failed item indefinitely. An in-memory quarantine/failed-item set may be used so that the next Full Reconcile is the next retry point.

A server restart may clear in-memory failure state; the next event or Full Reconcile may then retry.

### 15.4 Full Reconcile failures

A failure for one item does not abort the entire Full Reconcile. Continue with other items and report a summary of successes and failures.

---

## 16. User interface outline

### 16.1 Continuous mappings page

Display mapping groups by target:

```text
Target: Collection "Animation"
Policy: Authoritative
Sources:
  Tag "Animation"
  Tag "Waltney"
  Tag "Blooth"

[Edit] [Disable] [Preview Reconcile]
```

```text
Target: Tag "kid-approved"
Policy: Authoritative
Sources:
  Collection "Kid Approved"

[Edit] [Disable] [Preview Reconcile]
```

Controls:

- Add mapping group
- Validate graph
- Preview pending configuration
- Run Full Reconcile

### 16.2 Node selectors

For tags:

- autocomplete/discover existing tags where feasible;
- permit explicit free-text entry;
- trim and validate only after submission;
- show configured display spelling while matching tag identity with
  `OrdinalIgnoreCase`.

For collections:

- picker-only selection of existing collections;
- no free-text binding to an existing collection name;
- target picker includes `Add new collection…`;
- add-new opens a distinct creation workflow on the same screen and selects the
  returned GUID after success;
- add-new rejects empty or trimmed, case-insensitive duplicate names and shows
  existing matches instead;
- a successfully created collection remains if the surrounding workflow is
  canceled or its save fails, with no automatic rollback;
- pre-existing duplicate names appear as separate, disambiguated picker entries;
- store GUID, not name, after selection.

### 16.3 Run-once page

```text
Sources:
  [Tag: kids-safe]
  [Tag: toddler-safe]

Target:
  [Collection picker: Kids Safe]

Policy:
  ( ) Additive
  ( ) Authoritative

[Preview]
[Run Once]
```

### 16.4 Status and diagnostics

Show:

- graph validation errors;
- unresolved collection references;
- last Full Reconcile summary;
- latest failed items/operations;
- whether a broader reconciliation is pending after an event storm;
- whether a bulk reconciliation is paused by the destructive circuit breaker.

Provide administrator controls for the absolute removal limit, per-group
percentage limit, percentage population floor, and circuit-breaker enabled
state. Disabling the circuit breaker requires an explicit warning and
confirmation.

Provide an administrator control for the startup Full Reconcile delay from 0
through 60 minutes, defaulting to 5 minutes.

---

## 17. Architecture

### 17.1 Layers

```text
Configuration / API / Jellyfin Events
                 ↓
      Reconciliation Coordinator
                 ↓
      Pure Graph + State Planner
                 ↓
       Jellyfin State Adapters
```

### 17.2 Pure domain layer

Responsibilities:

- normalize node identity;
- build graph from target groups;
- validate uniqueness and acyclicity;
- produce cycle path diagnostics;
- calculate topological order;
- evaluate effective state;
- generate deterministic reconciliation plans.

No Jellyfin API calls occur in this layer.

### 17.3 Application layer

Responsibilities:

- dirty-item set;
- serialized worker;
- configuration-triggered background reconciliation and status;
- Full Reconcile orchestration;
- run-once orchestration;
- preview orchestration;
- failed-item quarantine;
- summary reporting.

### 17.4 Jellyfin adapter layer

Responsibilities:

- read direct tags for Movie/Series;
- persist direct tag changes through supported Jellyfin APIs;
- read direct collection membership;
- add/remove collection membership through `ICollectionManager`;
- list/create collections;
- subscribe to relevant events;
- expose admin-only controller endpoints and configuration page.

### 17.5 Suggested source layout

```text
Jellyfin.Plugin.CollectionTagSync/
├── Plugin.cs
├── ServiceRegistrator.cs
├── Configuration/
│   ├── PluginConfiguration.cs
│   ├── MappingGroupConfiguration.cs
│   ├── NodeConfiguration.cs
│   └── configPage.html
├── Domain/
│   ├── SyncNode.cs
│   ├── MappingGroup.cs
│   ├── SyncGraph.cs
│   ├── CycleDetector.cs
│   ├── EffectiveStateEvaluator.cs
│   ├── ReconciliationPlan.cs
│   └── ReconciliationPlanner.cs
├── Application/
│   ├── ReconciliationCoordinator.cs
│   ├── DirtyItemSet.cs
│   ├── RunOnceService.cs
│   └── PreviewService.cs
├── Jellyfin/
│   ├── JellyfinStateReader.cs
│   ├── JellyfinStateWriter.cs
│   ├── EventSubscriber.cs
│   └── CollectionCatalog.cs
├── Tasks/
│   └── FullReconcileTask.cs
└── Api/
    └── CollectionTagSyncController.cs
```

This tree is guidance, not an ADR. Refactoring is allowed while behavior and boundaries remain intact.

---

## 18. Configuration representation

Persist serializer-friendly mutable DTOs rather than abstract domain records.

Illustrative shape:

```csharp
public sealed class MappingGroupConfiguration
{
    public Guid Id { get; set; }

    public NodeConfiguration Target { get; set; } = new();

    public List<NodeConfiguration> Sources { get; set; } = [];

    public ReconcilePolicy Policy { get; set; }

    public bool Enabled { get; set; }
}

public sealed class NodeConfiguration
{
    public SyncNodeType Type { get; set; }

    public string Tag { get; set; } = string.Empty;

    public Guid CollectionId { get; set; }

    public string CollectionDisplayName { get; set; } = string.Empty;
}
```

Load path:

```text
persisted DTO
    ↓ normalize + validate
immutable domain model
    ↓
sync graph
```

Include a configuration schema version from the first release.
Include a monotonically increasing active-configuration revision for preview
authorization binding.

---

## 19. Security and operational boundaries

- All custom API endpoints require Jellyfin administrator authorization.
- The plugin makes no external network calls during normal synchronization.
- The plugin stores no credentials or secrets.
- The plugin does not directly manipulate Jellyfin’s database.
- The plugin does not directly edit NFO files.
- Normal Jellyfin persistence behavior and configured metadata savers remain authoritative for storage side effects.

---

## 20. Architectural invariants

1. **Explicit only:** Unreferenced tags and collections have no synchronization effect.
2. **One target group:** A normalized target appears in at most one persisted
   mapping group, including disabled groups.
3. **OR sources:** Any effective source supports the target.
4. **DAG:** The enabled continuous graph contains no directed cycle.
5. **Direct scope:** Only direct Movie/Series state is evaluated or mutated.
6. **Desired-state planning:** Events identify candidates; they do not dictate blind add/remove actions.
7. **Serialized mutation:** Only one coordinator execution mutates synchronization state at a time.
8. **Idempotence:** Replanning after successful application yields no further delta.
9. **Shared planner:** Preview, run-once, incremental, and Full Reconcile use the same planning semantics.
10. **No rollback:** Partial application is repaired later.
11. **Metadata preservation on lifecycle changes:** Disable, delete, and uninstall do not clean up Jellyfin metadata.
12. **No silent collection rebinding:** Collection GUIDs define identity.
13. **Bulk destructive safety:** A bulk plan that exceeds the configured
    Authoritative-removal limit applies no mutations without current
    administrator confirmation.
14. **Staged run-once:** A run-once operation cannot compete with an enabled
    continuous target; it applies once and then affected downstream continuous
    mappings settle without persisting its edge.

---

## 21. V1 completion criteria

### Mapping behavior

- Tag → Collection
- Collection → Tag
- Many tags/collections → one target
- One source → multiple target groups
- Multi-hop acyclic propagation
- Additive policy
- Authoritative policy

### Safety

- Direct and indirect cycles rejected
- Human-readable cycle path
- Destructive preview and confirmation
- Bulk destructive-change circuit breaker
- Multi-source-safe removals
- Idempotent self-event settling
- Serialized mutation

### Operations

- Near-real-time incremental synchronization
- Manual Full Reconcile
- Scheduled Full Reconcile
- Run-once operation
- Existing collection picker
- Explicit create-collection path
- Event-storm degradation to broader reconciliation
- Partial-failure reporting and deferred retry

### Administration

- Create/edit/enable/disable mapping groups
- Broken collection diagnostics
- Item-level preview
- Reconcile status summary

### Distribution

- CI build and tests
- GitHub Release ZIP
- Third-party `manifest.json`
- Catalog install from custom repository
- Upgrade path with configuration retention
