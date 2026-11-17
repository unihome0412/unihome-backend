using UniHome.Domain.Verification;

namespace UniHome.UnitTests.Domain;

public sealed class DocumentoVerificacionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Sha = new('A', 64);

    [Fact]
    public void Create_ValidaTipoReferenciaContenidoTamanoHuellaYVersion()
    {
        Assert.Throws<ArgumentException>(() => DocumentoVerificacion.Create(
            (TipoDocumentoVerificacion)99,
            "doc-1",
            "ine.png",
            "image/png",
            1,
            Sha,
            1,
            Now));
        Assert.Throws<ArgumentException>(() => DocumentoVerificacion.Create(
            TipoDocumentoVerificacion.Ine,
            " ",
            "ine.png",
            "image/png",
            1,
            Sha,
            1,
            Now));
        Assert.Throws<ArgumentException>(() => DocumentoVerificacion.Create(
            TipoDocumentoVerificacion.Ine,
            new string('x', DocumentoVerificacion.MaxStorageKeyLength + 1),
            "ine.png",
            "image/png",
            1,
            Sha,
            1,
            Now));
        Assert.Throws<ArgumentException>(() => DocumentoVerificacion.Create(
            TipoDocumentoVerificacion.Ine,
            "doc-1",
            "ine.png",
            "text/plain",
            1,
            Sha,
            1,
            Now));
        Assert.Throws<ArgumentException>(() => DocumentoVerificacion.Create(
            TipoDocumentoVerificacion.Ine,
            "doc-1",
            "ine.png",
            "image/png",
            0,
            Sha,
            1,
            Now));
        Assert.Throws<ArgumentException>(() => DocumentoVerificacion.Create(
            TipoDocumentoVerificacion.Ine,
            "doc-1",
            "ine.png",
            "image/png",
            1,
            "no-es-hash",
            1,
            Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentoVerificacion.Create(
            TipoDocumentoVerificacion.Ine,
            "doc-1",
            "ine.png",
            "image/png",
            1,
            Sha,
            0,
            Now));
    }

    [Fact]
    public void Create_NormalizaNombreHuellaYEstadoInicial()
    {
        var longName = new string('x', DocumentoVerificacion.MaxOriginalFileNameLength + 20);
        var expectedName = new string('x', DocumentoVerificacion.MaxOriginalFileNameLength);
        var document = DocumentoVerificacion.Create(
            TipoDocumentoVerificacion.Ine,
            "doc-1",
            $"C:\\ruta\\{longName}",
            "image/png",
            12,
            Sha.ToLowerInvariant(),
            1,
            Now);

        Assert.Equal("doc-1", document.StorageKey);
        Assert.Equal(expectedName, document.OriginalFileName);
        Assert.Equal(Sha, document.Sha256);
        Assert.Null(document.NombreOcr);
        Assert.False(document.RevisionAutomaticaCompletada);

        var fallback = DocumentoVerificacion.Create(
            TipoDocumentoVerificacion.Comprobante,
            "doc-2",
            "   ",
            "application/pdf",
            1,
            Sha,
            1,
            Now);
        Assert.Equal("documento", fallback.OriginalFileName);
    }

    [Theory]
    [InlineData(@"C:\ruta\ine.png", "ine.png")]
    [InlineData("C:/ruta/ine.png", "ine.png")]
    [InlineData(@"C:\ruta/sub\ine.png", "ine.png")]
    [InlineData("/home/ana/ine.png", "ine.png")]
    [InlineData("ine.png", "ine.png")]
    [InlineData("C:ine.png", "ine.png")]
    [InlineData(@"..\..\ine.png", "ine.png")]
    [InlineData("ine (1) #final.png", "ine (1) #final.png")]
    [InlineData("ine\u0001.png", "ine.png")]
    [InlineData(@"C:\ruta\", "documento")]
    [InlineData("..", "documento")]
    [InlineData(" ", "documento")]
    [InlineData("", "documento")]
    [InlineData(null, "documento")]
    public void Create_NormalizaNombresDeArchivoEntreSistemasOperativos(
        string? fileName,
        string expected)
    {
        var document = DocumentoVerificacion.Create(
            TipoDocumentoVerificacion.Ine,
            "doc-1",
            fileName,
            "image/png",
            1,
            Sha,
            1,
            Now);

        Assert.Equal(expected, document.OriginalFileName);
    }

    [Fact]
    public void WithAnalysis_NormalizaNombreSenalesYEstado()
    {
        var document = DocumentoVerificacion.Create(
            TipoDocumentoVerificacion.Ine,
            "doc-1",
            "ine.png",
            "image/png",
            10,
            Sha,
            1,
            Now);
        var analyzed = document.WithAnalysis(
            "  ANA\n PEREZ  ",
            EstadoAnalisisDocumento.Completado,
            EstadoAnalisisDocumento.Completado,
            edicionDetectada: true,
            elaDisponible: true,
            metadatosDisponibles: true,
            editionSignals: [new string('s', 500), "señal"],
            detalleAnalisis: "detalle");

        Assert.Equal("ANA PEREZ", analyzed.NombreOcr);
        Assert.True(analyzed.RevisionAutomaticaCompletada);
        Assert.True(analyzed.EdicionDetectada);
        Assert.True(analyzed.ElaDisponible);
        Assert.True(analyzed.MetadatosDisponibles);
        Assert.True(analyzed.EditionSignals.Count <= IndicadorRiesgo.MaxSignals);
        Assert.Equal("detalle", analyzed.DetalleAnalisis);
        Assert.Equal("Pendiente", document.EstadoOcr.ToString());

        var pending = document.WithAnalysis(
            null,
            EstadoAnalisisDocumento.Pendiente,
            EstadoAnalisisDocumento.Completado,
            false,
            false,
            false,
            null,
            null);
        Assert.Null(pending.NombreOcr);
        Assert.False(pending.RevisionAutomaticaCompletada);
    }

    [Fact]
    public void Restore_NormalizaNombreYLimitaSenales()
    {
        var restored = DocumentoVerificacion.Restore(
            TipoDocumentoVerificacion.Comprobante,
            "doc-1",
            "predial.pdf",
            "application/pdf",
            10,
            Sha.ToLowerInvariant(),
            2,
            Now,
            "\tANA\u0001 PEREZ ",
            EstadoAnalisisDocumento.NoDisponible,
            EstadoAnalisisDocumento.Completado,
            false,
            false,
            true,
            Enumerable.Range(0, IndicadorRiesgo.MaxSignals + 5).Select(i => $"señal-{i}"),
            null);

        Assert.Equal("ANA PEREZ", restored.NombreOcr);
        Assert.Equal(Sha, restored.Sha256);
        Assert.Equal(IndicadorRiesgo.MaxSignals, restored.EditionSignals.Count);
        Assert.True(restored.MetadatosDisponibles);
    }
}
