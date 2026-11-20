using System.Data.Common;
using System.Text.Json;
using DuckDB.NET.Data;
using Oracle.Harris.Domain.Ipfs;
using Oracle.Harris.Domain.Pipeline;
using Oracle.Harris.Storage.Security;

namespace Oracle.Harris.Storage.Repositories;

public sealed class DuckDbQueryRepository(DuckDbConnectionFactory connectionFactory, SqlGuardrails guardrails)
{
    public IReadOnlyList<string> GetPropertyQuerySchema()
    {
        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT column_name || ' ' || data_type AS col
            FROM information_schema.columns
            WHERE table_name = 'v_property_leads'
            ORDER BY ordinal_position
            """;
        using var reader = cmd.ExecuteReader();
        var cols = new List<string>();
        while (reader.Read())
        {
            cols.Add(reader.GetString(0));
        }

        return cols;
    }

    public IReadOnlyList<Dictionary<string, object?>> QueryProperties(string sql, int limit = 100)
    {
        guardrails.EnsureReadOnlySelect(sql);
        var boundedSql = guardrails.ApplyRowLimit(sql, limit);

        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = boundedSql;
        using var reader = cmd.ExecuteReader();
        return ReadRows(reader);
    }

    public static IReadOnlyList<Dictionary<string, object?>> ReadRows(DuckDBCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        return ReadRows(reader);
    }

    public IReadOnlyList<PipelineRunRecord> ListPipelineRuns(int limit = 20)
    {
        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"""
            SELECT run_id, county_slug, started_at, completed_at, sources_json,
                   counts_json, deltas_json, source_limitations, status
            FROM pipeline_runs
            ORDER BY started_at DESC
            LIMIT {limit}
            """;
        using var reader = cmd.ExecuteReader();
        var runs = new List<PipelineRunRecord>();
        while (reader.Read())
        {
            runs.Add(new PipelineRunRecord
            {
                RunId = reader.GetString(0),
                CountySlug = reader.GetString(1),
                StartedAt = reader.GetFieldValue<DateTimeOffset>(2),
                CompletedAt = reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
                SourcesJson = reader.GetString(4),
                CountsJson = reader.GetString(5),
                DeltasJson = reader.GetString(6),
                SourceLimitations = reader.IsDBNull(7) ? null : reader.GetString(7),
                Status = reader.GetString(8),
            });
        }

        return runs;
    }

    /// <summary>Marks orphan <c>running</c> rows (e.g. interrupted CLI) as <c>interrupted</c>.</summary>
    public int MarkStaleRunningRuns()
    {
        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE pipeline_runs
            SET status = 'interrupted',
                completed_at = COALESCE(completed_at, started_at)
            WHERE lower(status) = 'running'
            """;
        return cmd.ExecuteNonQuery();
    }

    public PipelineRunManifest? GetRunManifest(string runId)
    {
        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT logical_name, cid, size_bytes, codec, sha256
            FROM published_artifacts
            WHERE run_id = $run
            ORDER BY logical_name
            """;
        cmd.Parameters.Add(new DuckDBParameter("run", runId));
        using var reader = cmd.ExecuteReader();
        var entries = new List<ArtifactManifestEntry>();
        while (reader.Read())
        {
            entries.Add(new ArtifactManifestEntry
            {
                Name = reader.GetString(0),
                Cid = reader.GetString(1),
                SizeBytes = reader.GetInt64(2),
                Codec = reader.GetString(3),
                Sha256 = reader.GetString(4),
            });
        }

        if (entries.Count == 0)
        {
            return null;
        }

        return new PipelineRunManifest
        {
            RunId = runId,
            CountySlug = Oracle.Harris.Domain.CountyConstants.Slug,
            PublishedAt = DateTimeOffset.UtcNow,
            Artifacts = entries,
        };
    }

    internal static List<Dictionary<string, object?>> ReadRows(DbDataReader reader)
    {
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    public string SerializeRows(IReadOnlyList<Dictionary<string, object?>> rows) =>
        JsonSerializer.Serialize(rows);

    public DatasetSummary GetDatasetSummary(int roofAgeThresholdYears = 15)
    {
        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        return new DatasetSummary(
            ScalarLong(connection, $"SELECT COUNT(*) FROM parcels WHERE jurisdiction_key = '{Oracle.Harris.Domain.CountyConstants.AppraiserSourceSystem}'"),
            ScalarLong(connection, $"SELECT COUNT(*) FROM properties WHERE source_system = '{Oracle.Harris.Domain.CountyConstants.AppraiserSourceSystem}'"),
            ScalarLong(connection, $"SELECT COUNT(*) FROM ownerships WHERE source_system = '{Oracle.Harris.Domain.CountyConstants.AppraiserSourceSystem}'"),
            ScalarLong(connection, "SELECT COUNT(*) FROM permits WHERE source_system = 'harris_tx_hcad_permits'"),
            ScalarLong(connection, $"SELECT COUNT(*) FROM v_property_leads WHERE roof_age_years >= {roofAgeThresholdYears}"));
    }

    private static long ScalarLong(DuckDBConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
    }
}