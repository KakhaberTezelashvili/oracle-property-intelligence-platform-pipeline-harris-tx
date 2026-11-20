namespace Oracle.Harris.Storage.Repositories;

public sealed record DatasetSummary(
    long Parcels,
    long Properties,
    long Ownerships,
    long RoofingPermits,
    long PropertiesRoofAge15Plus);