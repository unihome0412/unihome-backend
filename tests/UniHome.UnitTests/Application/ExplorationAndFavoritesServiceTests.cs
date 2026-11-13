using UniHome.Application.Common;
using UniHome.Application.Exploration;
using UniHome.Application.Favorites;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ExplorationAndFavoritesServiceTests
{
    [Fact]
    public async Task Lista_Combinada_DebeFiltrarPorDistanciaPrecioOcupacionYPublico()
    {
        var context = BuildContext();
        var service = new ExploreUnitsService(
            context.Universities,
            context.Units,
            context.Properties,
            context.Favorites,
            new UnitPhotoUrlResolver(context.Storage));

        var result = await service.ListAsync(
            new ExplorationQuery(
                ExplorationTestData.UniversityId,
                DistanciaMaxKm: 2,
                PrecioMaxMxn: 4500,
                SoloIndividual: true,
                ParaQuien: null),
            accountId: null);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Single(result.Value!.Items);
        Assert.Equal(context.Available.Id, result.Value.Items[0].Id);
        Assert.Equal("a pie", result.Value.Items[0].TiempoTraslado);
        Assert.Equal(1, result.Value.Total);
    }

    [Fact]
    public async Task Mapa_NoIncluyeUnidadesNoPublicasNiSinCoordenadas()
    {
        var context = BuildContext();
        var service = new ExploreUnitsService(
            context.Universities,
            context.Units,
            context.Properties,
            context.Favorites,
            new UnitPhotoUrlResolver(context.Storage));

        var result = await service.MapAsync(new ExplorationQuery(
            ExplorationTestData.UniversityId,
            null,
            null,
            null,
            null));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.Count);
        Assert.DoesNotContain(result.Value, item => item.Id == context.Occupied.Id);
        Assert.DoesNotContain(result.Value, item => item.Id == context.InReview.Id);
    }

    [Fact]
    public async Task Tarjeta_ConSesion_DebeIncluirFavoritoYSinPii()
    {
        var context = BuildContext();
        var service = new GetUnitCardService(
            context.Units,
            context.Properties,
            context.Universities,
            context.Favorites,
            new UnitPhotoUrlResolver(context.Storage));
        await context.Favorites.AddAsync(
            context.AccountId,
            context.Available.Id,
            ExplorationTestData.Now);

        var result = await service.GetAsync(context.Available.Id, context.AccountId);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.True(result.Value!.EsFavorito);
        Assert.NotNull(result.Value.FotoPrincipalUrl);
        Assert.Equal(0.5m, result.Value.DistanciaKm);
        Assert.Equal("a pie", result.Value.TiempoTraslado);
    }

    [Fact]
    public async Task Tarjeta_NoPublica_DebeDevolver404()
    {
        var context = BuildContext();
        var service = new GetUnitCardService(
            context.Units,
            context.Properties,
            context.Universities,
            context.Favorites,
            new UnitPhotoUrlResolver(context.Storage));

        var result = await service.GetAsync(context.InReview.Id, null);
        var occupied = await service.GetAsync(context.Occupied.Id, null);

        Assert.Equal(OperationStatus.NotFound, result.Status);
        Assert.Equal(OperationStatus.NotFound, occupied.Status);
    }

    [Fact]
    public async Task Ficha_DebeSepararServiciosYGaleria()
    {
        var context = BuildContext();
        var service = new GetUnitDetailService(
            context.Units,
            context.Properties,
            context.Universities,
            context.Favorites,
            new UnitPhotoUrlResolver(context.Storage));

        var result = await service.GetAsync(context.Available.Id, context.AccountId);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.GaleriaFotos.Count);
        Assert.Single(result.Value.ServiciosIncluidos);
        Assert.Single(result.Value.ServiciosAparte);
        Assert.Equal(300, result.Value.ServiciosAparte[0].MontoMensualMxn);
        Assert.Equal(PropertyType.Casa, result.Value.TipoPropiedad);
    }

    [Fact]
    public async Task Favoritos_SonIdempotentesYConservanUnidadNoPublica()
    {
        var context = BuildContext();
        var add = new AddFavoriteService(
            context.Units,
            context.Favorites,
            new FixedClock(ExplorationTestData.Now));
        var list = new ListFavoritesService(
            context.Favorites,
            context.Units,
            context.Properties,
            context.Universities,
            new UnitPhotoUrlResolver(context.Storage));
        var remove = new RemoveFavoriteService(context.Favorites);

        await add.AddAsync(context.AccountId, context.InReview.Id);
        await add.AddAsync(context.AccountId, context.InReview.Id);
        var listed = await list.ListAsync(context.AccountId, 1, 20);

        Assert.Equal(OperationStatus.Success, listed.Status);
        Assert.Single(listed.Value!.Items);
        Assert.Equal("EnRevision", listed.Value.Items[0].Tarjeta.EstadoActual);
        Assert.True(listed.Value.Items[0].Tarjeta.EsFavorito);

        var otherUser = await list.ListAsync(Guid.NewGuid(), 1, 20);
        Assert.Empty(otherUser.Value!.Items);

        await remove.RemoveAsync(context.AccountId, context.InReview.Id);
        await remove.RemoveAsync(context.AccountId, context.InReview.Id);
        var empty = await list.ListAsync(context.AccountId, 1, 20);

        Assert.Empty(empty.Value!.Items);
    }

    [Fact]
    public void UmbralDeTraslado_DebeSerInclusivoEnUnPuntoCinco()
    {
        Assert.Equal("a pie", ExplorationProjection.TravelMode(1.5m));
        Assert.Equal("en transporte", ExplorationProjection.TravelMode(1.51m));
        Assert.Null(ExplorationProjection.TravelMode(null));
    }

    private static Context BuildContext()
    {
        var accountId = Guid.NewGuid();
        var properties = new FakePropertyRepository();
        var units = new FakeUnitRepository();
        var universities = ExplorationTestData.UniversityRepository();
        var favorites = new FakeUnitFavoriteRepository();
        var storage = new FakeUnitPhotoStorage();

        var nearProperty = ExplorationTestData.CreateProperty(
            accountId,
            "Cerca",
            19.0045,
            -99.0);
        var farProperty = ExplorationTestData.CreateProperty(
            accountId,
            "Lejos",
            19.027,
            -99.0);
        properties.Items.Add(nearProperty);
        properties.Items.Add(farProperty);

        var available = ExplorationTestData.CreateUnit(accountId, nearProperty.Id, 3000);
        var occupied = ExplorationTestData.CreateUnit(
            accountId,
            nearProperty.Id,
            3100,
            availability: UnitAvailability.Ocupado);
        var inReview = ExplorationTestData.CreateUnit(
            accountId,
            nearProperty.Id,
            3200,
            state: PublicationState.EnRevision);
        var farShared = ExplorationTestData.CreateUnit(
            accountId,
            farProperty.Id,
            5000,
            occupancy: Occupancy.Compartida,
            forWhom: ForWhom.SoloMujeres);
        units.Items.AddRange([available, occupied, inReview, farShared]);

        return new Context(
            accountId,
            properties,
            units,
            universities,
            favorites,
            storage,
            available,
            occupied,
            inReview);
    }

    private sealed record Context(
        Guid AccountId,
        FakePropertyRepository Properties,
        FakeUnitRepository Units,
        FakeUniversityRepository Universities,
        FakeUnitFavoriteRepository Favorites,
        FakeUnitPhotoStorage Storage,
        Unit Available,
        Unit Occupied,
        Unit InReview);
}
