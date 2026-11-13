namespace UniHome.Application.Universities.Abstractions;

public sealed record GeoCoordinate(double Latitude, double Longitude)
{
    public bool IsValid =>
        Latitude is >= -90 and <= 90 &&
        Longitude is >= -180 and <= 180;
}
