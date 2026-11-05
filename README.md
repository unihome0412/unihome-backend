# UniHome Backend

API REST en .NET 10 para UniHome. Incluye la base de la Etapa 1 y dos rebanadas de
M01: registro (HU-001/HU-012) y sesión (login, refresh y logout: HU-004/HU-005).
**M01 no está completo**: confirmación de correo, recuperación y eliminación de cuenta
siguen pendientes.

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

## Endpoints disponibles de M01

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/v1/auth/registro` | Registro de cuenta única, sin elección de rol (HU-001/HU-012). |
| `POST` | `/api/v1/auth/login` | Login común por correo y contraseña (HU-004). |
| `POST` | `/api/v1/auth/refresh` | Renovación con rotación de refresh token (EP-M01-08). |
| `POST` | `/api/v1/auth/logout` | Cierre de la sesión actual con `Authorization: Bearer` (HU-005). |

Registro (`201`):

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "correo": "ana@uni.edu",
  "correoConfirmado": false,
  "requiereConfirmacionCorreo": true,
  "correoConfirmacionEnviado": false
}
```

Login y refresh (`200`):

```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<token opaco>",
  "expiraEn": "2026-01-15T13:00:00+00:00"
}
```

`correoConfirmacionEnviado` es `false` si no hay proveedor de correo configurado.
El código de confirmación se guarda como HMAC, nunca en claro. Los endpoints de
confirmación y reenvío aún no están expuestos porque el contrato del token de sesión de
registro no está definido.

## Seguridad de sesión

- Login universal con correo normalizado y contraseña; no se solicita ni se acepta un
  rol del cliente. Una cuenta suspendida o eliminada no puede iniciar sesión.
- El correo no confirmado no puede iniciar sesión.
- Las credenciales incorrectas y el correo inexistente devuelven el mismo `401` y el
  mismo mensaje: `Revisa tu correo y contraseña`.
- Access token JWT HS256 con sujeto interno, `sid` de sesión, `issuer`, `audience`,
  expiración y firma verificados. El estado de la cuenta se consulta en cada petición
  autenticada (BE-SEC-008).
- Refresh token opaco de 256 bits. En MongoDB solo se persiste SHA-256 del token; se
  rota en cada refresh y el reuso de un token rotado revoca la familia completa. Cada
  rotación emite un refresh nuevo con la vigencia configurada (30 días por defecto);
  fijar un máximo absoluto de sesión queda como decisión de producto/seguridad.
- Logout revoca únicamente la sesión del dispositivo actual.
- No se registran contraseñas, tokens, secretos ni datos personales en logs o respuestas
  de error.
- Los tokens de acceso no se guardan en el cliente por este backend; su almacenamiento
  seguro corresponde a la app móvil.

## Configuración de seguridad

- `VerificationCode__HmacSecret` (o `VerificationCode:HmacSecret`) debe tener al menos
  32 caracteres fuera de `Development`. En `Development`, si falta, se usa una clave
  efímera por proceso.
- `Jwt__SigningKey` (o `Jwt:SigningKey`) debe tener al menos 32 caracteres fuera de
  `Development`. En `Development`, si falta, se usa una clave efímera por proceso.
- `Jwt__Issuer` y `Jwt__Audience` son configurables. `Jwt__AccessTokenMinutes` vale 60
  por defecto y `Jwt__RefreshTokenDays` vale 30 por defecto. Estos valores de fuente
  siguen sujetos a revisión de seguridad.
- `Auth__RateLimit__LoginPermitLimit` / `LoginWindowSeconds` valen 10 intentos por 60
  segundos por IP; `RefreshPermitLimit` / `RefreshWindowSeconds` valen 20 por 60
  segundos. Son umbrales provisionales pendientes de aprobación. El limitador es en
  memoria y asume una sola instancia del proceso (BE-SEC-009); escalar horizontalmente
  requiere el ADR de estado distribuido.
- El envío SMTP es opcional en Development. Si no existe la sección `Smtp`, el registro
  persiste la cuenta y devuelve `correoConfirmacionEnviado: false`; si existe, se valida
  al arrancar y `correoConfirmacionEnviado: true` solo indica que el relé aceptó el
  mensaje, no que se haya entregado al buzón final.
- Registro, login, refresh y logout limitan el cuerpo de la solicitud a 8 KB.

## Persistencia y migraciones MongoDB

La colección `sesiones_refresh` guarda solo hashes de refresh tokens. La colección
`cuentas` guarda el correo normalizado como campo único. La rotación se hace con una
actualización condicionada por el hash vigente, de modo que una sola solicitud
concurrente puede consumir el token.

La API no crea colecciones ni índices al arrancar. La migración versionada `M01-001`
se ejecuta con una herramienta separada:

```powershell
dotnet run --project tools/UniHome.MongoMigrator -- audit --database unihome_modelo_pruebas
dotnet run --project tools/UniHome.MongoMigrator -- plan --database unihome_modelo_pruebas
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas
```

La migración ordinaria solo escribe en `unihome_modelo_pruebas` o en una base incluida
en `UNIHOME_MIGRATION_ALLOWED_DATABASES`; `unihome_desarrollo` está bloqueada para
`apply`. Registra la versión y el checksum en `migraciones`, usa la unicidad nativa de
`_id` y rechaza divergencias de clave, unicidad, TTL, filtro parcial, colación, `sparse`
u oculto. El TTL solo limpia sesiones vencidas; la autorización se comprueba siempre en
el backend.

La promoción a `unihome_desarrollo` es un comando separado (`promote`) con guard
propio, autorización explícita, confirmación literal, credencial exclusiva y bloqueo
distribuido con fencing y heartbeat. El mecanismo está implementado y probado
localmente, pero **no se ejecutó ninguna promoción ni escritura en
`unihome_desarrollo`**:

```powershell
$env:UNIHOME_PROMOTION_AUTHORIZED_DATABASE = 'unihome_desarrollo'
dotnet run --project tools/UniHome.MongoMigrator -- promote `
  --database unihome_desarrollo `
  --confirm-database unihome_desarrollo `
  --approval-ref "<REFERENCIA_DE_APROBACION>"
```

No ejecutar ese comando sin completar las puertas de conectividad TLS/SSPI, usuario
técnico separado, respaldo/restauración verificado y autorización humana. La primera
promoción solo crearía `M01-001`; las 20 colecciones externas aún no tienen migración
versionada ni contratos BSON/DTO aprobados.

Inventario BSON, colecciones/índices observados y límites operativos:
[`docs/mongodb-m01.md`](docs/mongodb-m01.md). El procedimiento de promoción, comandos,
riesgos, respaldo/restauración y criterios de aceptación están en
[`docs/mongodb-promocion.md`](docs/mongodb-promocion.md). La migración debe verificarse
antes de habilitar tráfico. Readiness y repositorios verifican los índices M01 en modo
solo lectura; si falta una garantía crítica, la API no opera esa colección.

## Health checks

| Ruta | Semántica |
|---|---|
| `GET /health/live` | Proceso vivo. No consulta dependencias externas. |
| `GET /health/ready` | Conectividad e índices M01 disponibles. Sin configuración o esquema válido responde `503`. |

## Errores y OpenAPI

- Errores homogéneos con `ProblemDetails`, incluyendo `traceId` y `errors` en validaciones.
- Excepciones no controladas: `500` genérico, sin stack trace.
- OpenAPI nativo de .NET 10 disponible solo en desarrollo: `GET /openapi/v1.json`.
  Incluye el esquema Bearer y marca `logout` como operación autenticada.
- Registro: `400` por validación, `409` por correo duplicado (provisional, DEC-09
  pendiente), `413` por cuerpo excesivo y `503` si MongoDB no está disponible.
- Login: `400` por datos inválidos, `401` genérico, `403` por cuenta no confirmada o
  suspendida, `413`, `429` y `503`.
- Refresh: `400` sin token, `401` por token/sesión inválidos o reuso, `413`, `429` y `503`.
- Logout: `204`, `401` sin sesión válida, `413` por cuerpo excesivo y `503` si el
  almacenamiento falla.

## Pruebas

```powershell
dotnet restore UniHome.Backend.slnx --locked-mode
dotnet build UniHome.Backend.slnx --configuration Release -warnaserror
dotnet test UniHome.Backend.slnx --configuration Release
dotnet format UniHome.Backend.slnx --verify-no-changes
```

Las pruebas de integración habituales usan `WebApplicationFactory` y dobles en memoria;
no requieren un clúster real. La CI de GitHub Actions solo ejecuta restore/build/test; no
despliega ni usa credenciales.

La prueba real de identidad contra MongoDB está deshabilitada por defecto. Para
ejecutarla en una base de verificación aislada, primero revisar y aplicar la
migración con la herramienta separada. La prueba no aplica DDL y puede utilizar
una credencial de ejecución con CRUD y lectura de metadatos sin permisos de DDL:

```powershell
$env:UNIHOME_RUN_MONGO_INTEGRATION_TESTS = '1'
$env:MongoDb__ConnectionString = '<desde User Secrets o variable segura>'
$env:MongoDb__DatabaseName = 'unihome_modelo_pruebas'
dotnet test tests/UniHome.IntegrationTests/UniHome.IntegrationTests.csproj `
  --configuration Release --filter FullyQualifiedName~MongoIdentityPersistenceTests
```

La prueba registra una cuenta ficticia, verifica el hash PBKDF2 y el índice único,
confirma el correo mediante dominio/repositorio con el código capturado en memoria,
prueba login, rotación sincronizada contra el CAS real y logout,
y elimina únicamente sus documentos de prueba. No envía correos reales.

## Pendiente de M01 y decisiones abiertas

- Confirmación y reenvío de código (EP-M01-02/03), recuperación (EP-M01-06/07) y
  eliminación de cuenta (EP-M01-09).
- **DEC-09**: mensaje/código definitivo ante correo duplicado en el registro.
- **DEC-10**: tratamiento de menores de edad y consentimiento aplicable.
- Rate limiting de registro y de intentos de confirmación; aprobación de los umbrales
  provisionales de login/refresh.
- Definir proxy/`ForwardedHeaders` confiables para que el rate limit por IP no agrupe
  clientes detrás de un balanceador; la implementación actual no confía en cabeceras.
- Logout invalida el refresh token de la sesión actual. El access token ya emitido
  conserva como máximo sus 60 minutos de vigencia; si se requiere revocación inmediata
  en todos los endpoints protegidos, debe aprobarse y añadirse la consulta de sesión al
  middleware de autenticación.
- Adopción de la migración `M01-001` en `unihome_desarrollo` requiere autorización
  explícita, usuario técnico verificado y respaldo/restauración comprobados. `apply`
  sigue bloqueado; solo el comando separado `promote` puede escribir, y aún no se ha
  ejecutado.
- Retención/limpieza de cuentas no confirmadas (ADR-005).
- Scalar/Swagger UI solo si se aprueba explícitamente.
