using UniHome.Domain.Chat;

namespace UniHome.UnitTests.Domain;

public sealed class ChatDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Conversation_Start_QuedaSinMensajes()
    {
        var unitId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var conversation = Conversation.Start(unitId, ownerId, requesterId, Now);

        Assert.Equal(unitId, conversation.UnitId);
        Assert.Equal(ownerId, conversation.OwnerAccountId);
        Assert.Equal(requesterId, conversation.RequesterAccountId);
        Assert.Equal(Now, conversation.CreatedAt);
        Assert.Null(conversation.LastMessageAt);
        Assert.Null(conversation.LastMessagePreview);
    }

    [Fact]
    public void Conversation_Start_ConUnoMismo_DebeRechazarse()
    {
        var unitId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() =>
            Conversation.Start(unitId, accountId, accountId, Now));
    }

    [Fact]
    public void Conversation_Start_SinUnidadPropietarioOSolicitante_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() =>
            Conversation.Start(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Now));
        Assert.Throws<ArgumentException>(() =>
            Conversation.Start(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Now));
        Assert.Throws<ArgumentException>(() =>
            Conversation.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Now));
    }

    [Fact]
    public void Conversation_ComputeId_EsEstableParaLaMismaTerna()
    {
        var unitId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var first = Conversation.ComputeId(unitId, ownerId, requesterId);
        var second = Conversation.ComputeId(unitId, ownerId, requesterId);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Conversation_ComputeId_CambiaSiCambiaCualquierComponente()
    {
        var unitId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var baseline = Conversation.ComputeId(unitId, ownerId, requesterId);

        Assert.NotEqual(baseline, Conversation.ComputeId(Guid.NewGuid(), ownerId, requesterId));
        Assert.NotEqual(baseline, Conversation.ComputeId(unitId, Guid.NewGuid(), requesterId));
        Assert.NotEqual(baseline, Conversation.ComputeId(unitId, ownerId, Guid.NewGuid()));
        // Propietario y solicitante son roles distintos: invertirlos cambia la conversación.
        Assert.NotEqual(baseline, Conversation.ComputeId(unitId, requesterId, ownerId));
    }

    [Fact]
    public void Conversation_HasParticipantYOtherParticipant_ResuelvenAmbosRoles()
    {
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var conversation = Conversation.Start(Guid.NewGuid(), ownerId, requesterId, Now);

        Assert.True(conversation.HasParticipant(ownerId));
        Assert.True(conversation.HasParticipant(requesterId));
        Assert.False(conversation.HasParticipant(Guid.NewGuid()));
        Assert.Equal(requesterId, conversation.OtherParticipant(ownerId));
        Assert.Equal(ownerId, conversation.OtherParticipant(requesterId));
        Assert.Throws<InvalidOperationException>(() => conversation.OtherParticipant(Guid.NewGuid()));
    }

    [Fact]
    public void Conversation_RegisterMessage_ActualizaResumen()
    {
        var conversation = Conversation.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        var sentAt = Now.AddMinutes(5);

        conversation.RegisterMessage("Hola, ¿sigue disponible?", sentAt);

        Assert.Equal(sentAt, conversation.LastMessageAt);
        Assert.Equal("Hola, ¿sigue disponible?", conversation.LastMessagePreview);
    }

    [Fact]
    public void Message_Create_RecortaEspaciosYConservaDatos()
    {
        var conversationId = Guid.NewGuid();
        var senderId = Guid.NewGuid();

        var message = Message.Create(conversationId, senderId, "  Hola  ", Now);

        Assert.Equal(conversationId, message.ConversationId);
        Assert.Equal(senderId, message.SenderAccountId);
        Assert.Equal("Hola", message.Body);
        Assert.Equal(Now, message.SentAt);
        Assert.NotEqual(Guid.Empty, message.Id);
    }

    [Fact]
    public void Message_Create_VacioOSoloEspacios_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() =>
            Message.Create(Guid.NewGuid(), Guid.NewGuid(), "   ", Now));
    }

    [Fact]
    public void Message_Create_ExcedeLongitudMaxima_DebeRechazarse()
    {
        var tooLong = new string('a', Message.MaxBodyLength + 1);

        Assert.Throws<ArgumentException>(() =>
            Message.Create(Guid.NewGuid(), Guid.NewGuid(), tooLong, Now));
    }

    [Fact]
    public void Message_Create_EnElLimiteDeLongitud_DebeAceptarse()
    {
        var atLimit = new string('a', Message.MaxBodyLength);

        var message = Message.Create(Guid.NewGuid(), Guid.NewGuid(), atLimit, Now);

        Assert.Equal(Message.MaxBodyLength, message.Body.Length);
    }

    [Fact]
    public void Message_Create_SinConversacionOSinRemitente_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() =>
            Message.Create(Guid.Empty, Guid.NewGuid(), "Hola", Now));
        Assert.Throws<ArgumentException>(() =>
            Message.Create(Guid.NewGuid(), Guid.Empty, "Hola", Now));
    }
}
