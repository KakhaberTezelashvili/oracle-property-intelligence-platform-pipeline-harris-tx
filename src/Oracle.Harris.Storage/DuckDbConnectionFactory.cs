using System.Data.Common;
using DuckDB.NET.Data;
using Microsoft.Extensions.Options;
using Oracle.Harris.Storage.Options;

namespace Oracle.Harris.Storage;

public sealed class DuckDbConnectionFactory(IOptions<DuckDbOptions> options)
{
    public DuckDbOptions OptionsValue { get; } = options.Value;

    public DbConnection CreateOpenConnection()
    {
        var path = OptionsValue.DatabasePath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new DuckDBConnection($"Data Source={path}");
        connection.Open();
        return connection;
    }
}