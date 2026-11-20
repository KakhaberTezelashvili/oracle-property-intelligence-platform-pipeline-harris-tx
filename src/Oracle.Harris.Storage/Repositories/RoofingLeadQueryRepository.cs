using DuckDB.NET.Data;

namespace Oracle.Harris.Storage.Repositories;

public sealed class RoofingLeadQueryRepository(DuckDbConnectionFactory connectionFactory)
{
    public IReadOnlyList<Dictionary<string, object?>> SearchByRadius(
        double latitude,
        double longitude,
        double radiusMiles,
        int limit = 100)
    {
        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT *,
                   3958.8 * 2 * ASIN(SQRT(
                       POWER(SIN(RADIANS((latitude - $lat) / 2)), 2) +
                       COS(RADIANS($lat)) * COS(RADIANS(latitude)) *
                       POWER(SIN(RADIANS((longitude - $lon) / 2)), 2)
                   )) AS distance_miles
            FROM v_property_leads
            WHERE latitude IS NOT NULL AND longitude IS NOT NULL
              AND 3958.8 * 2 * ASIN(SQRT(
                       POWER(SIN(RADIANS((latitude - $lat) / 2)), 2) +
                       COS(RADIANS($lat)) * COS(RADIANS(latitude)) *
                       POWER(SIN(RADIANS((longitude - $lon) / 2)), 2)
                   )) <= $radius
            ORDER BY distance_miles
            LIMIT $limit
            """;
        cmd.Parameters.Add(new DuckDBParameter("lat", latitude));
        cmd.Parameters.Add(new DuckDBParameter("lon", longitude));
        cmd.Parameters.Add(new DuckDBParameter("radius", radiusMiles));
        cmd.Parameters.Add(new DuckDBParameter("limit", limit));
        return DuckDbQueryRepository.ReadRows(cmd);
    }

    public IReadOnlyList<Dictionary<string, object?>> SearchByRoofAge(int minimumYears, int limit = 100) =>
        Query($"SELECT * FROM v_property_leads WHERE roof_age_years >= {minimumYears} ORDER BY roof_age_years DESC LIMIT {limit}");

    public IReadOnlyList<Dictionary<string, object?>> SearchLongOpenRoofingPermits(int minimumDaysOpen, int limit = 100) =>
        Query(
            $"""
             SELECT *
             FROM v_property_leads
             WHERE days_open IS NOT NULL AND days_open >= {minimumDaysOpen}
             ORDER BY days_open DESC
             LIMIT {limit}
             """);

    public IReadOnlyList<Dictionary<string, object?>> SearchStableOwnership(int minimumYears, int limit = 100) =>
        Query(
            $"""
             SELECT *
             FROM v_property_leads
             WHERE years_since_sale IS NOT NULL AND years_since_sale >= {minimumYears}
             ORDER BY years_since_sale DESC
             LIMIT {limit}
             """);

    public IReadOnlyList<Dictionary<string, object?>> SearchOutOfAreaOwners(int limit = 100) =>
        Query($"SELECT * FROM v_property_leads WHERE is_out_of_area_owner = true LIMIT {limit}");

    private IReadOnlyList<Dictionary<string, object?>> Query(string sql)
    {
        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return DuckDbQueryRepository.ReadRows(cmd);
    }
}