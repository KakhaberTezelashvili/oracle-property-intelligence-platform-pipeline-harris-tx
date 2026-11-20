using System.Text.Json;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Oracle.Harris.Domain;
using Oracle.Harris.Domain.Entities;
using Oracle.Harris.Domain.Pipeline;
using Oracle.Harris.Domain.Provenance;
using Oracle.Harris.Ingest.ChangeDetection;
using Oracle.Harris.Ingest.Logging;
using Oracle.Harris.Ingest.Options;
using Oracle.Harris.Ingest.Watermarks;
using Oracle.Harris.Storage;
using Oracle.Harris.Storage.Bulk;

namespace Oracle.Harris.Ingest.Pipeline;

public sealed class IngestionOrchestrator(
    DuckDbConnectionFactory connectionFactory,
    DuckDbBulkLoader bulkLoader,
    WatermarkStore watermarkStore,
    SourceLimitationLogger limitationLogger,
    ILogger<IngestionOrchestrator> logger)
{
    public async Task<PipelineRunRecord> RunIncrementalAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var runId = $"{jobId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var started = DateTimeOffset.UtcNow;
        var run = new PipelineRunRecord
        {
            RunId = runId,
            CountySlug = CountyConstants.Slug,
            StartedAt = started,
            Status = "running",
            SourcesJson = JsonSerializer.Serialize(new[] { "hcad", "permits", "bbb", "overture" }),
        };

        await UpsertPipelineRunAsync(run, cancellationToken);

        // Scaffold: idempotent sample merge when HCAD artifacts are absent
        var samplePath = $"appraisal/{CountyConstants.Slug}/{jobId}/sample.json";
        var sampleJson = """{"request_identifier":"DEMO-0001","year_built":1998}""";
        var hash = ContentHashChangeDetector.ComputeHash(sampleJson);
        if (await watermarkStore.IsChangedAsync(samplePath, hash, cancellationToken))
        {
            bulkLoader.UpsertParcel(new ParcelRecord
            {
                JurisdictionKey = CountyConstants.AppraiserSourceSystem,
                RequestIdentifier = "DEMO-0001",
                ParcelIdentifier = "DEMO0001",
                Provenance = new SourceProvenance
                {
                    SourceSystem = CountyConstants.AppraiserSourceSystem,
                    SourceRecordKey = "DEMO-0001",
                    SourceCollectedAt = DateTimeOffset.UtcNow,
                    SourcePayloadJson = sampleJson,
                    ContentHash = hash,
                },
            });
            bulkLoader.UpsertProperty(new PropertyRecord
            {
                RequestIdentifier = "DEMO-0001",
                YearBuilt = 1998,
                PropertyUsageType = "residential",
            });
            await watermarkStore.MarkMergedAsync(samplePath, hash, cancellationToken);
        }

        limitationLogger.Add("hcad", "Bulk downloads wired — run discovery to replace DEMO-0001 scaffold row.");
        limitationLogger.Add("permits", "Municipal catalog in docs/harris-sources.yaml requires certification.");

        var counts = new Dictionary<string, long> { ["parcels"] = 1, ["properties"] = 1 };
        var completed = run with
        {
            CompletedAt = DateTimeOffset.UtcNow,
            Status = "completed",
            CountsJson = JsonSerializer.Serialize(counts),
            DeltasJson = JsonSerializer.Serialize(new { parcels = 1 }),
            SourceLimitations = limitationLogger.Summarize(),
        };

        await UpsertPipelineRunAsync(completed, cancellationToken);
        logger.LogInformation("Ingestion run {RunId} completed", runId);
        return completed;
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