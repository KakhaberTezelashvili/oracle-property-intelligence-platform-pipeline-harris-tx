using Microsoft.Extensions.DependencyInjection;
using Oracle.Harris.Publish.Export;
using Oracle.Harris.Publish.Filebase;
using Oracle.Harris.Publish.Options;
using Oracle.Harris.Publish.Verification;

namespace Oracle.Harris.Publish;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOracleHarrisPublish(this IServiceCollection services, Action<FilebaseOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<FilebaseOptions>();
        }

        services.AddHttpClient<DualGatewayVerifier>();
        services.AddSingleton<QueryTableParquetExporter>();
        services.AddSingleton<FilebasePublisher>();
        services.AddSingleton<PublishOrchestrator>();
        return services;
    }
}