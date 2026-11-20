using System.Reflection;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;

namespace Oracle.Harris.Storage.Schema;

public sealed class DuckDbSchemaMigrator(DuckDbConnectionFactory connectionFactory, ILogger<DuckDbSchemaMigrator> logger)
{
    public void Migrate()
    {
        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();

        var version = GetCurrentVersion(connection);
        if (version >= 1)
        {
            logger.LogInformation("DuckDB schema already at version {Version}", version);
            return;
        }

        var sql = LoadEmbeddedSql("Oracle.Harris.Storage.Schema.Sql.001_initial.sql");
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        using (var stamp = connection.CreateCommand())
        {
            stamp.CommandText = "INSERT INTO schema_migrations (version) VALUES (1)";
            stamp.ExecuteNonQuery();
        }

        logger.LogInformation("Applied DuckDB schema migration 001");
    }

    private static int GetCurrentVersion(DuckDBConnection connection)
    {
        using var exists = connection.CreateCommand();
        exists.CommandText =
            """
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_name = 'schema_migrations'
            """;
        var count = Convert.ToInt32(exists.ExecuteScalar() ?? 0);
        if (count == 0)
        {
            return 0;
        }

        using var version = connection.CreateCommand();
        version.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations";
        return Convert.ToInt32(version.ExecuteScalar() ?? 0);
    }

    private static string LoadEmbeddedSql(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}