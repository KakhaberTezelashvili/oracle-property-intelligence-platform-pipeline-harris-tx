using Microsoft.Extensions.Logging;

namespace Oracle.Harris.Ingest.Reconciliation;

public sealed class EntityReconciler(ILogger<EntityReconciler> logger)
{
    public ReconciliationSummary ReconcileCounts(IReadOnlyDictionary<string, long> sourceCounts, IReadOnlyDictionary<string, long> dbCounts)
    {
        var merged = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in sourceCounts.Keys.Union(dbCounts.Keys))
        {
            sourceCounts.TryGetValue(key, out var source);
            dbCounts.TryGetValue(key, out var db);
            merged[key] = Math.Min(source, db);
            if (source != db)
            {
                logger.LogWarning("Reconcile mismatch for {Source}: source={SourceCount} db={DbCount}", key, source, db);
            }
        }

        return new ReconciliationSummary(merged);
    }
}

public sealed record ReconciliationSummary(IReadOnlyDictionary<string, long> CountsBySource);