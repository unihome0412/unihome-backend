using UniHome.Domain.Verification;

namespace UniHome.UnitTests.Domain;

public sealed class ExpedienteTests
{
    private static readonly string ShaA = new('A', 64);
    private static readonly string ShaB = new('B', 64);
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Expediente_NacePendienteSinDocumentos()
    {
        var expediente = CreateExpediente();

        Assert.Equal(EstadoExpediente.Pendiente, expediente.State);
        Assert.False(expediente.DocumentsReceived);
        Assert.False(expediente.AutomaticReviewCompleted);
        Assert.True(expediente.DecisionPending);
        Assert.False(expediente.IsSuspicious);
    }

    [Fact]
    public void SoloIne_PermanecePendiente()
    {
        var expediente = CreateExpediente();

        expediente.AddOrReplaceDocument(CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1), Now);

        Assert.Equal(EstadoExpediente.Pendiente, expediente.State);
        Assert.False(expediente.DocumentsReceived);
        Assert.True(expediente.DecisionPending);
    }

    [Fact]
    public void AmbosDocumentos_PasanAEnRevision()
    {
        var expediente = CreateExpediente();

        expediente.AddOrReplaceDocument(CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1), Now);
        expediente.AddOrReplaceDocument(
            CreateDocument(TipoDocumentoVerificacion.Comprobante, ShaB, 1),
            Now.AddMinutes(1));

        Assert.Equal(EstadoExpediente.EnRevision, expediente.State);
        Assert.True(expediente.DocumentsReceived);
        Assert.NotNull(expediente.SubmittedAt);
        Assert.False(expediente.AutomaticReviewCompleted);
    }

    [Fact]
    public void AnalisisCompletado_NoCambiaEstadoNiDecidePorIndicadorSospechoso()
    {
        var expediente = CreateExpediente();
        var ine = CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1);
        expediente.AddOrReplaceDocument(ine, Now);
        expediente.AddOrReplaceDocument(
            CreateDocument(TipoDocumentoVerificacion.Comprobante, ShaB, 1),
            Now);

        var recorded = expediente.RecordAnalysis(
            TipoDocumentoVerificacion.Ine,
            ine.Version,
            "Ana Pérez",
            EstadoAnalisisDocumento.Completado,
            EstadoAnalisisDocumento.Completado,
            editionDetected: true,
            elaAvailable: true,
            metadataAvailable: true,
            editionSignals: ["Metadatos indican software de edición: photoshop."],
            analysisDetail: null,
            now: Now.AddMinutes(2));

        Assert.True(recorded);
        Assert.Equal(EstadoExpediente.EnRevision, expediente.State);
        Assert.True(expediente.IsSuspicious);
        Assert.Contains(expediente.RiskIndicator.Signals, signal =>
            signal.Contains("photoshop", StringComparison.Ordinal));
    }

    [Fact]
    public void MismoHash_EsIdempotenteYNoCambiaVersion()
    {
        var expediente = CreateExpediente();
        var document = CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1);

        var first = expediente.AddOrReplaceDocument(document, Now);
        var second = expediente.AddOrReplaceDocument(
            CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 2),
            Now.AddMinutes(1));

        Assert.Same(first, second);
        Assert.Equal(1, expediente.GetRequiredDocument(TipoDocumentoVerificacion.Ine).Version);
    }

    [Fact]
    public void EvidenciaNuevaEnRevision_DebeRechazarse()
    {
        var expediente = CreateExpediente();
        expediente.AddOrReplaceDocument(CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1), Now);
        expediente.AddOrReplaceDocument(
            CreateDocument(TipoDocumentoVerificacion.Comprobante, ShaB, 1),
            Now);

        Assert.Throws<InvalidOperationException>(() =>
            expediente.AddOrReplaceDocument(
                CreateDocument(TipoDocumentoVerificacion.Ine, ShaB, 2),
                Now.AddMinutes(1)));
    }

    [Fact]
    public void RechazoExigeMotivoYAprobacionEsTerminal()
    {
        var expediente = CreateExpediente();
        expediente.AddOrReplaceDocument(CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1), Now);
        expediente.AddOrReplaceDocument(
            CreateDocument(TipoDocumentoVerificacion.Comprobante, ShaB, 1),
            Now);

        Assert.Throws<ArgumentException>(() =>
            expediente.Reject("   ", Guid.NewGuid(), Now));

        var admin = Guid.NewGuid();
        expediente.Approve(admin, Now.AddMinutes(1));

        Assert.Equal(EstadoExpediente.Aprobado, expediente.State);
        Assert.NotNull(expediente.Decision);
        Assert.False(expediente.DecisionPending);
        Assert.Throws<InvalidOperationException>(() =>
            expediente.Reject("Motivo", admin, Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() =>
            expediente.AddOrReplaceDocument(
                CreateDocument(TipoDocumentoVerificacion.Ine, ShaB, 2),
                Now.AddMinutes(2)));
    }

    [Fact]
    public void NoSePuedeDecidirAntesDeEnRevision()
    {
        var expediente = CreateExpediente();

        Assert.Throws<InvalidOperationException>(() =>
            expediente.Approve(Guid.NewGuid(), Now));
    }

    [Fact]
    public void AnalisisConVersionObsoleta_NoModificaDocumento()
    {
        var expediente = CreateExpediente();
        expediente.AddOrReplaceDocument(CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1), Now);

        var recorded = expediente.RecordAnalysis(
            TipoDocumentoVerificacion.Ine,
            version: 2,
            ocrName: "Nombre",
            EstadoAnalisisDocumento.Completado,
            EstadoAnalisisDocumento.Completado,
            editionDetected: false,
            elaAvailable: true,
            metadataAvailable: true,
            editionSignals: [],
            analysisDetail: null,
            now: Now);

        Assert.False(recorded);
        Assert.Null(expediente.GetRequiredDocument(TipoDocumentoVerificacion.Ine).NombreOcr);
    }

    [Fact]
    public void DatosObligatoriosYFolioInvalido_DebenRechazarse()
    {
        Assert.Throws<ArgumentException>(() => Expediente.Create(
            Guid.Empty,
            "UH-2026-ABCDEF123456",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now));
        Assert.Throws<ArgumentException>(() => Expediente.Create(
            Guid.NewGuid(),
            "UH-2026-ABCDEF123456",
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now));
        Assert.Throws<ArgumentException>(() => Expediente.Create(
            Guid.NewGuid(),
            " ",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now));

        foreach (var folio in new[] { "CORTO", "UH 2026", "UH_2026", new string('A', 41) })
        {
            Assert.Throws<ArgumentException>(() => Expediente.Create(
                Guid.NewGuid(),
                folio,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now));
        }
    }

    [Fact]
    public void DocumentoInexistenteYVersionInicialIncorrecta_DebenRechazarse()
    {
        var expediente = CreateExpediente();

        Assert.Null(expediente.FindDocument(TipoDocumentoVerificacion.Ine));
        Assert.Throws<KeyNotFoundException>(() =>
            expediente.GetRequiredDocument(TipoDocumentoVerificacion.Ine));
        Assert.Throws<ArgumentException>(() =>
            expediente.AddOrReplaceDocument(
                CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 2),
                Now));
        Assert.False(expediente.RecordAnalysis(
            TipoDocumentoVerificacion.Ine,
            1,
            "Nombre",
            EstadoAnalisisDocumento.Completado,
            EstadoAnalisisDocumento.Completado,
            false,
            true,
            true,
            [],
            null,
            Now));
    }

    [Fact]
    public void VersionAnteriorEnBorrador_DebeRechazarse()
    {
        var expediente = CreateExpediente();
        expediente.AddOrReplaceDocument(CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1), Now);

        Assert.Throws<ArgumentException>(() =>
            expediente.AddOrReplaceDocument(
                CreateDocument(TipoDocumentoVerificacion.Ine, ShaB, 1),
                Now.AddMinutes(1)));
    }

    [Fact]
    public void RechazoPermiteReenviarEvidenciaYVolverARevision()
    {
        var expediente = CreateExpediente();
        expediente.AddOrReplaceDocument(CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1), Now);
        expediente.AddOrReplaceDocument(
            CreateDocument(TipoDocumentoVerificacion.Comprobante, ShaB, 1),
            Now);
        expediente.Reject("Documento no legible", Guid.NewGuid(), Now.AddMinutes(1));

        expediente.AddOrReplaceDocument(
            CreateDocument(TipoDocumentoVerificacion.Ine, ShaB, 2),
            Now.AddMinutes(2));

        Assert.Equal(EstadoExpediente.EnRevision, expediente.State);
        Assert.Null(expediente.RejectionReason);
        Assert.Null(expediente.Decision);
        Assert.Equal(
            2,
            expediente.GetRequiredDocument(TipoDocumentoVerificacion.Ine).Version);
    }

    [Fact]
    public void AnalisisCompletoYEdicionSinDetalle_DebenReflejarseEnRiesgo()
    {
        var expediente = CreateExpediente();
        expediente.AddOrReplaceDocument(
            CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1).WithAnalysis(
                "Ana Pérez",
                EstadoAnalisisDocumento.Completado,
                EstadoAnalisisDocumento.Completado,
                edicionDetectada: true,
                elaDisponible: true,
                metadatosDisponibles: true,
                editionSignals: [],
                detalleAnalisis: null),
            Now);
        expediente.AddOrReplaceDocument(
            CreateDocument(TipoDocumentoVerificacion.Comprobante, ShaB, 1).WithAnalysis(
                "Ana Pérez",
                EstadoAnalisisDocumento.Completado,
                EstadoAnalisisDocumento.Completado,
                edicionDetectada: false,
                elaDisponible: true,
                metadatosDisponibles: true,
                editionSignals: [],
                detalleAnalisis: null),
            Now);

        Assert.True(expediente.AutomaticReviewCompleted);
        Assert.True(expediente.IsSuspicious);
        Assert.Contains(expediente.RiskIndicator.Signals, signal =>
            signal.Contains("sin detalle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DecisionesInvalidas_DebenRechazarseSinCambiarEstado()
    {
        var expediente = CreateExpediente();

        Assert.Throws<ArgumentException>(() =>
            expediente.Approve(Guid.Empty, Now));
        Assert.Throws<InvalidOperationException>(() =>
            expediente.Approve(Guid.NewGuid(), Now));
        Assert.Throws<InvalidOperationException>(() =>
            expediente.Reject("Motivo", Guid.NewGuid(), Now));

        expediente.AddOrReplaceDocument(CreateDocument(TipoDocumentoVerificacion.Ine, ShaA, 1), Now);
        expediente.AddOrReplaceDocument(
            CreateDocument(TipoDocumentoVerificacion.Comprobante, ShaB, 1),
            Now);

        Assert.Throws<ArgumentException>(() =>
            expediente.Reject("Motivo", Guid.Empty, Now));
        Assert.Throws<ArgumentException>(() =>
            expediente.Reject(new string('a', Expediente.MaxRejectionReasonLength + 1), Guid.NewGuid(), Now));
        Assert.Throws<ArgumentException>(() =>
            expediente.Reject("Motivo\u0001", Guid.NewGuid(), Now));
    }

    private static Expediente CreateExpediente() =>
        Expediente.Create(
            Guid.NewGuid(),
            "UH-2026-ABCDEF123456",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now);

    private static DocumentoVerificacion CreateDocument(
        TipoDocumentoVerificacion type,
        string sha,
        int version) =>
        DocumentoVerificacion.Create(
            type,
            $"doc-{Guid.NewGuid():N}",
            type == TipoDocumentoVerificacion.Ine ? "ine.png" : "predial.pdf",
            type == TipoDocumentoVerificacion.Ine ? "image/png" : "application/pdf",
            1024,
            sha,
            version,
            Now);
}
