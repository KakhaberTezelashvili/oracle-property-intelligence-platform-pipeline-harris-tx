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
        services.Configure<FilebaseOptions>(o =>
        {
            FilebaseEnvironment.Apply(o);
            configure?.Invoke(o);
        });

        services.AddHttpClient<DualGatewayVerifier>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Oracle.Harris/1.0 (ipfs-gateway-verify)");
        });
        services.AddSingleton<QueryTableParquetExporter>();
        services.AddSingleton<FilebasePublisher>();
        services.AddSingleton<PublishOrchestrator>();
        return services;
    }
}