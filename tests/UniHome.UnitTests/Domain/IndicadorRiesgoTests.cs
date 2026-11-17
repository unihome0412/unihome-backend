using UniHome.Domain.Verification;

namespace UniHome.UnitTests.Domain;

public sealed class IndicadorRiesgoTests
{
    [Fact]
    public void Create_NormalizaFiltraOrdenaLimitaYNoRepite()
    {
        var signals = Enumerable.Range(0, IndicadorRiesgo.MaxSignals + 5)
            .Select(index => $" señal-{index} ")
            .Append("señal-0")
            .Append(" ")
            .ToArray();

        var result = IndicadorRiesgo.Create(signals);

        Assert.True(result.IsSuspicious);
        Assert.Equal(IndicadorRiesgo.MaxSignals, result.Signals.Count);
        Assert.Equal(result.Signals.Order(StringComparer.Ordinal), result.Signals);
        Assert.DoesNotContain(result.Signals, signal => signal.Contains(' '));
    }

    [Fact]
    public void Create_TruncaLongitudYEliminaControles()
    {
        var result = IndicadorRiesgo.Create(
            [$"\u0001{new string('x', IndicadorRiesgo.MaxSignalLength + 20)}"]);

        Assert.Single(result.Signals);
        Assert.Equal(IndicadorRiesgo.MaxSignalLength - 1, result.Signals[0].Length);
        Assert.DoesNotContain(result.Signals[0], character => char.IsControl(character));
    }

    [Fact]
    public void Create_SinSenalesUtiles_DebeSerVacio()
    {
        Assert.False(IndicadorRiesgo.Create(null).IsSuspicious);
        Assert.False(IndicadorRiesgo.Create([" ", "\t", ""]).IsSuspicious);
    }
}
