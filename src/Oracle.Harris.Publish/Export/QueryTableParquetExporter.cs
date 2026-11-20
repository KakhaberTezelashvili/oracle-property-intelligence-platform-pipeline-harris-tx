using DuckDB.NET.Data;
using Oracle.Harris.Storage;

namespace Oracle.Harris.Publish.Export;

public sealed class QueryTableParquetExporter(DuckDbConnectionFactory connectionFactory)
{
    public async Task ExportAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        await using var cmd = connection.CreateCommand();
        var posixPath = outputPath.Replace('\\', '/');
        cmd.CommandText =
            $"""
            COPY (SELECT * FROM v_property_leads)
            TO '{posixPath}' (FORMAT PARQUET)
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ExportOpenDataJsonAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        await using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM v_property_leads LIMIT 1000";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var index = new List<object>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(reader.GetOrdinal("request_identifier"));
            var json = $"{{\"request_identifier\":\"{id}\"}}";
            var path = Path.Combine(outputDirectory, $"{id}.json");
            await File.WriteAllTextAsync(path, json, cancellationToken);
            index.Add(new { id, path = $"{id}.json" });
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "index.json"),
            System.Text.Json.JsonSerializer.Serialize(new { properties = index }),
            cancellationToken);
    }
}