namespace UniHome.Domain.OccupancyLinks;

/// <summary>
/// Vínculo entre un inquilino y una unidad (M09). Un mismo par cuenta/unidad
/// conserva un único documento a lo largo del tiempo: salir y volver a unirse con
/// código reactiva el mismo vínculo en lugar de crear uno nuevo. No hay ventanas
/// de cobro ni renta asociadas; HU-048/049/093/094/095 solo gestionan la
/// vinculación y su efecto en la disponibilidad de la unidad.
/// </summary>
public sealed class OccupancyLink
{
    private OccupancyLink(
        Guid tenantAccountId,
        Guid unitId,
        OccupancyStatus status,
        DateTimeOffset joinedAt,
        DateTimeOffset? endedAt,
        OccupancyEndReason? endedReason)
    {
        TenantAccountId = tenantAccountId;
        UnitId = unitId;
        Status = status;
        JoinedAt = joinedAt;
        EndedAt = endedAt;
        EndedReason = endedReason;
    }

    public Guid TenantAccountId { get; }
    public Guid UnitId { get; }
    public OccupancyStatus Status { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public OccupancyEndReason? EndedReason { get; private set; }

    public static OccupancyLink CreateActive(
        Guid tenantAccountId,
        Guid unitId,
        DateTimeOffset now)
    {
        if (tenantAccountId == Guid.Empty)
        {
            throw new ArgumentException("El inquilino es obligatorio.", nameof(tenantAccountId));
        }

        if (unitId == Guid.Empty)
        {
            throw new ArgumentException("La unidad es obligatoria.", nameof(unitId));
        }

        return new OccupancyLink(tenantAccountId, unitId, OccupancyStatus.Activo, now, null, null);
    }

    public static OccupancyLink Restore(
        Guid tenantAccountId,
        Guid unitId,
        OccupancyStatus status,
        DateTimeOffset joinedAt,
        DateTimeOffset? endedAt,
        OccupancyEndReason? endedReason) =>
        new(tenantAccountId, unitId, status, joinedAt, endedAt, endedReason);

    /// <summary>Reactiva un vínculo finalizado cuando el inquilino vuelve a unirse con código.</summary>
    public void Reactivate(DateTimeOffset now)
    {
        if (Status == OccupancyStatus.Activo)
        {
            throw new InvalidOperationException("El vínculo ya está activo.");
        }

        Status = OccupancyStatus.Activo;
        JoinedAt = now;
        EndedAt = null;
        EndedReason = null;
    }

    public void End(OccupancyEndReason reason, DateTimeOffset now)
    {
        if (Status != OccupancyStatus.Activo)
        {
            throw new InvalidOperationException("El vínculo ya no está activo.");
        }

        Status = OccupancyStatus.Finalizado;
        EndedAt = now;
        EndedReason = reason;
    }
}
