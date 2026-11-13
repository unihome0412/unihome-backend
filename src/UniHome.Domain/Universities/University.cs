using System.Globalization;
using System.Text;

namespace UniHome.Domain.Universities;

/// <summary>
/// Universidad del catálogo M03 (HU-075). El nombre y el campus se capturan en un
/// único campo; la forma normalizada sin acentos ni mayúsculas se usa para
/// detectar duplicados y alimentar la búsqueda. La desactivación pertenece a
/// HU-077 y no se expone todavía.
/// </summary>
public sealed class University
{
    public const int MaxNameAndCampusLength = 200;

    private University(
        Guid id,
        string nameAndCampus,
        string normalizedName,
        bool isActive,
        DateTimeOffset createdAt)
    {
        Id = id;
        NameAndCampus = nameAndCampus;
        NormalizedName = normalizedName;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string NameAndCampus { get; }

    public string NormalizedName { get; }

    public bool IsActive { get; }

    public DateTimeOffset CreatedAt { get; }

    public static University Create(string? nameAndCampus, DateTimeOffset now) =>
        Create(Guid.NewGuid(), nameAndCampus, isActive: true, now);

    public static University Restore(
        Guid id,
        string nameAndCampus,
        bool isActive,
        DateTimeOffset createdAt)
    {
        var normalized = Normalize(RequireValid(nameAndCampus));

        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la universidad es obligatorio.", nameof(id));
        }

        return new University(id, CollapseWhitespace(nameAndCampus!), normalized, isActive, createdAt);
    }

    /// <summary>
    /// Normaliza mayúsculas, acentos y espacios para comparar nombres de forma
    /// estable entre la creación y la búsqueda pública (HU-007/HU-075).
    /// </summary>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                normalized.Append(char.ToLowerInvariant(character));
            }
        }

        return CollapseWhitespace(normalized.ToString());
    }

    private static University Create(
        Guid id,
        string? nameAndCampus,
        bool isActive,
        DateTimeOffset now)
    {
        var valid = RequireValid(nameAndCampus);
        var normalized = Normalize(valid);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(
                "El nombre y campus no contiene caracteres válidos.",
                nameof(nameAndCampus));
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la universidad es obligatorio.", nameof(id));
        }

        return new University(id, CollapseWhitespace(valid), normalized, isActive, now);
    }

    private static string RequireValid(string? nameAndCampus)
    {
        if (string.IsNullOrWhiteSpace(nameAndCampus))
        {
            throw new ArgumentException(
                "El nombre y campus es obligatorio.",
                nameof(nameAndCampus));
        }

        var collapsed = CollapseWhitespace(nameAndCampus);

        if (collapsed.Length > MaxNameAndCampusLength)
        {
            throw new ArgumentException(
                $"El nombre y campus no debe superar {MaxNameAndCampusLength} caracteres.",
                nameof(nameAndCampus));
        }

        return collapsed;
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
