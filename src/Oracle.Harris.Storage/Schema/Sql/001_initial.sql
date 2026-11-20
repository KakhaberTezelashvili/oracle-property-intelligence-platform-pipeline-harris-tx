CREATE TABLE IF NOT EXISTS schema_migrations (
    version INTEGER PRIMARY KEY,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

CREATE TABLE IF NOT EXISTS parcels (
    jurisdiction_key VARCHAR NOT NULL,
    request_identifier VARCHAR NOT NULL,
    parcel_identifier VARCHAR,
    source_system VARCHAR NOT NULL,
    source_record_key VARCHAR NOT NULL,
    source_artifact_uri VARCHAR,
    source_collected_at TIMESTAMPTZ,
    source_payload_json JSON,
    content_hash VARCHAR,
    ingested_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
    PRIMARY KEY (jurisdiction_key, request_identifier)
);

CREATE TABLE IF NOT EXISTS properties (
    request_identifier VARCHAR PRIMARY KEY,
    property_usage_type VARCHAR,
    year_built INTEGER,
    roof_year INTEGER,
    roof_covering_material VARCHAR,
    exterior_wall_material VARCHAR,
    lot_area_sqft DOUBLE,
    lot_size_acre DOUBLE,
    source_system VARCHAR NOT NULL,
    source_payload_json JSON,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

CREATE TABLE IF NOT EXISTS geometries (
    request_identifier VARCHAR PRIMARY KEY,
    latitude DOUBLE,
    longitude DOUBLE,
    source_system VARCHAR NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

CREATE TABLE IF NOT EXISTS ownerships (
    request_identifier VARCHAR PRIMARY KEY,
    owner_name VARCHAR,
    mailing_city VARCHAR,
    mailing_state VARCHAR,
    mailing_postal_code VARCHAR,
    situs_state VARCHAR DEFAULT 'TX',
    last_sale_date DATE,
    source_system VARCHAR NOT NULL,
    source_payload_json JSON,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

CREATE TABLE IF NOT EXISTS permits (
    permit_number VARCHAR NOT NULL,
    source_system VARCHAR NOT NULL,
    request_identifier VARCHAR,
    permit_type VARCHAR,
    status VARCHAR,
    issued_at DATE,
    closed_at DATE,
    days_open INTEGER,
    is_open BOOLEAN,
    is_roofing_related BOOLEAN NOT NULL DEFAULT false,
    contractor_name VARCHAR,
    source_payload_json JSON,
    ingested_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
    PRIMARY KEY (permit_number, source_system)
);

CREATE TABLE IF NOT EXISTS bbb_profiles (
    profile_key VARCHAR PRIMARY KEY,
    business_name VARCHAR NOT NULL,
    rating VARCHAR,
    category VARCHAR,
    city VARCHAR,
    state VARCHAR,
    source_payload_json JSON,
    ingested_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

CREATE TABLE IF NOT EXISTS business_locations (
    gers_id VARCHAR PRIMARY KEY,
    taxonomy_primary VARCHAR,
    taxonomy_hierarchy VARCHAR,
    basic_category VARCHAR,
    latitude DOUBLE,
    longitude DOUBLE,
    overture_release VARCHAR NOT NULL,
    source_payload_json JSON,
    ingested_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

CREATE TABLE IF NOT EXISTS pipeline_runs (
    run_id VARCHAR PRIMARY KEY,
    county_slug VARCHAR NOT NULL,
    started_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ,
    sources_json JSON NOT NULL,
    counts_json JSON NOT NULL,
    deltas_json JSON NOT NULL,
    source_limitations VARCHAR,
    status VARCHAR NOT NULL
);

CREATE TABLE IF NOT EXISTS published_artifacts (
    id VARCHAR PRIMARY KEY,
    run_id VARCHAR NOT NULL REFERENCES pipeline_runs(run_id),
    logical_name VARCHAR NOT NULL,
    cid VARCHAR NOT NULL,
    size_bytes BIGINT NOT NULL,
    codec VARCHAR NOT NULL,
    sha256 VARCHAR NOT NULL,
    published_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

CREATE TABLE IF NOT EXISTS ingest_watermarks (
    source_name VARCHAR NOT NULL,
    artifact_path VARCHAR NOT NULL,
    content_hash VARCHAR NOT NULL,
    merged_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
    PRIMARY KEY (source_name, artifact_path)
);

CREATE OR REPLACE VIEW v_roof_age AS
SELECT
    p.request_identifier,
    p.year_built,
    p.roof_year,
    GREATEST(
        CASE WHEN p.roof_year IS NOT NULL THEN year(current_date) - p.roof_year ELSE NULL END,
        CASE WHEN p.year_built IS NOT NULL THEN year(current_date) - p.year_built ELSE NULL END,
        (
            SELECT year(current_date) - year(max(pm.issued_at))
            FROM permits pm
            WHERE pm.request_identifier = p.request_identifier
              AND pm.is_roofing_related
              AND pm.issued_at IS NOT NULL
        )
    )::INTEGER AS roof_age_years
FROM properties p;

CREATE OR REPLACE VIEW v_property_leads AS
WITH open_roof AS (
    SELECT
        request_identifier,
        max(days_open) FILTER (WHERE is_open AND is_roofing_related) AS max_permit_days_open,
        arg_max(contractor_name, days_open) FILTER (WHERE is_open AND is_roofing_related) AS contractor_name
    FROM permits
    GROUP BY request_identifier
)
SELECT
    p.request_identifier,
    ra.roof_age_years,
    g.latitude,
    g.longitude,
    o.owner_name,
    o.mailing_state,
    o.situs_state,
    o.last_sale_date,
    CASE
        WHEN o.last_sale_date IS NULL THEN NULL
        ELSE (year(current_date) - year(o.last_sale_date))::INTEGER
    END AS years_since_sale,
    CASE
        WHEN o.mailing_state IS NOT NULL AND o.situs_state IS NOT NULL
             AND upper(o.mailing_state) <> upper(o.situs_state) THEN true
        ELSE false
    END AS is_out_of_area_owner,
    orf.max_permit_days_open AS days_open,
    orf.contractor_name,
  (
        SELECT max(b.rating)
        FROM bbb_profiles b
        WHERE orf.contractor_name IS NOT NULL
          AND lower(b.business_name) LIKE '%' || lower(orf.contractor_name) || '%'
    ) AS bbb_rating
FROM properties p
LEFT JOIN v_roof_age ra ON ra.request_identifier = p.request_identifier
LEFT JOIN geometries g ON g.request_identifier = p.request_identifier
LEFT JOIN ownerships o ON o.request_identifier = p.request_identifier
LEFT JOIN open_roof orf ON orf.request_identifier = p.request_identifier;