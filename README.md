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
- Sin SQL Server, EF Core, Testcontainers ni Scalar. Swagger UI y el documento
  OpenAPI nativo existen como documentación interna **protegida** por el
  privilegio administrativo existente; no se exponen públicamente. Docker se usa
  únicamente para construir/validar la imagen de Azure Container Apps y los jobs
  de CI autorizados; no se usa Docker para el desarrollo local.

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
contraseña ni claims administrativos. `universidadActual` permanece `null` porque
M01 no persiste una asociación cuenta-universidad y M03 solo asocia
`UniversidadId` a propiedades/unidades; la dependencia funcional es HU-087 y no
se inventa una relación. La respuesta autenticada envía `Cache-Control: no-store`.
La barra de navegación y la visibilidad del panel son responsabilidad de Android;
el backend entrega los indicadores y revalida cada ruta administrativa.

Sprint 2 / M05: `POST /api/v1/propiedades`, `POST /api/v1/propiedades/{id}/unidades`,
`POST /api/v1/unidades/{id}/fotos` y `GET /api/v1/fotos/{photoRef}`, `PUT /api/v1/unidades/{id}/servicios`,
`PUT /api/v1/unidades/{id}/zona-reglas`, `GET /api/v1/unidades/{id}/vista-previa` y
`POST /api/v1/publicaciones/primera/terminos`. Todas las rutas requieren Bearer y
verifican que el recurso pertenezca al titular. La edad se recalcula en servidor.
La unidad nace en `Borrador`; el envío a revisión de Sprint 4 se implementó en M06.
La carga de fotos usa **Cloudinary Free** mediante `IUnitPhotoStorage`; las im?genes
se suben como `authenticated` y se entregan por `GET /api/v1/fotos/{photoRef}`, no por
una URL de Cloudinary. Si faltan `Cloudinary__CloudName`, `Cloudinary__ApiKey` o
`Cloudinary__ApiSecret`, el endpoint falla cerrado con `503`. La prueba real opt-in
sigue **PENDIENTE** de credenciales seguras. Detalle en
[`docs/dec-04-cloudinary.md`](dec-04-cloudinary.md).

Eliminación de cuenta (`204`, sin cuerpo): requiere `Authorization: Bearer`,
`contrasena` y `confirmacionPerdidaInformacion: true`. Hace borrado lógico
(`AccountStatus.Deleted`) y revoca todas las sesiones de la cuenta en todos los
dispositivos (a diferencia de `logout`, que solo revoca la sesión actual). Tras
eliminar, la cuenta no puede iniciar sesión, renovar tokens ni seguir usando un access
token emitido antes de eliminarla (BE-SEC-008 ya cubre esto: el estado se revisa en
cada petición autenticada). **Pendiente:** el rechazo `409` por vínculos activos de
propiedades/ocupación (M05/M09) y la política de retención/anonimización de datos
(ADR-005) no están implementados.

## Sprint 4 / M06: verificación documental

El flujo de primera publicación se completa con:

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/v1/expedientes/mi-expediente/ine` | Sube la INE (JPG/PNG/PDF, multipart, campo `Archivo`). |
| `POST` | `/api/v1/expedientes/mi-expediente/comprobante` | Sube escritura, boleta predial o constancia. |
| `POST` | `/api/v1/publicaciones/primera/enviar` | Confirma la primera publicación en `EnRevision`. |
| `GET` | `/api/v1/admin/expedientes` | Bandeja administrativa con solicitante, folio, fecha, estado y `esSospechoso`. |
| `GET` | `/api/v1/admin/expedientes/{id}` | Detalle con OCR, ELA/metadatos y enlaces temporales. |
| `GET` | `/api/v1/admin/expedientes/{id}/documentos/{tipo}` | Emite un enlace firmado temporal para el documento. |
| `GET` | `/api/v1/expedientes/documentos/{token}` | Consume el enlace; exige sesión administrativa y queda auditado. |

Las cargas se validan por firma binaria real (no por extensión/MIME declarado),
pesan como máximo 10 MB y rechazan PDF con JavaScript/Launch/EmbeddedFile. El
expediente nace `Pendiente`; pasa a `EnRevision` solo cuando existen ambos
documentos. La unidad también pasa a `EnRevision` si los términos están aceptados
y tiene al menos una foto, por lo que M04 no la muestra. La decisión
`Aprobado`/`Rechazado`, el motivo de rechazo y el cambio de etiqueta de cuenta
se implementan en Sprint 5; el dominio valida esas transiciones y los endpoints
quedan documentados en la sección siguiente.

Los documentos se guardan **cifrados con AES-256-GCM** en
`documentos_verificacion`; el binario en claro no se persiste. El detalle
administrativo nunca devuelve la `storageKey` ni una URL permanente. La respuesta
de carga incluye `avisoAlmacenamiento` con el texto «Almacenamiento cifrado y
acceso restringido» (HU-088). Las señales
ELA/metadatos son orientativas y se acompañan del aviso «El análisis automático
es orientativo y no determina la decisión». Si falta el motor OCR o el documento
no es legible, el nombre queda `null` y se marca revisión manual; no se inventa.
Detalle en [`docs/sprint-04-cierre.md`](docs/sprint-04-cierre.md) y
[`docs/mongodb-m06.md`](docs/mongodb-m06.md).

## Sprint 5 / M02-M06: dictamen y navegación

El Sprint 5 completa el dictamen humano y los indicadores de navegación:

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/v1/expedientes/mi-expediente` | Estado propio: documentos recibidos, revisión automática, decisión y motivo. |
| `POST` | `/api/v1/admin/expedientes/{id}/aprobar` | Aprobación administrativa; el expediente y la unidad pasan a `Aprobado`/`Aprobada`. |
| `POST` | `/api/v1/admin/expedientes/{id}/rechazar` | Rechazo con motivo obligatorio; la unidad no se publica. |
| `GET` | `/api/v1/mi-publicacion/estado-mapa` | Pin privado de la primera publicación `EnRevision`, con la etiqueta «Tu espacio · En revisión». |

Reglas principales:

- Solo un administrador puede decidir. La decisión se registra con actor, fecha,
  auditoría y CAS de concurrencia; una segunda decisión responde `409`.
- La aprobación y la unidad se sincronizan con compensación si falla la unidad o
  la auditoría; una publicación nunca se hace visible por accidente.
- La aprobación cambia la etiqueta derivada del perfil a
  `ConEspaciosPublicados` y el anuncio aparece en M04. El rechazo deja la unidad
  oculta y expone el motivo al solicitante.
- `PerfilResponse` añade `estadoPrimeraPublicacion` para que Android decida entre
  Resumen, «Tu espacio está en revisión» y el estado rechazado; la barra inferior
  y el menú Rentas siguen siendo UI.
- `resultadoComparacionNombres` compara OCR normalizado (acentos, orden, nombres
  compuestos). Una diferencia marca `esSospechoso` y **nunca** aprueba/rechaza
  automáticamente. Si falta texto fiable queda `NoDisponible`.
- `arrendadorVerificado` pasa a `true` en tarjeta, ficha, miniaturas y favoritos
  cuando el expediente del propietario está aprobado.
- PDF escaneados se rasterizan con `pdftoppm` (Poppler) y se leen con Tesseract;
  las miniaturas PDF también se generan localmente. Si falta el binario, el
  resultado se marca `NoDisponible` y la vista previa vuelve al PDF original.
- `DocumentSecurity:CurrentKeyVersion` y `PreviousEncryptionKeys` permiten rotar
  AES-256-GCM sin perder documentos anteriores. Los enlaces firmados siguen
  expirando en 300 s.

Detalle en [`docs/sprint-05-cierre.md`](docs/sprint-05-cierre.md), incluida la
matriz de las 56 HU de Sprints 1–5.

## Sprint 6 / M05: mis propiedades

El Sprint 6 añade, por ahora, la vista agregada de propiedades del titular:

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/v1/mis-propiedades` | Propiedades del titular con tipo, total de habitaciones, cuántas están disponibles y el estado de cada una (HU-033). |

Reglas principales:

- `HU-036` (dar de alta una propiedad desde el Resumen o desde Mis propiedades)
  y `HU-092` (agregar otra habitación a una propiedad ya registrada, sin volver a
  capturar su dirección) no requieren endpoints nuevos: reutilizan
  `POST /api/v1/propiedades` y `POST /api/v1/propiedades/{propertyId}/unidades`
  de M05 (Sprint 2), que ya permiten varias unidades sobre la misma propiedad sin
  reenviar domicilio. El origen del botón (Resumen o Mis propiedades) es
  responsabilidad de Android.
- `GET /api/v1/mis-propiedades` exige `Authorization: Bearer` y solo devuelve
  propiedades del titular autenticado; `tipoPropiedad` filtra por el tipo de la
  propiedad y `estadoHabitacion` incluye una propiedad si al menos una de sus
  habitaciones está en ese estado. Ambos filtros son opcionales y se combinan con
  `AND`. Un valor fuera del enum (incluido un entero) responde `400`.
  Una propiedad sin habitaciones aparece con `totalHabitaciones: 0`,
  `habitacionesDisponibles: 0` y `habitaciones: []`.
- No se añadió una migración nueva: la propiedad reutiliza el índice existente
  `ix_propiedades_propietario` de `M05-001` para listar por titular; las
  habitaciones de cada propiedad se obtienen con `ListByOwnerAsync` (ya usado por
  HU-028) y se agrupan en memoria por `propertyId`.
- La respuesta añade `Cache-Control: no-store` por ser datos propios del
  arrendador.

HU-033/036/092 pertenecen a M05; M08 (mensajería) del Sprint 6 se documenta al
cerrarse.

## Sprint 6 / M09: ocupación

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/v1/unidades/{unitId}/invitacion` | Código de invitación vigente de una unidad propia, con su fecha de vigencia (HU-048). |
| `POST` | `/api/v1/unidades/unirse` | Vincula al solicitante como inquilino con un código de invitación (HU-049). |
| `PUT` | `/api/v1/unidades/{unitId}/mantenimiento` | Marca o desmarca el mantenimiento de una unidad propia (HU-093). |
| `DELETE` | `/api/v1/unidades/{unitId}/inquilinos/{tenantAccountId}` | Da de baja a un inquilino de una unidad propia, con confirmación (HU-094). |
| `DELETE` | `/api/v1/unidades/{unitId}/mi-vinculo` | El propio inquilino deja la unidad, con confirmación (HU-095). |
| `GET` | `/api/v1/unidades/{unitId}/inquilinos` | Inquilinos activos de una unidad propia, con nombre y fecha de vínculo; nunca correo ni teléfono (HU-094). |

Reglas principales:

- El código de invitación es de la unidad, no de un inquilino concreto: se
  reutiliza mientras no venza y no se consume al unirse, así que varias
  personas pueden unirse a una unidad compartida con el mismo código mientras
  haya lugares. `GET /invitacion` es idempotente: devuelve el código vigente si
  existe, o genera uno nuevo (8 caracteres, sin `0`/`O`/`1`/`I`) si no hay uno o
  venció. La vigencia es de 7 días, un umbral provisional sin aprobación de
  producto. Los códigos anteriores no se borran, quedan vencidos.
- `POST /unidades/unirse` exige `codigo` en el cuerpo; un código inexistente o
  mal formado responde `400`, uno vencido responde `410`, el propietario no
  puede unirse a su propia unidad (`403`) y una unidad sin lugares responde
  `409`. Un inquilino ya vinculado recibe `200` de forma idempotente; uno que
  había dejado la unidad reactiva el mismo vínculo al volver a unirse.
- **Cupo protegido con CAS de un solo documento:** `unidades.lugaresOcupados`
  es un contador atómico mantenido exclusivamente por
  `IUnitRepository.TryReserveSlotAsync`/`ReleaseSlotAsync`
  (`findOneAndUpdate` con filtro `lugaresOcupados < lugares` e `$inc`). Si dos
  solicitudes de unión llegan al mismo tiempo con un solo lugar libre, MongoDB
  solo aplica el incremento a una; la otra recibe `409`. Los vínculos de
  ocupación (`vinculos_ocupacion`) no se usan para contar cupo, solo para el
  detalle por inquilino; contar documentos ahí sería una condición de carrera
  entre el conteo y la inserción. Cubierto por una prueba de concurrencia
  local (dos uniones simultáneas con un lugar) y una prueba real contra
  MongoDB (opt-in).
- `vinculos_ocupacion` guarda un único documento por par inquilino/unidad
  (`_id` determinista `inquilino:unidad`, igual que `favoritos`): salir y
  volver a unirse reactiva el documento existente en vez de crear uno nuevo.
- La disponibilidad de la unidad (`estadoOcupacion` en `UnidadResponse` y
  `UnidadVistaPreviaResponse`) se recalcula tras unirse, dar de baja o
  abandonar, a partir de `lugaresOcupados` frente a `lugares`: `Ocupado` si se
  alcanzan, `Disponible` en otro caso. `Mantenimiento` es un estado manual del
  propietario que no se sobreescribe automáticamente mientras esté activo; al
  desactivarlo se recalcula desde `lugaresOcupados`.
- HU-094 y HU-095 exigen `confirmacion: true` en el cuerpo, igual que
  `confirmacionPerdidaInformacion` en la eliminación de cuenta: la UI puede
  pedir confirmación, pero el backend también la exige. Sin ella responden
  `400`. HU-095 usa el claim `sub` de la sesión, nunca un id enviado por el
  cliente. Para dar de baja a un inquilino concreto (HU-094) el propietario
  primero consulta `GET /inquilinos` para obtener su `accountId`; el backend
  nunca expone ese id por otra vía (ni en el correo ni en el teléfono, que no
  se devuelven).
- `PerfilResponse.vinculadoAUnidad` (HU-008/HU-009) ahora es real: `true`
  cuando la cuenta tiene al menos un vínculo de ocupación activo como
  inquilino.
- Migración `M09-001`: crea `vinculos_ocupacion` (con
  `ix_ocupacion_inquilino_estado` e `ix_ocupacion_unidad_estado`, sin índice
  único declarado) e `invitaciones_unidad` (con `ix_invitaciones_unidad` y
  `ux_invitaciones_codigo` único). No modifica `M05-001`; `unidades` añade el
  campo flexible `lugaresOcupados` (además de `estadoOcupacion`, ya usado por
  M07) sin requerir una migración nueva.

## Swagger/OpenAPI protegido

La documentación interna se habilita explícitamente con
`Api__EnableSwagger=true`; el valor por defecto es `false`. Cuando está activa:

- `/swagger/login` es el único recurso público: formulario mínimo con CSRF,
  sin UI ni documento OpenAPI.
- Abrir `/swagger` sin sesión redirige a `/swagger/login`; OpenAPI y los assets
  responden `401`/redirección de autenticación y nunca se sirven antes del login.
- El login usa las mismas credenciales y reglas de estado que `/api/v1/auth/login`
  (`LoginService.AuthenticateAsync`), con el rate limit de login existente.
  Nunca crea una sesión de refresh ni devuelve un JWT al navegador.
- La autorización documental es `Admin__AccountIds` **o**
  `DocumentationAccess__AccountIds__0`, `__1`, ...: administradores y lectores
  autorizados acceden; una sesión válida sin permiso recibe `403`; sin sesión,
  `401`. El formulario devuelve un error genérico para credenciales no
  autorizadas. Un lector documental no obtiene acceso a `/api/v1/admin/*`.
- La sesión se guarda en la cookie `__Host-unihome-swagger` (HttpOnly, Secure,
  SameSite=Strict, 15 minutos por defecto). No se usa localStorage/sessionStorage.
  El formulario de logout exige CSRF y elimina la cookie; la cookie no autentica
  `/api/*`.
- Para clientes Bearer (Android/Postman) sigue disponible
  `POST /api/v1/auth/swagger-session` y `DELETE /api/v1/auth/swagger-session`.
- `SwaggerAccess__CookieLifetimeMinutes` se valida entre 1 y 60. No se guardan
  usuarios, contraseñas ni tokens en configuración.
- El login documental exige HTTPS lógico mediante el filtro estándar
  `RequireHttps`; un HTTP directo redirige a HTTPS sin emitir cookies. Detrás
  del ingress se acepta `X-Forwarded-Proto: https` solo desde
  `Proxy__KnownProxies`/`Proxy__KnownNetworks`; el DAST de CI simula ese proxy
  únicamente en loopback.
- La interfaz y el JSON envían `Cache-Control: no-store` y
  `X-Robots-Tag: noindex, nofollow`. La CSP de Swagger permite estilos inline
  necesarios para su renderizado y formularios same-origin, pero mantiene
  `script-src 'self'`, `form-action 'self'` y `frame-ancestors 'none'`; el resto
  de la API conserva `default-src 'none'`.
- La activación en Azure requiere autorización productiva. El valor
  `Api__EnableSwagger=false` sigue siendo el recomendado fuera de una ventana
  autorizada.

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
  todas las rutas `/admin/*`.
- `DocumentationAccess__AccountIds__0`, `DocumentationAccess__AccountIds__1`, ...
  contiene lectores de documentación sin privilegios administrativos. Se valida
  por GUID; una lista vacía no concede acceso. Los administradores conservan el
  acceso documental sin necesidad de duplicarlos aquí. La provisión definitiva de administradores pertenece a
  M13 y queda pendiente de aprobación.
- `Auth__RateLimit__LoginPermitLimit` / `LoginWindowSeconds` valen 10 intentos por 60
  segundos por IP; `RefreshPermitLimit` / `RefreshWindowSeconds` valen 20 por 60
  segundos; `ConfirmEmailPermitLimit` / `ConfirmEmailWindowSeconds` valen 10 por 60;
  `ResendPermitLimit` / `ResendWindowSeconds` valen 5 por 60;
  `PasswordResetRequestPermitLimit` / `PasswordResetRequestWindowSeconds` valen 5 por
  60; `PasswordResetConfirmPermitLimit` / `PasswordResetConfirmWindowSeconds` valen 10
  por 60; `DocumentUploadPermitLimit` / `DocumentUploadWindowSeconds` valen 10 por 60.
  Son umbrales provisionales pendientes de aprobación. El limitador usa el
  adaptador distribuido configurado: Upstash Redis cuando `Redis:Enabled=true` y el
  adaptador en memoria solo en Development/pruebas. Fuera de Development Redis es
  obligatorio y las IP se seudonimizan con `Auth:RateLimit:IpHashSalt`. El timeout
  por intento es `Redis:CommandTimeoutMs=2000`, con `Redis:MaxAttempts=2`,
  `Redis:RetryDelayMs=100` y `Redis:ReadinessTimeoutMs=800`. Los fallos
  transitorios se reintentan de forma acotada; FailClosed conserva el 503 y los
  errores de autenticación/contrato no se reintentan.
- `DocumentSecurity__EncryptionKey` debe ser Base64 de 32 bytes fuera de
  Development y se usa para AES-256-GCM. `DocumentSecurity__CurrentKeyVersion` y
  `DocumentSecurity__PreviousEncryptionKeys` permiten rotar conservando lectura;
  `DocumentSecurity__UrlSigningKey` es opcional (se deriva de la clave de cifrado)
  y `UrlLifetimeSeconds` vale 300 por defecto, máximo 900.
  `DocumentSecurity__PublicBaseUrl` permite enlaces absolutos HTTPS; si falta, el
  enlace es relativo y nunca se usa la cabecera `Host`. En Development, si falta
  la clave de cifrado, se genera una efímera por proceso y los documentos dejan de
  abrirse al reiniciar.
- `DocumentAnalysis__TesseractExecutablePath` apunta al binario Tesseract
  (`tesseract` por defecto). `TesseractLanguage=spa+eng`, timeout 30 s y PSM 6.
  `DocumentAnalysis__PdftoppmExecutablePath` apunta a Poppler (`pdftoppm`);
  `PdfRasterMaxPages=3` y `PdfRasterDpi=200` limitan el rasterizado. La imagen
  Docker instala `tesseract-ocr`, `tesseract-ocr-eng`, `tesseract-ocr-spa` y
  `poppler-utils`; si falta un binario, el OCR/rasterizado queda `NoDisponible`
  y no se inventa el nombre. ELA y metadatos se ejecutan en proceso con
  SkiaSharp/PdfPig y no requieren servicios.

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
  `PasswordRecovery__AndroidAppScheme` permite declarar un esquema Android
  (por ejemplo `unihome`) para deep links; se valida contra una lista de esquemas
  peligrosos y un App Link HTTPS sigue siendo la opción recomendada.
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

M07 usa `favoritos` (`accountId`, `unitId`, `createdAt`) con índice único
`ux_favoritos_cuenta_unidad`, índice `ix_favoritos_cuenta_fecha` y la migración
`M07-001` (checksum SHA-256
`003D4CC39CCB6ACC6E44C8762AEEBD60C417A08C03ABA35B2AE585392F38887E`); no cambia
M05-001. La API no crea la colección: si falta la migración o un índice, los
endpoints de favoritos fallan cerrados con `503`. Detalle, preflight de solo
lectura y recuperación en [`docs/mongodb-m07.md`](docs/mongodb-m07.md).

M05 usa `propiedades`, `unidades` y `aceptaciones_terminos` con la migración
`M05-001`; las fotos guardan solo metadatos y el binario se almacena en Cloudinary. Detalle en
[`docs/mongodb-m05.md`](docs/mongodb-m05.md).

M06 usa `expedientes_verificacion`, `documentos_verificacion` y
`eventos_expediente` con la migración `M06-001`. Los documentos se cifran con
AES-256-GCM antes de persistirse; el binario en claro no se guarda. El detalle de
colecciones, campos, índices, cifrado y límites está en
[`docs/mongodb-m06.md`](docs/mongodb-m06.md).

M09 usa `vinculos_ocupacion` (`inquilinoAccountId`, `unitId`, `estado`,
`fechaVinculo`, `fechaFin`, `motivoFin`; `_id` determinista
`inquilino:unidad`) e `invitaciones_unidad` (`unitId`, `codigo`, `createdAt`,
`vigenciaHasta`) con la migración `M09-001`. No modifica `M05-001`. Detalle en
la sección "Sprint 6 / M09: ocupación".

Sprint 3 / M04-M07: `GET /api/v1/exploracion/mapa`, `GET /api/v1/exploracion/lista`,
`GET /api/v1/unidades/{id}/tarjeta`, `GET /api/v1/unidades/{id}/ficha`,
`PUT/DELETE /api/v1/favoritos/{id}` y `GET /api/v1/favoritos`. Mapa, lista y tarjeta
son públicos (la tarjeta acepta Bearer opcional para `esFavorito`); ficha y favoritos
exigen sesión. Solo se exponen unidades Aprobadas y Disponibles. HU-007 reutiliza la
búsqueda M03 existente. No se expone correo ni teléfono del arrendador. La distancia
usa Haversine y el umbral de 1.5 km; no se inventan minutos ni rutas. Detalle en
[`docs/sprint-03-cierre.md`](docs/sprint-03-cierre.md) y
[`docs/mongodb-m07.md`](docs/mongodb-m07.md).

La API no crea colecciones ni índices al arrancar. Las migraciones versionadas
`M01-001`, `M03-001`, `M05-001`, `M06-001`, `M07-001` y `M09-001` se ejecutan con una herramienta separada:

```powershell
dotnet run --project tools/UniHome.MongoMigrator -- audit --database unihome_modelo_pruebas
dotnet run --project tools/UniHome.MongoMigrator -- plan --database unihome_modelo_pruebas
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M01-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M01-001
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M03-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M03-001
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M05-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M05-001
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M06-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M06-001
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M07-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M07-001
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M09-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M09-001
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
promoción selecciona una versión; hoy existen `M01-001`, `M01-002`, `M03-001`,
`M05-001`, `M06-001`, `M07-001` y `M09-001`. Las demás colecciones externas aún
no tienen migración versionada ni contratos BSON/DTO aprobados.

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
| `GET /health/ready` | MongoDB (conectividad e índices M01) y Redis/rate limiting distribuido disponibles. Sin configuración, esquema válido o Redis accesible responde `503`. |

La colección externa `restablecimientos_contrasena` no forma parte de `M01-001`; su
verificación de índices se ejecuta al invocar los endpoints de recuperación. Si falta
o diverge, HU-006 responde `503` y no se declara operativa.

## Errores y OpenAPI

- Errores homogéneos con `ProblemDetails`, incluyendo `traceId` y `errors` en validaciones.
- Excepciones no controladas: `500` genérico, sin stack trace.
- Todas las respuestas incluyen `X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`, `Permissions-Policy` y una CSP; las rutas de autenticación,
  cuentas y perfil añaden `Cache-Control: no-store`. La CSP de Swagger UI permite
  sus recursos propios y estilos inline; el resto de la API usa
  `default-src 'none'`.
  Fuera de Development, HSTS se emite para requests HTTPS con host público no
  loopback (`max-age=31536000; includeSubDomains`, sin preload). También se
  emite si el ingress declara `X-Forwarded-Proto: https`, aunque su red no esté
  en `Proxy:KnownProxies`; esa cabecera solo decide HSTS y no altera el esquema,
  la IP, la autenticación ni el rate limiting.
- OpenAPI nativo de .NET 10 disponible solo con `Api:EnableSwagger=true` y
  sesión administrativa: `GET /openapi/v1.json`. Incluye el esquema Bearer y
  marca `logout` como operación autenticada. La interfaz `/swagger` usa la misma
  policy; ver “Swagger/OpenAPI protegido”.
- Los 17 enums públicos (`Gender`, `PropertyType`, `PublicationState`, `UnitType`,
  `Occupancy`, `ForWhom`, `BathroomType`, `FurnitureType`, `UnitTemplate`,
  `ServiceName`, `ServiceMode`, `AccountLabel`, `EstadoExpediente`,
  `TipoDocumentoVerificacion`, `ResultadoComparacionNombres`, `UnitAvailability`,
  `OccupancyStatus`)
  se documentan como string con sus
  valores reales; los enteros se rechazan en runtime. `tipoPropiedad` y
  `estadoHabitacion` en `GET /api/v1/mis-propiedades` siguen el mismo criterio:
  un entero o un valor fuera del enum responde `400`. `POST
  /api/v1/unidades/{unitId}/fotos` se documenta como `multipart/form-data` con
  `Fotos` repetible, array de binarios, JPEG/PNG, 10 fotos, 5 MB por archivo y
  52 MB por solicitud.
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
- Mis propiedades: `200` siempre que la sesión sea válida (lista vacía si no hay
  propiedades), `400` por `tipoPropiedad`/`estadoHabitacion` inválidos, `401` sin
  sesión y `503` si MongoDB no está disponible.
- Invitación de unidad: `200`, `401` sin sesión, `403` si la unidad no es propia,
  `404` si no existe, `409` si no se pudo generar un código tras reintentar y
  `503`.
- Unirse con código: `200` (incluido el reingreso idempotente), `400` por código
  vacío/inexistente, `401` sin sesión, `403` si el propietario intenta unirse a su
  propia unidad, `409` sin lugares disponibles, `410` por código vencido, `413` y
  `503`.
- Mantenimiento: `200`, `401` sin sesión, `403` si la unidad no es propia, `404`
  si no existe, `413` y `503`.
- Baja de inquilino y abandono de unidad: `204`, `400` sin
  `confirmacion: true`, `401` sin sesión, `403` si la unidad no es propia (solo
  baja de inquilino), `404` si la unidad o el vínculo activo no existen, `413` y
  `503`.

## Pruebas

```powershell
dotnet restore UniHome.Backend.slnx --locked-mode
dotnet build UniHome.Backend.slnx --configuration Release -warnaserror
dotnet test UniHome.Backend.slnx --configuration Release
dotnet format UniHome.Backend.slnx --verify-no-changes
```

La suite local cubre M06 con validación de archivos, cifrado AES-GCM, rotación de
claves, firma y expiración de enlaces, transiciones y decisiones del expediente,
aislamiento administrativo, idempotencia, reenvío conflictivo, comparación de
nombres y visibilidad pública. La prueba OCR real se habilita con Tesseract y
Poppler locales:

```powershell
$env:UNIHOME_TESSERACT_PATH = "$env:LOCALAPPDATA\Programs\Tesseract-OCR\tesseract.exe"
$env:UNIHOME_PDFTOPPM_PATH = "<ruta-a-pdftoppm.exe>"
dotnet test tests/UniHome.UnitTests --configuration Release --filter "FullyQualifiedName~RealOcrTests|FullyQualifiedName~RealScannedPdfTests"
dotnet test tests/UniHome.IntegrationTests --configuration Release --filter "FullyQualifiedName~RealOcrVerificationEndpointTests"
```

Si Tesseract o Poppler no están instalados, esas pruebas se omiten explícitamente
y no se declaran aprobadas. El workflow manual
[`.github/workflows/ocr-documental.yml`](.github/workflows/ocr-documental.yml)
prepara la ejecución autorizada en CI con ambos motores reales.

Estado local del cierre: **790 unitarias** y **256 integraciones ejecutadas**
(9 opt-in de Mongo/Cloudinary omitidas de forma explícita). La matriz de las
56 HU de Sprints 1–5 está en [`docs/sprint-05-cierre.md`](docs/sprint-05-cierre.md).
La matriz de aceptación HTTPS de las 41 operaciones y la colección
Postman/Newman parametrizada están en
[`docs/aceptacion-sprints-1-5.md`](docs/aceptacion-sprints-1-5.md) y
[`tests/acceptance/`](tests/acceptance/). La ejecución remota autenticada queda
**PENDIENTE_AUTORIZACIÓN** hasta disponer de una sesión legítima.

Las pruebas de integración habituales usan `WebApplicationFactory` y dobles en memoria;
no requieren un clúster real. La CI de GitHub Actions (`.github/workflows/ci.yml`)
ejecuta restore/build/formato/pruebas, revisión de dependencias vulnerables y Gitleaks
sobre el historial completo; `.github/workflows/codeql.yml` ejecuta CodeQL para C#. No
despliega ni usa credenciales.

El job manual `mongo-db-test` (habilitado con la variable de repositorio
`UNIHOME_ENABLE_MONGO_CI=true`) levanta MongoDB Community 8.0.34 dentro del runner
de GitHub Actions, en un contenedor efímero fijado por digest. No usa Atlas, Docker
local ni secretos: la URI se limita a `127.0.0.1`, la base es
`unihome_ci_verificacion`, se aplican `M01-001`, `M01-002`, `M03-001`, `M05-001`,
`M06-001` y `M07-001` a esa base temporal y las pruebas reales de identidad,
recuperación de contraseña, universidades, publicaciones, verificación documental,
exploración/favoritos y bloqueo distribuido
se ejecutan con TRX. El job falla
si MongoDB no inicia, si alguna migración no queda válida, si las pruebas se omiten
o si no se genera la evidencia; el artefacto se publica como `mongodb-ci-results`.
La ejecución manual de verificación
([43395389178](https://github.com/unihome0412/unihome-backend/actions/runs/43395389178),
commit de código `f51ac10`) dejó 4/4 pruebas reales aprobadas, 0 omitidas,
`M01-001` y `M03-001` válidas y la limpieza temporal confirmada. **Sprint 4/5**
añade `M06-001` y las dos pruebas de verificación documental; la siguiente
ejecución autorizada debe mostrar **8/8** y permanece **PENDIENTE_CI**.

El job manual `dast-zap` (variable `UNIHOME_ENABLE_ZAP=true`) ejecuta un
**baseline pasivo real** de OWASP ZAP con la imagen oficial
`ghcr.io/zaproxy/zaproxy:2.17.0` fijada por digest. Levanta la API solo en
`127.0.0.1:5088`, un MongoDB efímero inicializado con M01-001, M01-002, M03-001,
M05-001, M06-001 y M07-001, y un SMTP de captura local con STARTTLS; crea cuentas sintéticas mediante registro,
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

**Sprint 4:** el catálogo OpenAPI pasó a **37 operaciones**. La ejecución manual
#280 visitó 30/37 porque el fixture DAST no ejercía todavía las rutas M06; el
fixture se amplió para cargar INE/comprobante, enviar la primera publicación y
consultar bandeja/detalle/enlace firmado administrativo. La nueva ejecución
37/37 quedó **PENDIENTE_CI** y no se declara PASS anticipado.

**Sprint 5:** el catálogo OpenAPI pasa a **41 operaciones**. El fixture DAST
ejecuta el flujo completo con dos cuentas: la primera consulta su estado y su pin
privado, y el administrador la aprueba; la segunda repite el flujo y el
administrador la rechaza con motivo, que el solicitante consulta después. La
exploración M04 se valida tras la aprobación real. La ejecución real 41/41 queda
**PENDIENTE_CI** y no se declara PASS anticipado.

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
- **DEC-04 resuelto en c?digo con Cloudinary Free:** las fotos se suben como
  `authenticated` y se entregan mediante `GET /api/v1/fotos/{photoRef}`; la URL
  firmada de Cloudinary y las credenciales no salen del backend. Sin
  `Cloudinary__CloudName`, `Cloudinary__ApiKey` o `Cloudinary__ApiSecret`, el
  almacenamiento falla cerrado con `503`. La prueba real opt-in est? **PENDIENTE**
  de credenciales seguras. Detalle en
  [`docs/dec-04-cloudinary.md`](docs/dec-04-cloudinary.md).
- **Sprints 4–5 implementados, no productivos:** M06 recibe INE/comprobante,
  ejecuta OCR/ELA/metadatos, aplica comparación orientativa de nombres, registra
  decisiones administrativas con auditoría y sincroniza la unidad. La publicación
  solo aparece en M04 después de aprobación; el rechazo expone el motivo al
  solicitante. Notificaciones push/mensajería M12 pertenecen a Sprint 6.
- `arrendadorVerificado` se calcula desde un expediente aprobado en tarjeta,
  ficha, miniaturas y favoritos; `etiquetaCuenta` y
  `estadoPrimeraPublicacion` permiten la navegación de arrendador.
- `M06-001` no se aplicó ni promovió a Atlas. La prueba real
  `MongoVerificationPersistenceTests` está preparada y **BLOCKED** en el entorno
  local sin `mongod`; debe ejecutarse en `mongo-db-test` con autorización.
- OCR: Tesseract y Poppler están integrados localmente; un PDF escaneado se
  rasteriza y se lee con OCR real. Si falta un binario, se marca `NoDisponible`
  y no se inventa el nombre. La miniatura PDF se genera con Poppler; si falla,
  se devuelve el PDF original.
- Cifrado documental: AES-256-GCM con rotación de claves mediante
  `CurrentKeyVersion`/`PreviousEncryptionKeys`. La política de retención/purga de
  versiones sigue pendiente (ADR-005).
- El panel de HU-011 cubre Universidades en Sprint 1; la bandeja administrativa de
  expedientes ya existe en backend. Usuarios pertenece a M13 y no se crearon
  endpoints de relleno.
- Retención/limpieza de cuentas no confirmadas y anonimización de datos tras
  eliminar una cuenta (ADR-005).
- Scalar no se usa. Swagger UI está implementado y protegido por la policy
  administrativa; su activación por entorno sigue siendo una decisión explícita.
