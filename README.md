# Collection Tag Sync

Collection Tag Sync is a third-party Jellyfin server plugin for explicit,
policy-driven synchronization between direct Movie and Series tags and
collection memberships.

> **Project status:** The server-side V1 engine now includes continuous and Full
> Reconcile workflows, destructive preview/confirmation, and previewed run-once
> execution. The administration UI and release hardening remain in progress.

## Implemented server capabilities

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
- [Local development](docs/DEVELOPMENT.md)
- [Testing strategy](docs/TESTING.md)
- [Packaging and release plan](docs/RELEASE.md)
- [Jellyfin 10.11.11 compatibility](docs/compatibility/jellyfin-10.11.11.md)
- [Architectural decision records](docs/adr/)
- [Upstream references](docs/REFERENCES.md)

## Development

The target-ABI integration spike is complete. Behavioral implementation follows
the ordered GitHub issue backlog, a test-first workflow, and the phased plan in
[docs/PLAN.md](docs/PLAN.md).

## License

Collection Tag Sync is licensed under the
[GNU General Public License v3.0 only](LICENSE).

Jellyfin is a trademark of its respective owner. This project is independent
and is not affiliated with or endorsed by the Jellyfin project.
