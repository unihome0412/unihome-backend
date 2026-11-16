using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SkiaSharp;
using UniHome.Domain.Accounts;
using UniHome.Domain.Properties;
using UniHome.Domain.Publications;
using UniHome.Domain.Units;
using UniHome.Domain.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

public sealed class RealOcrVerificationEndpointTests
    : IClassFixture<RealOcrVerificationApiFactory>
{
    private readonly RealOcrVerificationApiFactory _factory;

    public RealOcrVerificationEndpointTests(RealOcrVerificationApiFactory factory)
    {
        _factory = factory;
    }

    [RealOcrIntegrationFact]
    public async Task IneReal_DebeExtraerNombreConTesseractYMostrarloEnElExpediente()
    {
        var account = await AuthTestHelpers.SeedAccountAsync(
            _factory,
            AuthTestHelpers.UniqueEmail());
        var now = DateTimeOffset.UtcNow;
        var university = University.Create($"Universidad OCR {Guid.NewGuid():N}", now);
        await _factory.Universities.AddAsync(university);
        var property = Property.Create(
            account.Id,
            $"Casa OCR {Guid.NewGuid():N}",
            $"Calle OCR {Guid.NewGuid():N}",
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
        await _factory.Units.AddAsync(unit);
        await _factory.Terms.AddAsync(TermsAcceptance.Create(account.Id, "2026-11", now));

        using var applicant = _factory.CreateClient();
        applicant.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokenFactory.Create(account.Id, Guid.NewGuid()));

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(CreateIneImage());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "Archivo", "ine.png");
        using var upload = await applicant.PostAsync(
            "/api/v1/expedientes/mi-expediente/ine",
            form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var expedienteId = uploadJson.RootElement.GetProperty("id").GetGuid();

        var admin = await _factory.Accounts.FindByIdAsync(UniHomeApiFactory.TestAdminAccountId);

        if (admin is null)
        {
            await AuthTestHelpers.SeedAccountAsync(
                _factory,
                "real.ocr.admin@uni.edu",
                id: UniHomeApiFactory.TestAdminAccountId);
        }

        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokenFactory.Create(UniHomeApiFactory.TestAdminAccountId, Guid.NewGuid()));
        using var detail = await adminClient.GetAsync($"/api/v1/admin/expedientes/{expedienteId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        var name = detailJson.RootElement.GetProperty("nombreOcrIne").GetString();
        Assert.NotNull(name);
        Assert.Contains("ANA", name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PEREZ", name, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            detailJson.RootElement.GetProperty("indicadorEdicion")
                .GetProperty("elaDisponible").GetBoolean());
    }

    private static byte[] CreateIneImage()
    {
        using var bitmap = new SKBitmap(1000, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var font = new SKFont(SKTypeface.Default, 44);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        canvas.DrawText("NOMBRE: ANA PEREZ LOPEZ", 50, 120, SKTextAlign.Left, font, paint);
        canvas.DrawText("INSTITUTO NACIONAL ELECTORAL", 50, 220, SKTextAlign.Left, font, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
