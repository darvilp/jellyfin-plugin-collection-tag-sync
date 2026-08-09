# ADR-002 — Effective-state and policy semantics

**Status:** Proposed<br>
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
- treats configured sources as the complete authority for that target.

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
Waltney → Animation
Item is manually in Animation but lacks Waltney
Result: Animation remains true
```

### Authoritative manual target

```text
Waltney → Animation
Item is manually in Animation but lacks Waltney
Result: remove Animation membership
```

### Cascade

```text
Waltney → Animation → animated → Kids
Observed Waltney = true
Result in one plan: Animation, animated, and Kids become true
```
