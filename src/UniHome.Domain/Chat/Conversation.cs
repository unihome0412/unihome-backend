using System.Security.Cryptography;
using System.Text;

namespace UniHome.Domain.Chat;

/// <summary>
/// Conversación 1:1 entre el propietario de una unidad y quien la contactó desde
/// su ficha (M08, HU-067). El identificador se deriva de forma determinística de
/// la unidad y el par de cuentas, así que reabrir una conversación existente
/// siempre resuelve al mismo documento en lugar de crear uno nuevo, incluso bajo
/// solicitudes simultáneas (un upsert por <c>_id</c> en Mongo nunca duplica).
/// </summary>
public sealed class Conversation
{
    private Conversation(
        Guid id,
        Guid unitId,
        Guid ownerAccountId,
        Guid requesterAccountId,
        DateTimeOffset createdAt,
        DateTimeOffset? lastMessageAt,
        string? lastMessagePreview)
    {
        Id = id;
        UnitId = unitId;
        OwnerAccountId = ownerAccountId;
        RequesterAccountId = requesterAccountId;
        CreatedAt = createdAt;
        LastMessageAt = lastMessageAt;
        LastMessagePreview = lastMessagePreview;
    }

    public Guid Id { get; }
    public Guid UnitId { get; }
    public Guid OwnerAccountId { get; }
    public Guid RequesterAccountId { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? LastMessageAt { get; private set; }
    public string? LastMessagePreview { get; private set; }

    /// <summary>
    /// Deriva el mismo identificador para una unidad, su propietario y quien la
    /// contacta. Propietario y solicitante son roles distintos (nunca se
    /// intercambian), así que no hace falta ordenar el par: no es un GUID
    /// aleatorio, es una huella SHA-256 de "unidad|propietario|solicitante"
    /// truncada a 16 bytes, igual en espíritu al <c>_id</c> determinista
    /// <c>"inquilino:unidad"</c> de <c>OccupancyLinkDocument</c>.
    /// </summary>
    public static Guid ComputeId(Guid unitId, Guid ownerAccountId, Guid requesterAccountId)
    {
        var canonical = $"{unitId:N}|{ownerAccountId:N}|{requesterAccountId:N}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new Guid(hash[..16]);
    }

    public static Conversation Start(
        Guid unitId,
        Guid ownerAccountId,
        Guid requesterAccountId,
        DateTimeOffset now)
    {
        if (unitId == Guid.Empty)
        {
            throw new ArgumentException("La unidad es obligatoria.", nameof(unitId));
        }

        if (ownerAccountId == Guid.Empty)
        {
            throw new ArgumentException("El propietario es obligatorio.", nameof(ownerAccountId));
        }

        if (requesterAccountId == Guid.Empty)
        {
            throw new ArgumentException("El solicitante es obligatorio.", nameof(requesterAccountId));
        }

        if (ownerAccountId == requesterAccountId)
        {
            throw new InvalidOperationException("No se puede iniciar una conversación con uno mismo.");
        }

        var id = ComputeId(unitId, ownerAccountId, requesterAccountId);
        return new Conversation(id, unitId, ownerAccountId, requesterAccountId, now, null, null);
    }

    public static Conversation Restore(
        Guid id,
        Guid unitId,
        Guid ownerAccountId,
        Guid requesterAccountId,
        DateTimeOffset createdAt,
        DateTimeOffset? lastMessageAt,
        string? lastMessagePreview) =>
        new(id, unitId, ownerAccountId, requesterAccountId, createdAt, lastMessageAt, lastMessagePreview);

    public bool HasParticipant(Guid accountId) =>
        accountId == OwnerAccountId || accountId == RequesterAccountId;

    public Guid OtherParticipant(Guid accountId)
    {
        if (accountId == OwnerAccountId)
        {
            return RequesterAccountId;
        }

        if (accountId == RequesterAccountId)
        {
            return OwnerAccountId;
        }

        throw new InvalidOperationException("La cuenta no es parte de esta conversación.");
    }

    /// <summary>Actualiza el resumen que ve HU-069 al enviarse un nuevo mensaje.</summary>
    public void RegisterMessage(string preview, DateTimeOffset at)
    {
        LastMessageAt = at;
        LastMessagePreview = preview;
    }
}
