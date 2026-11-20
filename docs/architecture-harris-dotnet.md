# Harris County Oracle Pipeline — .NET 10 + DuckDB Architecture

Canonical county slug: **`harris-tx`** (use everywhere: CLI flags, MCP county parameter, IPNS labels, artifact paths, `PROPERTY_QUERY_TABLE_MAP` keys).

## Goals

Satisfy [README.md](../README.md) acceptance criteria without Oracle-hosted database infrastructure:

- Continuous, idempotent ingestion with run history and deltas
- DuckDB as the portable analytical store (local demo + published Parquet on IPFS)
- Public IPFS artifacts (CIDv1 base32, manifest, CAR, dual-gateway verification)
- MCP + Web UI for the demo transcript (roofing lead queries)

## Solution map

| Project | Responsibility |
|---------|----------------|
| `Oracle.Harris.Domain` | Lexicon-aligned entities, provenance, roofing derived fields, IPFS manifest models |
| `Oracle.Harris.Storage` | DuckDB schema, migrations, bulk load, read repositories |
| `Oracle.Harris.Ingest` | Watermarks, content-hash change detection, reconciliation, source limitations |
| `Oracle.Harris.Publish` | Parquet/JSON export, CIDv1 + SHA-256, CAR, Filebase, gateway verification |
| `Oracle.Harris.Mcp` | Stateless HTTP MCP (`/mcp`) with guarded SQL + roofing lead tools |
| `Oracle.Harris.Web` | Blazor demo UI + chat endpoint (`Microsoft.Extensions.AI`) |
| `Oracle.Harris.Cli` | `migrate`, `ingest`, `publish`, `verify-gateways` |

## Data flow

```
Sources (HCAD bulk, permit portals, BBB, Overture)
    → Ingest (artifacts/ + watermarks)
    → DuckDB (harris-tx.duckdb)
    → Publish (Parquet, JSON shards, manifest, CAR, IPFS)
    → MCP / Web (DuckDB local or IPFS Parquet)
```

Source catalog: [harris-sources.yaml](./harris-sources.yaml).

## DuckDB model

Core tables: `parcels`, `properties`, `geometries`, `ownerships`, `permits`, `bbb_profiles`, `business_locations`, `pipeline_runs`, `published_artifacts`, `ingest_watermarks`.

Views:

- `v_roof_age` — roof age proxies (`year_built`, `roof_year`, last roofing permit)
- `v_property_leads` — CRM join surface (coordinates, open permits, BBB, ownership tenure, out-of-area flag)

Parcel identity: **`request_identifier`** (folio/account) is the 1:1 key; never merge on digits-only normalized parcel id.

## IPFS publication

Each `pipeline_runs` row gets an `artifact_manifest.json` listing every object with:

- `cid` (CIDv1 base32)
- logical `name` / path
- `sizeBytes`, `codec` (`file` | `directory`)
- `sha256` digest
- optional `origins`

Directory roots also emit a `.car` file. Prior run CIDs remain in `published_artifacts` (immutable history). `Oracle.Harris.Publish` verifies fetches against `ipfs.io` and `dweb.link`.

## MCP tools

| Tool | Purpose |
|------|---------|
| `getPropertyQuerySchema` | Column metadata for `v_property_leads` / base tables |
| `queryProperties` | Read-only SQL (`SELECT`/`WITH` only, row cap) |
| `listPipelineRuns` | Run history for demo step 1 |
| `getRunManifest` | Manifest JSON for demo step 4 |
| `searchPropertiesByRadius` | Haversine radius (miles) |
| `searchPropertiesByRoofAge` | `roof_age_years >= threshold` |
| `searchLongOpenRoofingPermits` | Open roofing permits + contractor + BBB |
| `searchStableOwnership` | No sale in N years |
| `searchOutOfAreaOwners` | Mailing vs situs state mismatch |

## Configuration

Copy [.env.example](../.env.example) to `.env`. Local paths default under `data/harris-tx/` and `artifacts/harris-tx/`.

## CLI quick start

```bash
dotnet build Oracle.Harris.sln
dotnet run --project src/Oracle.Harris.Cli -- migrate
dotnet run --project src/Oracle.Harris.Cli -- ingest --job pilot-1
dotnet run --project src/Oracle.Harris.Cli -- publish --run-id <run_id>
dotnet run --project src/Oracle.Harris.Cli -- verify-gateways --run-id <run_id>
```

Run MCP (`http://localhost:5090/mcp`) and Web (`https://localhost:5001`) per project `launchSettings.json`.