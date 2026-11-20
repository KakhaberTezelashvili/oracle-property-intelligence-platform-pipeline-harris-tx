using Microsoft.Extensions.DependencyInjection;
using Oracle.Harris.Ingest.Logging;
using Oracle.Harris.Ingest.Options;
using Oracle.Harris.Ingest.Pipeline;
using Oracle.Harris.Ingest.Reconciliation;
using Oracle.Harris.Ingest.Watermarks;

namespace Oracle.Harris.Ingest;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOracleHarrisIngest(this IServiceCollection services, Action<IngestPathsOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<IngestPathsOptions>();
        }

        services.AddSingleton<WatermarkStore>();
        services.AddSingleton<SourceLimitationLogger>();
        services.AddSingleton<EntityReconciler>();
        services.AddSingleton<IngestionOrchestrator>();
        return services;
    }
}