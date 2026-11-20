using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Oracle.Harris.Domain;
using Oracle.Harris.Storage.Repositories;

namespace Oracle.Harris.Mcp.Tools;

[McpServerToolType]
public sealed class HarrisMcpTools(
    DuckDbQueryRepository queryRepository,
    RoofingLeadQueryRepository roofingRepository)
{
    [McpServerTool(Name = "getPropertyQuerySchema"), Description("Returns DuckDB column types for v_property_leads.")]
    public string GetPropertyQuerySchema() =>
        string.Join('\n', queryRepository.GetPropertyQuerySchema());

    [McpServerTool(Name = "queryProperties"), Description("Execute a read-only SQL SELECT/WITH against Harris DuckDB.")]
    public string QueryProperties(
        [Description("Read-only SQL")] string sql,
        [Description("Maximum rows (<=500)")] int limit = 100) =>
        queryRepository.SerializeRows(queryRepository.QueryProperties(sql, limit));

    [McpServerTool(Name = "listPipelineRuns"), Description("List recent pipeline runs for harris-tx.")]
    public string ListPipelineRuns(int limit = 20) =>
        JsonSerializer.Serialize(queryRepository.ListPipelineRuns(limit));

    [McpServerTool(Name = "getRunManifest"), Description("Return the artifact manifest for a pipeline run.")]
    public string GetRunManifest(string runId)
    {
        var manifest = queryRepository.GetRunManifest(runId);
        return manifest is null ? "{}" : JsonSerializer.Serialize(manifest);
    }

    [McpServerTool(Name = "searchPropertiesByRadius"), Description("Properties within radius miles of a lat/lon.")]
    public string SearchPropertiesByRadius(double latitude, double longitude, double radiusMiles, int limit = 100) =>
        queryRepository.SerializeRows(roofingRepository.SearchByRadius(latitude, longitude, radiusMiles, limit));

    [McpServerTool(Name = "searchPropertiesByRoofAge"), Description("Properties with roof_age_years >= threshold.")]
    public string SearchPropertiesByRoofAge(
        int minimumYears = CountyConstants.DefaultRoofAgeThresholdYears,
        int limit = 100) =>
        queryRepository.SerializeRows(roofingRepository.SearchByRoofAge(minimumYears, limit));

    [McpServerTool(Name = "searchLongOpenRoofingPermits"), Description("Long-open roofing permits with contractor and BBB.")]
    public string SearchLongOpenRoofingPermits(int minimumDaysOpen = 365, int limit = 100) =>
        queryRepository.SerializeRows(roofingRepository.SearchLongOpenRoofingPermits(minimumDaysOpen, limit));

    [McpServerTool(Name = "searchStableOwnership"), Description("Properties without ownership change for N years.")]
    public string SearchStableOwnership(
        int minimumYears = CountyConstants.DefaultOwnershipStabilityYears,
        int limit = 100) =>
        queryRepository.SerializeRows(roofingRepository.SearchStableOwnership(minimumYears, limit));

    [McpServerTool(Name = "searchOutOfAreaOwners"), Description("Properties with out-of-area owners.")]
    public string SearchOutOfAreaOwners(int limit = 100) =>
        queryRepository.SerializeRows(roofingRepository.SearchOutOfAreaOwners(limit));
}