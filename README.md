# Collection Tag Sync

Collection Tag Sync is a third-party Jellyfin server plugin for explicit,
policy-driven synchronization between direct Movie and Series tags and
collection memberships.

> **Project status:** Design phase. The initial architectural decision records
> are accepted; production implementation has not begun.

## Planned capabilities

- Explicit Tag to Collection and Collection to Tag mappings
- Many-to-many relationships using target groups and OR aggregation
- Additive and Authoritative reconciliation policies
- Multi-hop propagation through an acyclic mapping graph
- Near-real-time synchronization backed by Full Reconcile
- Previewed run-once operations and destructive-change confirmation

The plugin will not automatically convert every Jellyfin tag into a collection.

## Documentation

- [Domain glossary](CONTEXT.md)
- [Design specification](docs/DESIGN.md)
- [Development plan](docs/PLAN.md)
- [Testing strategy](docs/TESTING.md)
- [Packaging and release plan](docs/RELEASE.md)
- [Architectural decision records](docs/adr/)
- [Upstream references](docs/REFERENCES.md)

## Development

The next gate is the target-ABI integration spike and implementation planning.
Behavioral work will then follow a test-first workflow and the phased plan in
[docs/PLAN.md](docs/PLAN.md).

## License

Collection Tag Sync is licensed under the
[GNU General Public License v3.0 only](LICENSE).

Jellyfin is a trademark of its respective owner. This project is independent
and is not affiliated with or endorsed by the Jellyfin project.
