namespace UniHome.Application.Identity.PasswordRecovery;

public sealed record ConfirmPasswordResetCommand(string? Token, string? NewPassword);
