using UniHome.Domain.Verification;

namespace UniHome.UnitTests.Domain;

public sealed class PersonNameComparisonTests
{
    [Theory]
    [InlineData("ANA PÉREZ LÓPEZ", "ana perez lopez")]
    [InlineData("PEREZ LOPEZ ANA", "ANA PEREZ LOPEZ")]
    [InlineData("MARÍA DE LA LUZ GARCÍA", "MARIA GARCIA")]
    [InlineData("JOSÉ  LUIS   HERNÁNDEZ", "JOSE LUIS HERNANDEZ")]
    public void NombresEquivalentes_DeberSerCoincidenciaOrientativa(
        string first,
        string second)
    {
        Assert.Equal(
            ResultadoComparacionNombres.CoincidenciaOrientativa,
            PersonNameComparison.Compare(first, second));
    }

    [Theory]
    [InlineData("ANA PEREZ", "CARLOS RAMIREZ")]
    [InlineData("MARIA GARCIA", "MARIA LOPEZ")]
    public void NombresDistintos_DeberSerDiferencia(string first, string second)
    {
        Assert.Equal(
            ResultadoComparacionNombres.DiferenciaDetectada,
            PersonNameComparison.Compare(first, second));
    }

    [Theory]
    [InlineData(null, "ANA PEREZ")]
    [InlineData("ANA", "ANA PEREZ")]
    [InlineData("", "")]
    public void TextoInsuficiente_DeberSerNoDisponible(string? first, string? second)
    {
        Assert.Equal(
            ResultadoComparacionNombres.NoDisponible,
            PersonNameComparison.Compare(first, second));
    }

    [Fact]
    public void DiferenciaDeNombres_MarcaRiesgoSinCambiarEstado()
    {
        var now = DateTimeOffset.UtcNow;
        var expediente = Expediente.Create(
            Guid.NewGuid(),
            "UH-2026-ABCDEF123456",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            now);
        expediente.AddOrReplaceDocument(
            DocumentoVerificacion.Create(
                TipoDocumentoVerificacion.Ine,
                $"doc-{Guid.NewGuid():N}",
                "ine.png",
                "image/png",
                10,
                new string('A', 64),
                1,
                now),
            now);
        expediente.AddOrReplaceDocument(
            DocumentoVerificacion.Create(
                TipoDocumentoVerificacion.Comprobante,
                $"doc-{Guid.NewGuid():N}",
                "predial.png",
                "image/png",
                10,
                new string('B', 64),
                1,
                now),
            now);

        var ine = expediente.GetRequiredDocument(TipoDocumentoVerificacion.Ine);
        var proof = expediente.GetRequiredDocument(TipoDocumentoVerificacion.Comprobante);
        expediente.RecordAnalysis(
            ine.Tipo,
            ine.Version,
            "ANA PEREZ",
            EstadoAnalisisDocumento.Completado,
            EstadoAnalisisDocumento.Completado,
            false,
            true,
            true,
            [],
            null,
            now);
        expediente.RecordAnalysis(
            proof.Tipo,
            proof.Version,
            "CARLOS RAMIREZ",
            EstadoAnalisisDocumento.Completado,
            EstadoAnalisisDocumento.Completado,
            false,
            true,
            true,
            [],
            null,
            now);

        Assert.Equal(EstadoExpediente.EnRevision, expediente.State);
        Assert.Equal(
            ResultadoComparacionNombres.DiferenciaDetectada,
            expediente.NameComparison);
        Assert.True(expediente.IsSuspicious);
        Assert.NotNull(expediente.NameComparisonUpdatedAt);
        Assert.Contains(expediente.RiskIndicator.Signals, signal =>
            signal.Contains("comparación orientativa", StringComparison.OrdinalIgnoreCase));
    }
}
