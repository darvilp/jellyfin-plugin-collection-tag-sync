# ADR-006 — Lifecycle, run-once, and destructive safety

**Status:** Proposed<br>
**Decision owner:** Project maintainer<br>
**Review gate:** Must be Accepted before production coding

## Context

Mappings may be temporary, configuration changes may remove substantial metadata, and uninstall/disable behavior must be predictable.

## Decision

1. Continuous mappings are persisted and active until disabled/deleted.
2. Run-once operations use the same planning semantics but do not persist a mapping.
3. Disabling or deleting a mapping preserves existing Jellyfin tags and collection memberships.
4. Plugin uninstall preserves existing Jellyfin tags and collection memberships.
5. A valid configuration save triggers immediate reconciliation of affected behavior.
6. Preview includes item-level additions/removals, cascades, and final settled state.
7. Preview and execution use the same planner.
8. Authoritative changes that would remove state require current preview and explicit confirmation.
9. Run-once target collection selection supports existing collections and, where feasible, explicit creation from the picker.

## Consequences

- Temporarily running then disabling a mapping acts like an ad hoc conversion.
- No provenance or cleanup database is needed.
- Metadata cleanup is always explicit rather than an accidental lifecycle side effect.
- Destructive configuration is visible before activation.

## Required examples

```text
Enable Kid Approved → kid-approved.
Synchronize.
Disable mapping.
Existing kid-approved tags remain.
```

```text
Run once: kids-safe → Kids Safe collection.
Result remains, but no continuous edge is stored.
```
