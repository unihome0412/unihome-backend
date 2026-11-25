using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Rentals;

/// <summary>
/// Migración M10-001: renta y pagos informativos del Sprint 7. Los pagos son
/// solo una bitácora (HU-090); UniHome no procesa dinero.
/// </summary>
public static class MongoRentalsMigration
{
    public const string Version = "M10-001";
    public const string Name = "Rentas y pagos M10";
    public const string Description =
        "Crea rentas_periodo con índice único (unidad, periodo) y " +
        "pagos_informativos con índice de periodo/inquilino.";

    public static MongoMigrationDefinition Definition { get; } = new(
        Version,
        Name,
        Description,
        MongoRentalsIndexes.Definitions);
}
