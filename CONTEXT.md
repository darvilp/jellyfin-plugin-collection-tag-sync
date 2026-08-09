# Collection Tag Sync

Collection Tag Sync describes administrator-defined relationships between
Jellyfin media tags and collections.

## Language

**Node**:
A tag or collection that can participate in synchronization.
_Avoid_: Endpoint

**Collection node**:
A Jellyfin collection identified by GUID. Its name is mutable display data and
never its synchronization identity.
_Avoid_: Collection name

**Tag node**:
A non-empty, trimmed tag identity compared with `OrdinalIgnoreCase`. An existing
case-equivalent spelling is preserved; the configured spelling is used only
when adding an absent tag. Making the node absent removes every case-equivalent
variant.
_Avoid_: Case-sensitive tag, lowercased tag

**Collection creation**:
An explicit Jellyfin action completed independently of mapping activation or a
run-once operation. A successfully created collection remains if the surrounding
workflow is later canceled or fails.
_Avoid_: Draft collection, provisional collection

**Mapping group**:
A persisted relationship with one target, one or more sources, one policy, and
an enabled or disabled state. Each normalized target belongs to at most one
mapping group.
_Avoid_: Rule, mapping rule

**Unresolved mapping group**:
An enabled mapping group with at least one collection GUID that no longer
resolves. The whole group is operationally skipped, its target's observed state
passes through unchanged, and its persisted configuration remains available for
explicit repair.
_Avoid_: Broken rule, partially active group

**Source**:
A node whose effective state can support a mapping group's target. One mapping
group may contain both tag and collection sources.
_Avoid_: Input

**Target**:
The single node whose state a mapping group manages.
_Avoid_: Output, destination

**Observed state**:
The direct tag or collection-membership state an eligible item currently has
before reconciliation planning. Its meaning does not depend on whether the
change came from an administrator, metadata provider, import, plugin, or API
client.
_Avoid_: Actual state

**Effective state**:
The settled state of a node after applying its mapping policy and all upstream
mapping groups. Preserved manual state on an Additive target is effective state.
_Avoid_: Calculated state, derived state

**Additive policy**:
A target policy that preserves observed target state and adds state supported by
configured sources. It never removes target state.
_Avoid_: Merge policy

**Authoritative policy**:
A target policy under which configured sources completely determine target
state, including removal of unsupported manual or externally added state.
_Avoid_: Replace policy, mirror policy

**Bulk reconciliation**:
A reconciliation that evaluates a set of eligible items as one operational run,
including Full Reconcile and event-storm recovery.
_Avoid_: Batch sync

**Dirty item**:
An eligible item marked for desired-state reevaluation after a potentially
relevant event. It is a candidate, not an instruction to perform a specific
mutation.
_Avoid_: Queued event, pending change

**Full Reconcile**:
The canonical bulk reconciliation that evaluates every eligible item to repair
missed events, partial writes, downtime drift, and external changes.
_Avoid_: Full sync, rescan

**Background reconciliation**:
A reconciliation request accepted by the serialized coordinator and processed
outside the initiating HTTP request. Its status, rather than the save response,
reports when metadata has settled.
_Avoid_: Synchronous save, fire-and-forget sync

**Destructive circuit breaker**:
A safety boundary that pauses a bulk reconciliation before any mutation when its
planned Authoritative removals exceed the configured limit.
_Avoid_: Rate limit

**Continuous mapping**:
An enabled persisted mapping group that remains active for incremental and bulk
reconciliation.
_Avoid_: Permanent mapping, live rule

**Continuous graph**:
The directed acyclic graph formed by all enabled continuous mappings. Disabled
groups and run-once operations do not contribute edges to this graph.
_Avoid_: Mapping graph, rule graph

**Run-once operation**:
A non-persisted mapping-shaped operation evaluated once before affected
downstream continuous mappings settle. It leaves resulting metadata but no
active relationship.
_Avoid_: Temporary mapping

**Candidate configuration**:
A complete proposed configuration that is validated and previewed without
replacing the active configuration.
_Avoid_: Draft configuration, pending configuration

**Preview authorization**:
A short-lived, single-use authorization to execute one destructive candidate
configuration, run-once operation, or paused bulk plan when its recomputed
removal set still matches what an administrator previewed.
_Avoid_: Confirmation token, approval flag

**Run-once exclusion**:
An ephemeral per-item choice to keep the run-once target's observed state instead
of applying its planned direct change. It is part of one preview and is never a
persisted mapping exception.
_Avoid_: Exception, override, ban

**Eligible item**:
A Movie or Series that participates in V1 synchronization. Seasons, episodes,
music, playlists, and other item types are outside the V1 domain.
_Avoid_: Media item, library item

**Direct state**:
A tag or collection membership stored on the eligible item itself, excluding
inherited metadata and state derived for descendants.
_Avoid_: Local state, own state
