namespace UniHome.Application.Universities.CreateUniversity;

/// <summary>HU-075: alta de una universidad con nombre y campus en un solo campo.</summary>
public sealed record CreateUniversityCommand(string? NameAndCampus);
