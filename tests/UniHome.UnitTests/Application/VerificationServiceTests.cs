using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Publications.SubmitFirstPublication;
using UniHome.Application.Verification.Abstractions;
using UniHome.Application.Verification.Admin;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Accounts;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Domain.Verification;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class VerificationServiceTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFElEQVR42mP4z8DAAMb//0MZUA4Aep8J9x7TI9wAAAAASUVORK5CYII=");

    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SubirIneYComprobante_DebeCrearExpedienteYEnviarUnidadARevision()
    {
        var context = await CreateContextAsync();

        var first = await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("ine.png", Png));
        var second = await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Comprobante,
            Upload("predial.png", Png));

        Assert.Equal(OperationStatus.Success, first.Status);
        Assert.Equal(EstadoExpediente.Pendiente, first.Value!.Expediente.Estado);
        Assert.Equal(OperationStatus.Success, second.Status);
        Assert.Equal(EstadoExpediente.EnRevision, second.Value!.Expediente.Estado);
        Assert.True(second.Value.Expediente.RevisionAutomaticaCompletada);
        Assert.True(second.Value.Expediente.DecisionPendiente);
        Assert.Equal(
            PublicationState.EnRevision,
            context.Units.Items.Single().PublicationState);
        Assert.Contains(
            context.Audit.Entries,
            entry => entry.Action == VerificationAuditActions.PublicationSubmitted);
    }

    [Fact]
    public async Task ArchivoInvalido_DebeResponderValidacionSinCrearExpediente()
    {
        var context = await CreateContextAsync();

        var result = await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("ine.pdf", Png));

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.Empty(context.Repository.All);
        Assert.Equal(0, context.Documents.SaveCalls);
    }

    [Fact]
    public async Task CuentaNoElegible_DebeResponderForbidden()
    {
        var context = await CreateContextAsync(minor: true);

        var result = await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("ine.png", Png));

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task MismoHash_DebeSerIdempotenteYSinNuevaAuditoria()
    {
        var context = await CreateContextAsync();
        var first = await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("ine.png", Png));
        var auditCount = context.Audit.Entries.Count;

        var second = await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("copia.png", Png));

        Assert.Equal(OperationStatus.Success, first.Status);
        Assert.True(second.Value!.DocumentoYaExistente);
        Assert.Equal(auditCount, context.Audit.Entries.Count);
        Assert.Single(context.Documents.StoredKeys);
    }

    [Fact]
    public async Task CambiarEvidenciaEnRevision_DebeResponderConflict()
    {
        var context = await CreateContextAsync();
        await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("ine.png", Png));
        await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Comprobante,
            Upload("predial.png", Png));

        var other = Png.Concat(new byte[] { 0x01 }).ToArray();
        var result = await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("ine2.png", other));

        Assert.Equal(OperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task AnalizadorFallido_DebeGuardarDocumentoConNombreNulo()
    {
        var context = await CreateContextAsync();
        context.Analyzer.Throw = true;

        var result = await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("ine.png", Png));

        Assert.Equal(OperationStatus.Success, result.Status);
        var expediente = await context.Repository.FindByOwnerAsync(context.Account.Id);
        Assert.NotNull(expediente);
        Assert.Null(expediente!.GetRequiredDocument(TipoDocumentoVerificacion.Ine).NombreOcr);
        Assert.Equal(
            EstadoAnalisisDocumento.NoDisponible,
            expediente.GetRequiredDocument(TipoDocumentoVerificacion.Ine).EstadoOcr);
    }

    [Fact]
    public async Task FalloDeAlmacenamiento_DebePropagarseComo503()
    {
        var context = await CreateContextAsync();
        context.Documents.ThrowOnSave = true;

        await Assert.ThrowsAsync<UniHome.Application.Identity.Exceptions.StorageUnavailableException>(
            () => context.Upload.UploadAsync(
                context.Account.Id,
                TipoDocumentoVerificacion.Ine,
                Upload("ine.png", Png)));
    }

    [Fact]
    public async Task ConcurrenciaOptimistaAgotada_DebeResponderConflictYLimpiar()
    {
        var context = await CreateContextAsync();
        context.Repository.ReplaceResult = false;

        var result = await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("ine.png", Png));

        Assert.Equal(OperationStatus.Conflict, result.Status);
        Assert.Equal(3, context.Repository.ReplaceCalls);
        Assert.Empty(context.Documents.StoredKeys);
    }

    [Fact]
    public async Task EnviarSinTerminosFotoODocumentos_DebeResponderUnprocessable()
    {
        var context = await CreateContextAsync();
        context.Terms.ExistsResult = false;
        var noTerms = await context.Submit.SubmitAsync(context.Account.Id);
        Assert.Equal(OperationStatus.Unprocessable, noTerms.Status);

        context.Terms.ExistsResult = true;
        var withoutDocs = await context.Submit.SubmitAsync(context.Account.Id);
        Assert.Equal(OperationStatus.Unprocessable, withoutDocs.Status);

        await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("ine.png", Png));
        await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Comprobante,
            Upload("predial.png", Png));
        context.Terms.ExistsResult = false;
        var withoutTermsAgain = await context.Submit.SubmitAsync(context.Account.Id);
        Assert.Equal(OperationStatus.Unprocessable, withoutTermsAgain.Status);
    }

    [Fact]
    public async Task EnviarConTodoCompleto_DebeSerIdempotenteYEnRevision()
    {
        var context = await CreateContextAsync();
        await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Ine,
            Upload("ine.png", Png));
        await context.Upload.UploadAsync(
            context.Account.Id,
            TipoDocumentoVerificacion.Comprobante,
            Upload("predial.png", Png));

        var first = await context.Submit.SubmitAsync(context.Account.Id);
        var second = await context.Submit.SubmitAsync(context.Account.Id);

        Assert.Equal(OperationStatus.Success, first.Status);
        Assert.Equal(PublicationState.EnRevision, first.Value!.Estado);
        Assert.Equal(OperationStatus.Success, second.Status);
    }

    [Fact]
    public async Task Bandeja_DebeValidarPaginacionYOrdenarSospechosos()
    {
        var context = await CreateContextAsync();
        var normal = CreateExpediente(context.Account.Id, suspicious: false, Now);
        var suspicious = CreateExpediente(Guid.NewGuid(), suspicious: true, Now.AddMinutes(1));
        await context.Repository.AddAsync(normal);
        await context.Repository.AddAsync(suspicious);

        var list = new ListExpedientesService(context.Repository, context.Accounts);
        var invalid = await list.ListAsync(null, true, 0, 20);
        Assert.Equal(OperationStatus.ValidationFailed, invalid.Status);

        var result = await list.ListAsync(null, true, 1, 20);
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.True(result.Value!.Items[0].EsSospechoso);
        Assert.Equal(2, result.Value.Total);
        Assert.Equal(2, result.Value.PendingReviewCount);
    }

    [Fact]
    public async Task DetalleYEnlace_DebenAuditarYNoExponerLlaves()
    {
        var context = await CreateContextAsync();
        var expediente = await SeedExpedienteAsync(context);

        var detailService = new GetExpedienteDetailService(
            context.Repository,
            context.Accounts,
            context.Signer,
            context.Urls,
            context.Audit,
            context.Clock);
        var detail = await detailService.GetAsync(Guid.NewGuid(), expediente.Id);

        Assert.Equal(OperationStatus.Success, detail.Status);
        Assert.Equal("Ana Pérez", detail.Value!.NombreOcrIne);
        Assert.NotNull(detail.Value.IneUrlMiniatura);
        Assert.DoesNotContain("doc-", detail.Value.IneUrlMiniatura, StringComparison.Ordinal);
        Assert.Contains(context.Audit.Entries, entry =>
            entry.Action == VerificationAuditActions.ExpedienteViewed);

        var issueService = new IssueDocumentAccessUrlService(
            context.Repository,
            context.Signer,
            context.Urls,
            context.Audit,
            context.Clock);
        var link = await issueService.IssueAsync(
            Guid.NewGuid(),
            expediente.Id,
            TipoDocumentoVerificacion.Ine);

        Assert.Equal(OperationStatus.Success, link.Status);
        Assert.StartsWith("/api/v1/expedientes/documentos/", link.Value!.Url, StringComparison.Ordinal);
        Assert.Contains(context.Audit.Entries, entry =>
            entry.Action == VerificationAuditActions.DocumentUrlIssued);
    }

    [Fact]
    public async Task LecturaDeDocumento_DebeValidarTokenYAuditar()
    {
        var context = await CreateContextAsync();
        var expediente = await SeedExpedienteAsync(context);
        var signer = context.Signer;
        var grant = signer.Create(
            expediente.Id,
            TipoDocumentoVerificacion.Ine,
            1,
            DocumentUrlVariant.Full,
            context.Clock.UtcNow);
        var service = new ReadVerificationDocumentService(
            context.Repository,
            context.Documents,
            new FakeDocumentPreviewGenerator(),
            signer,
            context.Audit,
            context.Clock);

        var result = await service.ReadAsync(Guid.NewGuid(), grant.Token);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(Png, result.Value!.Content);
        Assert.Contains(context.Audit.Entries, entry =>
            entry.Action == VerificationAuditActions.DocumentViewed);

        var invalid = await service.ReadAsync(Guid.NewGuid(), "invalido");
        Assert.Equal(OperationStatus.NotFound, invalid.Status);
    }

    private static async Task<TestContext> CreateContextAsync(bool minor = false)
    {
        var accounts = new InMemoryAccountRepository();
        var birthDate = minor
            ? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10))
            : new DateOnly(2004, 5, 10);
        var account = AccountTestData.Create(
            $"verification.{Guid.NewGuid():N}@uni.edu",
            birthDate: birthDate);
        await accounts.AddAsync(account);

        var units = new FakeUnitRepository();
        var property = Property.Create(
            account.Id,
            $"Casa {Guid.NewGuid():N}",
            $"Calle {Guid.NewGuid():N}",
            "Centro",
            PropertyType.Casa,
            Guid.NewGuid(),
            19.4326,
            -99.1332,
            null,
            null,
            Now);
        var unit = Unit.Create(
            property.Id,
            account.Id,
            property.UniversityId,
            UnitType.Habitacion,
            Occupancy.Individual,
            1,
            3500,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            [],
            null,
            Now);
        unit.AppendPhotos(
        [
            new UnitPhoto(
                $"photo-{Guid.NewGuid():N}",
                "image/png",
                10,
                new string('A', 64),
                0,
                true,
                Now)
        ]);
        await units.AddAsync(unit);

        var terms = new FakeTermsAcceptanceRepository { ExistsResult = true };
        var repository = new FakeVerificationRepository();
        var documents = new FakeVerificationDocumentStore();
        var audit = new FakeVerificationAuditRepository();
        var analyzer = new FakeDocumentAnalyzer();
        var clock = new FixedClock(Now);
        var eligibility = new PublishingEligibilityService(accounts, clock);
        var upload = new UploadVerificationDocumentService(
            repository,
            documents,
            audit,
            analyzer,
            units,
            terms,
            eligibility,
            clock);
        var submit = new SubmitFirstPublicationService(
            units,
            repository,
            terms,
            audit,
            eligibility,
            clock);

        return new TestContext(
            account,
            accounts,
            units,
            terms,
            repository,
            documents,
            audit,
            analyzer,
            clock,
            upload,
            submit,
            new FakeDocumentUrlSigner(),
            new FakeDocumentAccessUrlBuilder());
    }

    private static async Task<Expediente> SeedExpedienteAsync(TestContext context)
    {
        var stored = await context.Documents.SaveAsync(
            Guid.NewGuid(),
            TipoDocumentoVerificacion.Ine,
            1,
            Png,
            "image/png",
            "ine.png",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Png)));
        var expediente = Expediente.Create(
            Guid.NewGuid(),
            $"UH-2026-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            context.Account.Id,
            context.Units.Items.Single().Id,
            context.Units.Items.Single().PropertyId,
            Now);
        expediente.AddOrReplaceDocument(
            DocumentoVerificacion.Create(
                    TipoDocumentoVerificacion.Ine,
                    stored.StorageKey,
                    "ine.png",
                    "image/png",
                    Png.LongLength,
                    stored.Sha256,
                    1,
                    Now)
                .WithAnalysis(
                    "Ana Pérez",
                    EstadoAnalisisDocumento.Completado,
                    EstadoAnalisisDocumento.Completado,
                    false,
                    true,
                    true,
                    [],
                    null),
            Now);
        await context.Repository.AddAsync(expediente);
        return expediente;
    }

    private static Expediente CreateExpediente(
        Guid owner,
        bool suspicious,
        DateTimeOffset createdAt)
    {
        var expediente = Expediente.Create(
            Guid.NewGuid(),
            $"UH-2026-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            owner,
            Guid.NewGuid(),
            Guid.NewGuid(),
            createdAt);

        if (suspicious)
        {
            var document = DocumentoVerificacion.Create(
                    TipoDocumentoVerificacion.Ine,
                    $"doc-{Guid.NewGuid():N}",
                    "ine.png",
                    "image/png",
                    10,
                    new string('A', 64),
                    1,
                    createdAt)
                .WithAnalysis(
                    "Ana",
                    EstadoAnalisisDocumento.Completado,
                    EstadoAnalisisDocumento.Completado,
                    true,
                    true,
                    true,
                    ["señal"],
                    null);
            expediente.AddOrReplaceDocument(document, createdAt);
        }

        return expediente;
    }

    private static VerificationDocumentUpload Upload(string name, byte[] content) =>
        new(name, "image/png", content);

    private sealed record TestContext(
        Account Account,
        InMemoryAccountRepository Accounts,
        FakeUnitRepository Units,
        FakeTermsAcceptanceRepository Terms,
        FakeVerificationRepository Repository,
        FakeVerificationDocumentStore Documents,
        FakeVerificationAuditRepository Audit,
        FakeDocumentAnalyzer Analyzer,
        FixedClock Clock,
        UploadVerificationDocumentService Upload,
        SubmitFirstPublicationService Submit,
        FakeDocumentUrlSigner Signer,
        FakeDocumentAccessUrlBuilder Urls);
}
