# Jellyfin tag casing semantics

**Research date:** 2026-08-09

**Version examined:** Jellyfin Server 10.11.11 (`1fbd8739`) and Jellyfin Web
10.11.11 (`35c0793e`)

## Question

Does Jellyfin treat tags that differ only by casing as distinct, and what
semantics should Collection Tag Sync use?

## Conclusion

Jellyfin 10.11.11 has no single, universal tag-casing rule.

- The item model, update endpoint, and database preserve exact tag spelling and
  can represent case-only variants separately.
- Common server operations—including `AddTag`, metadata merging, tag filters,
  parental visibility, and filter-option discovery—treat case-only variants as
  equivalent.
- Jellyfin Web's metadata editor submits the displayed strings exactly and does
  not itself deduplicate them.

Therefore, exact storage casing is observable, but treating `Kid-Approved` and
`kid-approved` as independent logical tags would disagree with several
user-facing Jellyfin behaviors.

## Evidence

### Version scope

The official release page identifies 10.11.11 as the latest stable server
release and points to commit `1fbd8739`.[^release] The relevant tag extension,
filter controller, database index configuration, and database migration are
unchanged between the `v10.11.0` and `v10.11.11` tags; the tag-specific portion
of the item-update controller is also unchanged. The findings therefore apply
across the released 10.11.x line as inspected, not only to 10.11.11.[^compare]

### Exact spelling is preserved and case-only variants are representable

`BaseItem.Tags` is a string array. When the repository writes an item, it joins
the array without changing case; when it reads the item, it splits the stored
value without changing case.[^raw-storage]

The repository also indexes each tag as both its exact `Value` and a
`CleanValue`. `CleanValue` is the value with diacritics removed and then
lowercased invariantly.[^item-values] The current schema makes `(Type, Value)`
unique but leaves `(Type, CleanValue)` non-unique.[^index-config] That index
shape was introduced explicitly by the `FixItemValuesIndices` migration.[^index-migration]
No collation is assigned to `Value` in the current SQLite model snapshot, so
SQLite's default `BINARY` collation applies and distinguishes case.[^snapshot]
SQLite documents `BINARY` as its default collation when none is specified.[^sqlite]
Consequently, `Kid-Approved` and `kid-approved` can coexist as separate exact
values while sharing the same clean value.

The item-update endpoint likewise assigns `request.Tags` directly to the item.
Its added/removed calculations call LINQ `Except` without a comparer, which
uses the default comparer; for strings, default equality is ordinal and
case-sensitive.[^item-update][^dotnet-except][^dotnet-string]

### Several domain operations treat casing as equivalent

Jellyfin's `AddTag` helper refuses to add a tag if the item already contains a
case-insensitive ordinal match.[^add-tag] The XBMC/NFO parser uses that helper,
so the first case-equivalent spelling encountered wins on that ingestion
path.[^nfo-parser]

Metadata merging concatenates existing and incoming tags and deduplicates with
`StringComparer.OrdinalIgnoreCase`.[^metadata-merge] The item-update endpoint's
propagation from a series, season, or album to descendants also ends with an
ordinal-ignore-case deduplication, even though its initial added/removed set
calculation is case-sensitive.[^item-update]

Not every parser agrees: the local Jellyfin XML parser deduplicates with
`StringComparer.Ordinal`, which is case-sensitive.[^xml-parser] This is further
evidence that exact storage and ingestion behavior are inconsistent rather
than governed by a single canonical rule.

### Filtering and visibility are case-insensitive

The server's `Tags` and `ExcludeTags` item filters convert both the query values
and stored tag values to `CleanValue`, so tag filtering is case-insensitive and
also diacritic-insensitive.[^tag-filter]

The legacy filter-options endpoint used by Jellyfin Web collapses tag choices
with `StringComparer.OrdinalIgnoreCase`.[^filter-options] The web filter dialog
then sends the selected displayed spelling through the `Tags` filter.[^web-filter]
As a result, the UI cannot reliably expose `Kid-Approved` and `kid-approved` as
two independently selectable filter identities.

Parental allowed/blocked-tag checks also use ordinal-ignore-case comparisons,
and inherited tag aggregation deduplicates the same way.[^visibility]

General `SearchTerm` handling is separate from the tag filter. In 10.11.11 the
search engine passes a search term into the item query, whose predicate checks
clean item name and original title, not tag values.[^general-search]

### Jellyfin Web preserves editor input

The 10.11.11 metadata editor collects the visible tag text and submits that
array as `Tags`.[^web-submit] Adding an editor entry simply appends the supplied
text; the list renderer sorts case-insensitively for display but does not
deduplicate or normalize the strings.[^web-editor] This makes exact variants
possible through the UI even though filters and several server operations later
collapse them semantically.

## Recommendation for Collection Tag Sync

Use the following V1 contract:

1. Normalize tag identity for mapping, matching, target uniqueness, and
   add/remove decisions with trimmed `StringComparer.OrdinalIgnoreCase`.
2. If an item already contains `Kid-Approved` and the configured target is
   `kid-approved`, consider the target present and preserve the stored spelling.
   A mapping should not rewrite metadata merely to change case.
3. When adding a tag that has no case-insensitive match, write the trimmed
   spelling configured by the administrator.
4. When an authoritative operation removes a tag, remove all
   ordinal-ignore-case variants. Leaving a case variant behind would still make
   the item match Jellyfin filters, `AddTag`, and parental visibility checks.
5. If an item already contains multiple case-only variants, surface that as a
   diagnostic but do not silently choose a new canonical spelling in Additive
   mode. Authoritative removal may remove the entire normalized identity.

This contract follows the dominant operational semantics while preserving
Jellyfin's observable user spelling. Treating case-only variants as independent
mapping nodes is the strongest alternative, but it would create distinctions
that Jellyfin's filter UI, query filters, `AddTag`, metadata merge, and parental
controls cannot consistently honor.

Diacritic equivalence should remain a separate decision. The database filter
path removes diacritics, while `OrdinalIgnoreCase` operations do not, so the
server evidence does not support silently extending the plugin's tag identity
normalization from case folding to accent folding.

## Confidence and gaps

**Confidence: high** for the 10.11.x source-level conclusions. They are based on
the tagged 10.11.11 server and web sources, the SQLite schema/migration, and the
unchanged relevant files across the 10.11.0-to-10.11.11 range.

Gaps:

- No Jellyfin test directly states a normative tag-casing contract; the result
  is synthesized from production paths.
- No live 10.11.11 server/database experiment was performed for this note.
- Provider-specific ingestion paths beyond the shared metadata merger and the
  two inspected XML/NFO parsers may behave differently.
- The first spelling returned when case-only variants are collapsed by a
  database query is not specified as stable and should not be used as identity.

[^release]: [Jellyfin Server 10.11.11 release](https://github.com/jellyfin/jellyfin/releases/tag/v10.11.11)
[^compare]: [Jellyfin Server v10.11.0...v10.11.11 comparison](https://github.com/jellyfin/jellyfin/compare/v10.11.0...v10.11.11)
[^raw-storage]: [`BaseItemRepository` reads and writes the exact tag strings](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server.Implementations/Item/BaseItemRepository.cs#L915-L918) and [writes them by joining the array](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server.Implementations/Item/BaseItemRepository.cs#L1077-L1080)
[^item-values]: [`BaseItemRepository` writes exact `Value` plus normalized `CleanValue`](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server.Implementations/Item/BaseItemRepository.cs#L677-L705) and [`GetCleanValue` removes diacritics and lowercases invariantly](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server.Implementations/Item/BaseItemRepository.cs#L1438-L1465)
[^index-config]: [`ItemValuesConfiguration` has a non-unique clean-value index and unique exact-value index](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/src/Jellyfin.Database/Jellyfin.Database.Implementations/ModelConfiguration/ItemValuesConfiguration.cs#L13-L18)
[^index-migration]: [`FixItemValuesIndices` migration](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/20250405075612_FixItemValuesIndices.cs#L11-L26)
[^snapshot]: [10.11.11 SQLite model snapshot for `ItemValue`](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/JellyfinDbModelSnapshot.cs#L723-L745)
[^sqlite]: [SQLite collation rules](https://www.sqlite.org/datatype3.html#collating_sequences)
[^item-update]: [`ItemUpdateController` direct assignment and descendant propagation](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/ItemUpdateController.cs#L286-L305)
[^dotnet-except]: [Microsoft documentation for `Enumerable.Except`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.except?view=net-8.0)
[^dotnet-string]: [Microsoft documentation for default string equality](https://learn.microsoft.com/en-us/dotnet/api/system.string.op_equality?view=net-8.0)
[^add-tag]: [`TagExtensions.AddTag` uses `OrdinalIgnoreCase`](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/Entities/TagExtensions.cs#L11-L30)
[^nfo-parser]: [The XBMC/NFO parser calls `AddTag`](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.XbmcMetadata/Parsers/BaseNfoParser.cs#L585-L593)
[^metadata-merge]: [`MetadataService` deduplicates merged tags with `OrdinalIgnoreCase`](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Providers/Manager/MetadataService.cs#L1125-L1134)
[^xml-parser]: [The Jellyfin XML parser deduplicates tags with `StringComparer.Ordinal`](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.LocalMetadata/Parsers/BaseItemXmlParser.cs#L639-L672)
[^tag-filter]: [`BaseItemRepository` tag filters compare normalized clean values](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server.Implementations/Item/BaseItemRepository.cs#L2158-L2169)
[^filter-options]: [`FilterController` collapses filter-option tags with `OrdinalIgnoreCase`](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/FilterController.cs#L91-L109)
[^web-filter]: [Jellyfin Web obtains the legacy filter data](https://github.com/jellyfin/jellyfin-web/blob/35c0793ece3adbd247eab290ae1effab851f3d37/src/components/filterdialog/filterdialog.js#L62-L76), [renders its tag options](https://github.com/jellyfin/jellyfin-web/blob/35c0793ece3adbd247eab290ae1effab851f3d37/src/components/filterdialog/filterdialog.js#L43-L55), and [sets `query.Tags`](https://github.com/jellyfin/jellyfin-web/blob/35c0793ece3adbd247eab290ae1effab851f3d37/src/components/filterdialog/filterdialog.js#L366-L380)
[^visibility]: [`BaseItem` inherited tag and parental visibility checks use `OrdinalIgnoreCase`](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/Entities/BaseItem.cs#L1664-L1698)
[^general-search]: [`SearchEngine` builds an item query from `SearchTerm`](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Emby.Server.Implementations/Library/SearchEngine.cs#L145-L173), whose [repository predicate searches name and original title](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server.Implementations/Item/BaseItemRepository.cs#L1779-L1791)
[^web-submit]: [Jellyfin Web submits visible list values as `Tags`](https://github.com/jellyfin/jellyfin-web/blob/35c0793ece3adbd247eab290ae1effab851f3d37/src/components/metadataEditor/metadataEditor.js#L135-L150)
[^web-editor]: [The editor appends exact text without deduplication](https://github.com/jellyfin/jellyfin-web/blob/35c0793ece3adbd247eab290ae1effab851f3d37/src/components/metadataEditor/metadataEditor.js#L208-L223) and [sorts only for display](https://github.com/jellyfin/jellyfin-web/blob/35c0793ece3adbd247eab290ae1effab851f3d37/src/components/metadataEditor/metadataEditor.js#L921-L940)
