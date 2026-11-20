namespace Oracle.Harris.Storage.Options;

public sealed class DuckDbOptions
{
    public const string SectionName = "DuckDb";

    public string DatabasePath { get; set; } = "data/harris-tx/harris-tx.duckdb";
}