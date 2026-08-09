# ADR-004 — V1 media and metadata scope

**Status:** Proposed<br>
**Decision owner:** Project maintainer<br>
**Review gate:** Must be Accepted before production coding

## Context

Jellyfin contains many item types and may expose inherited/derived metadata behavior. Supporting every type and physically propagating tags to descendants would increase risk and library write volume.

## Decision

V1 supports only:

- Movie
- Series

The plugin evaluates and mutates only:

- direct tags stored on the eligible Movie/Series;
- direct membership of the eligible Movie/Series in a collection.

The plugin does not:

- evaluate inherited tags;
- modify seasons or episodes;
- propagate Series tags to descendants;
- support music or other item types;
- directly edit NFO files.

## Consequences

- The motivating video-library use cases are covered.
- Library write volume remains controlled.
- Jellyfin remains responsible for any downstream behavior it derives from Series metadata.
- Additional item types can be added later behind an eligibility abstraction.

## Required examples

```text
Bluppy Series directly has tag kids:
  Tag source is true for the Series.

Bluppy Episode lacks a direct kids tag:
  Episode is ignored entirely in v1.
```
