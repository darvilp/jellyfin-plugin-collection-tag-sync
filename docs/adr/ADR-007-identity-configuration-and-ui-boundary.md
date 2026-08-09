# ADR-007 — Identity, configuration, and UI boundary

**Status:** Proposed<br>
**Decision owner:** Project maintainer<br>
**Review gate:** Must be Accepted before production coding

## Context

Collection names are mutable and may collide. Tag strings need normalization. Jellyfin plugin configuration serialization favors simple DTOs. UI validation alone is insufficient.

## Decision

1. Collections are identified by Jellyfin GUID.
2. Collection names are display data only.
3. Tags are trimmed and compared case-insensitively while preserving display casing.
4. Existing collections are selected through a picker.
5. Target collection selection should include an explicit `Create new collection…` path where technically feasible.
6. No silent collection lookup/rebinding by name occurs.
7. If a referenced collection is deleted later, retain the mapping as unresolved, skip it, and surface a warning.
8. Persist simple serializer-friendly mutable configuration DTOs.
9. Convert persisted DTOs into validated immutable domain objects before graph construction.
10. Include a configuration schema version from the first release.
11. Server-side validation is authoritative; the UI is a convenience layer.
12. Custom API operations require Jellyfin administrator authorization.

## Consequences

- Collection renames do not break mappings.
- Delete/recreate produces a new identity and requires explicit rebinding.
- Configuration migration can be deliberate.
- UI implementation does not become a second business-logic engine.
- Broken collection references are handled simply without elaborate auto-recovery.

## Required examples

```text
Rename collection Animation → Animated Movies:
  mapping continues because GUID is unchanged.

Delete collection and create a new collection with the same name:
  old mapping remains unresolved; no automatic rebinding.
```
