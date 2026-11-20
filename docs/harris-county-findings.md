# Harris County (harris-tx) — county findings

## Appraisal — HCAD PDATA

- **Source:** Harris County Appraisal District public PDATA text exports (local path `tempdata/`).
- **Format:** Tab-delimited `.txt` with header row.
- **Primary key:** `acct` (stored trimmed as `request_identifier`).
- **Core files in use:**
  - `real_acct.txt` — situs, mailing, acreage, `yr_impr`, `new_own_dt`, state class
  - `building_res.txt` — `date_erected`, `yr_remodel` (roof age proxies)
  - `ownership_history.txt` — `purchase_date` fallback for tenure
  - `permits.txt` — HCAD permit cross-refs (roofing-filtered on ingest)
- **Access mode:** Bulk file load via DuckDB `read_csv` (no scraping).

## Known limitations (assessment)

| Gap | Impact | Mitigation |
|-----|--------|------------|
| No lat/lon in PDATA bundle | Radius / map queries empty | Add HCAD GIS shapefile or geocode later |
| `permits.txt` is HCAD, not municipal | Missing contractor, some open permits | Houston Accela adapter (see `harris-sources.yaml`) |
| BBB / Overture | No contractor ratings / POIs yet | Future enrichment runs |
| ~1.6M accounts | Long full ingest | Default pilot `25,000`; CLI `--full` for county |

## Ingest CLI

```bash
dotnet run --project src/Oracle.Harris.Cli -- ingest --job hcad-pilot          # 25k accounts
dotnet run --project src/Oracle.Harris.Cli -- ingest --job hcad-full --full  # entire roll
dotnet run --project src/Oracle.Harris.Cli -- ingest --job hcad-pilot --force  # ignore hash watermark
```

Watermark: SHA-256 of `real_acct.txt` stored under `artifacts/harris-tx/watermarks/`.