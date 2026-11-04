# UniHome Backend

API REST en .NET 10 para UniHome. Incluye la base de la Etapa 1 y la primera rebanada de
M01 (registro HU-001/HU-012). **M01 no está completo**: login, confirmación de correo,
recuperación y eliminación de cuenta siguen pendientes.

## Stack y capas

- .NET 10 / C# / ASP.NET Core Web API.
- Clean Architecture: `Domain` no depende de otros proyectos; `Application` depende de
  `Domain`; `Infrastructure` depende de `Application` y `Domain`; `Api` compone ambas.
- MongoDB mediante `MongoDB.Driver`, aislado detrás de puertos de `Application`.
- Sin SQL Server, EF Core, Docker, Testcontainers, Scalar ni Swagger UI.

## Ejecutar localmente

```powershell
dotnet restore UniHome.Backend.slnx
dotnet build UniHome.Backend.slnx
dotnet run --project src/UniHome.Api
```

La API inicia sin MongoDB configurado. La configuración de MongoDB es externa
(User Secrets o variables de entorno), por ejemplo `MongoDb__ConnectionString` y
`MongoDb__DatabaseName`. No se guardan cadenas de conexión ni secretos en el repositorio.

## Endpoint disponible de M01

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/v1/auth/registro` | Registro de cuenta única, sin elección de rol (HU-001/HU-012). |

Respuesta `201`:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "correo": "ana@uni.edu",
  "correoConfirmado": false,
  "requiereConfirmacionCorreo": true,
  "correoConfirmacionEnviado": false
}
```

`correoConfirmacionEnviado` es `false` mientras no exista un proveedor de correo aprobado.
El código de confirmación se guarda como HMAC, nunca en claro. Los endpoints de
confirmación y reenvío aún no están expuestos porque el contrato del token de sesión de
registro no está definido.

## Configuración de seguridad

- `VerificationCode__HmacSecret` (o `VerificationCode:HmacSecret`) debe tener al menos
  32 caracteres fuera de `Development`. En `Development`, si falta, se usa una clave
  efímera por proceso.
- No hay proveedor SMTP/SendGrid configurado. El registro persiste la cuenta y devuelve
  `correoConfirmacionEnviado: false`; no se simula un envío real.
- `POST /api/v1/auth/registro` limita el cuerpo de la solicitud a 8 KB.

## Health checks

| Ruta | Semántica |
|---|---|
| `GET /health/live` | Proceso vivo. No consulta dependencias externas. |
| `GET /health/ready` | Dependencia de almacenamiento disponible. Sin MongoDB configurado responde `503`; nunca un falso OK. |

## Errores y OpenAPI

- Errores homogéneos con `ProblemDetails`, incluyendo `traceId` y `errors` en validaciones.
- Excepciones no controladas: `500` genérico, sin stack trace.
- OpenAPI nativo de .NET 10 disponible solo en desarrollo: `GET /openapi/v1.json`.
- El registro devuelve `400` por validación, `409` por correo duplicado (provisional,
  DEC-09 pendiente), `413` por cuerpo excesivo y `503` si MongoDB no está disponible.

## Pruebas

```powershell
dotnet restore UniHome.Backend.slnx --locked-mode
dotnet build UniHome.Backend.slnx --configuration Release -warnaserror
dotnet test UniHome.Backend.slnx --configuration Release
dotnet format UniHome.Backend.slnx --verify-no-changes
```

Las pruebas de integración usan `WebApplicationFactory` y dobles en memoria; no requieren
un clúster real. La CI de GitHub Actions solo ejecuta restore/build/test; no despliega ni
usa credenciales.

## Pendiente de M01 y decisiones abiertas

- Confirmación y reenvío de código (EP-M01-02/03), login/refresh/logout, recuperación y
  eliminación de cuenta (EP-M01-04 a EP-M01-09).
- **DEC-09**: mensaje/código definitivo ante correo duplicado en el registro.
- **DEC-10**: tratamiento de menores de edad y consentimiento aplicable.
- Rate limiting de registro y de intentos de confirmación.
- Prueba de integración contra MongoDB aislado para la carrera de correo duplicado.
- Retención/limpieza de cuentas no confirmadas (ADR-005).
- Scalar/Swagger UI solo si se aprueba explícitamente.
