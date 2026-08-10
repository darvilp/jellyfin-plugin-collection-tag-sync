# ADR-003 — Continuous mappings form a directed acyclic graph

**Status:** Accepted<br>
**Decision owner:** Project maintainer<br>
**Review gate:** Must be Accepted before production coding

## Context

A continuous cycle makes removal semantically self-restoring even if recursive events are suppressed.

Example:

```text
Waltney tag → Animation collection
Animation collection → Waltney tag
```

Removing either side causes the other side to restore it during the next reconciliation.

## Decision

1. The complete enabled continuous mapping graph must be a DAG.
2. Direct and indirect cycles are rejected before configuration activation.
3. Validation returns the discovered cycle path.
4. Disabled groups do not participate in the active graph.
5. Run-once groups are persisted reusable definitions, but they never become
   continuous graph edges. Each run-once operation executes exactly one group
   independently.
6. A run-once operation is planned in stages:
   - evaluate the active continuous DAG to establish baseline effective state;
   - evaluate the one-time target;
   - re-evaluate affected downstream continuous mappings to their final settled
     state.
7. A run-once target that is already managed by an enabled continuous group is
   rejected because the two policies would compete in one plan.
8. A disabled persisted group does not block a run-once operation targeting the
   same node.
9. A reverse/bootstrap run-once path may point against an active continuous path
   because the one-time edge is staged and never joins the persisted graph.

## Consequences

- Multi-hop propagation is deterministic.
- Topological evaluation is possible.
- Continuous true bidirectional synchronization is not supported.
- Reverse/bootstrap behavior remains available through run-once operations.
- Run-once cannot temporarily override an enabled continuous target.
- Event suppression is not used as a substitute for semantic cycle prevention.

## Required examples

Valid:

```text
Waltney → Animation → animated → Kids
```

Invalid:

```text
Waltney → Animation → animated → Kids → Waltney
```

Expected diagnostic includes the complete path.

Valid reverse bootstrap:

```text
CONTINUOUS:
Tag "Waltney" → Collection "Animation"

RUN ONCE:
Collection "Animation" → Tag "Waltney"

Result:
The run-once result is applied, affected continuous mappings settle, and no
reverse edge remains active.
```

Invalid target conflict:

```text
CONTINUOUS:
Tag "Waltney" → Collection "Animation"

RUN ONCE:
Tag "Blooth" → Collection "Animation"

Result:
Rejected because Collection "Animation" already has an enabled continuous
group.
```

Disabled then enabled:

```text
An enabled path contains A → B → C.
A disabled group contains C → A.

While disabled:
  valid because C → A is not in the continuous graph

Attempt to enable:
  rejected with cycle path A → B → C → A
```
