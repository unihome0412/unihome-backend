using System.Globalization;
using System.Text.RegularExpressions;

namespace UniHome.Domain.Rentals;

/// <summary>
/// EP-M10 (HU-050/051/052/053/054): configuración de renta de una unidad para
/// un periodo mensual ("yyyy-MM"). La clave de negocio es (unidad, periodo);
/// <c>ux_rentas_periodo</c> la protege a nivel de índice.
/// </summary>
public sealed partial class RentPeriod
{
    public const int MinCutoffDay = 1;
    public const int MaxCutoffDay = 31;

    private readonly List<RentConcept> _concepts;
    private readonly List<TenantShare> _shares;

    private RentPeriod(
        Guid id,
        Guid unitId,
        string periodo,
        decimal totalMxn,
        int cutoffDay,
        IReadOnlyList<RentConcept> concepts,
        RentSplitMethod? splitMethod,
        IReadOnlyList<TenantShare> shares,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        UnitId = unitId;
        Periodo = periodo;
        TotalMxn = totalMxn;
        CutoffDay = cutoffDay;
        _concepts = [.. concepts];
        SplitMethod = splitMethod;
        _shares = [.. shares];
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public Guid UnitId { get; }

    /// <summary>Formato "yyyy-MM"; valida con <see cref="IsValidPeriodo"/>.</summary>
    public string Periodo { get; }

    public decimal TotalMxn { get; private set; }

    /// <summary>Día del mes (1-31); la fecha real se ajusta al mes con <see cref="ComputeCutoffDate"/>.</summary>
    public int CutoffDay { get; private set; }

    public IReadOnlyList<RentConcept> Concepts => _concepts.AsReadOnly();

    /// <summary>Null mientras la división esté pendiente (HU-053/HU-054 no se han aplicado).</summary>
    public RentSplitMethod? SplitMethod { get; private set; }

    public IReadOnlyList<TenantShare> Shares => _shares.AsReadOnly();
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsSplitPending => SplitMethod is null;

    /// <summary>Primera configuración de un periodo: no hereda nada, división pendiente.</summary>
    public static RentPeriod Configure(
        Guid unitId,
        string periodo,
        decimal totalMxn,
        int cutoffDay,
        IReadOnlyCollection<RentConcept> concepts,
        DateTimeOffset now)
    {
        if (unitId == Guid.Empty)
        {
            throw new ArgumentException("La unidad es obligatoria.", nameof(unitId));
        }

        ValidatePeriodo(periodo);
        ValidateTotal(totalMxn);
        ValidateCutoffDay(cutoffDay);
        ValidateConcepts(concepts);

        return new RentPeriod(
            Guid.NewGuid(), unitId, periodo, totalMxn, cutoffDay,
            [.. concepts], splitMethod: null, shares: [], now, now);
    }

    /// <summary>
    /// Hereda el periodo anterior más reciente de la misma unidad: siempre copia
    /// renta total, conceptos y día de corte; copia la división y las cuotas
    /// solo si el conjunto de inquilinos activos no cambió. Si cambió, la
    /// división queda pendiente para que el titular la rehaga.
    /// </summary>
    public static RentPeriod InheritFrom(
        RentPeriod previous,
        string periodo,
        IReadOnlyCollection<Guid> activeTenantAccountIds,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(activeTenantAccountIds);
        ValidatePeriodo(periodo);

        var previousTenants = previous._shares
            .Select(share => share.TenantAccountId)
            .ToHashSet();
        var currentTenants = activeTenantAccountIds.ToHashSet();
        var sameTenants = !previous.IsSplitPending && previousTenants.SetEquals(currentTenants);

        return new RentPeriod(
            Guid.NewGuid(),
            previous.UnitId,
            periodo,
            previous.TotalMxn,
            previous.CutoffDay,
            [.. previous._concepts],
            sameTenants ? previous.SplitMethod : null,
            sameTenants ? [.. previous._shares] : [],
            now,
            now);
    }

    public static RentPeriod Restore(
        Guid id,
        Guid unitId,
        string periodo,
        decimal totalMxn,
        int cutoffDay,
        IEnumerable<RentConcept> concepts,
        RentSplitMethod? splitMethod,
        IEnumerable<TenantShare> shares,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(
            id, unitId, periodo, totalMxn, cutoffDay,
            [.. concepts], splitMethod, [.. shares], createdAt, updatedAt);

    /// <summary>
    /// HU-050/051/052: redefine renta total, conceptos y día de corte. Siempre
    /// reinicia la división a pendiente, incluso si ya estaba dividida o
    /// heredada: unas cuotas calculadas contra la renta anterior ya no suman
    /// correctamente contra la nueva.
    /// </summary>
    public void SetConfiguration(
        decimal totalMxn,
        int cutoffDay,
        IReadOnlyCollection<RentConcept> concepts,
        DateTimeOffset now)
    {
        ValidateTotal(totalMxn);
        ValidateCutoffDay(cutoffDay);
        ValidateConcepts(concepts);

        TotalMxn = totalMxn;
        CutoffDay = cutoffDay;
        _concepts.Clear();
        _concepts.AddRange(concepts);
        SplitMethod = null;
        _shares.Clear();
        UpdatedAt = now;
    }

    /// <summary>
    /// HU-053: divide en centavos (renta × 100 / N, truncado) y reparte el
    /// residuo de un centavo a los inquilinos ordenados por <c>accountId</c>
    /// (formato "N", orden ordinal) hasta agotarlo, para que la suma cierre
    /// exacto contra la renta total. Ejemplo: $1000.00 entre 3 inquilinos →
    /// $333.34 + $333.33 + $333.33 (el primero en el orden absorbe el centavo
    /// sobrante).
    /// </summary>
    public void ApplyEqualSplit(IReadOnlyCollection<Guid> tenantAccountIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tenantAccountIds);

        if (tenantAccountIds.Count == 0)
        {
            throw new ArgumentException(
                "Se necesita al menos un inquilino para dividir la renta.",
                nameof(tenantAccountIds));
        }

        if (tenantAccountIds.Distinct().Count() != tenantAccountIds.Count)
        {
            throw new ArgumentException(
                "Un inquilino no puede repetirse en la división.",
                nameof(tenantAccountIds));
        }

        var totalCents = decimal.ToInt64(Math.Round(TotalMxn * 100m, MidpointRounding.AwayFromZero));
        var count = tenantAccountIds.Count;
        var baseCents = totalCents / count;
        var remainder = (int)(totalCents % count);
        var ordered = tenantAccountIds
            .OrderBy(id => id.ToString("N"), StringComparer.Ordinal)
            .ToArray();

        var shares = new List<TenantShare>(count);

        for (var index = 0; index < count; index++)
        {
            var cents = baseCents + (index < remainder ? 1 : 0);
            shares.Add(new TenantShare(ordered[index], cents / 100m));
        }

        _shares.Clear();
        _shares.AddRange(shares);
        SplitMethod = RentSplitMethod.Igual;
        UpdatedAt = now;
    }

    /// <summary>HU-054: la suma de las cuotas debe ser igual a la renta total.</summary>
    public void ApplyCustomSplit(IReadOnlyList<TenantShare> shares, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(shares);

        if (shares.Count == 0)
        {
            throw new ArgumentException("Se necesita al menos una cuota.", nameof(shares));
        }

        if (shares.Select(share => share.TenantAccountId).Distinct().Count() != shares.Count)
        {
            throw new ArgumentException("Un inquilino no puede repetirse en la división.", nameof(shares));
        }

        if (shares.Any(share => share.AmountMxn <= 0))
        {
            throw new ArgumentException("Cada cuota debe ser mayor que cero.", nameof(shares));
        }

        if (shares.Sum(share => share.AmountMxn) != TotalMxn)
        {
            throw new ArgumentException(
                "La suma de las cuotas debe ser igual a la renta total.",
                nameof(shares));
        }

        _shares.Clear();
        _shares.AddRange(shares);
        SplitMethod = RentSplitMethod.Personalizada;
        UpdatedAt = now;
    }

    public TenantShare? FindShare(Guid tenantAccountId) =>
        _shares.SingleOrDefault(share => share.TenantAccountId == tenantAccountId);

    /// <summary>Ajusta <see cref="CutoffDay"/> al último día real del mes del periodo (31 en febrero → 28/29).</summary>
    public static DateOnly ComputeCutoffDate(string periodo, int cutoffDay)
    {
        var (year, month) = ParsePeriodo(periodo);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(cutoffDay, daysInMonth));
    }

    public static bool IsValidPeriodo(string? periodo) =>
        !string.IsNullOrWhiteSpace(periodo) && PeriodoPattern().IsMatch(periodo);

    /// <summary>Compara dos periodos "yyyy-MM"; el formato ordena correctamente como texto.</summary>
    public static int ComparePeriodo(string left, string right) =>
        string.CompareOrdinal(left, right);

    private static (int Year, int Month) ParsePeriodo(string periodo)
    {
        ValidatePeriodo(periodo);
        var year = int.Parse(periodo.AsSpan(0, 4), CultureInfo.InvariantCulture);
        var month = int.Parse(periodo.AsSpan(5, 2), CultureInfo.InvariantCulture);
        return (year, month);
    }

    private static void ValidatePeriodo(string periodo)
    {
        if (!IsValidPeriodo(periodo))
        {
            throw new ArgumentException(
                "El periodo debe tener el formato \"yyyy-MM\".",
                nameof(periodo));
        }
    }

    private static void ValidateTotal(decimal totalMxn)
    {
        if (totalMxn <= 0)
        {
            throw new ArgumentException("La renta total debe ser mayor que cero.", nameof(totalMxn));
        }
    }

    private static void ValidateCutoffDay(int cutoffDay)
    {
        if (cutoffDay is < MinCutoffDay or > MaxCutoffDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cutoffDay),
                $"El día de corte debe estar entre {MinCutoffDay} y {MaxCutoffDay}.");
        }
    }

    private static void ValidateConcepts(IReadOnlyCollection<RentConcept> concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);
        var names = concepts.Select(concept => concept.Nombre).ToHashSet();
        var expected = Enum.GetValues<RentConceptName>();

        if (names.Count != concepts.Count || names.Count != expected.Length || !expected.All(names.Contains))
        {
            throw new ArgumentException(
                "Deben especificarse, sin repetir, los cuatro conceptos de cobro.",
                nameof(concepts));
        }
    }

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex PeriodoPattern();
}
