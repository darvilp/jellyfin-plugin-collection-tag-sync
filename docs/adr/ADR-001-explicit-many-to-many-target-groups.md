# ADR-001 — Explicit many-to-many target groups

**Status:** Proposed<br>
**Decision owner:** Project maintainer<br>
**Review gate:** Must be Accepted before production coding

## Context

The plugin must support:

```text
Animation → Animation collection
Waltney    → Animation collection
Blooth     → Animation collection
```

and:

```text
Waltney → Animation collection
Waltney → Kids collection
Waltney → American collection
```

Jellyfin may contain many unrelated tags, so implicit conversion of every tag is unsafe and noisy. Allowing separate groups for the same target with different policies makes removal semantics ambiguous.

## Decision

1. Synchronization is explicitly configured.
2. The user-facing unit is a target group:
   - one target;
   - one or more sources;
   - one policy;
   - enabled/disabled state.
3. A source may participate in any number of target groups.
4. A target may have at most one enabled continuous group.
5. Multiple sources use OR semantics.
6. Internally, groups flatten into directed source → target edges.

## Consequences

- Many-to-many relationships are supported.
- A target’s behavior is visible in one place.
- Policy conflicts for one target are impossible.
- Removing one source does not remove a target still supported by another source.
- There is no “all tags become collections” mode.

## Required examples

```text
Sources: Waltney, Blooth
Target: Animation

Item has Waltney and Blooth:
  target supported

Remove Waltney:
  target remains supported by Blooth

Remove Blooth:
  target becomes unsupported
```

Whether unsupported target state is removed depends on ADR-002 policy semantics.
