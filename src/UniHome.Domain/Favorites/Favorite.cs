namespace UniHome.Domain.Favorites;

/// <summary>Par cuenta/unidad único de M07. La persistencia garantiza la unicidad.</summary>
public sealed record Favorite(Guid AccountId, Guid UnitId, DateTimeOffset CreatedAt)
{
    public static Favorite Create(Guid accountId, Guid unitId, DateTimeOffset now)
    {
        if (accountId == Guid.Empty || unitId == Guid.Empty)
        {
            throw new ArgumentException("La cuenta y la unidad son obligatorias.");
        }

        return new Favorite(accountId, unitId, now);
    }
}
