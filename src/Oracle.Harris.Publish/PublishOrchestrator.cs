using System.Text.Json;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.Harris.Domain;
using Oracle.Harris.Domain.Ipfs;
using Oracle.Harris.Ingest.Options;
using Oracle.Harris.Publish.Export;
using Oracle.Harris.Publish.Filebase;
using Oracle.Harris.Publish.Ipfs;
using Oracle.Harris.Publish.Options;
using Oracle.Harris.Publish.Verification;
using Oracle.Harris.Storage;

namespace Oracle.Harris.Publish;

public sealed class PublishOrchestrator(
    DuckDbConnectionFactory connectionFactory,
    QueryTableParquetExporter exporter,
    FilebasePublisher filebasePublisher,
    DualGatewayVerifier gatewayVerifier,
    IOptions<IngestPathsOptions> ingestPaths,
    ILogger<PublishOrchestrator> logger)
{
    public async Task<PipelineRunManifest> PublishRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        var publishDir = Path.Combine(ingestPaths.Value.ArtifactsRoot, CountyConstants.Slug, "publish", runId);
        Directory.CreateDirectory(publishDir);

        var parquetPath = Path.Combine(publishDir, "query-table.parquet");
        await exporter.ExportAsync(parquetPath, cancellationToken);
        await exporter.ExportOpenDataJsonAsync(Path.Combine(publishDir, "open-data"), cancellationToken);

        var parquetBytes = await File.ReadAllBytesAsync(parquetPath, cancellationToken);
        var parquetSha = CidV1Encoder.Sha256Hex(parquetBytes);
        var rawParquetCid = CidV1Encoder.EncodeFile(parquetBytes);

        var carBytes = CarArchiveWriter.WriteSingleBlockArchive(
            parquetBytes,
            CarArchiveWriter.ExtractCidPayloadWithoutMultibase(rawParquetCid));
        var carPath = Path.Combine(publishDir, "query-table.car");
        await File.WriteAllBytesAsync(carPath, carBytes, cancellationToken);
        var carSha = CidV1Encoder.Sha256Hex(carBytes);

        FilebaseUploadResult? parquetUpload = null;
        FilebaseUploadResult? carUpload = null;

        await using (var stream = File.OpenRead(parquetPath))
        {
            parquetUpload = await filebasePublisher.UploadFileAsync("query-tables/harris-tx/query-table.parquet", stream, cancellationToken);
        }

        await using (var carStream = File.OpenRead(carPath))
        {
            carUpload = await filebasePublisher.UploadFileAsync("query-tables/harris-tx/query-table.car", carStream, cancellationToken);
        }

        var rawCarCid = CidV1Encoder.EncodeFile(carBytes);
        var parquetCid = ChooseNetworkCid(parquetUpload, rawParquetCid);
        var carCid = ChooseNetworkCid(carUpload, rawCarCid);

        var manifest = new PipelineRunManifest
        {
            RunId = runId,
            CountySlug = CountyConstants.Slug,
            PublishedAt = DateTimeOffset.UtcNow,
            Artifacts =
            [
                new ArtifactManifestEntry
                {
                    Cid = parquetCid,
                    Name = "query-tables/harris-tx/query-table.parquet",
                    SizeBytes = parquetBytes.LongLength,
                    Codec = "file",
                    Sha256 = parquetSha,
                },
                new ArtifactManifestEntry
                {
                    Cid = carCid,
                    Name = "query-tables/harris-tx/query-table.car",
                    SizeBytes = carBytes.LongLength,
                    Codec = "file",
                    Sha256 = carSha,
                },
            ],
        };

        var manifestPath = Path.Combine(publishDir, "artifact-manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

        await RecordPublishedArtifactsAsync(runId, manifest, cancellationToken);
        logger.LogInformation(
            "Published run {RunId} parquet network CID {Cid} (raw codec fallback {Raw})",
            runId,
            parquetCid,
            rawParquetCid);
        return manifest;
    }

    private static string ChooseNetworkCid(FilebaseUploadResult? upload, string rawCodecCid) =>
        !string.IsNullOrWhiteSpace(upload?.IpfsCid) ? upload!.IpfsCid! : rawCodecCid;

    public Task<IReadOnlyList<GatewayVerificationResult>> VerifyPublishedAsync(
        string cid,
        long sizeBytes,
        string sha256,
        CancellationToken cancellationToken = default) =>
        gatewayVerifier.VerifyAsync(cid, sizeBytes, sha256, cancellationToken);

    private async Task RecordPublishedArtifactsAsync(string runId, PipelineRunManifest manifest, CancellationToken cancellationToken)
    {
        await using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        foreach (var entry in manifest.Artifacts)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO published_artifacts (id, run_id, logical_name, cid, size_bytes, codec, sha256)
                VALUES ($id, $run, $name, $cid, $size, $codec, $sha)
                ON CONFLICT (id) DO UPDATE SET cid = excluded.cid
                """;
            cmd.Parameters.Add(new DuckDBParameter("id", $"{runId}:{entry.Name}"));
            cmd.Parameters.Add(new DuckDBParameter("run", runId));
            cmd.Parameters.Add(new DuckDBParameter("name", entry.Name));
            cmd.Parameters.Add(new DuckDBParameter("cid", entry.Cid));
            cmd.Parameters.Add(new DuckDBParameter("size", entry.SizeBytes));
            cmd.Parameters.Add(new DuckDBParameter("codec", entry.Codec));
            cmd.Parameters.Add(new DuckDBParameter("sha", entry.Sha256));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}