# Collection Tag Sync — Testing Strategy

## 1. TDD standard

Behavioral work follows:

```text
red → green → refactor → full suite
```

Tests should describe observable semantics rather than mirror internal class structure.

The pure graph/planner layer should carry most of the behavioral test weight. Jellyfin runtime tests should focus on API contracts, persistence, event behavior, and packaging.

---

## 2. Test layers

### 2.1 Pure unit/property tests

No Jellyfin runtime required.

Covers:

- node identity and normalization;
- group validation;
- graph construction;
- cycle detection/path;
- topological evaluation;
- Additive and Authoritative semantics;
- many-to-many relationships;
- multi-hop cascades;
- plan generation;
- idempotence.

### 2.2 Application-service tests

Use fakes for readers/writers/events.

Covers:

- dirty-item deduplication;
- serialization;
- event coalescing;
- self-event settling;
- failed-item quarantine;
- Full Reconcile continuation;
- preview/run-once orchestration.

### 2.3 Jellyfin contract/integration tests

Run against the selected Jellyfin target where practical.

Covers:

- direct tag read/write persistence;
- direct Movie/Series collection membership;
- actual event emission;
- collection creation;
- missing/deleted collection behavior;
- plugin configuration serialization;
- admin-only controllers;
- package loading.

### 2.4 Release smoke tests

Covers:

- manual ZIP install;
- custom manifest catalog install;
- plugin restart/load;
- upgrade from prior version;
- configuration retention.

---

## 3. High-value properties

### Idempotence

```text
plan(apply(plan(state))) = empty
```

### Additive monotonicity

Additive never emits a true → false target transition.

### Authoritative equivalence

```text
effective(target) = OR(effective(inbound sources))
```

### Source independence

Removing one source does not remove a target supported by another source.

### DAG convergence

One topological evaluation reaches the final desired state for a multi-hop acyclic graph.

### Topological-order independence

Any valid topological order produces the same final effective state and mutation set.

### Duplicate-event tolerance

Any number of duplicate notifications for the same unchanged item produce at most one effective mutation set.

### Recovery

Any supported partially applied state is repaired by Full Reconcile.

### Preview equivalence

For the same observed state and candidate configuration, preview and execution planning produce the same plan.

---

## 4. Core behavior matrix

| Area | Required case |
|---|---|
| Identity | Tag comparison is trimmed and case-insensitive |
| Identity | Collection identity is GUID-based |
| Mapping | One source → one target |
| Mapping | Many sources → one target |
| Mapping | One source → many targets |
| Mapping | One target cannot have two active groups |
| Aggregation | OR semantics |
| Graph | Direct self-cycle rejected |
| Graph | Multi-hop cycle rejected |
| Graph | Disabled group excluded from active DAG |
| Graph | Useful cycle path returned |
| Scope | Movie participates |
| Scope | Series participates |
| Scope | Episode ignored |
| Scope | Season ignored |
| Scope | Music ignored |
| Tags | Only direct tags count |
| Additive | Missing target added |
| Additive | Existing unsupported target preserved |
| Additive | No removal emitted |
| Authoritative | Missing supported target added |
| Authoritative | Unsupported target removed |
| Many-source | First source removed; target remains |
| Many-source | Last source removed; Authoritative target removed |
| Cascade | Derived target feeds downstream rule in same pass |
| Idempotence | Correct state produces no write |
| Events | Duplicate events coalesce |
| Events | Plugin self-event settles at zero delta |
| Failure | Partial state retained |
| Failure | Failed item deferred to Full Reconcile |
| Recovery | Full Reconcile repairs partial state |
| Recovery | One item failure does not abort run |
| Lifecycle | Disable preserves metadata |
| Lifecycle | Delete preserves metadata |
| Lifecycle | Uninstall contract preserves metadata |
| Config | Valid save triggers reconcile |
| Config | Cyclic candidate config is not saved |
| Config | Missing collection becomes unresolved/skipped |
| Run-once | Does not persist active graph edge |
| Run-once | Existing collection target works |
| Run-once | Created collection target uses returned GUID |
| Preview | Includes item-level adds/removes |
| Preview | Includes cascaded final state |
| Safety | Authoritative removals require confirmation |

---

## 5. Integration spike checklist

Before relying on Jellyfin adapters, verify:

1. Exact target framework and Jellyfin package versions.
2. Minimal plugin loads on target server.
3. `ICollectionManager` events observed after UI and plugin mutations.
4. `ILibraryManager.ItemUpdated` behavior after tag edits.
5. Direct tag persistence across server restart.
6. Direct Movie collection membership persistence.
7. Direct Series collection membership persistence.
8. Whether tag writes trigger metadata save/NFO behavior under common server settings.
9. Collection creation API and duplicate-name behavior.
10. Plugin-generated writes generate expected self-events.
11. Plugin configuration serialization round-trips.
12. JPRM package loads when installed manually and from a manifest.

Record the results in `docs/compatibility/<server-version>.md` or equivalent.

---

## 6. Failure tests

Use fault-injecting writers to fail each operation position in a multi-operation plan.

Example:

```text
1. Add Animation membership   succeeds
2. Add animated tag           succeeds
3. Add Kids membership        fails
```

Assert:

- operations 1 and 2 remain applied;
- operation 3 is reported;
- later operations for that item do not run;
- item enters failed-until-Full-Reconcile state;
- Full Reconcile later completes the desired state.

---

## 7. Test evidence per issue

An issue is complete only when its acceptance criteria have evidence such as:

- named test cases;
- full test command and result;
- integration transcript or reproducible steps when automation is impractical;
- package/install evidence for release work.
