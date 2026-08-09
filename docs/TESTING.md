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

The release-tooling contract also rejects tag/build/assembly/package/manifest
version drift, verifies the manifest's exact immutable asset URL and JPRM
checksum, and verifies the human-readable SHA-256 companion file. A true
cross-version upgrade test is required beginning with the second public package;
the first release cannot manufacture prior-release evidence.

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

### Circuit-breaker atomicity

When a bulk plan exceeds the configured Authoritative-removal limit, no
operation from that plan is applied before administrator confirmation.

Default boundary cases:

- 25 unique affected items do not trip the absolute limit; 26 do.
- Exactly 20 percent does not trip the relative limit; more than 20 percent does.
- The relative limit is not evaluated below 10 current target assignments.
- One item with several removals counts once toward the absolute limit.
- Either limit independently pauses the entire plan.

---

## 4. Core behavior matrix

| Area | Required case |
|---|---|
| Identity | Tag comparison is trimmed and case-insensitive |
| Identity | Empty-after-trim tag is rejected |
| Identity | Existing case-equivalent tag satisfies target without a casing rewrite |
| Identity | Absent tag addition uses configured trimmed spelling |
| Identity | Logical removal removes every case-equivalent variant |
| Identity | Case-only variants cannot be separate persisted target groups |
| Identity | Diacritic variants remain distinct |
| Identity | Collection identity is GUID-based |
| Mapping | One source → one target |
| Mapping | Many sources → one target |
| Mapping | One group accepts mixed tag and collection sources |
| Mapping | One source → many targets |
| Mapping | One target cannot have two persisted groups, even when disabled |
| Aggregation | OR semantics |
| Graph | Direct self-cycle rejected |
| Graph | Multi-hop cycle rejected |
| Graph | Disabled group excluded from active DAG |
| Graph | Enabling a disabled group revalidates the complete active DAG |
| Graph | Useful cycle path returned |
| Scope | Movie participates |
| Scope | Series participates |
| Scope | Episode ignored |
| Scope | Season ignored |
| Scope | Music ignored |
| Tags | Only direct tags count |
| Collections | Only direct Movie/Series membership counts |
| Additive | Missing target added |
| Additive | Existing unsupported target preserved |
| Additive | No removal emitted |
| Authoritative | Missing supported target added |
| Authoritative | Unsupported target removed |
| Authoritative | Unsupported externally added target removed |
| Authoritative | Ordinary external source change reconciles without a new prompt |
| Many-source | First source removed; target remains |
| Many-source | Last source removed; Authoritative target removed |
| Many-source | Mixed source types use the same OR behavior |
| Cascade | Derived target feeds downstream rule in same pass |
| Cascade | Preserved manual Additive target feeds downstream rule |
| Idempotence | Correct state produces no write |
| Events | Duplicate events coalesce |
| Events | Event payload does not bypass desired-state replanning |
| Events | Plugin self-event settles at zero delta |
| Failure | Partial state retained |
| Failure | Failed item deferred to Full Reconcile |
| Recovery | Full Reconcile repairs partial state |
| Recovery | One item failure does not abort run |
| Recovery | Startup queues one coalesced Full Reconcile when mappings are enabled |
| Recovery | Startup delay defaults to 5 minutes and accepts 0 through 60 |
| Recovery | Active scan or event storm defers an expired startup request until quiet |
| Lifecycle | Disable preserves metadata |
| Lifecycle | Delete preserves metadata |
| Lifecycle | Uninstall contract preserves metadata |
| Lifecycle | Cleanup requires a separate explicit operation |
| Config | Valid save persists a revision and enqueues background reconcile before returning |
| Config | Save response does not wait for metadata settlement |
| Config | Reconciliation status exposes queued through terminal/paused outcomes |
| Config | Cyclic candidate config is not saved |
| Config | Destructive candidate remains unsaved until confirmed |
| Config | Changed removal set rejects confirmation and saves nothing |
| Config | Accepted configuration remains active after partial write failure |
| Preview | Authorization expires after 10 minutes and is single-use |
| Preview | Authorization is bound to administrator, candidate, revision, and removal tuples |
| Preview | Restart invalidates authorization |
| Preview | Addition-only drift does not invalidate authorization |
| Config | Missing source collection makes the entire mixed-source group unresolved |
| Config | Missing target collection makes every group referencing it unresolved |
| Config | Unresolved group preserves enabled configuration and emits no target mutations |
| Config | Unresolved group passes observed target state to valid downstream groups |
| Config | Unresolved warning persists until explicit repair or disable |
| Run-once | Does not persist active graph edge |
| Run-once | Remains available while continuous automatic mappings are active |
| Run-once | Reverse/bootstrap path settles through active continuous DAG |
| Run-once | Enabled continuous target conflict is rejected |
| Run-once | Disabled group does not block the same target |
| Run-once | Existing collection target works |
| Run-once | Existing collection target cannot be bound by free-text name |
| Run-once | Add-new workflow selects the returned collection GUID |
| Run-once | Add-new rejects empty and normalized duplicate collection names |
| Run-once | Created collection remains after surrounding workflow cancellation |
| Run-once | Created collection remains after surrounding workflow save failure |
| Run-once | Existing duplicate names remain distinct picker choices |
| Run-once | Authoritative policy covers every eligible Movie and Series |
| Run-once | Optimized candidate set matches whole-library semantics |
| Run-once | Excluded addition keeps current target false |
| Run-once | Excluded removal keeps current target true |
| Run-once | Exclusion recomputes cascades and cannot suppress them directly |
| Run-once | Exclusions are authorization-bound and not persisted |
| Run-once | Created collection target uses returned GUID |
| Preview | Includes item-level adds/removes |
| Preview | Includes cascaded final state |
| Safety | Authoritative removals require confirmation |
| Safety | Bulk destructive limit pauses the whole plan before writes |
| Safety | Circuit-breaker defaults and exact boundaries are configurable and tested |
| Safety | Disabling the circuit breaker requires warning and confirmation |
| UI | Embedded administrator page and controller resources are discoverable |
| UI | Duplicate collection names remain distinct GUID-valued picker choices |
| UI | Validation messages are rendered from server responses, not reimplemented rules |
| UI | Editing a candidate, operation, or exclusions invalidates its preview action |
| UI | Background queued, running, completed, failed, and paused states are rendered |
| UI | Actions use native keyboard-operable controls and status uses live regions |
| UI | Tag discovery and operational diagnostics require administrator elevation |
| UI | Manual Full Reconcile is queued through a server-owned background action |

---

## 5. Integration spike checklist

Before relying on Jellyfin adapters, verify:

1. Exact target framework and Jellyfin package versions.
2. Minimal plugin loads on target server.
3. `ICollectionManager` events observed after UI and plugin mutations.
4. `ILibraryManager.ItemUpdated` behavior after tag edits.
5. Direct tag persistence across server restart.
6. Confirm 10.11.x source findings for case-equivalent tag add, read, filter,
   and removal behavior on a live server.
7. Direct Movie collection membership persistence.
8. Direct Series collection membership persistence.
9. Whether tag writes trigger metadata save/NFO behavior under common server settings.
10. Collection creation API, duplicate-name behavior, and independent lifecycle
   across surrounding workflow cancellation/failure.
11. Plugin-generated writes generate expected self-events.
12. Plugin configuration serialization round-trips.
13. JPRM package loads when installed manually and from a manifest.

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
- other items continue processing;
- Full Reconcile later completes the desired state.

---

## 7. Test evidence per issue

An issue is complete only when its acceptance criteria have evidence such as:

- named test cases;
- full test command and result;
- integration transcript or reproducible steps when automation is impractical;
- package/install evidence for release work.
