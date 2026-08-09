# ADR-007 — Identity, configuration, and UI boundary

**Status:** Accepted<br>
**Decision owner:** Project maintainer<br>
**Review gate:** Must be Accepted before production coding

## Context

Collection names are mutable and may collide. Tag strings need normalization. Jellyfin plugin configuration serialization favors simple DTOs. UI validation alone is insufficient.

## Decision

1. Collections are identified by Jellyfin GUID.
2. Collection names are display data only.
3. Tags use the Jellyfin-aligned casing contract documented in
   [the 10.11.x source research](../research/jellyfin-tag-casing.md):
   - trim configured values and reject an empty result;
   - use `StringComparer.OrdinalIgnoreCase` for mapping identity, matching,
     target uniqueness, and add/remove planning;
   - treat an existing case-equivalent spelling as present and preserve that
     spelling rather than performing a case-only rewrite;
   - use the administrator's trimmed configured spelling when adding a tag that
     has no case-equivalent match;
   - when the logical tag must be absent, remove every case-equivalent variant.
4. Existing collections are selected only through a collection picker; free-text
   collection names cannot resolve or bind an existing collection.
5. The picker includes an explicit `Add new collection…` action. It opens a
   distinct creation workflow on the same screen. After successful creation,
   the returned Jellyfin GUID becomes the selected target.
6. Collection creation rejects an empty name or a trimmed, case-insensitive name
   match with an existing collection. It creates nothing and presents the
   existing matches for explicit picker selection.
7. Successful collection creation is an immediate Jellyfin action independent
   of the mapping or run-once workflow. The collection remains if that workflow
   is canceled or its save fails; the plugin does not roll it back.
8. Existing duplicate-named collections remain distinct GUID identities and are
   shown as separate picker entries with disambiguating details.
9. No silent collection lookup/rebinding by name occurs.
10. If any collection GUID referenced by an enabled group is deleted later,
    retain the group's configuration and enabled state but mark the entire group
    operationally unresolved. The planner performs no target mutations from
    that group and passes its target's current observed state through as
    effective state for valid downstream groups. Surface a persistent warning
    until the group is repaired or disabled; do not partially evaluate its
    remaining sources.
11. Persist simple serializer-friendly mutable configuration DTOs.
12. Convert persisted DTOs into validated immutable domain objects before graph construction.
13. Include a configuration schema version from the first release.
14. Server-side validation is authoritative; the UI is a convenience layer.
15. Custom API operations require Jellyfin administrator authorization.

## Consequences

- Collection renames do not break mappings.
- Case-only tag variants cannot become separate mapping nodes that Jellyfin's
  own filtering, metadata merge, and visibility behavior cannot reliably
  distinguish.
- Existing tag spelling is not rewritten merely to normalize casing.
- Collection-name typos cannot silently bind an unintended existing collection;
  entering a name occurs only inside the explicit creation workflow.
- The creation workflow does not add another collection with a duplicate display
  name, while pre-existing duplicates remain explicitly selectable by GUID.
- Canceling or failing a surrounding workflow does not delete a successfully
  created collection that Jellyfin users or other processes may already use.
- Delete/recreate produces a new identity and requires explicit rebinding.
- Configuration migration can be deliberate.
- UI implementation does not become a second business-logic engine.
- Missing collection references fail closed instead of causing partial-source
  evaluation or unintended Authoritative removals.

## Required examples

```text
Rename collection Animation → Animated Movies:
  mapping continues because GUID is unchanged.

Delete collection and create a new collection with the same name:
  old mapping remains unresolved; no automatic rebinding.

Delete one collection source from a mixed-source group:
  the whole group is skipped; its target remains as observed and may still feed
  valid downstream groups.

Configured target `kid-approved`; item already has `Kid-Approved`:
  target is present and existing spelling is preserved.

Authoritative target `kid-approved` becomes unsupported while an item contains
`Kid-Approved` and `KID-APPROVED`:
  every case-equivalent variant is removed.
```
