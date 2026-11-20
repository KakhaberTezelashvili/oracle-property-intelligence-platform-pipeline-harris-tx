using System.Text.Json;
using Microsoft.Extensions.Options;
using Oracle.Harris.Ingest.Options;

namespace Oracle.Harris.Ingest.Watermarks;

public sealed class WatermarkStore(IOptions<IngestPathsOptions> paths)
{
    private string IndexPath =>
        Path.Combine(paths.Value.ArtifactsRoot, CountySlugDirectory(), "watermarks", "hash-index.json");

    private static string CountySlugDirectory() => Oracle.Harris.Domain.CountyConstants.Slug;

    public async Task<IReadOnlyDictionary<string, string>> LoadIndexAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(IndexPath))
        {
            return new Dictionary<string, string>();
        }

        await using var stream = File.OpenRead(IndexPath);
        var index = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken);
        return index ?? new Dictionary<string, string>();
    }

    public async Task SaveIndexAsync(IDictionary<string, string> index, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(IndexPath)!);
        await using var stream = File.Create(IndexPath);
        await JsonSerializer.SerializeAsync(stream, index, cancellationToken: cancellationToken);
    }

    public async Task<bool> IsChangedAsync(string artifactPath, string contentHash, CancellationToken cancellationToken = default)
    {
        var index = await LoadIndexAsync(cancellationToken);
        if (!index.TryGetValue(artifactPath, out var existing))
        {
            return true;
        }

        return !string.Equals(existing, contentHash, StringComparison.Ordinal);
    }

    public async Task MarkMergedAsync(string artifactPath, string contentHash, CancellationToken cancellationToken = default)
    {
        var index = new Dictionary<string, string>(await LoadIndexAsync(cancellationToken))
        {
            [artifactPath] = contentHash,
        };
        await SaveIndexAsync(index, cancellationToken);
    }
}