# Collection Tag Sync

Collection Tag Sync is a third-party Jellyfin server plugin for explicit,
policy-driven synchronization between direct Movie and Series tags and
collection memberships.

> **Project status:** V1 alpha. The server engine and administrator UI include
> continuous and Full Reconcile workflows, destructive preview/confirmation,
> and previewed run-once execution. Treat the current package as prerelease
> software and back up Jellyfin before installing it.

## Implemented server capabilities

- Explicit Tag to Collection and Collection to Tag mappings
- Many-to-many relationships using target groups and OR aggregation
- Additive and Authoritative reconciliation policies
- Multi-hop propagation through an acyclic mapping graph
- Near-real-time synchronization backed by Full Reconcile
- Persistent reusable run-once groups with independent preview and execution
- GUID-backed collection picker and independent Add New workflow
- Thin administrator UI over the server-validated configuration and operation APIs

The plugin will not automatically convert every Jellyfin tag into a collection.

## Alpha installation

V1 targets Jellyfin 10.11.11 (`targetAbi` 10.11.11.0). In the Jellyfin
administrator dashboard:

1. Open **Plugins → Repositories**.
2. Add `Collection Tag Sync` with this repository URL:

   ```text
   https://raw.githubusercontent.com/darvilp/jellyfin-plugin-collection-tag-sync/manifest/manifest.json
   ```

3. Open **Catalog**, select **Collection Tag Sync**, and install version
   `0.2.0.0`.
4. Restart Jellyfin when prompted.
5. Open **Dashboard → Plugins → Collection Tag Sync** to configure mappings or
   run one-time operations.

Back up Jellyfin's configuration and data before alpha installation or upgrade.
Uninstalling the plugin does not undo tag or collection metadata it previously
synchronized. See the [current alpha release notes](docs/releases/v0.2.0.0-alpha.md)
for compatibility and known limitations.

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

The target-ABI integration spike and V1 implementation are complete. Ongoing
work follows the test-first workflow and phased plan in
[docs/PLAN.md](docs/PLAN.md).

## License

Collection Tag Sync is licensed under the
[GNU General Public License v3.0 only](LICENSE).

Jellyfin is a trademark of its respective owner. This project is independent
and is not affiliated with or endorsed by the Jellyfin project.
