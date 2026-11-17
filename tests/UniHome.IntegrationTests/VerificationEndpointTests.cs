using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SkiaSharp;
using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Domain.Properties;
using UniHome.Domain.Publications;
using UniHome.Domain.Units;
using UniHome.Domain.Universities;
using UniHome.Domain.Verification;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

public sealed class VerificationEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private const string AdminEmail = "verification.admin@uni.edu";

    private readonly UniHomeApiFactory _factory;

    public VerificationEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FlujoCompleto_DocumentosPasanARevisionYLaUnidadNoEsPublica()
    {
        var (account, client) = await CreateApplicantAsync();
        var publication = await SeedFirstPublicationAsync(account);
        using var clientScope = client;

        using var ine = await UploadAsync(
            client,
            "mi-expediente/ine",
            CreatePng(120, 80),
            "ine.png",
            "image/png");
        Assert.Equal(HttpStatusCode.OK, ine.StatusCode);
        Assert.True(ine.Headers.CacheControl?.NoStore);
        using var ineJson = await ReadJsonAsync(ine);
        var expedienteId = ineJson.RootElement.GetProperty("id").GetGuid();
        var folio = ineJson.RootElement.GetProperty("folio").GetString();
        Assert.False(string.IsNullOrWhiteSpace(folio));
        Assert.Equal("Pendiente", ineJson.RootElement.GetProperty("estado").GetString());
        Assert.True(ineJson.RootElement.GetProperty("documentosRecibidos").GetProperty("ine").GetBoolean());
        Assert.False(ineJson.RootElement.GetProperty("documentosRecibidos").GetProperty("comprobante").GetBoolean());
        Assert.Equal(
            "Almacenamiento cifrado y acceso restringido.",
            ineJson.RootElement.GetProperty("avisoAlmacenamiento").GetString());
        Assert.False(ineJson.RootElement.TryGetProperty("esSospechoso", out _));

        using var proof = await UploadAsync(
            client,
            "mi-expediente/comprobante",
            CreatePng(130, 90),
            "predial.png",
            "image/png");
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        using var proofJson = await ReadJsonAsync(proof);
        Assert.Equal("EnRevision", proofJson.RootElement.GetProperty("estado").GetString());
        Assert.True(proofJson.RootElement.GetProperty("revisionAutomaticaCompletada").GetBoolean());
        Assert.True(proofJson.RootElement.GetProperty("decisionPendiente").GetBoolean());

        using var submit = await client.PostAsync("/api/v1/publicaciones/primera/enviar", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        using var submitJson = await ReadJsonAsync(submit);
        Assert.Equal("EnRevision", submitJson.RootElement.GetProperty("estado").GetString());

        using var list = await client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={publication.UniversityId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = await ReadJsonAsync(list);
        Assert.Empty(listJson.RootElement.EnumerateArray());
        Assert.Equal("0", list.Headers.GetValues("X-Total-Count").Single());

        using var map = await client.GetAsync(
            $"/api/v1/exploracion/mapa?universidadId={publication.UniversityId}");
        Assert.Equal(HttpStatusCode.OK, map.StatusCode);
        using var mapJson = await ReadJsonAsync(map);
        Assert.Empty(mapJson.RootElement.EnumerateArray());

        using var admin = await CreateAdminClientAsync();
        using var detail = await admin.GetAsync($"/api/v1/admin/expedientes/{expedienteId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailJson = await ReadJsonAsync(detail);
        Assert.Equal("EnRevision", detailJson.RootElement.GetProperty("estado").GetString());
        Assert.Equal(
            "Ana Pérez López",
            detailJson.RootElement.GetProperty("nombreOcrIne").GetString());
        Assert.Equal(
            "Ana Pérez López",
            detailJson.RootElement.GetProperty("nombreOcrComprobante").GetString());
        Assert.False(detailJson.RootElement.GetProperty("indicadorEdicion").GetProperty("detectado").GetBoolean());
        Assert.True(detailJson.RootElement.GetProperty("indicadorEdicion").GetProperty("elaDisponible").GetBoolean());
        Assert.True(detailJson.RootElement.GetProperty("indicadorEdicion").GetProperty("metadatosDisponibles").GetBoolean());
        Assert.Contains(
            "orientativo",
            detailJson.RootElement.GetProperty("indicadorEdicion").GetProperty("aviso").GetString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.True(detail.Headers.CacheControl?.NoStore);

        var detailsText = await detail.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storageKey", detailsText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doc-", detailsText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cloudinary", detailsText, StringComparison.OrdinalIgnoreCase);

        var audit = _factory.VerificationAudit.All
            .Where(entry => entry.ExpedienteId == expedienteId)
            .ToArray();
        Assert.Contains(audit, entry =>
            entry.Action == VerificationAuditActions.DocumentUploaded &&
            entry.ActorAccountId == account.Id);
        Assert.Contains(audit, entry =>
            entry.Action == VerificationAuditActions.ExpedienteViewed &&
            entry.ActorAccountId == UniHomeApiFactory.TestAdminAccountId);
    }

    [Fact]
    public async Task IndicadorSospechoso_OrdenaPrimeroYExigeAdministrador()
    {
        var (normalAccount, normalClient) = await CreateApplicantAsync();
        var (suspiciousAccount, suspiciousClient) = await CreateApplicantAsync();
        using var normalClientScope = normalClient;
        using var suspiciousClientScope = suspiciousClient;
        await SeedFirstPublicationAsync(normalAccount);
        await SeedFirstPublicationAsync(suspiciousAccount);

        _factory.DocumentAnalyzer.ResultFactory = type => new DocumentAnalysisResult(
            type == TipoDocumentoVerificacion.Ine ? "Persona Normal" : "Persona Normal",
            EstadoAnalisisDocumento.Completado,
            EstadoAnalisisDocumento.Completado,
            EditionDetected: false,
            EditionSignals: [],
            ElaAvailable: true,
            MetadataAvailable: true,
            UnavailableDetail: null);

        try
        {
            var normal = await UploadBothAsync(normalClient);

            _factory.DocumentAnalyzer.ResultFactory = type => new DocumentAnalysisResult(
                "Persona Sospechosa",
                EstadoAnalisisDocumento.Completado,
                EstadoAnalisisDocumento.Completado,
                EditionDetected: type == TipoDocumentoVerificacion.Ine,
                EditionSignals: type == TipoDocumentoVerificacion.Ine
                    ? ["Metadatos indican software de edición: photoshop."]
                    : [],
                ElaAvailable: true,
                MetadataAvailable: true,
                UnavailableDetail: null);

            var suspicious = await UploadBothAsync(suspiciousClient);

            using var anonymous = _factory.CreateClient();
            using var anonymousList = await anonymous.GetAsync("/api/v1/admin/expedientes");
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);

            using var normalAsAdmin = await normalClient.GetAsync("/api/v1/admin/expedientes");
            Assert.Equal(HttpStatusCode.Forbidden, normalAsAdmin.StatusCode);

            using var admin = await CreateAdminClientAsync();
            using var list = await admin.GetAsync(
                "/api/v1/admin/expedientes?prioridadSospechosos=true&pageSize=100");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.True(int.Parse(list.Headers.GetValues("X-Pending-Review-Count").Single()) >= 2);
            using var listJson = await ReadJsonAsync(list);
            var items = listJson.RootElement.EnumerateArray().ToArray();
            var suspiciousIndex = Array.FindIndex(
                items,
                item => item.GetProperty("id").GetGuid() == suspicious.ExpedienteId);
            var normalIndex = Array.FindIndex(
                items,
                item => item.GetProperty("id").GetGuid() == normal.ExpedienteId);
            Assert.True(suspiciousIndex >= 0 && normalIndex >= 0);
            Assert.True(suspiciousIndex < normalIndex);
            Assert.True(items[suspiciousIndex].GetProperty("esSospechoso").GetBoolean());
            Assert.False(items[normalIndex].GetProperty("esSospechoso").GetBoolean());
            var listState = items[suspiciousIndex].GetProperty("estado").GetString();

            using var detail = await admin.GetAsync(
                $"/api/v1/admin/expedientes/{suspicious.ExpedienteId}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var detailJson = await ReadJsonAsync(detail);
            Assert.Equal(listState, detailJson.RootElement.GetProperty("estado").GetString());
            Assert.True(detailJson.RootElement.GetProperty("esSospechoso").GetBoolean());
            Assert.Contains(
                detailJson.RootElement.GetProperty("senalesRiesgo").EnumerateArray(),
                signal => signal.GetString()!.Contains("photoshop", StringComparison.OrdinalIgnoreCase));

            using var forbiddenDetail = await suspiciousClient.GetAsync(
                $"/api/v1/admin/expedientes/{suspicious.ExpedienteId}");
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDetail.StatusCode);
        }
        finally
        {
            _factory.DocumentAnalyzer.ResultFactory = null;
        }
    }

    [Fact]
    public async Task EnlaceDeDocumento_EsTemporalExigeAdminYAuditaElAcceso()
    {
        var (account, client) = await CreateApplicantAsync();
        using var clientScope = client;
        await SeedFirstPublicationAsync(account);
        var uploaded = await UploadBothAsync(client);

        using var admin = await CreateAdminClientAsync();
        using var urlResponse = await admin.GetAsync(
            $"/api/v1/admin/expedientes/{uploaded.ExpedienteId}/documentos/ine");
        Assert.Equal(HttpStatusCode.OK, urlResponse.StatusCode);
        using var urlJson = await ReadJsonAsync(urlResponse);
        var url = urlJson.RootElement.GetProperty("url").GetString();
        Assert.NotNull(url);
        Assert.StartsWith("/api/v1/expedientes/documentos/", url, StringComparison.Ordinal);
        Assert.DoesNotContain("cloudinary", url, StringComparison.OrdinalIgnoreCase);

        using var anonymous = _factory.CreateClient();
        using var anonymousRead = await anonymous.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousRead.StatusCode);

        using var applicantRead = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, applicantRead.StatusCode);

        using var read = await admin.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("image/png", read.Content.Headers.ContentType?.MediaType);
        Assert.True(read.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", read.Headers.GetValues("X-Content-Type-Options").Single());

        var tampered = url[..^1] + (url[^1] == 'A' ? 'B' : 'A');
        using var tamperedRead = await admin.GetAsync(tampered);
        Assert.Equal(HttpStatusCode.NotFound, tamperedRead.StatusCode);

        var audit = _factory.VerificationAudit.All
            .Where(entry => entry.ExpedienteId == uploaded.ExpedienteId)
            .ToArray();
        Assert.Contains(audit, entry =>
            entry.Action == VerificationAuditActions.DocumentViewed &&
            entry.ActorAccountId == UniHomeApiFactory.TestAdminAccountId &&
            entry.DocumentType == TipoDocumentoVerificacion.Ine);
    }

    [Fact]
    public async Task CargasInvalidas_DebenRechazarseYLaRepeticionSerIdempotente()
    {
        var (account, client) = await CreateApplicantAsync();
        using var clientScope = client;
        await SeedFirstPublicationAsync(account);
        var png = CreatePng(140, 90);

        using var fakePdf = new MultipartFormDataContent();
        var fakeFile = new ByteArrayContent(png);
        fakeFile.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        fakePdf.Add(fakeFile, "Archivo", "ine.pdf");
        using var fakeResponse = await client.PostAsync(
            "/api/v1/expedientes/mi-expediente/ine",
            fakePdf);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, fakeResponse.StatusCode);

        using var missing = new MultipartFormDataContent();
        using var missingResponse = await client.PostAsync(
            "/api/v1/expedientes/mi-expediente/ine",
            missing);
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);

        using var urlencoded = new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("Archivo", "texto")]);
        using var unsupported = await client.PostAsync(
            "/api/v1/expedientes/mi-expediente/ine",
            urlencoded);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);

        var oversized = new byte[10 * 1024 * 1024 + 1];
        using var tooLarge = await UploadAsync(
            client,
            "mi-expediente/ine",
            oversized,
            "ine.png",
            "image/png");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);

        using var first = await UploadAsync(
            client,
            "mi-expediente/ine",
            png,
            "ine.png",
            "image/png");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var storedBefore = _factory.VerificationDocuments.All.Count;
        var auditBefore = _factory.VerificationAudit.All.Count;

        using var repeated = await UploadAsync(
            client,
            "mi-expediente/ine",
            png,
            "ine.png",
            "image/png");
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(storedBefore, _factory.VerificationDocuments.All.Count);
        Assert.Equal(auditBefore, _factory.VerificationAudit.All.Count);

        await UploadDocumentAndReadAsync(
            client,
            "mi-expediente/comprobante",
            CreatePng(141, 91),
            "predial.png",
            "image/png");

        using var replacement = await UploadAsync(
            client,
            "mi-expediente/ine",
            CreatePng(142, 92),
            "ine2.png",
            "image/png");
        Assert.Equal(HttpStatusCode.Conflict, replacement.StatusCode);
    }

    [Fact]
    public async Task EnvioSinDocumentosTerminosOFoto_DebeResponder422()
    {
        var (withoutDocuments, withoutDocumentsClient) = await CreateApplicantAsync();
        using var withoutDocumentsScope = withoutDocumentsClient;
        await SeedFirstPublicationAsync(withoutDocuments);

        using var noDocuments = await withoutDocumentsClient.PostAsync(
            "/api/v1/publicaciones/primera/enviar",
            null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noDocuments.StatusCode);

        var (withoutPhoto, withoutPhotoClient) = await CreateApplicantAsync();
        using var withoutPhotoScope = withoutPhotoClient;
        await SeedFirstPublicationAsync(withoutPhoto, addPhoto: false);
        await UploadBothAsync(withoutPhotoClient);

        using var noPhoto = await withoutPhotoClient.PostAsync(
            "/api/v1/publicaciones/primera/enviar",
            null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noPhoto.StatusCode);
    }

    [Fact]
    public async Task CargasSinSesionSinUnidadYMenor_DebenFallarConElCodigoCorrecto()
    {
        using var anonymous = _factory.CreateClient();
        using var noSession = await UploadAsync(
            anonymous,
            "mi-expediente/ine",
            CreatePng(160, 120),
            "ine.png",
            "image/png");
        Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);

        var (_, withoutUnit) = await CreateApplicantAsync();
        using var withoutUnitScope = withoutUnit;
        using var noUnit = await UploadAsync(
            withoutUnit,
            "mi-expediente/ine",
            CreatePng(161, 121),
            "ine.png",
            "image/png");
        Assert.Equal(HttpStatusCode.NotFound, noUnit.StatusCode);

        var (_, minor) = await CreateApplicantAsync(new DateOnly(2010, 1, 1));
        using var minorScope = minor;
        using var minorUpload = await UploadAsync(
            minor,
            "mi-expediente/ine",
            CreatePng(162, 122),
            "ine.png",
            "image/png");
        Assert.Equal(HttpStatusCode.Forbidden, minorUpload.StatusCode);
    }

    [Fact]
    public async Task Aprobar_DebePublicarUnidadCambiarPerfilYSerTerminal()
    {
        var (account, client) = await CreateApplicantAsync();
        using var clientScope = client;
        var publication = await SeedFirstPublicationAsync(account);
        var uploaded = await UploadBothAsync(client);
        using var admin = await CreateAdminClientAsync();

        using var profileInReview = await client.GetAsync("/api/v1/perfil/mi-perfil");
        using var profileInReviewJson = await ReadJsonAsync(profileInReview);
        Assert.Equal(
            "PublicacionEnRevision",
            profileInReviewJson.RootElement.GetProperty("etiquetaCuenta").GetString());
        Assert.False(profileInReviewJson.RootElement.GetProperty("puedePublicar").GetBoolean());
        Assert.Equal(
            "EnRevision",
            profileInReviewJson.RootElement.GetProperty("estadoPrimeraPublicacion").GetString());

        using var approve = await admin.PostAsync(
            $"/api/v1/admin/expedientes/{uploaded.ExpedienteId}/aprobar",
            null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        using var approveJson = await ReadJsonAsync(approve);
        Assert.Equal("Aprobado", approveJson.RootElement.GetProperty("estado").GetString());

        using var ownStatus = await client.GetAsync("/api/v1/expedientes/mi-expediente");
        Assert.Equal(HttpStatusCode.OK, ownStatus.StatusCode);
        using var ownStatusJson = await ReadJsonAsync(ownStatus);
        Assert.Equal("Aprobado", ownStatusJson.RootElement.GetProperty("estado").GetString());
        Assert.False(ownStatusJson.RootElement.GetProperty("decisionPendiente").GetBoolean());
        var decision = ownStatusJson.RootElement.GetProperty("decision");
        Assert.Equal("Aprobado", decision.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, decision.GetProperty("motivo").ValueKind);
        Assert.False(ownStatusJson.RootElement.TryGetProperty("esSospechoso", out _));
        Assert.False(ownStatusJson.RootElement.TryGetProperty("nombreOcrIne", out _));

        using var profile = await client.GetAsync("/api/v1/perfil/mi-perfil");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var profileJson = await ReadJsonAsync(profile);
        Assert.Equal("ConEspaciosPublicados", profileJson.RootElement.GetProperty("etiquetaCuenta").GetString());
        Assert.False(profileJson.RootElement.GetProperty("puedePublicar").GetBoolean());
        Assert.Equal(
            "Aprobada",
            profileJson.RootElement.GetProperty("estadoPrimeraPublicacion").GetString());

        using var publicList = await client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={publication.UniversityId}");
        Assert.Equal(HttpStatusCode.OK, publicList.StatusCode);
        using var publicListJson = await ReadJsonAsync(publicList);
        var card = Assert.Single(publicListJson.RootElement.EnumerateArray());
        Assert.Equal(publication.UnitId, card.GetProperty("id").GetGuid());
        Assert.True(card.GetProperty("arrendadorVerificado").GetBoolean());

        using var detail = await client.GetAsync($"/api/v1/unidades/{publication.UnitId}/ficha");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailJson = await ReadJsonAsync(detail);
        Assert.True(detailJson.RootElement.GetProperty("arrendadorVerificado").GetBoolean());

        using var mapAfterApproval = await client.GetAsync(
            "/api/v1/mi-publicacion/estado-mapa");
        Assert.Equal(HttpStatusCode.NotFound, mapAfterApproval.StatusCode);

        using var duplicate = await admin.PostAsync(
            $"/api/v1/admin/expedientes/{uploaded.ExpedienteId}/aprobar",
            null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var rejectAfter = await admin.PostAsJsonAsync(
            $"/api/v1/admin/expedientes/{uploaded.ExpedienteId}/rechazar",
            new { motivo = "Ya decidido" });
        Assert.Equal(HttpStatusCode.Conflict, rejectAfter.StatusCode);
    }

    [Fact]
    public async Task Rechazar_ExigeMotivoYNoPublicaLaUnidad()
    {
        var (account, client) = await CreateApplicantAsync();
        using var clientScope = client;
        var publication = await SeedFirstPublicationAsync(account);
        var uploaded = await UploadBothAsync(client);
        using var admin = await CreateAdminClientAsync();

        using var missingReason = await admin.PostAsJsonAsync(
            $"/api/v1/admin/expedientes/{uploaded.ExpedienteId}/rechazar",
            new { motivo = " " });
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);

        using var reject = await admin.PostAsJsonAsync(
            $"/api/v1/admin/expedientes/{uploaded.ExpedienteId}/rechazar",
            new { motivo = "El comprobante no es legible" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        using var rejectJson = await ReadJsonAsync(reject);
        Assert.Equal("Rechazado", rejectJson.RootElement.GetProperty("estado").GetString());
        Assert.Equal(
            "El comprobante no es legible",
            rejectJson.RootElement.GetProperty("motivoRechazo").GetString());

        using var ownStatus = await client.GetAsync("/api/v1/expedientes/mi-expediente");
        using var ownStatusJson = await ReadJsonAsync(ownStatus);
        Assert.Equal("Rechazado", ownStatusJson.RootElement.GetProperty("estado").GetString());
        Assert.Equal(
            "El comprobante no es legible",
            ownStatusJson.RootElement.GetProperty("decision").GetProperty("motivo").GetString());

        using var publicList = await client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={publication.UniversityId}");
        using var publicListJson = await ReadJsonAsync(publicList);
        Assert.Empty(publicListJson.RootElement.EnumerateArray());

        using var profileAfterReject = await client.GetAsync("/api/v1/perfil/mi-perfil");
        using var profileAfterRejectJson = await ReadJsonAsync(profileAfterReject);
        Assert.Equal(
            "Rechazada",
            profileAfterRejectJson.RootElement.GetProperty("estadoPrimeraPublicacion").GetString());
        Assert.Equal(
            "UsuarioUniHome",
            profileAfterRejectJson.RootElement.GetProperty("etiquetaCuenta").GetString());
        Assert.False(
            profileAfterRejectJson.RootElement.GetProperty("puedePublicar").GetBoolean());

        using var approveAfter = await admin.PostAsync(
            $"/api/v1/admin/expedientes/{uploaded.ExpedienteId}/aprobar",
            null);
        Assert.Equal(HttpStatusCode.Conflict, approveAfter.StatusCode);
    }

    [Fact]
    public async Task Decision_RequiereAdminYNoAceptaExpedienteInexistente()
    {
        var (_, client) = await CreateApplicantAsync();
        using var clientScope = client;
        var id = Guid.NewGuid();

        using var anonymous = _factory.CreateClient();
        using var anonymousApprove = await anonymous.PostAsync(
            $"/api/v1/admin/expedientes/{id}/aprobar",
            null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousApprove.StatusCode);

        using var applicantApprove = await client.PostAsync(
            $"/api/v1/admin/expedientes/{id}/aprobar",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, applicantApprove.StatusCode);

        using var admin = await CreateAdminClientAsync();
        using var missing = await admin.PostAsync(
            $"/api/v1/admin/expedientes/{id}/aprobar",
            null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task AprobacionesConcurrentes_SoloUnaDebeGanar()
    {
        var (account, client) = await CreateApplicantAsync();
        using var clientScope = client;
        await SeedFirstPublicationAsync(account);
        var uploaded = await UploadBothAsync(client);
        using var firstAdmin = await CreateAdminClientAsync();
        using var secondAdmin = await CreateAdminClientAsync();
        var url = $"/api/v1/admin/expedientes/{uploaded.ExpedienteId}/aprobar";

        var responses = await Task.WhenAll(
            firstAdmin.PostAsync(url, null),
            secondAdmin.PostAsync(url, null));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        using var ownStatus = await client.GetAsync("/api/v1/expedientes/mi-expediente");
        using var ownStatusJson = await ReadJsonAsync(ownStatus);
        Assert.Equal("Aprobado", ownStatusJson.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task ComparacionOcr_DebeMarcarDiferenciaSinDecidirNiFiltrarAlSolicitante()
    {
        var (account, client) = await CreateApplicantAsync();
        using var clientScope = client;
        var publication = await SeedFirstPublicationAsync(account);

        _factory.DocumentAnalyzer.ResultFactory = type => new DocumentAnalysisResult(
            type == TipoDocumentoVerificacion.Ine ? "ANA PEREZ" : "CARLOS RAMIREZ",
            EstadoAnalisisDocumento.Completado,
            EstadoAnalisisDocumento.Completado,
            EditionDetected: false,
            EditionSignals: [],
            ElaAvailable: true,
            MetadataAvailable: true,
            UnavailableDetail: null);

        try
        {
            var uploaded = await UploadBothAsync(client);
            using var admin = await CreateAdminClientAsync();
            using var detail = await admin.GetAsync(
                $"/api/v1/admin/expedientes/{uploaded.ExpedienteId}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var detailJson = await ReadJsonAsync(detail);
            Assert.Equal(
                "DiferenciaDetectada",
                detailJson.RootElement.GetProperty("resultadoComparacionNombres").GetString());
            Assert.True(detailJson.RootElement.GetProperty("esSospechoso").GetBoolean());

            using var own = await client.GetAsync("/api/v1/expedientes/mi-expediente");
            using var ownJson = await ReadJsonAsync(own);
            Assert.Equal("EnRevision", ownJson.RootElement.GetProperty("estado").GetString());
            Assert.False(ownJson.RootElement.TryGetProperty("resultadoComparacionNombres", out _));
            Assert.False(ownJson.RootElement.TryGetProperty("esSospechoso", out _));

            using var publicList = await client.GetAsync(
                $"/api/v1/exploracion/lista?universidadId={publication.UniversityId}");
            using var publicListJson = await ReadJsonAsync(publicList);
            Assert.Empty(publicListJson.RootElement.EnumerateArray());
        }
        finally
        {
            _factory.DocumentAnalyzer.ResultFactory = null;
        }
    }

    [Fact]
    public async Task MiEspacioEnRevision_DebeSerPrivadoYMostrarEtiqueta()
    {
        var (owner, ownerClient) = await CreateApplicantAsync();
        var (other, otherClient) = await CreateApplicantAsync();
        using var ownerScope = ownerClient;
        using var otherScope = otherClient;
        var publication = await SeedFirstPublicationAsync(owner);
        var otherPublication = await SeedFirstPublicationAsync(other);
        await UploadBothAsync(ownerClient);

        using var anonymous = _factory.CreateClient();
        using var anonymousMap = await anonymous.GetAsync("/api/v1/mi-publicacion/estado-mapa");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMap.StatusCode);

        using var ownerMap = await ownerClient.GetAsync("/api/v1/mi-publicacion/estado-mapa");
        Assert.Equal(HttpStatusCode.OK, ownerMap.StatusCode);
        using var ownerJson = await ReadJsonAsync(ownerMap);
        Assert.Equal(publication.UnitId, ownerJson.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Tu espacio · En revisión", ownerJson.RootElement.GetProperty("etiqueta").GetString());
        Assert.Equal("EnRevision", ownerJson.RootElement.GetProperty("estadoPublicacion").GetString());

        using var otherMap = await otherClient.GetAsync("/api/v1/mi-publicacion/estado-mapa");
        Assert.Equal(HttpStatusCode.NotFound, otherMap.StatusCode);

        using var publicList = await otherClient.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={publication.UniversityId}");
        using var publicListJson = await ReadJsonAsync(publicList);
        Assert.Empty(publicListJson.RootElement.EnumerateArray());
        Assert.NotEqual(Guid.Empty, otherPublication.UnitId);
    }

    [Fact]
    public async Task EstadoPropio_SinExpediente_DebeResponder404()
    {
        var (_, client) = await CreateApplicantAsync();
        using var clientScope = client;

        using var response = await client.GetAsync("/api/v1/expedientes/mi-expediente");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EnvioSinPublicacionYAdminConParametrosInvalidos_DebenFallarControladamente()
    {
        var (_, applicant) = await CreateApplicantAsync();
        using var applicantScope = applicant;
        using var noPublication = await applicant.PostAsync(
            "/api/v1/publicaciones/primera/enviar",
            null);
        Assert.Equal(HttpStatusCode.NotFound, noPublication.StatusCode);

        using var admin = await CreateAdminClientAsync();
        using var invalidPage = await admin.GetAsync(
            "/api/v1/admin/expedientes?page=0&pageSize=20");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);

        var missingId = Guid.NewGuid();
        using var missingDetail = await admin.GetAsync(
            $"/api/v1/admin/expedientes/{missingId}");
        Assert.Equal(HttpStatusCode.NotFound, missingDetail.StatusCode);

        using var missingDocument = await admin.GetAsync(
            $"/api/v1/admin/expedientes/{missingId}/documentos/ine");
        Assert.Equal(HttpStatusCode.NotFound, missingDocument.StatusCode);
    }

    private async Task<(Account Account, HttpClient Client)> CreateApplicantAsync(
        DateOnly? birthDate = null)
    {
        var account = await AuthTestHelpers.SeedAccountAsync(
            _factory,
            AuthTestHelpers.UniqueEmail(),
            birthDate: birthDate);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokenFactory.Create(account.Id, Guid.NewGuid()));
        return (account, client);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var existing = await _factory.Accounts.FindByIdAsync(UniHomeApiFactory.TestAdminAccountId);

        if (existing is null)
        {
            await AuthTestHelpers.SeedAccountAsync(
                _factory,
                AdminEmail,
                id: UniHomeApiFactory.TestAdminAccountId);
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokenFactory.Create(UniHomeApiFactory.TestAdminAccountId, Guid.NewGuid()));
        return client;
    }

    private async Task<SeededPublication> SeedFirstPublicationAsync(
        Account account,
        bool addPhoto = true)
    {
        var now = DateTimeOffset.UtcNow;
        var university = University.Create($"Universidad {Guid.NewGuid():N}", now);
        await _factory.Universities.AddAsync(university);

        var property = Property.Create(
            account.Id,
            $"Casa {Guid.NewGuid():N}",
            $"Calle {Guid.NewGuid():N}",
            "Centro",
            PropertyType.Casa,
            university.Id,
            19.4326,
            -99.1332,
            null,
            null,
            now);
        await _factory.Properties.AddAsync(property);

        var unit = Unit.Create(
            property.Id,
            account.Id,
            university.Id,
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
            now);

        if (addPhoto)
        {
            unit.AppendPhotos(
            [
                new UnitPhoto(
                    $"photo-{Guid.NewGuid():N}",
                    "image/png",
                    10,
                    new string('A', 64),
                    0,
                    true,
                    now)
            ]);
        }

        await _factory.Units.AddAsync(unit);
        await _factory.Terms.AddAsync(TermsAcceptance.Create(account.Id, "2026-11", now));

        return new SeededPublication(unit.Id, university.Id);
    }

    private static async Task<UploadedExpediente> UploadBothAsync(HttpClient client)
    {
        var ine = await UploadDocumentAndReadAsync(
            client,
            "mi-expediente/ine",
            CreatePng(150, 100),
            "ine.png",
            "image/png");
        await UploadDocumentAndReadAsync(
            client,
            "mi-expediente/comprobante",
            CreatePng(151, 101),
            "predial.png",
            "image/png");
        return ine;
    }

    private static async Task<UploadedExpediente> UploadDocumentAndReadAsync(
        HttpClient client,
        string path,
        byte[] content,
        string fileName,
        string contentType)
    {
        using var response = await UploadAsync(client, path, content, fileName, contentType);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return new UploadedExpediente(
            json.RootElement.GetProperty("id").GetGuid(),
            json.RootElement.GetProperty("folio").GetString()!);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string path,
        byte[] content,
        string fileName,
        string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "Archivo", fileName);
        return await client.PostAsync($"/api/v1/expedientes/{path}", form);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(
                    x,
                    y,
                    new SKColor(
                        (byte)(x % 256),
                        (byte)(y % 256),
                        (byte)((x + y) % 256)));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private sealed record SeededPublication(Guid UnitId, Guid UniversityId);

    private sealed record UploadedExpediente(Guid ExpedienteId, string Folio);
}
