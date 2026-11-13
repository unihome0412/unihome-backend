namespace UniHome.Domain.Units;

/// <summary>
/// Servicio de una unidad. Un servicio Aparte exige monto positivo y nunca se
/// suma a la renta base; uno Incluido no conserva monto.
/// </summary>
public sealed record ServiceCharge(ServiceName Name, ServiceMode Mode, decimal? Amount)
{
    public static ServiceCharge Create(ServiceName name, ServiceMode mode, decimal? amount)
    {
        if (!Enum.IsDefined(name))
        {
            throw new ArgumentException("El servicio no pertenece al catálogo.", nameof(name));
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentException("La modalidad del servicio no es válida.", nameof(mode));
        }

        if (mode == ServiceMode.Aparte && (amount is null || amount <= 0))
        {
            throw new ArgumentException(
                "Un servicio aparte requiere un monto mensual mayor que cero.",
                nameof(amount));
        }

        return new ServiceCharge(
            name,
            mode,
            mode == ServiceMode.Aparte ? amount : null);
    }
}
