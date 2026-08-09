# ADR-002 — Effective-state and policy semantics

**Status:** Accepted<br>
**Decision owner:** Project maintainer<br>
**Review gate:** Must be Accepted before production coding

## Context

A mapped target may already exist manually, may be derived from upstream nodes, and may itself feed downstream mappings. Event-by-event add/remove translation is insufficient for multi-hop and many-source behavior.

## Decision

For each eligible item, evaluate nodes in topological order.

### Unmapped node

```text
effective(node) = observed(node)
```

### Additive target

```text
effective(target) = observed(target) OR any(effective(source))
```

Additive:

- adds missing supported state;
- never removes existing target state;
- allows existing manual target state to feed downstream mappings.

### Authoritative target

```text
effective(target) = any(effective(source))
```

Authoritative:

- adds supported target state;
- removes unsupported target state;
- treats configured sources as the complete authority for that target,
  including state added manually or by an external metadata writer.
- treats ordinary source and target changes from every writer as observed state;
- reconciles ordinary single-item changes while active without requesting a new
  confirmation for each event.

### Planning

Only mapped targets produce mutations:

```text
delta = effective(target) versus observed(target)
```

## Consequences

- One pass calculates final multi-hop state.
- Additive is the safe preserve-manual-state policy.
- Authoritative can remove manually created state.
- No provenance database is required.
- “Managed mirror” behavior is deferred.

## Required examples

### Additive manual target

```text
Waltney → Animation [Additive]
Animation → animated

Observed:
  Waltney = false
  Animation = true from manual membership
  animated = false

Result:
  Animation remains true
  animated becomes true because preserved Animation state feeds downstream
```

### Authoritative manual target

```text
Waltney → Animation [Authoritative]

Observed:
  Waltney = false
  Animation = true from manual membership

Result:
  Animation becomes false
  remove Animation membership
```

### Cascade

```text
Waltney → Animation → animated → Kids
Observed Waltney = true
Result in one plan: Animation, animated, and Kids become true
```

## Bulk external-change safety

Automatic metadata providers, NFO imports, library scans, other plugins, and API
clients may change source tags or collection memberships. Those external changes
can make an Authoritative target unsupported and trigger cascading removals.

An active Authoritative mapping treats ordinary external source and target
changes as immediate inputs without requesting a new confirmation for each
event.

Bulk reconciliation uses a destructive circuit breaker:

1. Calculate the complete bulk plan before applying any mutation.
2. Compare planned Authoritative removals with a configurable safety limit.
3. If the limit is exceeded, apply none of the plan.
4. Mark the run as awaiting administrator approval and expose its item-level
   preview.
5. Recompute before confirmed execution using the same stale-preview safeguards
   as other destructive operations.

The circuit breaker is enabled by default and trips when either condition is
true:

- the plan removes one or more target states from more than 25 unique items
  across the bulk run; or
- one mapping group removes more than 20 percent of its currently observed
  target assignments, when that group currently has at least 10 assignments.

An item affected by several removal operations counts once toward the absolute
limit. Cascaded removals count toward the group that owns each removed target.
The limits are administrator-configurable. Disabling the circuit breaker
entirely requires an explicit warning and confirmation.
