# Collection Tag Sync

Collection Tag Sync is a third-party Jellyfin server plugin for explicit,
policy-driven synchronization between direct Movie and Series tags and
collection memberships.

> **Project status:** Design phase. The architectural decision records are
> proposed and must be accepted before production implementation begins.

## Planned capabilities

- Explicit Tag to Collection and Collection to Tag mappings
- Many-to-many relationships using target groups and OR aggregation
- Additive and Authoritative reconciliation policies
- Multi-hop propagation through an acyclic mapping graph
- Near-real-time synchronization backed by Full Reconcile
- Previewed run-once operations and destructive-change confirmation

The plugin will not automatically convert every Jellyfin tag into a collection.

## Documentation

- [Design specification](docs/DESIGN.md)
- [Development plan](docs/PLAN.md)
- [Testing strategy](docs/TESTING.md)
- [Packaging and release plan](docs/RELEASE.md)
- [Architectural decision records](docs/adr/)
- [Upstream references](docs/REFERENCES.md)

## Development

Implementation will begin after the proposed ADRs have been reviewed, aligned,
and accepted. Behavioral work will follow a test-first workflow and the phased
plan in [docs/PLAN.md](docs/PLAN.md).

## License

Collection Tag Sync is licensed under the
[GNU General Public License v3.0 only](LICENSE).

Jellyfin is a trademark of its respective owner. This project is independent
and is not affiliated with or endorsed by the Jellyfin project.
