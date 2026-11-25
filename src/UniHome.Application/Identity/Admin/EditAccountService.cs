using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.Admin;

/// <summary>EP-M13-03 (HU-074): el administrador edita el nombre y el correo de una cuenta.</summary>
public sealed class EditAccountService
{
    private readonly IAccountRepository _accounts;

    public EditAccountService(IAccountRepository accounts)
    {
        _accounts = accounts;
    }

    public async Task<OperationResult<Account>> EditAsync(
        Guid targetAccountId,
        string? fullName,
        string? email,
        CancellationToken cancellationToken = default)
    {
        var account = await _accounts.FindByIdAsync(targetAccountId, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return OperationResult<Account>.NotFound();
        }

        EmailAddress normalizedEmail;

        try
        {
            normalizedEmail = EmailAddress.Create(email);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Account>.ValidationFailed("correo", exception.Message);
        }

        try
        {
            account.EditProfile(fullName, normalizedEmail);
        }
        catch (ArgumentException exception)
        {
            var field = exception.ParamName == nameof(email) ? "correo" : "nombreCompleto";
            return OperationResult<Account>.ValidationFailed(field, exception.Message);
        }

        try
        {
            await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateEmailException)
        {
            return OperationResult<Account>.ValidationFailed(
                "correo", "El correo ya está en uso por otra cuenta.");
        }

        return OperationResult<Account>.Success(account);
    }
}
