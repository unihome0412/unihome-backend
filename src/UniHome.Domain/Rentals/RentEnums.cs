namespace UniHome.Domain.Rentals;

/// <summary>EP-M10 (HU-051): conceptos de cobro que el titular marca como fijos o variables.</summary>
public enum RentConceptName
{
    Renta,
    Mantenimiento,
    Luz,
    Gas
}

/// <summary>EP-M10 (HU-053/HU-054): método de división de la renta entre inquilinos.</summary>
public enum RentSplitMethod
{
    Igual,
    Personalizada
}

/// <summary>EP-M10 (HU-056): estado del pago de un inquilino en un periodo, frente a su cuota.</summary>
public enum RentPaymentState
{
    Pendiente,
    Parcial,
    Registrado
}

/// <summary>Un concepto de cobro con su marca de fijo/variable (HU-051).</summary>
public sealed record RentConcept(RentConceptName Nombre, bool EsFijo);

/// <summary>La cuota de un inquilino dentro de un periodo (HU-053/HU-054).</summary>
public sealed record TenantShare(Guid TenantAccountId, decimal AmountMxn);
