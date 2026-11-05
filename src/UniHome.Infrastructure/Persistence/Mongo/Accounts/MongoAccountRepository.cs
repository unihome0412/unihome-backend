using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Domain.Accounts;

namespace UniHome.Infrastructure.Persistence.Mongo.Accounts;

/// <summary>
/// Adaptador MongoDB del puerto de cuentas. El índice único lo crea la migración
/// versionada, no el proceso de la API; aquí solo se traduce el error de clave
/// duplicada a un conflicto.
/// </summary>
internal sealed class MongoAccountRepository : IAccountRepository
{
    private const int DuplicateKeyCode = 11000;

    private static readonly string BirthDateFormat = "yyyy-MM-dd";

    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoAccountRepository> _logger;

    public MongoAccountRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoAccountRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(ToDocument(account), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoWriteException exception) when (IsDuplicateKey(exception))
        {
            throw new DuplicateEmailException();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<AccountDocument>.Filter.Eq(
                document => document.Id,
                id.ToString("N", CultureInfo.InvariantCulture));

            var document = await collection
                .Find(filter)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return document is null ? null : ToDomain(document);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<Account?> FindByEmailAsync(
        EmailAddress email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<AccountDocument>.Filter.Eq(
                document => document.NormalizedEmail,
                email.Value);

            var document = await collection
                .Find(filter)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return document is null ? null : ToDomain(document);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<AccountDocument>.Filter.Eq(
                document => document.Id,
                account.Id.ToString("N", CultureInfo.InvariantCulture));

            var result = await collection
                .ReplaceOneAsync(
                    filter,
                    ToDocument(account),
                    options: new ReplaceOptions { IsUpsert = false },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (result.MatchedCount == 0)
            {
                throw new AccountNotFoundException();
            }
        }
        catch (MongoWriteException exception) when (IsDuplicateKey(exception))
        {
            throw new DuplicateEmailException();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<AccountDocument>> GetCollectionAsync(CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<AccountDocument>(AccountIndexes.CollectionName);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static bool IsDuplicateKey(MongoWriteException exception) =>
        exception.WriteError?.Code == DuplicateKeyCode;

    private static AccountDocument ToDocument(Account account) =>
        new()
        {
            Id = account.Id.ToString("N", CultureInfo.InvariantCulture),
            FullName = account.FullName,
            NormalizedEmail = account.Email.Value,
            BirthDate = account.BirthDate.ToString(BirthDateFormat, CultureInfo.InvariantCulture),
            Gender = account.Gender.ToString(),
            PasswordHash = account.PasswordHash,
            PrivacyAcceptance = new PrivacyAcceptanceDocument
            {
                Version = account.PrivacyAcceptance.Version,
                AcceptedAtUtc = account.PrivacyAcceptance.AcceptedAt.UtcDateTime
            },
            Status = account.Status.ToString(),
            EmailConfirmed = account.IsEmailConfirmed,
            CreatedAtUtc = account.CreatedAt.UtcDateTime,
            EmailConfirmedAtUtc = account.EmailConfirmedAt?.UtcDateTime,
            EmailVerification = account.EmailVerification is null
                ? null
                : new VerificationCodeDocument
                {
                    CodeHash = account.EmailVerification.CodeHash,
                    IssuedAtUtc = account.EmailVerification.IssuedAt.UtcDateTime,
                    ExpiresAtUtc = account.EmailVerification.ExpiresAt.UtcDateTime,
                    ConsumedAtUtc = account.EmailVerification.ConsumedAt?.UtcDateTime,
                    FailedAttempts = account.EmailVerification.FailedAttempts
                },
            VerificationResendAttemptsUtc = account.VerificationResendAttempts
                .Select(attempt => attempt.UtcDateTime)
                .ToList(),
            DeletedAtUtc = account.DeletedAt?.UtcDateTime
        };

    private static Account ToDomain(AccountDocument document)
    {
        var privacy = document.PrivacyAcceptance
            ?? throw new InvalidOperationException("El documento de cuenta no contiene la aceptación de privacidad.");

        var verification = document.EmailVerification is null
            ? null
            : VerificationCode.Restore(
                document.EmailVerification.CodeHash,
                ToDateTimeOffset(document.EmailVerification.IssuedAtUtc),
                ToDateTimeOffset(document.EmailVerification.ExpiresAtUtc),
                document.EmailVerification.ConsumedAtUtc is null
                    ? null
                    : ToDateTimeOffset(document.EmailVerification.ConsumedAtUtc.Value),
                document.EmailVerification.FailedAttempts);

        return Account.Restore(
            Guid.ParseExact(document.Id, "N"),
            document.FullName,
            EmailAddress.Create(document.NormalizedEmail),
            DateOnly.ParseExact(document.BirthDate, BirthDateFormat, CultureInfo.InvariantCulture),
            Enum.Parse<Gender>(document.Gender),
            document.PasswordHash,
            new PrivacyAcceptance(privacy.Version, ToDateTimeOffset(privacy.AcceptedAtUtc)),
            Enum.Parse<AccountStatus>(document.Status),
            document.EmailConfirmed,
            ToDateTimeOffset(document.CreatedAtUtc),
            document.EmailConfirmedAtUtc is null
                ? null
                : ToDateTimeOffset(document.EmailConfirmedAtUtc.Value),
            verification,
            document.VerificationResendAttemptsUtc.Select(ToDateTimeOffset),
            document.DeletedAtUtc is null
                ? null
                : ToDateTimeOffset(document.DeletedAtUtc.Value));
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private void LogStorageFailure(MongoException exception)
    {
        // No se registra el mensaje: puede contener datos de conexión.
        _logger.LogWarning(
            "La operación de MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }

}
