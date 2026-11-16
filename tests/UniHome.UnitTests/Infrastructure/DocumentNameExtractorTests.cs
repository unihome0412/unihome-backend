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
}
