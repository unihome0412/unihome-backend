# Corrección de aceptación funcional Sprint 1 y Sprint 2

**Rama:** `fix/s1-s2-functional-acceptance`
**Base:** `main` en `ca5f430b5981ccedb02f1d332e7e390bf53ed17a`
**Alcance:** corregir K3, K4, K5, K8, K9 y K10 sin ampliar Sprint 3 ni modificar Azure.

## 1. K3 — Login 503 intermitente por Redis

### Causa exacta

`UpstashRedisDistributedRateLimiter` construía un `HttpClient` con
`Redis:CommandTimeoutMs=250`. El primer request HTTPS después de inactividad
(incluye DNS/TCP/TLS y la latencia de Upstash) superaba 250 ms y producía
`TaskCanceledException`. El rate limiting distribuido está en `FailClosed`, por
lo que el lease se marcaba como servicio no disponible y `/api/v1/auth/login`
respondía `503`. Un reintento posterior encontraba la conexión caliente y
devolvía `401` correctamente.

### Corrección

- Timeout por intento recomendado y predeterminado: **2000 ms**.
- Reintento acotado ante fallos transitorios: **1 reintento adicional**
  (`MaxAttempts=2`), con espera de 100 ms.
- Solo se reintentan timeout, error de conexión y HTTP 408/425/429/5xx.
- No se reintentan HTTP 401/403 ni errores de contrato de Redis.
- El timeout se aplica por intento con `CancellationTokenSource`; también
  aplica cuando se inyecta un `HttpClient` de pruebas.
- Se conserva `FailClosed`: si se agotan los intentos, login sigue devolviendo
  `503`, nunca `401`.
- No se usan contadores locales en Production ni se sustituye Redis por memoria.
- Los detalles internos no salen en la respuesta HTTP; `OnRejected` mantiene el
  `ProblemDetails` genérico.

### Variables para el próximo Gate

| Variable | Valor recomendado | Obligatoria |
|---|---|---|
| `Redis__CommandTimeoutMs` | `2000` | Sí, si Azure conserva un override de 250 |
| `Redis__MaxAttempts` | `2` | No (valor por defecto) |
| `Redis__RetryDelayMs` | `100` | No (valor por defecto) |
| `Redis__ReadinessTimeoutMs` | `800` | No (valor por defecto) |

No se modificó ningún valor de Azure en esta tarea.

## 2. K9 — Readiness

- `/health/live` sigue comprobando únicamente el proceso y no consulta Redis,
  MongoDB ni ningún servicio externo.
- `/health/ready` ahora incluye `storage` (MongoDB + índices M01) y `redis`.
- `redis` ejecuta `PING` contra Upstash con timeout acotado de 800 ms y sin
  reintentos para no exceder el timeout de la sonda de Azure.
- Si Redis falla, readiness responde `503`; si se recupera, vuelve a `200`.
- No se exponen URL, token ni mensajes de excepción.
- No se modificaron las sondas de infraestructura. El PING queda por debajo del
  timeout habitual de 1 s. Si un Gate confirma otro `timeoutSeconds`, ajustar
  solo `Redis__ReadinessTimeoutMs` por debajo de ese valor.

## 3. K4 — Enums OpenAPI

El runtime ya usa `JsonStringEnumConverter(allowIntegerValues: false)`. El
documento OpenAPI ahora describe como `string` los 12 enums públicos, con los
valores reales de cada enum:

`Gender`, `PropertyType`, `PublicationState`, `UnitType`, `Occupancy`,
`ForWhom`, `BathroomType`, `FurnitureType`, `UnitTemplate`, `ServiceName`,
`ServiceMode`, `AccountLabel`.

No se cambió la representación JSON ni se habilitaron enteros. Las pruebas
cubren schema, valores, request, response, rechazo de enteros y un flujo real de
propiedad/unidad.

## 4. K5 — Fotos multipart

`POST /api/v1/unidades/{unitId}/fotos` se documenta ahora como:

- `Content-Type: multipart/form-data`.
- Campo repetible `Fotos`.
- Schema `array` de `string`/`binary`.
- `minItems=1`, `maxItems=10`.
- 5 MB por archivo y 52 MB por solicitud.
- JPEG/PNG validados por firma binaria.
- Bearer obligatorio y respuestas 200/400/401/403/404/409/413/503.

La normalización de RESTler valida ese contrato antes de generar la gramática y
aborta si encuentra `application/x-www-form-urlencoded`, un tipo distinto de
array o un elemento que no sea binario. ZAP ya enviaba multipart real.

## 5. K10 y K8 — Perfil, caché y HSTS

- `universidadActual` sigue en `null`: M01 no persiste una asociación
  cuenta-universidad y M03 solo asocia `UniversidadId` a propiedades/unidades.
  No se inventa una relación. La dependencia funcional es HU-087.
- `/api/v1/perfil/mi-perfil` ahora envía `Cache-Control: no-store`; las
  respuestas públicas no reciben esa cabecera.
- HSTS se configura explícitamente con 365 días, `includeSubDomains` y sin
  `preload`. Solo se emite en requests HTTPS con host público no loopback y
  `X-Forwarded-Proto` confiable.
- Se conservan estrictamente:
  - `Proxy__TrustForwardedHeaders=true`
  - `Proxy__KnownProxies__0=127.0.0.1`
  - `Proxy__KnownProxies__1=::1`
- No se desactivó el fail-fast de proxies.

## 6. Gate administrativo (K1/K2), sin ejecutar ahora

Procedimiento mínimo para un Gate posterior:

1. Validar el `sub` de una cuenta real autorizada, sin crear cuentas.
2. Configurar únicamente ese ID en `Admin__AccountIds`.
3. Verificar `401` anónimo y `403` de una cuenta no administradora.
4. Verificar el acceso real del administrador.
5. Cargar el catálogo de universidades aprobado por el Scrum Master.
6. Verificar búsqueda y creación de propiedad con datos autorizados.

### Datos mínimos que debe aprobar el usuario

Para el administrador:

- Cuenta real existente y confirmada.
- `sub`/ID de cuenta en formato GUID de 32 hex (`N`), sin inventarlo.
- Nombre del responsable que autoriza.

Para el catálogo de universidades:

- Lista de nombres y campus exactos.
- Confirmación de que cada universidad debe quedar activa.
- Autorización explícita para escribir ese catálogo en la base objetivo.
- Entorno y base destino (no usar `unihome_desarrollo` por accidente).

No se crean administradores, GUID, universidades ni escrituras en MongoDB Atlas
en esta corrección.

## 7. Cloudinary y galería

- No se ejecutaron subidas reales ni se crearon secretos.
- `CloudinaryPersistenceTests` ya limpia el asset en `finally` y verifica que
  deja de existir.
- Se preparó el job manual `cloudinary-integration`, protegido por el environment
  `cloudinary-integration` y por `UNIHOME_ENABLE_CLOUDINARY_INTEGRATION=true`.
  Requiere secretos dedicados y aprobación humana; no se ejecuta en PR/push.
- Límites actuales del contrato: solo `fotoPrincipalUrl`, sin listado completo
  de `photoRef` y sin `DELETE` público de fotografías.
- No se agregan endpoints nuevos sin contrato y alcance SDD aprobados.

## 8. Verificación local

Comandos ejecutados en esta rama:

```powershell
dotnet restore UniHome.Backend.slnx --locked-mode
dotnet build UniHome.Backend.slnx --configuration Release --no-restore -warnaserror
dotnet test tests/UniHome.UnitTests/UniHome.UnitTests.csproj -c Release --no-build --no-restore
dotnet test tests/UniHome.IntegrationTests/UniHome.IntegrationTests.csproj -c Release --no-build --no-restore
python -m unittest discover -s tools/ci -p 'test_*.py'
dotnet format UniHome.Backend.slnx --verify-no-changes --no-restore
git diff --check
```

Resultados locales: unitarias 562/562; integración 190 aprobadas y 6 omitidas
por opt-in (MongoDB real y Cloudinary real); Python 161/161. Las pruebas
externas de GitHub Actions, RESTler, ZAP, SonarCloud, Stryker, Docker y
Cloudinary real quedan pendientes de commit/push manual y de sus Gates.

## 9. Estado de infraestructura y Git

- Azure, Container Apps, MongoDB Atlas, Upstash, Cloudinary e ingress no fueron
  modificados.
- El SHA `ca5f430b5981ccedb02f1d332e7e390bf53ed17a` permanece operativo.
- No se ejecutó `git add`, `commit`, `push`, `merge`, `gh pr create` ni
  `gh pr merge`.
- No se modificó `main` ni el PR #35.
