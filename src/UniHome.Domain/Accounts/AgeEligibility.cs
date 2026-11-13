namespace UniHome.Domain.Accounts;

/// <summary>Mayoría de edad calculada en servidor desde la fecha persistida.</summary>
public static class AgeEligibility
{
    public static bool IsAdult(DateOnly birthDate, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var age = today.Year - birthDate.Year;

        if (birthDate > today.AddYears(-age))
        {
            age--;
        }

        return age >= 18;
    }
}
