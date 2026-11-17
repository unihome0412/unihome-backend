using UniHome.Domain.Verification;
using UniHome.Infrastructure.Analysis;

namespace UniHome.UnitTests.Infrastructure;

public sealed class DocumentNameExtractorTests
{
    [Fact]
    public void Ine_ConEtiquetas_DebeComponerApellidosYNombre()
    {
        const string text = """
            INSTITUTO NACIONAL ELECTORAL
            NOMBRE
            ANA
            APELLIDO PATERNO
            PEREZ
            APELLIDO MATERNO
            LOPEZ
            CLAVE DE ELECTOR PRLPNR...
            """;

        var name = DocumentNameExtractor.Extract(text, TipoDocumentoVerificacion.Ine);

        Assert.Equal("PEREZ LOPEZ ANA", name);
    }

    [Fact]
    public void Comprobante_ConPropietarioEnMismaLinea_DebeExtraerNombre()
    {
        const string text = """
            RECIBO PREDIAL
            CONTRIBUYENTE: MARIA GARCIA HERNANDEZ
            DOMICILIO: CALLE 1
            TOTAL: 1500
            """;

        var name = DocumentNameExtractor.Extract(text, TipoDocumentoVerificacion.Comprobante);

        Assert.Equal("MARIA GARCIA HERNANDEZ", name);
    }

    [Fact]
    public void TextoSinNombre_DebeDevolverNullSinInventar()
    {
        const string text = """
            RECIBO PREDIAL
            TOTAL 1500
            FECHA 2026-11-16
            CLAVE 123456789
            """;

        var name = DocumentNameExtractor.Extract(text, TipoDocumentoVerificacion.Comprobante);

        Assert.Null(name);
    }

    [Fact]
    public void TextoVacio_DebeDevolverNull()
    {
        Assert.Null(DocumentNameExtractor.Extract(null, TipoDocumentoVerificacion.Ine));
        Assert.Null(DocumentNameExtractor.Extract("   ", TipoDocumentoVerificacion.Ine));
    }

    [Fact]
    public void EtiquetaConParentesisYGuion_DebeExtraerElValor()
    {
        const string text = """
            NOMBRE (NOMBRES): ANA PEREZ
            APELLIDO PATERNO - LOPEZ
            """;

        var name = DocumentNameExtractor.Extract(text, TipoDocumentoVerificacion.Ine);

        Assert.Equal("LOPEZ ANA PEREZ", name);
    }

    [Fact]
    public void Fallback_DebeSaltarStopWordsSinCortarPalabrasParecidas()
    {
        const string text = """
            TOTAL 1500
            ANA PEREZ
            TOTALIZADOR
            """;

        var name = DocumentNameExtractor.Extract(text, TipoDocumentoVerificacion.Comprobante);

        Assert.Equal("ANA PEREZ", name);
    }

    [Fact]
    public void EtiquetaSinValorInmediato_DebeDetenerLaBusquedaAnteOtraEtiqueta()
    {
        const string text = """
            PROPIETARIO
            DOMICILIO: CALLE 1
            ANA PEREZ
            """;

        var name = DocumentNameExtractor.Extract(text, TipoDocumentoVerificacion.Comprobante);

        Assert.Equal("ANA PEREZ", name);
    }

    [Fact]
    public void ValoresConDigitosDemasiadoLargosOUnaPalabra_NoDebenInventarse()
    {
        Assert.Null(DocumentNameExtractor.Extract(
            "NOMBRE: ANA 123",
            TipoDocumentoVerificacion.Ine));
        Assert.Null(DocumentNameExtractor.Extract(
            "NOMBRE: ANA MARIA PEREZ LOPEZ GARCIA HERNANDEZ SANCHEZ",
            TipoDocumentoVerificacion.Ine));
        Assert.Null(DocumentNameExtractor.Extract(
            "NOMBRE: A",
            TipoDocumentoVerificacion.Ine));
    }

    [Fact]
    public void ParentesisSinCierreYFallbackConStopWord_DebenUsarTextoLegible()
    {
        const string text = """
            NOMBRE (SIN CIERRE
            TOTAL 1500
            ANA MARIA PEREZ
            """;

        var name = DocumentNameExtractor.Extract(text, TipoDocumentoVerificacion.Ine);

        Assert.Equal("ANA MARIA PEREZ", name);
    }
}
