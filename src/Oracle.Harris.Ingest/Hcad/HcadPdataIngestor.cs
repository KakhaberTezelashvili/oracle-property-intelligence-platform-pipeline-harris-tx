using System.Security.Cryptography;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.Harris.Domain;
using Oracle.Harris.Ingest.Options;
using Oracle.Harris.Ingest.Watermarks;
using Oracle.Harris.Storage;

namespace Oracle.Harris.Ingest.Hcad;

public sealed class HcadPdataIngestor(
    DuckDbConnectionFactory connectionFactory,
    IOptions<IngestPathsOptions> paths,
    WatermarkStore watermarkStore,
    ILogger<HcadPdataIngestor> logger)
{
    private const string AcctWatermarkKey = "hcad:real_acct.txt";
    private const string PermitsSource = "harris_tx_hcad_permits";

    public async Task<HcadIngestResult> LoadAsync(
        string jobId,
        int? rowLimit,
        bool forceReload,
        CancellationToken cancellationToken = default)
    {
        var root = ResolveTempDataRoot();
        var acctPath = Path.Combine(root, "real_acct.txt");
        if (!File.Exists(acctPath))
        {
            throw new FileNotFoundException(
                $"HCAD real_acct.txt not found under '{root}'. Place PDATA under tempdata/ or set TEMPDATA_ROOT.");
        }

        var fileHash = await ComputeFileHashAsync(acctPath, cancellationToken);
        if (!forceReload && !await watermarkStore.IsChangedAsync(AcctWatermarkKey, fileHash, cancellationToken))
        {
            logger.LogInformation("HCAD PDATA unchanged (hash {Hash}); skipping bulk reload", fileHash);
            var existing = await CountRowsAsync(cancellationToken);
            return new HcadIngestResult(false, fileHash, rowLimit, existing, existing, "hcad_pdata_unchanged");
        }

        var buildingPath = Path.Combine(root, "building_res.txt");
        var permitsPath = Path.Combine(root, "permits.txt");
        var ownershipHistPath = Path.Combine(root, "ownership_history.txt");

        var limitClause = rowLimit is > 0 ? $"LIMIT {rowLimit.Value}" : string.Empty;

        await using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        await ClearAppraisalSourceAsync(connection, cancellationToken);

        var acctCsv = ToDuckDbPath(acctPath);
        logger.LogInformation("Loading HCAD accounts from {Path} (limit: {Limit})", acctPath, rowLimit?.ToString() ?? "none");

        await ExecuteAsync(connection, $"""
            INSERT INTO parcels (
                jurisdiction_key, request_identifier, parcel_identifier, source_system,
                source_record_key, source_collected_at, content_hash
            )
            SELECT
                '{CountyConstants.AppraiserSourceSystem}',
                TRIM(acct),
                TRIM(acct),
                '{CountyConstants.AppraiserSourceSystem}',
                TRIM(acct),
                now(),
                '{fileHash}'
            FROM (
                SELECT * FROM read_csv('{acctCsv}', delim='\t', header=true, ignore_errors=true, all_varchar=true)
                {limitClause}
            )
            ON CONFLICT (jurisdiction_key, request_identifier) DO UPDATE SET
                content_hash = excluded.content_hash,
                ingested_at = now()
            """, cancellationToken);

        await ExecuteAsync(connection, $"""
            INSERT INTO properties (
                request_identifier, property_usage_type, year_built, lot_area_sqft, lot_size_acre, source_system
            )
            SELECT
                TRIM(acct),
                NULLIF(TRIM(state_class), ''),
                TRY_CAST(NULLIF(TRIM(yr_impr), '') AS INTEGER),
                TRY_CAST(NULLIF(TRIM(land_ar), '') AS DOUBLE),
                TRY_CAST(NULLIF(TRIM(acreage), '') AS DOUBLE),
                '{CountyConstants.AppraiserSourceSystem}'
            FROM (
                SELECT * FROM read_csv('{acctCsv}', delim='\t', header=true, ignore_errors=true, all_varchar=true)
                {limitClause}
            )
            ON CONFLICT (request_identifier) DO UPDATE SET
                property_usage_type = excluded.property_usage_type,
                year_built = excluded.year_built,
                lot_area_sqft = excluded.lot_area_sqft,
                lot_size_acre = excluded.lot_size_acre,
                updated_at = now()
            """, cancellationToken);

        await ExecuteAsync(connection, $"""
            INSERT INTO ownerships (
                request_identifier, owner_name, mailing_city, mailing_state, mailing_postal_code,
                situs_state, last_sale_date, source_system
            )
            SELECT
                TRIM(acct),
                NULLIF(TRIM(mailto), ''),
                NULLIF(TRIM(mail_city), ''),
                NULLIF(TRIM(mail_state), ''),
                NULLIF(TRIM(mail_zip), ''),
                'TX',
                TRY_CAST(
                    COALESCE(
                        TRY_STRPTIME(NULLIF(TRIM(new_own_dt), ''), '%m/%d/%Y'),
                        TRY_STRPTIME(NULLIF(TRIM(new_own_dt), ''), '%Y-%m-%d')
                    ) AS DATE
                ),
                '{CountyConstants.AppraiserSourceSystem}'
            FROM (
                SELECT * FROM read_csv('{acctCsv}', delim='\t', header=true, ignore_errors=true, all_varchar=true)
                {limitClause}
            )
            ON CONFLICT (request_identifier) DO UPDATE SET
                owner_name = excluded.owner_name,
                mailing_state = excluded.mailing_state,
                last_sale_date = excluded.last_sale_date,
                updated_at = now()
            """, cancellationToken);

        if (File.Exists(ownershipHistPath))
        {
            var histCsv = ToDuckDbPath(ownershipHistPath);
            await ExecuteAsync(connection, $"""
                UPDATE ownerships o
                SET last_sale_date = COALESCE(
                    o.last_sale_date,
                    h.purchase_date_parsed
                )
                FROM (
                    SELECT
                        TRIM(acct) AS acct,
                        TRY_CAST(TRY_STRPTIME(NULLIF(TRIM(purchase_date), ''), '%m/%d/%Y') AS DATE) AS purchase_date_parsed
                    FROM read_csv('{histCsv}', delim='\t', header=true, ignore_errors=true, all_varchar=true)
                ) h
                WHERE o.request_identifier = h.acct
                """, cancellationToken);
        }

        if (File.Exists(buildingPath))
        {
            var bldCsv = ToDuckDbPath(buildingPath);
            await ExecuteAsync(connection, $"""
                UPDATE properties p
                SET
                    year_built = COALESCE(p.year_built, b.erected_year),
                    roof_year = COALESCE(p.roof_year, b.remodel_year, b.erected_year)
                FROM (
                    SELECT
                        TRIM(acct) AS acct,
                        MAX(TRY_CAST(NULLIF(TRIM(date_erected), '') AS INTEGER)) AS erected_year,
                        MAX(TRY_CAST(NULLIF(TRIM(yr_remodel), '') AS INTEGER)) FILTER (
                            WHERE TRY_CAST(NULLIF(TRIM(yr_remodel), '') AS INTEGER) > 0
                        ) AS remodel_year
                    FROM read_csv('{bldCsv}', delim='\t', header=true, ignore_errors=true, all_varchar=true)
                    WHERE TRIM(acct) IN (SELECT request_identifier FROM parcels WHERE jurisdiction_key = '{CountyConstants.AppraiserSourceSystem}')
                    GROUP BY TRIM(acct)
                ) b
                WHERE p.request_identifier = b.acct
                """, cancellationToken);
        }

        if (File.Exists(permitsPath))
        {
            var permitCsv = ToDuckDbPath(permitsPath);
            await ExecuteAsync(connection, $"""
                INSERT INTO permits (
                    permit_number, source_system, request_identifier, permit_type, status,
                    issued_at, days_open, is_open, is_roofing_related, source_payload_json
                )
                SELECT
                    TRIM(acct) || '-' || CAST(id AS VARCHAR) || '-' || TRIM(agency_id),
                    '{PermitsSource}',
                    TRIM(acct),
                    NULLIF(TRIM(permit_tp_descr), ''),
                    NULLIF(TRIM(status), ''),
                    TRY_CAST(TRY_STRPTIME(NULLIF(TRIM(issue_date), ''), '%m/%d/%Y') AS DATE),
                    CASE
                        WHEN TRIM(status) IN ('C', 'V') THEN NULL
                        ELSE DATE_DIFF('day',
                            TRY_CAST(TRY_STRPTIME(NULLIF(TRIM(issue_date), ''), '%m/%d/%Y') AS DATE),
                            current_date)
                    END,
                    TRIM(status) NOT IN ('C', 'V'),
                    true,
                    CAST(NULL AS JSON)
                FROM read_csv('{permitCsv}', delim='\t', header=true, ignore_errors=true, all_varchar=true)
                WHERE TRIM(acct) IN (SELECT request_identifier FROM parcels WHERE jurisdiction_key = '{CountyConstants.AppraiserSourceSystem}')
                  AND (
                    LOWER(COALESCE(permit_tp_descr, '')) LIKE '%roof%'
                    OR LOWER(COALESCE(dscr, '')) LIKE '%roof%'
                    OR LOWER(COALESCE(dscr, '')) LIKE '%reroof%'
                    OR LOWER(COALESCE(dscr, '')) LIKE '%shingle%'
                  )
                ON CONFLICT (permit_number, source_system) DO UPDATE SET
                    status = excluded.status,
                    days_open = excluded.days_open,
                    is_open = excluded.is_open,
                    source_payload_json = excluded.source_payload_json,
                    ingested_at = now()
                """, cancellationToken);
        }

        await watermarkStore.MarkMergedAsync(AcctWatermarkKey, fileHash, cancellationToken);

        var counts = await CountRowsAsync(cancellationToken);
        logger.LogInformation(
            "HCAD ingest finished: parcels={Parcels} properties={Properties} permits={Permits}",
            counts.Parcels,
            counts.Properties,
            counts.Permits);

        return new HcadIngestResult(true, fileHash, rowLimit, counts, counts, null);
    }

    private string ResolveTempDataRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable("TEMPDATA_ROOT");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        var configured = paths.Value.TempDataRoot;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        return Path.GetFullPath("tempdata");
    }

    private static string ToDuckDbPath(string path) =>
        Path.GetFullPath(path).Replace('\\', '/').Replace("'", "''");

    private static async Task ExecuteAsync(DuckDBConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ClearAppraisalSourceAsync(DuckDBConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, $"DELETE FROM permits WHERE source_system = '{PermitsSource}'", cancellationToken);
        await ExecuteAsync(connection, $"DELETE FROM ownerships WHERE source_system = '{CountyConstants.AppraiserSourceSystem}'", cancellationToken);
        await ExecuteAsync(connection, $"DELETE FROM geometries WHERE source_system = '{CountyConstants.AppraiserSourceSystem}'", cancellationToken);
        await ExecuteAsync(connection, $"DELETE FROM properties WHERE source_system = '{CountyConstants.AppraiserSourceSystem}'", cancellationToken);
        await ExecuteAsync(connection, $"DELETE FROM parcels WHERE jurisdiction_key = '{CountyConstants.AppraiserSourceSystem}'", cancellationToken);
        await ExecuteAsync(connection, "DELETE FROM parcels WHERE request_identifier = 'DEMO-0001'", cancellationToken);
    }

    private async Task<RowCounts> CountRowsAsync(CancellationToken cancellationToken)
    {
        await using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        return new RowCounts(
            await ScalarCountAsync(connection, $"SELECT COUNT(*) FROM parcels WHERE jurisdiction_key = '{CountyConstants.AppraiserSourceSystem}'", cancellationToken),
            await ScalarCountAsync(connection, $"SELECT COUNT(*) FROM properties WHERE source_system = '{CountyConstants.AppraiserSourceSystem}'", cancellationToken),
            await ScalarCountAsync(connection, $"SELECT COUNT(*) FROM ownerships WHERE source_system = '{CountyConstants.AppraiserSourceSystem}'", cancellationToken),
            await ScalarCountAsync(connection, $"SELECT COUNT(*) FROM permits WHERE source_system = '{PermitsSource}'", cancellationToken));
    }

    private static async Task<long> ScalarCountAsync(DuckDBConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result ?? 0);
    }

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var buffer = new byte[81920];
        using var sha = SHA256.Create();
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}

public sealed record RowCounts(long Parcels, long Properties, long Ownerships, long Permits);

public sealed record HcadIngestResult(
    bool Reloaded,
    string ContentHash,
    int? RowLimit,
    RowCounts Counts,
    RowCounts CurrentCounts,
    string? SkipReason);