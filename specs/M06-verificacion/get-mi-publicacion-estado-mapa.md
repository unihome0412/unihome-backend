# `GET /api/v1/mi-publicacion/estado-mapa`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-028 |
| Operación OpenAPI | `GET /api/v1/mi-publicacion/estado-mapa` |
| Controlador | `src/UniHome.Api/Controllers/PublicationsController.cs` → `GetMyPublicationMap` (EP-M05-10) |

## Autenticación y autorización

Requiere `Authorization: Bearer` (`[Authorize]`). Resuelve siempre la
primera unidad propia del `accountId` del token
(`IUnitRepository.FindFirstOwnedAsync`); no hay id en la ruta, por lo que
no existe superficie para pedir el pin de otra cuenta.

## Request

Sin parámetros.

## Response

`200 OK` con `MiPublicacionMapaResponse` (`MyPublicationMapView`): id de
unidad, latitud/longitud, precio por lugar, tipo de unidad y la etiqueta
fija `"Tu espacio · En revisión"` (`GetMyPublicationMapService.InReviewLabel`).

## Validaciones

- La unidad debe existir y estar exactamente en `PublicationState.EnRevision`;
  en cualquier otro estado (incluido `Aprobada`, dueño ajeno, o sin unidad)
  responde `404` — no distingue los casos para no filtrar información.
- La propiedad asociada debe tener latitud y longitud capturadas; si no,
  `422`.

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 404 | No hay una primera publicación propia en estado `EnRevision` (incluye unidad ya aprobada/rechazada o inexistente). |
| 422 | La propiedad no tiene ubicación (latitud/longitud) registrada. |
| 503 | Preflight de esquema Mongo fallido. |

## Reglas de negocio

- Es el "pin privado" que permite al titular ver su propio espacio en el
  mapa mientras está en revisión, sin que aparezca para otros usuarios en
  M04 (exploración pública), que solo muestra unidades `Aprobada`.
- Deja de ser accesible (`404`) en cuanto la unidad se aprueba o rechaza:
  en ese punto el titular ve el resultado por otras vías (`mi-expediente`,
  ficha pública si fue aprobada).

## Controles de seguridad aplicados

- Resolución exclusivamente por el `accountId` del token (sin id de unidad
  en la ruta): anti-IDOR por diseño.
- Respuesta uniforme `404` para "no existe", "no es del dueño" y "no está
  en revisión", para no filtrar el estado real de la publicación de otra
  cuenta.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/MyPublicationMapServiceTests.cs`
  (`EnRevision_DebeDevolverPinPrivadoConEtiqueta`,
  `AprobadaODuenoAjeno_DebeDevolverNotFound`,
  `SinUbicacion_DebeSerUnprocessable`).
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs`
  (`MiEspacioEnRevision_DebeSerPrivadoYMostrarEtiqueta`,
  `EstadoPropio_SinExpediente_DebeResponder404`).
- Seguridad: cubierto por el recorrido dinámico de ZAP y por la gramática
  RESTler sobre el documento OpenAPI; sin prueba de seguridad dedicada
  adicional a la verificación de pertenencia ya cubierta por las pruebas
  unitarias/integración anteriores.
