# UniHome Backend

API REST en .NET 10 para UniHome. Incluye la base de la Etapa 1 y M01: registro
(HU-001/HU-012), sesión (login, refresh y logout: HU-004/HU-005), confirmación de
correo y su reenvío (HU-003), recuperación de contraseña (HU-006) y eliminación de
cuenta (HU-096).

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
| `POST` | `/api/v1/auth/recuperacion/solicitud` | Solicita la recuperación con respuesta genérica (HU-006, EP-M01-06). |
| `POST` | `/api/v1/auth/recuperacion/confirmacion` | Consume el enlace y establece la nueva contraseña (HU-006, EP-M01-07). |
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

Recuperación de contraseña (HU-006): `recuperacion/solicitud` responde siempre `200`
con el mensaje `Si el correo está registrado, recibirás instrucciones para restablecer
tu contraseña`, exista o no la cuenta. Solo envía el enlace a cuentas activas con
correo confirmado. El enlace contiene un token opaco de 256 bits, vence a los 30
minutos y es de un solo uso; en MongoDB solo se guarda su SHA-256. Si el token es
inválido o ya se usó responde `400`; si venció o agotó sus intentos responde `410`.
Al confirmar se aplica la misma política de contraseña del registro (8 a 128
caracteres) y se revocan todas las sesiones de refresh de la cuenta. La escritura de
la contraseña es un CAS de un solo documento sobre el hash anterior: si dos enlaces
válidos de la misma cuenta se confirman a la vez, solo uno cambia la contraseña. No
se usan transacciones multi-documento; un fallo posterior al consumo del token deja el
token consumido y las sesiones revocadas, y exige solicitar un enlace nuevo.

Universidades (HU-075): `POST /api/v1/admin/universidades` recibe
`{ "nombreYCampus": "..." }` y responde `201` con `id`, `nombreYCampus`, `activa` y
`fechaAlta`. Exige el privilegio administrativo interno; sin token responde `401`,
un usuario autenticado sin privilegio recibe `403`, un nombre vacío devuelve `400` y
un duplicado normalizado responde `409`. El nombre normalizado ignora mayúsculas,
acentos y espacios sobrantes. `GET /api/v1/universidades?buscar=` es público y
devuelve hasta 10 universidades activas; por eso el alta aparece de inmediato en el
buscador. La administración de HU-077 añade `GET /api/v1/admin/universidades` (listado
paginado con activas e inactivas), `PUT /api/v1/admin/universidades/{id}` (edición
con la misma normalización y protección de duplicados) y
`PUT /api/v1/admin/universidades/{id}/estado` (activación/desactivación lógica,
sin borrado). La búsqueda pública solo devuelve activas.

Perfil (HU-002/HU-008): `GET /api/v1/perfil/mi-perfil` requiere Bearer y devuelve
`nombreCompleto`, `correo`, `fechaNacimiento`, `genero`, `universidadActual`,
`etiquetaCuenta`, `vinculadoAUnidad` y `puedePublicar`. No expone hash de
contraseña ni claims administrativos. La barra de navegación y la visibilidad del
panel son responsabilidad de Android; el backend entrega los indicadores y
revalida cada ruta administrativa.

Sprint 2 / M05: `POST /api/v1/propiedades`, `POST /api/v1/propiedades/{id}/unidades`,
`POST /api/v1/unidades/{id}/fotos`, `PUT /api/v1/unidades/{id}/servicios`,
`PUT /api/v1/unidades/{id}/zona-reglas`, `GET /api/v1/unidades/{id}/vista-previa` y
`POST /api/v1/publicaciones/primera/terminos`. Todas las rutas requieren Bearer y
verifican que el recurso pertenezca al titular. La edad se recalcula en servidor.
La unidad nace en `Borrador`; no se implementó el envío a revisión de Sprint 4.
La carga de fotos usa un puerto de almacenamiento; **DEC-04 sigue pendiente**, por lo
que sin proveedor autorizado responde `503` y no se declara funcionalidad de fotos
lista para producción. Detalle en [`docs/mongodb-m05.md`](docs/mongodb-m05.md).

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
- Recuperación de contraseña: el token es opaco de 256 bits, se guarda como SHA-256,
  vence en 30 minutos, es de un solo uso y tiene un límite provisional de 5 intentos
  por token. Al restablecer la contraseña se revocan todas las sesiones de refresh de
  la cuenta; las cuentas suspendidas, eliminadas o no confirmadas no reciben enlace.
  El cambio de hash usa un CAS sobre el hash anterior para que dos confirmaciones
  concurrentes de la misma cuenta no puedan ganar ambas.
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
- `Admin__AccountIds__0`, `Admin__AccountIds__1`, ... contiene los identificadores
  internos con privilegio administrativo. La policy revalida el claim `sub` contra
  esa lista en cada petición; no se acepta rol del cliente y una lista vacía deniega
  todas las rutas `/admin/*`. La provisión definitiva de administradores pertenece a
  M13 y queda pendiente de aprobación.
- `Auth__RateLimit__LoginPermitLimit` / `LoginWindowSeconds` valen 10 intentos por 60
  segundos por IP; `RefreshPermitLimit` / `RefreshWindowSeconds` valen 20 por 60
  segundos; `ConfirmEmailPermitLimit` / `ConfirmEmailWindowSeconds` valen 10 por 60;
  `ResendPermitLimit` / `ResendWindowSeconds` valen 5 por 60;
  `PasswordResetRequestPermitLimit` / `PasswordResetRequestWindowSeconds` valen 5 por
  60; `PasswordResetConfirmPermitLimit` / `PasswordResetConfirmWindowSeconds` valen 10
  por 60. Son umbrales provisionales pendientes de aprobación. El limitador usa el
  adaptador distribuido configurado: Upstash Redis cuando `Redis:Enabled=true` y el
  adaptador en memoria solo en Development/pruebas. Fuera de Development Redis es
  obligatorio y las IP se seudonimizan con `Auth:RateLimit:IpHashSalt`.
- Límite "por cuenta" de confirmación (spec EP-M01-02, sin valor fijado):
  provisional de 5 intentos incorrectos por verificador
  (`VerificationCode.MaxConfirmationAttempts`); al agotarse, el código se trata como
  vencido (`410`) para forzar un reenvío.
- Límite "por token" de recuperación: 5 intentos incorrectos
  (`PasswordResetToken.MaxAttempts`); al agotarse, el enlace se trata como vencido
  (`410`) para exigir una solicitud nueva.
- `PasswordRecovery__ResetLinkBaseUrl` (o `PasswordRecovery:ResetLinkBaseUrl`) es la
  URL HTTPS del cliente a la que se agrega `?token=...`. Es necesaria para que el
  correo de recuperación se envíe; si falta o no es válida, no se simula el enlace.
- El envío SMTP es opcional en Development. Si no existe la sección `Smtp`, el registro
  persiste la cuenta y devuelve `correoConfirmacionEnviado: false`; si existe, se valida
  al arrancar y `correoConfirmacionEnviado: true` solo indica que el relé aceptó el
  mensaje, no que se haya entregado al buzón final.
- Registro, confirmación de correo, su reenvío, login, refresh, logout, recuperación y
  eliminación de cuenta limitan el cuerpo de la solicitud a 8 KB.

## Persistencia y migraciones MongoDB

La colección `sesiones_refresh` guarda solo hashes de refresh tokens. La colección
`cuentas` guarda el correo normalizado como campo único. La rotación se hace con una
actualización condicionada por el hash vigente, de modo que una sola solicitud
concurrente puede consumir el token.

La recuperación de contraseña usa la colección preexistente
`restablecimientos_contrasena` (`_id` string Guid N, `accountId`, `tokenHash` SHA-256,
`expiresAt`, `consumedAt`, `createdAt` y `failedAttempts`). Sus índices observados son
`ux_restablecimientos_token` (único), `ix_restablecimientos_cuenta` y
`ttl_restablecimientos_expires` (TTL 0 s). **No se añadió una migración nueva**: la
colección ya forma parte del modelo externo aprobado; la API solo verifica sus índices
en modo lectura y falla cerrada si faltan o divergen. La colección debe existir antes
de habilitar la recuperación en un entorno; no se creó ni se modificó en esta historia.

M03 usa la colección `universidades` (`nombreYCampus`, `nombreNormalizado`,
`activa`, `createdAt`) con el índice único `ux_universidades_nombre_normalizado`
sobre `nombreNormalizado`. La migración versionada `M03-001` crea la colección y el
índice; el adaptador verifica el esquema en modo lectura y falla cerrada si falta o
diverge. Detalle en [`docs/mongodb-m03.md`](docs/mongodb-m03.md).

M07 usa `favoritos` (`accountId`, `unitId`, `createdAt`) con ?ndice ?nico
`ux_favoritos_cuenta_unidad` y la migraci?n `M07-001`; no cambia M05-001. Detalle
en [`docs/mongodb-m07.md`](docs/mongodb-m07.md).

M05 usa `propiedades`, `unidades` y `aceptaciones_terminos` con la migración
`M05-001`; las fotos guardan solo metadatos y el binario depende de DEC-04. Detalle en
[`docs/mongodb-m05.md`](docs/mongodb-m05.md).

Sprint 3 / M04-M07: `GET /api/v1/exploracion/mapa`, `GET /api/v1/exploracion/lista`,
`GET /api/v1/unidades/{id}/tarjeta`, `GET /api/v1/unidades/{id}/ficha`,
`PUT/DELETE /api/v1/favoritos/{id}` y `GET /api/v1/favoritos`. Mapa, lista y tarjeta
son p?blicos (la tarjeta acepta Bearer opcional para `esFavorito`); ficha y favoritos
exigen sesi?n. Solo se exponen unidades Aprobadas y Disponibles. HU-007 reutiliza la
b?squeda M03 existente. No se expone correo ni tel?fono del arrendador. La distancia
usa Haversine y el umbral de 1.5 km; no se inventan minutos ni rutas. Detalle en
[`docs/sprint-03-cierre.md`](docs/sprint-03-cierre.md) y
[`docs/mongodb-m07.md`](docs/mongodb-m07.md).

La API no crea colecciones ni índices al arrancar. Las migraciones versionadas
`M01-001`, `M03-001`, `M05-001` y `M07-001` se ejecutan con una herramienta separada:

```powershell
dotnet run --project tools/UniHome.MongoMigrator -- audit --database unihome_modelo_pruebas
dotnet run --project tools/UniHome.MongoMigrator -- plan --database unihome_modelo_pruebas
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M01-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M01-001
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M03-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M03-001
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M05-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M05-001
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M07-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M07-001
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
técnico separado, respaldo/restauración verificado y autorización humana. Cada
promoción selecciona una versión; hoy existen `M01-001` y `M03-001`. Las demás
colecciones externas aún no tienen migración versionada ni contratos BSON/DTO
aprobados.

Matriz de cierre, endpoints y responsabilidades Android del Sprint 1:
[`docs/sprint-01-cierre.md`](docs/sprint-01-cierre.md).

Inventario BSON, colecciones/índices observados y límites operativos:
[`docs/mongodb-m01.md`](docs/mongodb-m01.md). El procedimiento de promoción, comandos,
riesgos, respaldo/restauración y criterios de aceptación están en
[`docs/mongodb-promocion.md`](docs/mongodb-promocion.md). La migración debe verificarse
antes de habilitar tráfico. Readiness y repositorios verifican los índices operativos
en modo solo lectura; si falta una garantía crítica, la API no opera esa colección.

## Health checks

| Ruta | Semántica |
|---|---|
| `GET /health/live` | Proceso vivo. No consulta dependencias externas. |
| `GET /health/ready` | Conectividad e índices M01 disponibles. Sin configuración o esquema válido responde `503`. |

La colección externa `restablecimientos_contrasena` no forma parte de `M01-001`; su
verificación de índices se ejecuta al invocar los endpoints de recuperación. Si falta
o diverge, HU-006 responde `503` y no se declara operativa.

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
- Recuperación de contraseña: solicitud `200` genérica para correo válido (exista o no
  la cuenta), `400` por correo mal formado, `413`, `429` y `503`; confirmación `200`,
  `400` por token inválido/reutilizado o contraseña inválida, `410` por token vencido o
  intentos agotados, `413`, `429` y `503`.
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
`unihome_ci_verificacion`, se aplican `M01-001`, `M03-001`, `M05-001` y `M07-001` a esa base
temporal y las pruebas reales de identidad, recuperación de contraseña, universidades,
publicaciones, exploraci?n/favoritos y bloqueo distribuido se ejecutan con TRX. El job falla
si MongoDB no inicia, si alguna migración no queda válida, si las pruebas se omiten
o si no se genera la evidencia; el artefacto se publica como `mongodb-ci-results`.
La ejecución manual de verificación
([43395389178](https://github.com/unihome0412/unihome-backend/actions/runs/43395389178),
commit de código `f51ac10`) dejó 4/4 pruebas reales aprobadas, 0 omitidas,
`M01-001` y `M03-001` válidas y la limpieza temporal confirmada.

El job manual `dast-zap` (variable `UNIHOME_ENABLE_ZAP=true`) ejecuta un
**baseline pasivo real** de OWASP ZAP con la imagen oficial
`ghcr.io/zaproxy/zaproxy:2.17.0` fijada por digest. Levanta la API solo en
`127.0.0.1:5088`, un MongoDB efímero inicializado con M01-001, M03-001 y M05-001, y un
SMTP de captura local con STARTTLS; crea cuentas sintéticas mediante registro,
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

## Preparación de Azure CD

El job `deploy-azure` está preparado para publicar una imagen etiquetada por
SHA completo con digest registrado en `acrunihomedev`, actualizar
`unihome-api`, exigir estado `Running`, verificar `/health/live` y
`/health/ready` y hacer rollback por imagen anterior. CI y CD tienen grupos de
concurrencia separados y los deploys se serializan. **No se ha desplegado**.

Permanece desactivado salvo que se cumplan simultáneamente:

- `workflow_dispatch` desde `main`;
- input `deploy_to_azure=true`;
- variable `UNIHOME_ENABLE_AZURE_CD=true`;
- environment `azure-dev` con branch policy `main` y aprobación humana;
- Build, contrato OpenAPI, Docker/Dockle, PR Quality Gate y Hardening en verde.

La configuración OIDC, permisos faltantes, estado de recursos, riesgos y
estimación FinOps están en
[`docs/azure-cd-preparacion.md`](docs/azure-cd-preparacion.md). El entorno
`azure-dev` existe, pero en la auditoría no tenía required reviewers; no se
declara listo para el primer despliegue hasta completar esos pasos.

## Pendiente de M01 y decisiones abiertas

- **DEC-09**: mensaje/código definitivo ante correo duplicado en el registro.
- **DEC-10**: tratamiento de menores de edad y consentimiento aplicable.
- Rate limiting de registro; aprobación de los umbrales provisionales de
  login/refresh/confirmación/reenvío/recuperación y de los límites por cuenta/token de
  intentos (`VerificationCode.MaxConfirmationAttempts`,
  `PasswordResetToken.MaxAttempts`).
- **EP-M01-09** (eliminar cuenta) implementado solo en lo exigido por la spec:
  borrado lógico y revocación de todas las sesiones. Pendiente: rechazo `409` por
  vínculos activos de propiedades/ocupación (M05/M09, HU-096) y la política de
  retención/anonimización de datos tras eliminar (ver ADR-005 más abajo). La ruta
  administrativa usa una allowlist interna de cuentas, pero todavía no se impide que
  una cuenta administradora se autoelimine por esta ruta.
- Definir proxy/`ForwardedHeaders` confiables para que el rate limit por IP no agrupe
  clientes detrás de un balanceador; la implementación actual no confía en cabeceras.
- Logout y recuperación de contraseña invalidan los refresh tokens de la cuenta, pero
  el access token ya emitido conserva como máximo sus 60 minutos de vigencia. Si se
  requiere revocación inmediata en todos los endpoints protegidos, debe aprobarse y
  añadirse la consulta de sesión al middleware de autenticación.
- La colección externa `restablecimientos_contrasena` no está incluida en `M01-001`;
  su existencia e índices deben verificarse en cada entorno antes de habilitar HU-006.
- Adopción de `M01-001` o `M03-001` en `unihome_desarrollo` requiere autorización
  explícita, usuario técnico verificado y respaldo/restauración comprobados. `apply`
  sigue bloqueado; solo el comando separado `promote` puede escribir, y aún no se ha
  ejecutado.
- La colección externa `universidades` observada en Atlas tiene un índice
  `ix_universidades_nombre` no necesariamente único y un validador previo. Antes de
  habilitar HU-075 en Atlas se debe revisar, autorizar y conciliar
  `ux_universidades_nombre_normalizado`; no se modificó Atlas.
- La allowlist `Admin__AccountIds` es un mecanismo provisional hasta que M13 defina
  la provisión, auditoría y revocación formales de administradores.
- **DEC-04 pendiente:** no hay proveedor de blobs autorizado. La carga de fotos M05
  está implementada con validación y metadatos, pero falla cerrada con `503` hasta
  aprobar/configurar el proveedor; no se usó almacenamiento simulado en producción.
- El Sprint 2 no implementa el envío a revisión ni el expediente de M06 (HU-013/HU-027,
  Sprint 4); las unidades permanecen en `Borrador`.
- M04/M07 requieren que M06 apruebe publicaciones para que existan unidades p?blicas
  reales; `arrendadorVerificado` permanece `false` hasta la verificaci?n de M06.
- La ubicaci?n de universidad no se administra por M03; sin ella la distancia es null
  y un filtro `distanciaMaxKm` responde 400. Se requiere carga controlada.
- El perfil devuelve `etiquetaCuenta=UsuarioUniHome` y `vinculadoAUnidad=false`
  mientras M05/M06/M09 no existan. Son indicadores derivados, no estados
  persistidos; deberán conectarse a esos módulos sin cambiar el contrato.
- El panel de HU-011 cubre Universidades en Sprint 1. Expedientes y Usuarios
  pertenecen a M06/M13 de sprints posteriores; no se crearon endpoints de relleno.
- Retención/limpieza de cuentas no confirmadas y anonimización de datos tras
  eliminar una cuenta (ADR-005).
- Scalar/Swagger UI solo si se aprueba explícitamente.
