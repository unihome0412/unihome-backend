namespace UniHome.Domain.OccupancyLinks;

/// <summary>
/// Código de invitación de una unidad (HU-048/HU-049). Un nuevo código se genera
/// solo cuando no existe uno vigente para la unidad; los anteriores no se borran,
/// quedan vencidos. La vigencia de 7 días es un umbral provisional sin aprobación
/// de producto, igual que otros plazos de Sprints anteriores (ver README).
/// </summary>
public sealed class UnitInvitation
{
    public const int ValidityDays = 7;
    public const int CodeLength = 8;

    private UnitInvitation(
        Guid unitId,
        string code,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        UnitId = unitId;
        Code = code;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid UnitId { get; }
    public string Code { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    public static UnitInvitation Create(Guid unitId, string code, DateTimeOffset now)
    {
        if (unitId == Guid.Empty)
        {
            throw new ArgumentException("La unidad es obligatoria.", nameof(unitId));
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length != CodeLength)
        {
            throw new ArgumentException(
                $"El código debe tener {CodeLength} caracteres.",
                nameof(code));
        }

        return new UnitInvitation(unitId, code, now, now.AddDays(ValidityDays));
    }

    public static UnitInvitation Restore(
        Guid unitId,
        string code,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) =>
        new(unitId, code, createdAt, expiresAt);

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
