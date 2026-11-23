using UniHome.Domain.OccupancyLinks;

namespace UniHome.Application.OccupancyLinks.JoinWithCode;

public enum JoinUnitWithCodeStatus
{
    Linked,
    ValidationFailed,
    InvalidCode,
    ExpiredCode,
    SelfJoinForbidden,
    UnitFull
}

public sealed record OccupancyLinkView(
    Guid UnidadId,
    OccupancyStatus Estado,
    DateTimeOffset FechaVinculo);

public sealed class JoinUnitWithCodeResult
{
    private JoinUnitWithCodeResult(
        JoinUnitWithCodeStatus status,
        OccupancyLinkView? value,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        Status = status;
        Value = value;
        ValidationErrors = validationErrors ?? EmptyErrors;
    }

    private static IReadOnlyDictionary<string, string[]> EmptyErrors { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    public JoinUnitWithCodeStatus Status { get; }

    public OccupancyLinkView? Value { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public static JoinUnitWithCodeResult Linked(OccupancyLinkView value) =>
        new(JoinUnitWithCodeStatus.Linked, value);

    public static JoinUnitWithCodeResult ValidationFailed(string field, string message) =>
        new(
            JoinUnitWithCodeStatus.ValidationFailed,
            null,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });

    public static JoinUnitWithCodeResult InvalidCode() => new(JoinUnitWithCodeStatus.InvalidCode, null);

    public static JoinUnitWithCodeResult ExpiredCode() => new(JoinUnitWithCodeStatus.ExpiredCode, null);

    public static JoinUnitWithCodeResult SelfJoinForbidden() =>
        new(JoinUnitWithCodeStatus.SelfJoinForbidden, null);

    public static JoinUnitWithCodeResult UnitFull() => new(JoinUnitWithCodeStatus.UnitFull, null);
}
