using System.Text.Json;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Oracle.Harris.Domain;
using Oracle.Harris.Domain.Pipeline;
using Oracle.Harris.Ingest.Hcad;
using Oracle.Harris.Ingest.Logging;
using Oracle.Harris.Storage;

namespace Oracle.Harris.Ingest.Pipeline;

public sealed class IngestionOrchestrator(
    DuckDbConnectionFactory connectionFactory,
    HcadPdataIngestor hcadIngestor,
    SourceLimitationLogger limitationLogger,
    ILogger<IngestionOrchestrator> logger)
{
    public const int DefaultPilotLimit = 25_000;

    public Task<PipelineRunRecord> RunIncrementalAsync(
        string jobId,
        int? rowLimit = DefaultPilotLimit,
        bool fullCounty = false,
        bool forceReload = false,
        CancellationToken cancellationToken = default)
    {
        if (fullCounty)
        {
            rowLimit = null;
        }

        return RunInternalAsync(jobId, rowLimit, forceReload, cancellationToken);
    }

    private async Task<PipelineRunRecord> RunInternalAsync(
        string jobId,
        int? rowLimit,
        bool forceReload,
        CancellationToken cancellationToken)
    {
        var runId = $"{jobId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var started = DateTimeOffset.UtcNow;
        limitationLogger.Add("coordinates", "HCAD PDATA has no lat/lon; radius search requires GIS (not in current tempdata bundle).");
        limitationLogger.Add("permits", "Loaded HCAD permit references; municipal portals (Houston Accela) needed for contractor + long-open roofing permits.");
        limitationLogger.Add("bbb", "Not loaded in this run — enrichment phase.");
        limitationLogger.Add("overture", "Not loaded in this run — enrichment phase.");

        var run = new PipelineRunRecord
        {
            RunId = runId,
            CountySlug = CountyConstants.Slug,
            StartedAt = started,
            Status = "running",
            SourcesJson = JsonSerializer.Serialize(new[]
            {
                "hcad_pdata:real_acct",
                "hcad_pdata:building_res",
                "hcad_pdata:ownership_history",
                "hcad_pdata:permits_roofing",
            }),
        };

        await UpsertPipelineRunAsync(run, cancellationToken);

        RowCounts? previous = null;
        try
        {
            previous = await ReadPreviousCountsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "No prior counts for delta calculation");
        }

        var hcad = await hcadIngestor.LoadAsync(jobId, rowLimit, forceReload, cancellationToken);

        var counts = new Dictionary<string, long>
        {
            ["parcels"] = hcad.Counts.Parcels,
            ["properties"] = hcad.Counts.Properties,
            ["ownerships"] = hcad.Counts.Ownerships,
            ["roofing_permits"] = hcad.Counts.Permits,
        };

        var deltas = new Dictionary<string, object>
        {
            ["reloaded"] = hcad.Reloaded,
            ["content_hash"] = hcad.ContentHash,
            ["row_limit"] = hcad.RowLimit ?? -1,
        };

        if (hcad.SkipReason is not null)
        {
            deltas["skipped"] = hcad.SkipReason;
        }

        if (previous is not null)
        {
            deltas["parcels_delta"] = hcad.Counts.Parcels - previous.Parcels;
            deltas["properties_delta"] = hcad.Counts.Properties - previous.Properties;
            deltas["roofing_permits_delta"] = hcad.Counts.Permits - previous.Permits;
        }

        var completed = run with
        {
            CompletedAt = DateTimeOffset.UtcNow,
            Status = "completed",
            CountsJson = JsonSerializer.Serialize(counts),
            DeltasJson = JsonSerializer.Serialize(deltas),
            SourceLimitations = limitationLogger.Summarize(),
        };

        await UpsertPipelineRunAsync(completed, cancellationToken);
        logger.LogInformation("Ingestion run {RunId} completed", runId);
        return completed;
    }

    private async Task<RowCounts?> ReadPreviousCountsAsync(CancellationToken cancellationToken)
    {
        await using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT counts_json FROM pipeline_runs
            WHERE county_slug = $county AND status = 'completed'
            ORDER BY started_at DESC LIMIT 1
            """;
        cmd.Parameters.Add(new DuckDBParameter("county", CountyConstants.Slug));
        var json = await cmd.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var doc = JsonDocument.Parse(json);
        return new RowCounts(
            doc.RootElement.GetProperty("parcels").GetInt64(),
            doc.RootElement.GetProperty("properties").GetInt64(),
            doc.RootElement.TryGetProperty("ownerships", out var o) ? o.GetInt64() : 0,
            doc.RootElement.TryGetProperty("roofing_permits", out var p) ? p.GetInt64() : 0);
    }

    private async Task UpsertPipelineRunAsync(PipelineRunRecord run, CancellationToken cancellationToken)
    {
        await using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO pipeline_runs (
                run_id, county_slug, started_at, completed_at, sources_json, counts_json, deltas_json, source_limitations, status
            ) VALUES ($id, $county, $started, $completed, $sources, $counts, $deltas, $limits, $status)
            ON CONFLICT (run_id) DO UPDATE SET
                completed_at = excluded.completed_at,
                counts_json = excluded.counts_json,
                deltas_json = excluded.deltas_json,
                source_limitations = excluded.source_limitations,
                status = excluded.status
            """;
        cmd.Parameters.Add(new DuckDBParameter("id", run.RunId));
        cmd.Parameters.Add(new DuckDBParameter("county", run.CountySlug));
        cmd.Parameters.Add(new DuckDBParameter("started", run.StartedAt.UtcDateTime));
        cmd.Parameters.Add(new DuckDBParameter("completed", run.CompletedAt?.UtcDateTime ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("sources", run.SourcesJson));
        cmd.Parameters.Add(new DuckDBParameter("counts", run.CountsJson));
        cmd.Parameters.Add(new DuckDBParameter("deltas", run.DeltasJson));
        cmd.Parameters.Add(new DuckDBParameter("limits", run.SourceLimitations ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("status", run.Status));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}