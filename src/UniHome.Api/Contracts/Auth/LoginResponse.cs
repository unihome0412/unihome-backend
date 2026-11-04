namespace UniHome.Api.Contracts.Auth;

/// <summary>
/// Contrato EP-M01-04/08. No incluye roles, datos personales ni el identificador
/// interno de la cuenta.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiraEn);
