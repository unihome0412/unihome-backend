namespace UniHome.Application.Identity.Abstractions;

public interface IVerificationCodeGenerator
{
    /// <summary>Genera un código numérico de 6 dígitos, incluidos ceros a la izquierda.</summary>
    string Generate();
}
