using UniHome.Domain.Common;

namespace UniHome.Domain.Reparaciones;

/// <summary>
/// EP-M11 (HU-062/063/064/065): reporte de reparación de una unidad. Lo puede
/// levantar el propietario o cualquier inquilino con vínculo activo; el
/// seguimiento del estado (HU-065) es responsabilidad exclusiva del
/// propietario, igual que el resto de la operación de la unidad (M09).
/// </summary>
public sealed class Reparacion
{
    public const int MaxTitleLength = 150;
    public const int MaxDescriptionLength = 2000;

    private Reparacion(
        Guid id,
        Guid unitId,
        Guid reportedByAccountId,
        string title,
        string description,
        ReparacionEstado status,
        ReparacionFoto? photo,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        UnitId = unitId;
        ReportedByAccountId = reportedByAccountId;
        Title = title;
        Description = description;
        Status = status;
        Photo = photo;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public Guid UnitId { get; }
    public Guid ReportedByAccountId { get; }
    public string Title { get; }
    public string Description { get; }
    public ReparacionEstado Status { get; private set; }
    public ReparacionFoto? Photo { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Reparacion Create(
        Guid unitId,
        Guid reportedByAccountId,
        string? title,
        string? description,
        ReparacionFoto? photo,
        DateTimeOffset now)
    {
        if (unitId == Guid.Empty)
        {
            throw new ArgumentException("La unidad es obligatoria.", nameof(unitId));
        }

        if (reportedByAccountId == Guid.Empty)
        {
            throw new ArgumentException("La cuenta que reporta es obligatoria.", nameof(reportedByAccountId));
        }

        var validTitle = RequireText(title, MaxTitleLength, "título", nameof(title));
        var validDescription = RequireText(description, MaxDescriptionLength, "descripción", nameof(description));

        return new Reparacion(
            Guid.NewGuid(),
            unitId,
            reportedByAccountId,
            validTitle,
            validDescription,
            ReparacionEstado.Pendiente,
            photo,
            now,
            now);
    }

    public static Reparacion Restore(
        Guid id,
        Guid unitId,
        Guid reportedByAccountId,
        string title,
        string description,
        ReparacionEstado status,
        ReparacionFoto? photo,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador del reporte es obligatorio.", nameof(id));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentException("El estado del reporte no es válido.", nameof(status));
        }

        return new Reparacion(
            id, unitId, reportedByAccountId, title, description, status, photo, createdAt, updatedAt);
    }

    /// <summary>
    /// HU-065: el propietario mueve el reporte entre Pendiente, En proceso y
    /// Atendido, en cualquier dirección (puede reabrir uno marcado por error).
    /// Devuelve false si el estado pedido ya era el actual (sin cambios).
    /// </summary>
    public bool ChangeStatus(ReparacionEstado status, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentException("El estado del reporte no es válido.", nameof(status));
        }

        if (Status == status)
        {
            return false;
        }

        Status = status;
        UpdatedAt = now;
        return true;
    }

    private static string RequireText(string? value, int maxLength, string label, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El {label} es obligatorio.", paramName);
        }

        var collapsed = TextNormalization.CollapseWhitespace(value);

        if (collapsed.Length > maxLength)
        {
            throw new ArgumentException(
                $"El {label} no debe superar {maxLength} caracteres.",
                paramName);
        }

        return collapsed;
    }
}
