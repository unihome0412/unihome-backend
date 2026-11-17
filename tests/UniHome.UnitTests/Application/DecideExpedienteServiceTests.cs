using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Application.Verification.Admin;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Domain.Verification;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class DecideExpedienteServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Aprobar_DebeRegistrarDecisionAuditoriaYPublicacionVisible()
    {
        var context = await CreateContextAsync();
        var admin = Guid.NewGuid();

        var result = await context.Service.ApproveAsync(admin, context.Expediente.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        var stored = await context.Repository.FindByIdAsync(context.Expediente.Id);
        Assert.Equal(EstadoExpediente.Aprobado, stored!.State);
        Assert.Equal(admin, stored.Decision!.ActorAccountId);
        Assert.Equal(PublicationState.Aprobada, context.Units.Items.Single().PublicationState);
        Assert.Contains(context.Audit.Entries, entry =>
            entry.Action == VerificationAuditActions.ExpedienteApproved &&
            entry.ActorAccountId == admin);
    }

    [Fact]
    public async Task Rechazar_DebeGuardarMotivoYNoPublicar()
    {
        var context = await CreateContextAsync();
        var admin = Guid.NewGuid();

        var result = await context.Service.RejectAsync(
            admin,
            context.Expediente.Id,
            "  Documento no coincide  ");

        Assert.Equal(OperationStatus.Success, result.Status);
        var stored = await context.Repository.FindByIdAsync(context.Expediente.Id);
        Assert.Equal(EstadoExpediente.Rechazado, stored!.State);
        Assert.Equal("Documento no coincide", stored.RejectionReason);
        Assert.Equal("Documento no coincide", stored.Decision!.Motivo);
        Assert.Equal(PublicationState.Rechazada, context.Units.Items.Single().PublicationState);
        Assert.Contains(context.Audit.Entries, entry =>
            entry.Action == VerificationAuditActions.ExpedienteRejected);
    }

    [Fact]
    public async Task RechazarSinMotivo_DebeFallarValidacionYSinCambiarEstado()
    {
        var context = await CreateContextAsync();

        var result = await context.Service.RejectAsync(
            Guid.NewGuid(),
            context.Expediente.Id,
            " ");

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        var stored = await context.Repository.FindByIdAsync(context.Expediente.Id);
        Assert.Equal(EstadoExpediente.EnRevision, stored!.State);
    }

    [Fact]
    public async Task DecisionDuplicada_DebeResponderConflict()
    {
        var context = await CreateContextAsync();
        var admin = Guid.NewGuid();
        await context.Service.ApproveAsync(admin, context.Expediente.Id);

        var approveAgain = await context.Service.ApproveAsync(admin, context.Expediente.Id);
        var rejectAfter = await context.Service.RejectAsync(
            admin,
            context.Expediente.Id,
            "Motivo");

        Assert.Equal(OperationStatus.Conflict, approveAgain.Status);
        Assert.Equal(OperationStatus.Conflict, rejectAfter.Status);
    }

    [Fact]
    public async Task ExpedientePendiente_NoPuedeAprobarse()
    {
        var context = await CreateContextAsync();
        var expediente = Expediente.Create(
            Guid.NewGuid(),
            "UH-2026-PENDIENTE01",
            Guid.NewGuid(),
            context.Expediente.PublicationId,
            context.Expediente.PropertyId,
            Now);
        await context.Repository.AddAsync(expediente);

        var result = await context.Service.ApproveAsync(Guid.NewGuid(), expediente.Id);

        Assert.Equal(OperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task Decision_ExpedienteInexistenteOAdminVacio_DebeRechazarse()
    {
        var context = await CreateContextAsync();

        var missing = await context.Service.ApproveAsync(Guid.NewGuid(), Guid.NewGuid());
        var noAdmin = await context.Service.ApproveAsync(Guid.Empty, context.Expediente.Id);

        Assert.Equal(OperationStatus.NotFound, missing.Status);
        Assert.Equal(OperationStatus.Forbidden, noAdmin.Status);
    }

    [Fact]
    public async Task UnidadAusenteOCasFallido_DebeResponderConflict()
    {
        var withoutUnit = await CreateContextAsync();
        withoutUnit.Units.Items.Clear();
        var missingUnit = await withoutUnit.Service.ApproveAsync(
            Guid.NewGuid(),
            withoutUnit.Expediente.Id);
        Assert.Equal(OperationStatus.Conflict, missingUnit.Status);

        var casFailure = await CreateContextAsync();
        casFailure.Repository.ReplaceResult = false;
        var conflict = await casFailure.Service.ApproveAsync(
            Guid.NewGuid(),
            casFailure.Expediente.Id);
        Assert.Equal(OperationStatus.Conflict, conflict.Status);
    }

    [Fact]
    public async Task UnidadYaAprobada_NoPuedeRecibirOtraDecision()
    {
        var context = await CreateContextAsync();
        var unit = context.Units.Items.Single();
        unit.TryApplyPublicationTransition(
            PublicationState.EnRevision,
            PublicationState.Aprobada);

        var result = await context.Service.ApproveAsync(
            Guid.NewGuid(),
            context.Expediente.Id);

        Assert.Equal(OperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task FalloDeAuditoria_DebeCompensarDecisionYPublicacion()
    {
        var context = await CreateContextAsync();
        context.Audit.ThrowOnAppend = true;

        await Assert.ThrowsAsync<UniHome.Application.Identity.Exceptions.StorageUnavailableException>(
            () => context.Service.ApproveAsync(Guid.NewGuid(), context.Expediente.Id));

        var stored = await context.Repository.FindByIdAsync(context.Expediente.Id);
        Assert.Equal(EstadoExpediente.EnRevision, stored!.State);
        Assert.Null(stored.Decision);
        Assert.Equal(PublicationState.EnRevision, context.Units.Items.Single().PublicationState);
    }

    [Fact]
    public async Task EstadoPropio_IncluyeDecisionYNoExponeIndicadoresInternos()
    {
        var context = await CreateContextAsync();
        await context.Service.RejectAsync(
            Guid.NewGuid(),
            context.Expediente.Id,
            "Documento ilegible");

        var view = await new GetMyExpedienteService(context.Repository)
            .GetAsync(context.Expediente.OwnerAccountId);

        Assert.Equal(OperationStatus.Success, view.Status);
        Assert.Equal(EstadoExpediente.Rechazado, view.Value!.Estado);
        Assert.Equal("Documento ilegible", view.Value.Decision!.Motivo);
        Assert.False(view.Value.DecisionPendiente);
    }

    private static async Task<Context> CreateContextAsync()
    {
        var owner = Guid.NewGuid();
        var unit = Unit.Restore(
            Guid.NewGuid(),
            Guid.NewGuid(),
            owner,
            Guid.NewGuid(),
            UnitType.Habitacion,
            Occupancy.Individual,
            1,
            3000,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            [],
            null,
            null,
            null,
            [],
            [],
            PublicationState.EnRevision,
            Now.AddDays(-1));
        var units = new FakeUnitRepository();
        units.Items.Add(unit);

        var expediente = Expediente.Create(
            Guid.NewGuid(),
            "UH-2026-ABCDEF123456",
            owner,
            unit.Id,
            unit.PropertyId,
            Now.AddDays(-1));
        var ine = DocumentoVerificacion.Create(
                TipoDocumentoVerificacion.Ine,
                $"doc-{Guid.NewGuid():N}",
                "ine.png",
                "image/png",
                10,
                new string('A', 64),
                1,
                Now)
            .WithAnalysis(
                "ANA PEREZ",
                EstadoAnalisisDocumento.Completado,
                EstadoAnalisisDocumento.Completado,
                false,
                true,
                true,
                [],
                null);
        var proof = DocumentoVerificacion.Create(
                TipoDocumentoVerificacion.Comprobante,
                $"doc-{Guid.NewGuid():N}",
                "predial.png",
                "image/png",
                10,
                new string('B', 64),
                1,
                Now)
            .WithAnalysis(
                "ANA PEREZ",
                EstadoAnalisisDocumento.Completado,
                EstadoAnalisisDocumento.Completado,
                false,
                true,
                true,
                [],
                null);
        expediente.AddOrReplaceDocument(ine, Now);
        expediente.AddOrReplaceDocument(proof, Now);
        var repository = new FakeVerificationRepository();
        await repository.AddAsync(expediente);
        var audit = new FakeVerificationAuditRepository();
        var service = new DecideExpedienteService(
            repository,
            units,
            audit,
            new FixedClock(Now));

        return new Context(expediente, repository, units, audit, service);
    }

    private sealed record Context(
        Expediente Expediente,
        FakeVerificationRepository Repository,
        FakeUnitRepository Units,
        FakeVerificationAuditRepository Audit,
        DecideExpedienteService Service);
}
