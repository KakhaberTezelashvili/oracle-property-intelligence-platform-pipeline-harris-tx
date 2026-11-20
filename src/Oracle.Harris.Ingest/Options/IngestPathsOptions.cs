namespace Oracle.Harris.Ingest.Options;

public sealed class IngestPathsOptions
{
    public const string SectionName = "Ingest";

    public string DataRoot { get; set; } = "data";

    public string ArtifactsRoot { get; set; } = "artifacts";

    public string TempDataRoot { get; set; } = "tempdata";
}