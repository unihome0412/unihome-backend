# UniHome Backend

API REST en .NET 10 para UniHome. Esta entrega corresponde únicamente a la **Etapa 1: cimientos funcionales**; el registro/login se implementará después de revisar estos resultados.

## Stack y capas

- .NET 10 / ASP.NET Core Web API / C#.
- Clean Architecture: `Domain` → sin dependencias externas; `Application` → `Domain`; `Infrastructure` → `Application` + `Domain`; `Api` compone ambas.
- MongoDB mediante `MongoDB.Driver`, aislado detrás de un puerto de `Application`.
- Sin SQL Server, EF Core, Docker, Testcontainers, Scalar ni Swagger UI.

## Ejecutar localmente

```powershell
dotnet restore UniHome.Backend.slnx
dotnet build UniHome.Backend.slnx
dotnet run --project src/UniHome.Api
```

La API inicia sin MongoDB configurado. La configuración de MongoDB es externa (User Secrets o variables de entorno), por ejemplo `MongoDb__ConnectionString` y `MongoDb__DatabaseName`. No se guardan cadenas de conexión ni secretos en el repositorio.

## Health checks

| Ruta | Semántica |
|---|---|
| `GET /health/live` | Proceso vivo. No consulta dependencias externas. |
| `GET /health/ready` | Dependencia de almacenamiento disponible. Sin MongoDB configurado responde `503` y `storage: Unhealthy`; nunca un falso OK. |

Las respuestas incluyen un `traceId` de correlación y no exponen cadenas de conexión, excepciones ni datos personales.

## Errores y OpenAPI

- Errores homogéneos con `ProblemDetails`, incluyendo `traceId` y `errors` en validaciones.
- Excepciones no controladas: `500` con mensaje genérico, sin stack trace.
- OpenAPI nativo de .NET 10 disponible solo en desarrollo: `GET /openapi/v1.json`.

## Pruebas

```powershell
dotnet test UniHome.Backend.slnx
```

Las pruebas de integración usan `WebApplicationFactory` y no requieren un clúster real. La CI de GitHub Actions solo ejecuta restore/build/test; no despliega ni usa credenciales.

## Pendiente para etapas siguientes

- M01 identidad/cuenta, M02 perfil y M03 universidades.
- Autenticación/autorización, rate limiting y contratos de los módulos.
- Índices de MongoDB e inicialización idempotente cuando los modelos de dominio estén aprobados.
- Evaluar Scalar/Swagger UI solo si el usuario lo aprueba.
