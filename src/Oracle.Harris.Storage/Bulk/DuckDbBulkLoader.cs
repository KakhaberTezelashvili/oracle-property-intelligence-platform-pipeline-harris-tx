using DuckDB.NET.Data;
using Oracle.Harris.Domain;
using Oracle.Harris.Domain.Entities;

namespace Oracle.Harris.Storage.Bulk;

public sealed class DuckDbBulkLoader(DuckDbConnectionFactory connectionFactory)
{
    public void UpsertParcel(ParcelRecord parcel)
    {
        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO parcels (
                jurisdiction_key, request_identifier, parcel_identifier, source_system,
                source_record_key, source_artifact_uri, source_collected_at, source_payload_json, content_hash
            ) VALUES ($jk, $rid, $pid, $ss, $srk, $uri, $col, $payload, $hash)
            ON CONFLICT (jurisdiction_key, request_identifier) DO UPDATE SET
                parcel_identifier = excluded.parcel_identifier,
                source_payload_json = excluded.source_payload_json,
                content_hash = excluded.content_hash,
                ingested_at = now()
            """;
        AddParameters(cmd, parcel);
        cmd.ExecuteNonQuery();
    }

    private static void AddParameters(DuckDBCommand cmd, ParcelRecord parcel)
    {
        cmd.Parameters.Add(new DuckDBParameter("jk", parcel.JurisdictionKey));
        cmd.Parameters.Add(new DuckDBParameter("rid", parcel.RequestIdentifier));
        cmd.Parameters.Add(new DuckDBParameter("pid", parcel.ParcelIdentifier ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("ss", parcel.Provenance.SourceSystem));
        cmd.Parameters.Add(new DuckDBParameter("srk", parcel.Provenance.SourceRecordKey));
        cmd.Parameters.Add(new DuckDBParameter("uri", parcel.Provenance.SourceArtifactUri ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("col", parcel.Provenance.SourceCollectedAt?.UtcDateTime ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("payload", parcel.Provenance.SourcePayloadJson ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("hash", parcel.Provenance.ContentHash ?? (object)DBNull.Value));
    }

    public void UpsertProperty(PropertyRecord property, string sourceSystem = CountyConstants.AppraiserSourceSystem)
    {
        using var connection = (DuckDBConnection)connectionFactory.CreateOpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO properties (
                request_identifier, property_usage_type, year_built, roof_year,
                roof_covering_material, exterior_wall_material, lot_area_sqft, lot_size_acre, source_system
            ) VALUES ($rid, $usage, $yb, $ry, $roof, $wall, $sqft, $acre, $ss)
            ON CONFLICT (request_identifier) DO UPDATE SET
                property_usage_type = excluded.property_usage_type,
                year_built = excluded.year_built,
                roof_year = excluded.roof_year,
                roof_covering_material = excluded.roof_covering_material,
                exterior_wall_material = excluded.exterior_wall_material,
                lot_area_sqft = excluded.lot_area_sqft,
                lot_size_acre = excluded.lot_size_acre,
                updated_at = now()
            """;
        cmd.Parameters.Add(new DuckDBParameter("rid", property.RequestIdentifier));
        cmd.Parameters.Add(new DuckDBParameter("usage", property.PropertyUsageType ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("yb", property.YearBuilt ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("ry", property.RoofYear ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("roof", property.RoofCoveringMaterial ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("wall", property.ExteriorWallMaterial ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("sqft", property.LotAreaSqft ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("acre", property.LotSizeAcre ?? (object)DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("ss", sourceSystem));
        cmd.ExecuteNonQuery();
    }
}