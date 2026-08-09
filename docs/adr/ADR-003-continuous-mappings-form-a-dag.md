# ADR-003 — Continuous mappings form a directed acyclic graph

**Status:** Proposed<br>
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
5. Run-once operations are not persisted and do not become continuous graph edges.

## Consequences

- Multi-hop propagation is deterministic.
- Topological evaluation is possible.
- Continuous true bidirectional synchronization is not supported.
- Reverse/bootstrap behavior remains available through run-once operations.
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
