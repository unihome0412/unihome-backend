namespace UniHome.Infrastructure.Persistence.Mongo.Rentals;

/// <summary>Nombres físicos M10. Un cambio exige una migración nueva.</summary>
public static class MongoRentalsSchema
{
    public const string RentPeriodsCollection = "rentas_periodo";
    public const string PaymentsCollection = "pagos_informativos";

    public const string RentPeriodUnitField = "unitId";
    public const string RentPeriodPeriodoField = "periodo";
    public const string RentPeriodTotalField = "rentaTotalMxn";
    public const string RentPeriodCutoffDayField = "diaCorte";
    public const string RentPeriodConceptsField = "conceptos";
    public const string RentPeriodSplitMethodField = "metodoDivision";
    public const string RentPeriodSharesField = "cuotas";
    public const string RentPeriodCreatedAtField = "createdAt";
    public const string RentPeriodUpdatedAtField = "updatedAt";

    public const string ConceptNameField = "nombre";
    public const string ConceptFixedField = "esFijo";

    public const string ShareTenantField = "inquilinoAccountId";
    public const string ShareAmountField = "montoMxn";

    public const string PaymentRentPeriodField = "rentaPeriodoId";
    public const string PaymentTenantField = "inquilinoAccountId";
    public const string PaymentAmountField = "montoMxn";
    public const string PaymentReceivedOnField = "fechaRecepcion";
    public const string PaymentRegisteredAtField = "registradoEnUtc";

    public const string RentPeriodUniqueIndex = "ux_rentas_periodo";
    public const string PaymentPeriodTenantIndex = "ix_pagos_periodo_inquilino";
}
