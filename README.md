# UniHome Backend

API REST en .NET 10 para UniHome. Incluye la base de la Etapa 1 y M01: registro
(HU-001/HU-012), sesión (login, refresh y logout: HU-004/HU-005), confirmación de
correo y su reenvío (HU-003) y eliminación de cuenta (HU-096).
**M01 no está completo**: la recuperación de contraseña (HU-006) sigue pendiente.

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
| `POST` | `/api/v1/auth/confirmacion-correo` | Confirma el correo con el código de 6 dígitos (HU-003, EP-M01-02). |
| `POST` | `/api/v1/auth/confirmacion-correo/reenvio` | Reenvía el código de confirmación; siempre `202` (HU-003, EP-M01-03). |
| `POST` | `/api/v1/auth/login` | Login común por correo y contraseña (HU-004). |
| `POST` | `/api/v1/auth/refresh` | Renovación con rotación de refresh token (EP-M01-08). |
| `POST` | `/api/v1/auth/logout` | Cierre de la sesión actual con `Authorization: Bearer` (HU-005). |
| `DELETE` | `/api/v1/cuentas/mi-cuenta` | Elimina (borrado lógico) la cuenta propia con `Authorization: Bearer` (HU-096, EP-M01-09). |

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
El código de confirmación se guarda como HMAC, nunca en claro.

Confirmación de correo (`200`), idéntica si la cuenta ya estaba confirmada:

```json
{ "correoConfirmado": true }
```

**Decisión aprobada:** `confirmacion-correo` y su reenvío reciben el correo en el
cuerpo (sin token de sesión de registro, porque ese contrato no está definido en la
spec). Un correo inexistente, un código incorrecto, sin emitir o ya usado responden
igual (`400`), para no revelar si la cuenta existe; un código vencido o con intentos
agotados responde `410`. El reenvío siempre responde `202` sin cuerpo, exista o no el
correo.

Eliminación de cuenta (`204`, sin cuerpo): requiere `Authorization: Bearer`,
`contrasena` y `confirmacionPerdidaInformacion: true`. Hace borrado lógico
(`AccountStatus.Deleted`) y revoca todas las sesiones de la cuenta en todos los
dispositivos (a diferencia de `logout`, que solo revoca la sesión actual). Tras
eliminar, la cuenta no puede iniciar sesión, renovar tokens ni seguir usando un access
token emitido antes de eliminarla (BE-SEC-008 ya cubre esto: el estado se revisa en
cada petición autenticada). **Pendiente:** el rechazo `409` por vínculos activos de
propiedades/ocupación (M05/M09) y la política de retención/anonimización de datos
(ADR-005) no están implementados.

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
  segundos; `ConfirmEmailPermitLimit` / `ConfirmEmailWindowSeconds` valen 10 por 60;
  `ResendPermitLimit` / `ResendWindowSeconds` valen 5 por 60. Son umbrales
  provisionales pendientes de aprobación. El limitador es en memoria y asume una sola
  instancia del proceso (BE-SEC-009); escalar horizontalmente requiere el ADR de
  estado distribuido.
- Límite "por cuenta" de intentos de confirmación (spec EP-M01-02, sin valor fijado):
  provisional de 5 intentos incorrectos por verificador
  (`VerificationCode.MaxConfirmationAttempts`); al agotarse, el código se trata como
  vencido (`410`) para forzar un reenvío en vez de permitir reintentos indefinidos.
- El envío SMTP es opcional en Development. Si no existe la sección `Smtp`, el registro
  persiste la cuenta y devuelve `correoConfirmacionEnviado: false`; si existe, se valida
  al arrancar y `correoConfirmacionEnviado: true` solo indica que el relé aceptó el
  mensaje, no que se haya entregado al buzón final.
- Registro, confirmación de correo, su reenvío, login, refresh, logout y eliminación
  de cuenta limitan el cuerpo de la solicitud a 8 KB.

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
- Todas las respuestas incluyen `X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`, `Permissions-Policy` y una CSP; las rutas de autenticación
  y cuentas añaden `Cache-Control: no-store`. La CSP de Swagger UI permite sus
  recursos propios e inline; el resto de la API usa `default-src 'none'`.
- OpenAPI nativo de .NET 10 disponible solo en desarrollo: `GET /openapi/v1.json`.
  Incluye el esquema Bearer y marca `logout` como operación autenticada.
- `Gender` se documenta como enum string (`Mujer`, `Hombre`,
  `PrefiereNoDecir`), igual que el JSON real; los enteros se rechazan. La
  corrección se aplica al documento OpenAPI sin cambiar el contrato HTTP.
  Verificación: ejecución manual
  [42850339456](https://github.com/unihome0412/unihome-backend/actions/runs/42850339456),
  `contract-test` con 10/10 pruebas OpenAPI aprobadas.
- Registro: `400` por validación, `409` por correo duplicado (provisional, DEC-09
  pendiente), `413` por cuerpo excesivo y `503` si MongoDB no está disponible.
- Login: `400` por datos inválidos, `401` genérico, `403` por cuenta no confirmada o
  suspendida, `413`, `429` y `503`.
- Refresh: `400` sin token, `401` por token/sesión inválidos o reuso, `413`, `429` y `503`.
- Logout: `204`, `401` sin sesión válida, `413` por cuerpo excesivo y `503` si el
  almacenamiento falla.
- Confirmación de correo: `200` (incluida la idempotente ya confirmada), `400` por
  datos inválidos, correo inexistente o código incorrecto/sin emitir/ya usado, `410`
  por código vencido o intentos agotados, `413`, `429` y `503`.
- Reenvío de confirmación: `202` siempre que el formato sea válido, `400` por correo
  mal formado, `413`, `429` y `503`.
- Eliminación de cuenta: `204`, `400` sin confirmar la pérdida de información, `401`
  por contraseña incorrecta o sesión inválida, `413` y `503`.

## Pruebas

```powershell
dotnet restore UniHome.Backend.slnx --locked-mode
dotnet build UniHome.Backend.slnx --configuration Release -warnaserror
dotnet test UniHome.Backend.slnx --configuration Release
dotnet format UniHome.Backend.slnx --verify-no-changes
```

Las pruebas de integración habituales usan `WebApplicationFactory` y dobles en memoria;
no requieren un clúster real. La CI de GitHub Actions (`.github/workflows/ci.yml`)
ejecuta restore/build/formato/pruebas, revisión de dependencias vulnerables y Gitleaks
sobre el historial completo; `.github/workflows/codeql.yml` ejecuta CodeQL para C#. No
despliega ni usa credenciales.

El job manual `mongo-db-test` (habilitado con la variable de repositorio
`UNIHOME_ENABLE_MONGO_CI=true`) levanta MongoDB Community 8.0.34 dentro del runner
de GitHub Actions, en un contenedor efímero fijado por digest. No usa Atlas, Docker
local ni secretos: la URI se limita a `127.0.0.1`, la base es
`unihome_ci_verificacion`, se aplica únicamente `M01-001` a esa base temporal y las
pruebas reales de identidad y bloqueo distribuido se ejecutan con TRX. El job falla
si MongoDB no inicia, si la migración no queda válida, si las pruebas se omiten o
si no se genera la evidencia; el artefacto se publica como `mongodb-ci-results`.
La ejecución manual de verificación
([42570584187](https://github.com/unihome0412/unihome-backend/actions/runs/42570584187),
commit `9f4b4b5`) dejó 2/2 pruebas reales aprobadas, 0 omitidas y la limpieza
temporal confirmada.

El job manual `dast-zap` (variable `UNIHOME_ENABLE_ZAP=true`) ejecuta un
**baseline pasivo real** de OWASP ZAP con la imagen oficial
`ghcr.io/zaproxy/zaproxy:2.17.0` fijada por digest. Levanta la API solo en
`127.0.0.1:5088`, un MongoDB efímero inicializado con M01-001 y un SMTP de
captura local con STARTTLS; crea cuentas sintéticas mediante registro,
confirmación, login, refresh y logout, descubre `/openapi/v1.json`, visita cada
operación documentada y publica `zap-report.html`, `zap-report.json`,
`dast-report.md`, `route-coverage.json` y evidencias de arranque/limpieza en el
artefacto `dast-zap-results`. No usa Active Scan, `-I`, exclusiones,
`continue-on-error`, Atlas, Brevo, Azure ni secretos reales.

```powershell
gh variable set UNIHOME_ENABLE_ZAP --body true
gh workflow run ci.yml --ref feature/ci-dast-zap
gh run list --workflow ci.yml --limit 5
gh run download <run-id> -n dast-zap-results -D artifacts/dast-zap
```

La ejecución real de verificación
([42772939746](https://github.com/unihome0412/unihome-backend/actions/runs/42772939746),
commit `01893d3`) terminó con `dast-zap` en verde y estado **PASS**: 7/7 rutas
OpenAPI visitadas, 26 mensajes procesados, 0 Critical/High/Medium/Low y 2
informativas. El artefacto `dast-zap-results` contiene HTML/JSON/Markdown y la
limpieza confirmó API/SMTP detenidos, contenedor ZAP eliminado y base efímera
borrada. `PR Quality Gate` y `Hardening Report` terminaron en verde.

El gate exige el job en `workflow_dispatch` desde `main` o `feature/*` con
cambios de código. High/Critical fallan; Medium produce `WARN`. Las alertas que
aparezcan en futuras ejecuciones deben revisarse; el escaneo pasivo no demuestra
lógica de negocio ni autorización a nivel de objeto.

El job manual `restler-fuzzing` (variable `UNIHOME_ENABLE_RESTLER=true`) usa
Microsoft RESTler **v9.3.1** fijado por commit
`eaacd92733771d35e90acfb2040fe37d4e5d09b1`. Compila desde fuente, ejecuta
**Compile**, **Test/Smoke** y **Fuzz-lean** contra la misma estrategia de API
efímera: loopback, MongoDB 8.0.34 con M01-001, SMTP de captura y cuentas
sintéticas. No usa fuzzing exhaustivo, Atlas, Brevo, Azure ni secretos reales.

El OpenAPI 3.1 se conserva; se genera una copia compatible 3.0.3 solo para
RESTler, con reporte de transformación y verificación de que no cambian rutas ni
métodos. El artefacto `restler-fuzzing-results` incluye compilación, gramática,
cobertura `speccov`, Test/Fuzz-lean, solicitudes, secuencias, errores HTTP, bug
buckets, replay logs saneados y `restler-summary.md`. El job falla con HTTP 5xx,
bug buckets, resultados faltantes u operaciones OpenAPI no intentadas; un 401/403
no se cuenta como prueba funcional válida.

```powershell
gh variable set UNIHOME_ENABLE_RESTLER --body true
gh workflow run ci.yml --ref feature/ci-restler
gh run download <run-id> -n restler-fuzzing-results -D artifacts/restler
```

La ejecución real de verificación
([42794026111](https://github.com/unihome0412/unihome-backend/actions/runs/42794026111),
commit `fe22365`) terminó con `restler-fuzzing` en verde y estado **PASS**:
Compile/Test/Fuzz-lean correctos, 7/7 operaciones intentadas y con al menos una
respuesta 2xx, 2.054 solicitudes, 16 secuencias, 0 HTTP 5xx y 0 bug buckets.
La limpieza confirmó API/SMTP detenidos y base `unihome_restler` borrada.

La verificación Pact del proveedor está preparada en `tests/UniHome.PactTests`
con PactNet 5.0.1, pero **no hay contratos reales del consumidor Android** en el
repositorio `unihome-mobile` ni en este repositorio. El job `pact-contract`
permanece desactivado mediante `UNIHOME_ENABLE_PACT`; si se activa sin contratos,
falla explícitamente y no se considera aprobado. El formato, origen y entrega de
los contratos se documentan en [`pacts/README.md`](pacts/README.md); no se generan
contratos desde OpenAPI ni se inventan interacciones.

La prueba real de identidad contra MongoDB está deshabilitada por defecto. Para
ejecutarla localmente en una base de verificación aislada, primero revisar y aplicar
la migración con la herramienta separada. La prueba no aplica DDL y puede utilizar
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

- Recuperación de contraseña (EP-M01-06/07).
- **DEC-09**: mensaje/código definitivo ante correo duplicado en el registro.
- **DEC-10**: tratamiento de menores de edad y consentimiento aplicable.
- Rate limiting de registro; aprobación de los umbrales provisionales de
  login/refresh/confirmación/reenvío y del límite "por cuenta" de intentos de
  confirmación (`VerificationCode.MaxConfirmationAttempts`).
- **EP-M01-09** (eliminar cuenta) implementado solo en lo exigido por la spec:
  borrado lógico y revocación de todas las sesiones. Pendiente: rechazo `409` por
  vínculos activos de propiedades/ocupación (M05/M09, HU-096) y la política de
  retención/anonimización de datos tras eliminar (ver ADR-005 más abajo). Tampoco se
  implementó la restricción de que una cuenta administradora no se autoelimine por
  esta ruta: hoy no existe concepto de administrador en el dominio.
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
- Retención/limpieza de cuentas no confirmadas y anonimización de datos tras
  eliminar una cuenta (ADR-005).
- Scalar/Swagger UI solo si se aprueba explícitamente.
