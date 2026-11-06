using System.Net.Mail;

namespace UniHome.Domain.Accounts;

/// <summary>
/// Correo electrónico normalizado. La normalización (recorte y minúsculas) es la
/// misma que se usa para el índice único y para las búsquedas en persistencia.
/// </summary>
public sealed class EmailAddress : IEquatable<EmailAddress>
{
    public const int MaxLength = 254;

    private EmailAddress(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static EmailAddress Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El correo electrónico es obligatorio.", nameof(value));
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength ||
            normalized.Any(char.IsWhiteSpace) ||
            normalized.Any(char.IsControl))
        {
            throw new ArgumentException("El correo electrónico no tiene un formato válido.", nameof(value));
        }

        if (!MailAddress.TryCreate(normalized, out var parsed) || parsed is null)
        {
            throw new ArgumentException("El correo electrónico no tiene un formato válido.", nameof(value));
        }

        // Rechaza entradas con nombre visible ("Nombre <correo>") y cualquier
        // normalización que cambie la dirección indicada por la persona.
        if (!string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("El correo electrónico no tiene un formato válido.", nameof(value));
        }

        return new EmailAddress(normalized);
    }

    public bool Equals(EmailAddress? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as EmailAddress);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
