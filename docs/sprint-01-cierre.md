# Cierre integral Sprint 1 — Backend

**Fecha:** 2026-11-13  
**Rama:** `feature/sprint-01-cierre-integral`  
**Base:** `main` posterior al merge de HU-075 (`45d8b234de71889b88d20b35c545cb40914ff510`)

Este paquete agrupa los pendientes backend del Sprint 1 sin duplicar HU-006 ni HU-075.
La trazabilidad individual se conserva por HU/RF/endpoint/prueba.

## Matriz de las 11 HU del Sprint 1

| HU | RF | Clasificación real | Endpoint / soporte backend | Pruebas principales |
|---|---|---|---|---|
| HU-001 | RF-01 | Backend implementado | `POST /api/v1/auth/registro` | `RegisterEndpointTests`, `AccountTests`, servicio de registro |
| HU-002 | RF-47 | Backend implementado en este cierre | `GET /api/v1/perfil/mi-perfil` | `ProfileEndpointTests`, `GetProfileServiceTests` |
| HU-003 | RF-01 | Backend implementado | `POST /api/v1/auth/confirmacion-correo`, `POST /api/v1/auth/confirmacion-correo/reenvio` | `ConfirmEmailEndpointTests`, `ResendConfirmationEndpointTests` |
| HU-004 | RF-02 | Backend implementado | `POST /api/v1/auth/login` | `LoginEndpointTests`, `JwtSecurityTests` |
| HU-005 | RF-02 | Backend implementado | `POST /api/v1/auth/logout` | `LogoutEndpointTests` |
| HU-006 | RF-02 | Backend implementado y mergeado | `POST /api/v1/auth/recuperacion/solicitud`, `POST /api/v1/auth/recuperacion/confirmacion` | Pruebas unitarias, integración y MongoDB real |
| HU-008 | RF-04 | Pendiente de integración funcional | `GET /api/v1/perfil/mi-perfil` entrega `vinculadoAUnidad`, `etiquetaCuenta` y `puedePublicar`; la barra es Android | `ProfileEndpointTests`, `GetProfileServiceTests` |
| HU-011 | RF-04 | Backend parcialmente implementado | Policy admin + endpoints `/api/v1/admin/universidades*`; Expedientes/Usuarios dependen de M06/M13 | `AdminUniversitiesEndpointTests`, pruebas de 401/403/claim falso |
| HU-012 | RF-05 | Backend implementado | Consentimiento y versión en `POST /api/v1/auth/registro` | `RegisterEndpointTests`, `AccountTests` |
| HU-075 | RF-39 | Backend implementado y mergeado | `POST /api/v1/admin/universidades` | Pruebas unitarias, integración y MongoDB real |
| HU-077 | RF-39 | Backend implementado en este cierre | `GET /api/v1/admin/universidades`, `PUT /api/v1/admin/universidades/{id}`, `PUT /api/v1/admin/universidades/{id}/estado` | Unitarias, integración, OpenAPI y MongoDB real |

### HU-008

El backend entrega los indicadores que el cliente necesita para decidir la navegación.
`vinculadoAUnidad` será verdadero cuando M09 (Sprint 6) aporte vínculos activos; hoy se
devuelve `false` porque ese módulo no existe. Mensajes (M08) y Mi renta (M10) tampoco
pertenecen al Sprint 1. No se crearon endpoints artificiales de navegación.

### HU-011

El backend revalida el privilegio administrativo en servidor mediante
`Admin__AccountIds` y el claim `sub` del JWT. No se acepta un rol del cliente ni un
claim `admin` manipulado. Las pantallas del panel y la visibilidad de la navegación
son responsabilidad de Android. Las secciones Expedientes y Usuarios no tienen
endpoints en Sprint 1: pertenecen a M06/M13 de sprints posteriores. La sección
Universidades queda cubierta por M03.

## Endpoints del paquete

### Añadidos

| Método | Ruta | HU | Auth |
|---|---|---|---|
| GET | `/api/v1/perfil/mi-perfil` | HU-002, HU-008 | Bearer |
| GET | `/api/v1/admin/universidades` | HU-077 | Bearer admin |
| PUT | `/api/v1/admin/universidades/{id}` | HU-077 | Bearer admin |
| PUT | `/api/v1/admin/universidades/{id}/estado` | HU-077 | Bearer admin |

### Existentes reutilizados

- `POST /api/v1/admin/universidades` (HU-075).
- `GET /api/v1/universidades?buscar=` (HU-075/HU-007).
- Endpoints M01 de registro, confirmación, reenvío, login, refresh, logout y recuperación.

No se modificaron rutas ni contratos existentes de forma incompatible.

## Contratos y validaciones

### Perfil

`PerfilResponse`: `nombreCompleto`, `correo`, `fechaNacimiento`, `genero`,
`universidadActual`, `etiquetaCuenta`, `vinculadoAUnidad`, `puedePublicar`.

- No expone hash de contraseña, claims administrativos ni identificadores internos.
- `etiquetaCuenta` es `UsuarioUniHome` mientras M05/M06 no existan.
- `universidadActual` es `null` hasta HU-087 (Sprint 8).
- `puedePublicar` se calcula con la fecha de nacimiento; no es un permiso persistido.

### Universidades administrativas

- Listado paginado con `page`, `pageSize`, `X-Total-Count`, `X-Page`, `X-Page-Size`.
- Incluye activas e inactivas.
- Edición con `nombreYCampus` obligatorio, normalización sin acentos/mayúsculas y
  detección atómica de duplicados.
- Activación/desactivación lógica; nunca borrado físico.
- La búsqueda pública sigue mostrando únicamente activas.
- Errores: `400`, `401`, `403`, `404`, `409`, `413`, `503` con ProblemDetails.

## MongoDB

- Se reutiliza `M03-001`; no hubo cambio de esquema que justificara otra migración.
- Campos: `nombreYCampus`, `nombreNormalizado`, `activa`, `createdAt`.
- Índice único: `ux_universidades_nombre_normalizado`.
- Actualización de nombre condicionada por el nombre normalizado anterior para no
  pisar una edición concurrente.
- La prueba real `MongoUniversityPersistenceTests` cubre alta, listado, edición,
  duplicado, desactivación, reactivación, búsqueda y autorización.

## Android / fuera del backend

- Barra de navegación, textos, botones y visibilidad del panel administrativo.
- Mensajes, Mi renta y Mis espacios dependen de M08/M09/M10/M05.
- La app no decide permisos: el backend responde 401/403 en cada ruta protegida.

## Evidencia local del paquete

- Build Release: 0 advertencias, 0 errores.
- Formato: sin cambios.
- Unitarias: 457/457.
- Integración regular: 164/164; 4 pruebas MongoDB opt-in omitidas en el job normal.
- OpenAPI: 10/10 en el filtro local.
- Python CI: 133/133.
- La evidencia de MongoDB real, SonarCloud, CodeQL y demás gates se registra en
  la ejecución del PR; no se declara aprobada antes de ejecutarse.

## Gate Atlas independiente

No se modificó Atlas. Antes de habilitar M03 en Atlas se debe conciliar el índice
externo `ix_universidades_nombre` y el validador previo con
`ux_universidades_nombre_normalizado`. Este Gate es independiente del merge del PR.
