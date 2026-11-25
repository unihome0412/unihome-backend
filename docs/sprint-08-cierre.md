# Sprint 8 — Cierre backend M11, Resumen, M13 y cierres de M01/M02/M03

**Rama:** `feature/sprint-08-backend-integral`
**Base:** `6da78fd` (Sprint 7 en `main`, PR #62)
**Alcance:** HU-034, 062, 063, 064, 065, 066, 072, 073, 074, 087 y 096 (11 HU
de la hoja de Sprint Planning). **Fuera de alcance:** el resto de M13 (si lo
hubiera) y cualquier endpoint de lectura/edición de la fotografía de un
reporte de reparación (HU-063 solo pide "adjuntar"; no se agregó un proxy de
lectura dedicado, a diferencia de las fotos de unidad).

## 1. Estado de las 11 HU

| HU | Módulo | Estado | Entrega backend | Pruebas |
|---|---|---|---|---|
| HU-034 | M05/M10/M11 | Implementada | `GET /mi-resumen`: rentas registradas del mes en curso, número de propiedades y reparaciones abiertas (Pendiente + En proceso) | `GetMySummaryServiceTests` + integración |
| HU-062/063 | M11 | Implementada | `POST /unidades/{unitId}/reparaciones` (multipart/form-data): título, descripción y foto opcional reutilizando `UnitPhotoValidator`/`IUnitPhotoStorage` (Cloudinary) | `CreateReparacionServiceTests` + integración |
| HU-064 | M11 | Implementada | `GET /unidades/{unitId}/reparaciones`: reportes de la unidad con estado, quién los levantó (nombre, nunca correo) y su detalle | `ListReparacionesServiceTests` + integración |
| HU-065 | M11 | Implementada | `PUT /unidades/{unitId}/reparaciones/{reparacionId}/estado`: el propietario mueve el reporte entre Pendiente/En proceso/Atendido, en cualquier dirección | `ChangeReparacionStatusServiceTests` + integración |
| HU-066 | M12 | Implementada | Al cambiar el estado (HU-065) se genera de inmediato una notificación `ReparacionActualizada` para todos los demás con acceso a la unidad (propietario + inquilinos activos), visible en `GET /notificaciones` | Cubierto en `ChangeReparacionStatusServiceTests` + integración (HU-065/066 combinadas) |
| HU-072 | M13 | Implementada | `PUT /admin/cuentas/{accountId}/estado`: el administrador suspende o reactiva una cuenta; suspender revoca todas sus sesiones | `SetAccountStatusServiceTests` + integración |
| HU-073 | M13 | Implementada | `GET /admin/cuentas`: búsqueda por nombre/correo, filtro por categoría (Todos/BuscanAlojamiento/ConPublicaciones/EnRevision) y paginación con cabeceras `X-Total-Count`/`X-Page`/`X-Page-Size` | `SearchAccountsServiceTests` + integración |
| HU-074 | M13 | Implementada | `PUT /admin/cuentas/{accountId}`: edita nombre completo y correo de una cuenta | `EditAccountServiceTests` + integración |
| HU-087 | M02/M03 | Implementada | `PUT /perfil/mi-universidad`: fija `Account.CurrentUniversityId`; mismo endpoint sirve al perfil y al mapa. `GET /perfil/mi-perfil` deja de forzar `universidadActual` a null | `ChangeMyUniversityServiceTests` + integración |
| HU-096 | M01 (cierre M05/M09) | Implementada | `DELETE /cuentas/mi-cuenta` ahora responde `409` por vínculo de ocupación activo o propiedad con inquilinos, y `403` si la cuenta es administrativa | `DeleteAccountServiceTests` (3 casos nuevos) + integración (3 casos nuevos) |

## 2. Endpoints nuevos

| Método | Ruta | Autorización | Éxito | Errores |
|---|---|---|---|---|
| GET | `/api/v1/mi-resumen` | Bearer | 200 | 401, 503 |
| POST | `/api/v1/unidades/{unitId}/reparaciones` | Bearer propietario o inquilino activo | 201 | 400, 401, 403, 404, 413, 503 |
| GET | `/api/v1/unidades/{unitId}/reparaciones` | Bearer propietario o inquilino activo | 200 | 401, 403, 404, 503 |
| PUT | `/api/v1/unidades/{unitId}/reparaciones/{reparacionId}/estado` | Bearer propietario | 200 | 400, 401, 403, 404, 413, 503 |
| GET | `/api/v1/admin/cuentas` | Bearer admin | 200 | 400, 401, 403, 503 |
| PUT | `/api/v1/admin/cuentas/{accountId}/estado` | Bearer admin | 200 | 400, 401, 403, 404, 413, 503 |
| PUT | `/api/v1/admin/cuentas/{accountId}` | Bearer admin | 200 | 400, 401, 403, 404, 413, 503 |
| PUT | `/api/v1/perfil/mi-universidad` | Bearer | 200 | 400, 401, 413, 503 |

El catálogo OpenAPI pasa de 61 a 69 operaciones (8 nuevas: 3 de M11, 1 de
Resumen, 3 de M13 y 1 de universidad propia).

`DELETE /api/v1/cuentas/mi-cuenta` (ya existente) agrega `403` y `409` a su
contrato publicado; no es un endpoint nuevo.

## 3. Decisiones provisionales (sin aprobación de producto)

Mismo estatus que los umbrales y criterios de Sprints anteriores:

- **El estado de un reporte de reparación admite cualquier transición, no
  solo lineal** (`Reparacion.ChangeStatus`, HU-065). No hay una regla de
  negocio que impida volver de `Atendido` a `En proceso`: el propietario
  puede estar corrigiendo un cierre apresurado. Fijar el mismo estado que ya
  tenía es un no-op (no actualiza `updatedAt` ni genera notificación).
- **Quién puede levantar un reporte vs. quién puede cambiar su estado son
  cosas distintas** (HU-062 vs. HU-065). Levantar un reporte solo exige tener
  acceso a la unidad (ser su propietario o tener un vínculo de ocupación
  activo, M09): un inquilino debe poder avisar de una fuga sin depender del
  propietario. Cambiar el estado es una operación exclusiva del propietario,
  igual que el resto de la operación de la unidad (mantenimiento, baja de
  inquilinos, M09); un inquilino que intenta cambiarlo recibe `403`.
- **HU-066 notifica a todos los que tienen acceso a la unidad, no solo a
  quien levantó el reporte.** "Cuando se actualice un reporte de mi unidad"
  se interpretó como cualquiera con acceso a esa unidad (propietario +
  inquilinos activos), no solo el autor original del reporte: si el
  propietario resuelve un reporte que él mismo levantó (p. ej. tras una
  inspección), los inquilinos activos igual se enteran de que ya se
  atendió. Nunca se notifica a quien hizo el cambio (sin autonotificación).
  A diferencia del recordatorio de renta (HU-059, generado al consultar
  `GET /notificaciones`), esta notificación se genera en el momento mismo
  del cambio de estado, con un id aleatorio (no determinista): cada cambio
  es un evento propio, no una ventana que se regenera.
- **La categoría de búsqueda administrativa (HU-073) reutiliza exactamente
  la etiqueta del perfil propio** (`AccountLabel` de HU-002/008/009):
  `BuscanAlojamiento` = sin unidades propias, `ConPublicaciones` = al menos
  una unidad aprobada, `EnRevision` = al menos una en revisión y ninguna
  aprobada. No se inventó una taxonomía administrativa aparte. El filtro
  `Todos` excluye las cuentas con `AccountStatus.Deleted` (una cuenta
  eliminada ya no es un sujeto de administración activo); como la categoría
  depende de las unidades de cada cuenta, la paginación se aplica después de
  calcularla, en memoria, no en la consulta a Mongo — aceptable a la escala
  de este proyecto, consistente con el resto de servicios que enriquecen una
  página con una consulta cruzada por elemento (p. ej. `ListExpedientesService`).
- **`PUT /perfil/mi-universidad` solo acepta una universidad activa del
  catálogo** (HU-087). Una universidad desactivada por un administrador
  (HU-077) no debería ofrecerse como destino nuevo, aunque una cuenta que ya
  la tuviera fijada desde antes la conserve (no se revalida retroactivamente
  al desactivar el catálogo).
- **Orden de las validaciones de HU-096**: primero se verifica que la cuenta
  no sea administrativa (`403`), después que no tenga vínculos de ocupación
  activos ni propiedades con inquilinos (`409`), y solo entonces se borra.
  El orden no tiene un efecto observable distinto (ambas son condiciones
  excluyentes de borrado), pero refleja la jerarquía: una cuenta
  administrativa nunca se autoelimina por esta vía, sin importar su estado
  de ocupación.
- **`GET /mi-resumen` (HU-034) no recibe un periodo por parámetro**: siempre
  usa el mes en curso del reloj del servidor, igual que el recordatorio de
  renta (HU-059). Las rentas del mes reutilizan `RentPeriodResolver` (con
  herencia de HU-050/051/052): un periodo nunca configurado para una unidad
  aporta cero sin necesidad de crearlo primero, igual que en HU-060/061.

Ninguna de estas seis decisiones tiene aprobación de producto registrada. Si
el equipo de producto fija otro valor, requiere una historia de cambio
explícita: hoy están codificadas en `Reparacion.ChangeStatus`,
`CreateReparacionService`/`ChangeReparacionStatusService` (acceso vs.
autoridad), `ChangeReparacionStatusService.NotifyUnitParticipantsAsync`,
`SearchAccountsService.MapFilter`/`IAccountRepository.SearchAsync`,
`ChangeMyUniversityService.ChangeAsync` y `DeleteAccountService.DeleteAsync`.

## 4. M11: diseño y cobertura (HU-062/063/064/065/066)

- **Acceso a la unidad, no propiedad del reporte.** `Reparacion` no tiene un
  concepto de "visibilidad" propio: quien puede ver/crear reportes de una
  unidad es exactamente quien puede acceder a ella (propietario o inquilino
  con vínculo activo, `OccupancyStatus.Activo`), reutilizando
  `IOccupancyLinkRepository.FindAsync`/`ListActiveByUnitAsync` ya existentes
  de M09. No se creó una tabla de permisos nueva.
- **Fotografía opcional reutilizando Cloudinary sin cambios.**
  `UnitPhotoValidator` (firma JPEG/PNG, límite de 5 MB) y `IUnitPhotoStorage`
  (Cloudinary `authenticated`, DEC-04) se reutilizan sin modificación para
  `ReparacionFoto`: la validación de contenido y el almacenamiento no son
  específicos de "fotos de unidad", son genéricos para cualquier fotografía
  subida por un usuario. `CreateReparacionService` limpia el storage
  (`DeleteAsync`) si la persistencia del reporte falla después de subir la
  foto, igual que `AddUnitPhotosService`.
- **`NotificationType.ReparacionActualizada` (nuevo) y `ReparacionId` (nuevo
  campo, nullable) en `Notification`.** El esquema de notificaciones admite
  más tipos sin migrar (ya documentado desde HU-059); se agregó el campo
  `reparacionId` a `notificaciones` sobre `M12-001` sin migración nueva,
  mismo patrón que `lugaresOcupados` sobre `unidades` en Sprint 6. El
  `Aviso` de pagos informativos (HU-090) solo se incluye para
  `RecordatorioRenta`; las notificaciones de reparación no llevan ese aviso
  porque no son del dominio de renta.
- Migración `M11-001`: crea `reparaciones` (`ix_reparaciones_unidad`,
  `ix_reparaciones_estado`). No modifica M01-M10/M12.
- Cobertura: dominio (`ReparacionDomainTests`, `NotificationDomainTests`
  ampliado), los tres servicios de Application con dobles en memoria
  (incluye el caso "el propio reportante cambia el estado y no se
  autonotifica" y "mismo estado, no genera notificación"), y los 13 casos
  HTTP de `ReparacionesEndpointTests` (éxito con/sin foto, 401,
  403, 404, 400, sin correo del reportante en la respuesta, HU-066
  verificada de punta a punta vía `GET /notificaciones`).

## 5. M05/M10/M11: Resumen del titular (HU-034)

`GetMySummaryService` recorre las unidades propias (`IUnitRepository.ListByOwnerAsync`)
y, por cada una, resuelve el periodo del mes en curso con el mismo
`RentPeriodResolver` de HU-060/061 (suma los pagos informativos registrados)
y cuenta sus reportes de reparación no `Atendido`
(`IReparacionRepository.ListByUnitAsync`). El número de propiedades es
`IPropertyRepository.ListByOwnerAsync(...).Count`, igual que HU-033. Una
cuenta sin propiedades recibe el resumen en ceros, nunca un error, mismo
criterio que HU-060/061.

## 6. M13: administración de cuentas (HU-072/073/074)

- **Suspender revoca sesiones; reactivar no las restaura.**
  `SetAccountStatusService` llama a `IRefreshSessionRepository.RevokeAllForAccountAsync`
  solo al suspender, mismo mecanismo y motivo (`account_suspended_by_admin`)
  que usa la eliminación de cuenta (`account_deleted`). El middleware de
  autenticación (BE-SEC-008) ya rechaza el access token de una cuenta que
  deje de estar `Active` en cada petición, así que una cuenta suspendida
  queda sin acceso de inmediato aunque su access token no haya expirado.
- **`Account.Suspend()`/`Reactivate()` son simétricos a `Delete()`:** lanzan
  `InvalidOperationException` si la cuenta ya está en el estado pedido o si
  está eliminada (una cuenta eliminada no se puede "reactivar" por esta vía).
- **Edición de cuenta (HU-074) reutiliza el índice único de correo.**
  `Account.EditProfile` ya no deja `FullName`/`Email` inmutables tras el
  registro; `IAccountRepository.UpdateAsync` traduce el conflicto de índice
  único a `DuplicateEmailException`, que `EditAccountService` convierte en
  `400` (campo `correo`) en vez de `409`, porque desde la perspectiva del
  administrador es un dato de entrada inválido, no un conflicto de
  concurrencia.
- **Sin migración nueva.** Ambas operaciones leen/escriben `cuentas`
  (M01-001). `IAccountRepository.SearchAsync` hace una búsqueda de texto
  (regex insensible a mayúsculas sobre `nombreCompleto`/`correoNormalizado`)
  sin índice de texto dedicado; a la escala de este proyecto (cuentas de
  prueba, no miles de usuarios reales) un `collscan` acotado es aceptable,
  mismo criterio que otras consultas cruzadas de este codebase.
- Cobertura: dominio (`AccountTests` ampliado con `Suspend`/`Reactivate`/`EditProfile`),
  los tres servicios de Application con `InMemoryAccountRepository`
  (reescrito para indexar por `Id` en vez de por correo, porque HU-074 ahora
  permite cambiarlo), y 14 casos HTTP de `AdminCuentasEndpointTests` (éxito,
  filtro por categoría, filtro inválido, 403 sin privilegio, 401, 404,
  correo duplicado, y que ninguna respuesta expone `hashContrasena`).

## 7. M02/M03: universidad propia (HU-087)

`Account.CurrentUniversityId` (nuevo, `Guid?`) se persiste en `cuentas` como
`universidadActualId` (campo flexible, sin migración nueva, mismo patrón que
`lugaresOcupados`). `ChangeMyUniversityService` valida que la universidad
exista y esté activa (`IUniversityRepository.FindByIdAsync`) antes de
fijarla; `GetProfileService` la resuelve de vuelta a `University` para que
`GET /perfil/mi-perfil` deje de forzar `universidadActual` a `null`. Un solo
endpoint (`PUT /perfil/mi-universidad`) sirve tanto al flujo del perfil como
al del mapa: el backend no distingue el origen de la solicitud, así que no
se duplicó la ruta.

## 8. Cierre de eliminación de cuenta (HU-096)

`DeleteAccountService` ya hacía el borrado lógico y la revocación de
sesiones desde M01; este sprint completa las dos validaciones que quedaban
pendientes desde entonces:

- **409 por vínculos activos.** Se rechaza si la cuenta tiene un vínculo de
  ocupación activo como inquilino (`IOccupancyLinkRepository.HasActiveLinkAsync`)
  o si alguna de sus unidades propias todavía tiene inquilinos activos
  (`IUnitRepository.ListByOwnerAsync` + `ListActiveByUnitAsync` por unidad).
  En ambos casos hay otra persona cuya ocupación depende de esta cuenta.
- **403 si es administrativa.** Nuevo puerto `IAdminAccessChecker` en
  Application, implementado en Api (`AdminAccessChecker`) sobre el mismo
  `AdminAccessOptions`/`AccountIds` que ya protege las rutas `/admin/*`
  (ARQ-004): Application no referencia tipos de Api directamente, solo la
  pregunta de negocio ("¿esta cuenta es administrativa?").
- Cobertura: 3 casos nuevos en `DeleteAccountServiceTests` (vínculo activo,
  propiedad con inquilinos, cuenta administrativa) y 3 casos HTTP nuevos en
  `DeleteAccountEndpointTests`, seedeando el estado directamente en los
  repositorios en memoria del factory de pruebas.

## 9. Cambios MongoDB

- **`M11-001` (nueva):** `reparaciones` (`ix_reparaciones_unidad`,
  `ix_reparaciones_estado`).
- **Sin migración nueva para M13 ni para universidad propia:** `cuentas`
  (M01-001) añade los campos flexibles `universidadActualId` (HU-087) y se
  consulta con una búsqueda de texto nueva (HU-073) sin índice dedicado;
  `notificaciones` (M12-001) añade el campo flexible `reparacionId`
  (HU-066). Ningún endpoint de este cierre modificó un esquema ya publicado.
- Ninguna migración de este sprint se aplicó ni promovió a Atlas. El orden
  completo para un entorno que parte de Sprint 7 es `M01-001`, `M01-002`,
  `M03-001`, `M05-001`, `M06-001`, `M07-001`, `M08-001`, `M09-001`,
  `M10-001`, `M11-001`, `M12-001`. No saltar `M11-001`.

## 10. Pendientes

1. **CI:** PR a `main` abierto desde `feature/sprint-08-backend-integral`,
   pendiente de ejecutar en GitHub Actions. Localmente: build/formato en
   verde, **1050/1050 unitarias PASS** (4 omitidas: OCR/PDF reales),
   **372/372 integraciones ejecutadas PASS** (13 opt-in Mongo/Cloudinary
   omitidas explícitamente), **167/167 Python CI PASS**, catálogo OpenAPI en
   **69 operaciones** y fixture DAST local en cobertura **69/69**.
2. Las seis decisiones provisionales de la sección 3 no tienen aprobación de
   producto.
3. `M11-001` no se aplicó ni promovió a ningún entorno (`unihome_verificacion`
   ni `unihome_desarrollo`); queda para una ventana de despliegue separada,
   igual que M10-001/M12-001 en Sprint 7.
4. No se agregó un endpoint de lectura para la fotografía de un reporte de
   reparación (solo "adjuntar", HU-063); si una futura HU pide visualizarla,
   se puede reutilizar el mismo patrón de `PhotoReference` + proxy
   autorizado que ya usan las fotos de unidad.
5. RESTler (`tools/ci/restler/*.py`) no se modificó: compila su gramática en
   vivo contra el `/openapi/v1.json` del servidor en cada ejecución, así que
   los 8 endpoints nuevos entran automáticamente sin cambios de código; solo
   se actualizó el catálogo compartido de migraciones en `ci.yml`.
6. Mismo alcance que sprints previos: Docker build/Container Smoke no se
   ejecutaron localmente; ZAP, RESTler, Mutation Test, Pact y Cloudinary
   Integration siguen **PENDING_CI** (jobs manuales, no disparados).
7. Pruebas Mongo reales de sprints anteriores siguen **BLOCKED** localmente
   sin `mongod`; este sprint no agregó una prueba real opt-in nueva (el
   repositorio `MongoReparacionRepository` sigue la misma forma de
   `ReplaceOneAsync`+`IsUpsert`/`Find` que el resto del proyecto, ya cubierta
   por los repositorios Mongo existentes).

## 11. Pruebas locales

| Control | Resultado |
|---|---|
| `dotnet build -c Release -warnaserror` | PASS, 0 avisos/errores |
| `dotnet format --verify-no-changes` | PASS |
| Unitarias | **1050/1050 PASS** (4 omitidas: OCR/PDF reales, requieren binarios locales) |
| Integración | **372/372 ejecutadas PASS**; 13 opt-in Mongo/Cloudinary omitidas explícitamente |
| OpenAPI | PASS: 69 operaciones (61 de Sprints 1–7 + 8 de Sprint 8) |
| Python CI (`tools/ci`) | **167/167 PASS** |
| Fixture DAST local (`test_zap_dast.py`) | Cobertura **69/69** confirmada contra el flujo real simulado |
| Mongo real (`mongo-db-test`) | **BLOCKED**: no hay `mongod` local |
| ZAP/RESTler/Sonar/CodeQL/Semgrep remotos | **PENDING_CI** |

## 12. Archivos principales

**Nuevos (Sprint 8)**
- `src/UniHome.Domain/Reparaciones/{Reparacion,ReparacionEnums}.cs`
- `src/UniHome.Application/Reparaciones/**` (crear, listar, cambiar estado +
  abstracción de repositorio)
- `src/UniHome.Application/Summary/GetMySummaryService.cs`
- `src/UniHome.Application/Identity/Admin/{AccountSearchFilter,SearchAccountsService,SetAccountStatusService,EditAccountService}.cs`
- `src/UniHome.Application/Identity/Abstractions/IAdminAccessChecker.cs`
- `src/UniHome.Application/Profiles/ChangeMyUniversityService.cs`
- `src/UniHome.Infrastructure/Persistence/Mongo/Reparaciones/**` (esquema,
  índices, migración `M11-001`, repositorio)
- `src/UniHome.Api/Security/AdminAccessChecker.cs`
- `src/UniHome.Api/Controllers/{ReparacionesController,SummaryController,AdminCuentasController}.cs`;
  endpoint nuevo en `ProfileController` (HU-087)
- `src/UniHome.Api/Contracts/{Reparaciones,Summary,Admin}/**`
- Tests de dominio, Application (con dobles en memoria) e integración HTTP
  (incluido el conteo OpenAPI) para las 11 HU.
- `docs/sprint-08-cierre.md` (este documento).

**Modificados**
- `src/UniHome.Domain/Accounts/Account.cs`: `Suspend`, `Reactivate`,
  `EditProfile`, `SetCurrentUniversity`, `CurrentUniversityId`.
- `src/UniHome.Domain/Notifications/Notification.cs`:
  `NotificationType.ReparacionActualizada`, `ReparacionId`,
  `CreateReparacionStatusUpdate`.
- `src/UniHome.Application/Identity/Abstractions/IAccountRepository.cs`:
  nuevo método `SearchAsync`.
- `src/UniHome.Application/Identity/DeleteAccount/{DeleteAccountService,DeleteAccountResult}.cs`:
  nuevos estados `ActiveOccupancyLinks`, `PropertiesWithTenants`,
  `AdminCannotSelfDelete`.
- `src/UniHome.Application/Profiles/GetProfile/{GetProfileService,ProfileView}.cs`:
  resuelve `CurrentUniversity`.
- `MongoMigrationRegistry`, `MongoAccountRepository`/`AccountDocument`,
  `MongoNotificationRepository`/`NotificationDocument`/`MongoNotificationsSchema`,
  `DependencyInjection`, `Program.cs`.
- `src/UniHome.Api/Controllers/{CuentasController,ProfileController}.cs`.
- `.github/workflows/ci.yml`: `M11-001` en `mongo-db-test`, `dast-zap` y
  `restler-fuzzing`.
- `tools/ci/zap_dast.py` y `tools/ci/test_zap_dast.py`: catálogo de 69
  operaciones, flujo real de las 11 HU (cobertura 69/69 confirmada
  localmente).
- `README.md`: secciones de Sprint 8 (M11, Resumen, M13, universidad propia,
  cierre de eliminación de cuenta).
