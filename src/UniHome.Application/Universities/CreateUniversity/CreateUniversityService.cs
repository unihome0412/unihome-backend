using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Application.Universities.Exceptions;
using UniHome.Domain.Universities;

namespace UniHome.Application.Universities.CreateUniversity;

/// <summary>
/// Caso de uso EP-M03-02 (HU-075): registra una universidad activa en un solo
/// campo «nombre y campus». El adaptador aplica la unicidad normalizada.
/// </summary>
public sealed class CreateUniversityService
{
    private readonly IUniversityRepository _universities;
    private readonly ISystemClock _clock;

    public CreateUniversityService(
        IUniversityRepository universities,
        ISystemClock clock)
    {
        _universities = universities;
        _clock = clock;
    }

    public async Task<CreateUniversityResult> CreateAsync(
        CreateUniversityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = Validate(command.NameAndCampus);

        if (errors.Count > 0)
        {
            return CreateUniversityResult.ValidationFailed(errors);
        }

        University university;

        try
        {
            university = University.Create(command.NameAndCampus, _clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return CreateUniversityResult.ValidationFailed(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["nombreYCampus"] = [exception.Message]
                });
        }

        try
        {
            await _universities
                .AddAsync(university, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DuplicateUniversityException)
        {
            return CreateUniversityResult.Duplicate();
        }

        return CreateUniversityResult.Created(university);
    }

    private static Dictionary<string, string[]> Validate(string? nameAndCampus)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(nameAndCampus))
        {
            errors["nombreYCampus"] = ["El nombre y campus es obligatorio."];
        }
        else if (nameAndCampus.Trim().Length > University.MaxNameAndCampusLength)
        {
            errors["nombreYCampus"] =
            [
                $"El nombre y campus no debe superar " +
                $"{University.MaxNameAndCampusLength} caracteres."
            ];
        }

        return errors;
    }
}
