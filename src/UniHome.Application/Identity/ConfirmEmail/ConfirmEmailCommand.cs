namespace UniHome.Application.Identity.ConfirmEmail;

public sealed record ConfirmEmailCommand(string? Email, string? Code);
