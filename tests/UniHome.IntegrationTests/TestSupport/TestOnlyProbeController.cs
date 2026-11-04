using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Controladores que solo existen en el ensamblado de pruebas para verificar
/// el pipeline de validación y errores. No forman parte de la API de producción.
/// </summary>
[ApiController]
[Route("__test")]
public sealed class TestOnlyProbeController : ControllerBase
{
    [HttpPost("validation")]
    public ActionResult<ValidationProbeRequest> Validation([FromBody] ValidationProbeRequest request) =>
        Ok(request);

    [HttpGet("throw")]
    public IActionResult Throw() =>
        throw new InvalidOperationException("Mensaje sensible que no debe exponerse.");
}

public sealed class ValidationProbeRequest
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string? Email { get; set; }
}
