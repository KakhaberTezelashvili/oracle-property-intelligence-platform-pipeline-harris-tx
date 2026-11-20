using Microsoft.Extensions.DependencyInjection;
using Oracle.Harris.Storage.Bulk;
using Oracle.Harris.Storage.Options;
using Oracle.Harris.Storage.Repositories;
using Oracle.Harris.Storage.Schema;
using Oracle.Harris.Storage.Security;

namespace Oracle.Harris.Storage;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOracleHarrisStorage(this IServiceCollection services, Action<DuckDbOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<DuckDbOptions>();
        }

        services.AddSingleton<DuckDbConnectionFactory>();
        services.AddSingleton<DuckDbSchemaMigrator>();
        services.AddSingleton<DuckDbBulkLoader>();
        services.AddSingleton<SqlGuardrails>();
        services.AddSingleton<DuckDbQueryRepository>();
        services.AddSingleton<RoofingLeadQueryRepository>();
        return services;
    }
}