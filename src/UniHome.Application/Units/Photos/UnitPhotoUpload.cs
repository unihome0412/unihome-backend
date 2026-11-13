namespace UniHome.Application.Units.Photos;

public sealed record UnitPhotoUpload(string FileName, string? ContentType, byte[] Content);
