namespace UniHome.Application.Universities.Exceptions;

/// <summary>
/// Ya existe una universidad con el mismo nombre normalizado. Se traduce a 409
/// en la API; no revela documentos ni identificadores internos.
/// </summary>
public sealed class DuplicateUniversityException : Exception
{
    public DuplicateUniversityException()
        : base("Ya existe una universidad con ese nombre y campus.")
    {
    }
}
