using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oracle.Harris.Ingest;
using Oracle.Harris.Ingest.Options;
using Oracle.Harris.Ingest.Pipeline;
using Oracle.Harris.Publish;
using Oracle.Harris.Publish.Options;
using Oracle.Harris.Storage;
using Oracle.Harris.Storage.Schema;

if (args.Length == 0)
{
    PrintHelp();
    return 1;
}

var duckPath = HarrisPaths.ResolveFromRepositoryRoot(
    Environment.GetEnvironmentVariable("DUCKDB_PATH") ?? "data/harris-tx/harris-tx.duckdb");
var tempDataRoot = HarrisPaths.ResolveFromRepositoryRoot(
    Environment.GetEnvironmentVariable("TEMPDATA_ROOT") ?? "tempdata");
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
services.AddOracleHarrisStorage(o => o.DatabasePath = duckPath);
services.AddOracleHarrisIngest(o =>
{
    o.DataRoot = HarrisPaths.ResolveFromRepositoryRoot(Environment.GetEnvironmentVariable("DATA_DIR") ?? "data");
    o.ArtifactsRoot = HarrisPaths.ResolveFromRepositoryRoot(Environment.GetEnvironmentVariable("ARTIFACTS_DIR") ?? "artifacts");
    o.TempDataRoot = tempDataRoot;
});
services.AddOracleHarrisPublish(o =>
{
    o.Endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT") ?? o.Endpoint;
    o.Bucket = Environment.GetEnvironmentVariable("FILEBASE_QUERY_TABLE_BUCKET_HARRIS_TX") ?? o.Bucket;
    o.AccessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY_ID") ?? "";
    o.SecretKey = Environment.GetEnvironmentVariable("S3_SECRET_ACCESS_KEY") ?? "";
    o.Gateway1 = Environment.GetEnvironmentVariable("IPFS_GATEWAY_1") ?? o.Gateway1;
    o.Gateway2 = Environment.GetEnvironmentVariable("IPFS_GATEWAY_2") ?? o.Gateway2;
});

await using var provider = services.BuildServiceProvider();

var command = args[0].ToLowerInvariant();
switch (command)
{
    case "migrate":
        provider.GetRequiredService<DuckDbSchemaMigrator>().Migrate();
        Console.WriteLine("DuckDB schema migrated.");
        return 0;
    case "ingest":
    {
        var job = GetArg(args, "--job") ?? "hcad-pilot";
        var full = HasFlag(args, "--full");
        var force = HasFlag(args, "--force");
        var limit = ParseLimit(args, full);
        var run = await provider.GetRequiredService<IngestionOrchestrator>().RunIncrementalAsync(
            job,
            rowLimit: limit,
            fullCounty: full,
            forceReload: force);
        Console.WriteLine($"Ingest completed: {run.RunId}");
        Console.WriteLine($"Counts: {run.CountsJson}");
        return 0;
    }
    case "publish":
    {
        var runId = GetArg(args, "--run-id");
        if (runId is null)
        {
            Console.Error.WriteLine("--run-id is required");
            return 1;
        }

        var manifest = await provider.GetRequiredService<PublishOrchestrator>().PublishRunAsync(runId);
        Console.WriteLine($"Published {manifest.Artifacts.Count} artifacts for {runId}");
        return 0;
    }
    case "verify-gateways":
    {
        var cid = GetArg(args, "--cid");
        var size = long.Parse(GetArg(args, "--size") ?? "0");
        var sha = GetArg(args, "--sha256");
        if (cid is null || sha is null)
        {
            Console.Error.WriteLine("--cid and --sha256 are required");
            return 1;
        }

        var results = await provider.GetRequiredService<PublishOrchestrator>().VerifyPublishedAsync(cid, size, sha);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(results));
        return results.All(r => r.Success) ? 0 : 2;
    }
    default:
        PrintHelp();
        return 1;
}

static int? ParseLimit(string[] args, bool full)
{
    if (full)
    {
        return null;
    }

    var text = GetArg(args, "--limit");
    if (text is not null && int.TryParse(text, out var parsed) && parsed > 0)
    {
        return parsed;
    }

    return IngestionOrchestrator.DefaultPilotLimit;
}

static bool HasFlag(string[] args, string flag) =>
    args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

static string? GetArg(string[] args, string name)
{
    var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static void PrintHelp() =>
    Console.WriteLine(
        """
        Oracle.Harris.Cli commands:
          migrate
          ingest --job <jobId> [--limit <n>] [--full] [--force]
          publish --run-id <runId>
          verify-gateways --cid <cid> --size <bytes> --sha256 <hex>

        Ingest defaults: pilot 25,000 accounts from tempdata/real_acct.txt.
        Use --full for entire county (~1.6M accounts). Use --force to ignore file hash watermark.
        """);