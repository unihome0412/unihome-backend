# `POST /api/v1/unidades/{unitId}/conversaciones`

| Campo | Valor |
|---|---|
| Módulo | M08 – Chat |
| HU | HU-067 |
| Operación OpenAPI | `POST /api/v1/unidades/{unitId}/conversaciones` |
| Controlador | `src/UniHome.Api/Controllers/ChatController.cs` → `StartConversation` (`StartConversationService.StartAsync`) |

## Autenticación y autorización

`[Authorize]` (Bearer JWT obligatorio, HU-086). No existe una autorización por rol adicional: cualquier cuenta autenticada puede iniciar una conversación sobre una unidad **aprobada**, salvo el propio propietario de la unidad (ver reglas de negocio).

## Request

- Parámetro de ruta: `unitId` (`Guid`).
- Sin cuerpo de solicitud.

## Response

`200 OK` con `ConversacionResponse` (vista `ConversacionResponse.FromView`). La respuesta incluye `Cache-Control: no-store`.

## Validaciones

- `unitId` debe ser un GUID válido (restricción de ruta `:guid`); si no, ASP.NET Core responde `404` antes de llegar al controlador.
- No hay validación de cuerpo (no se envía).

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sesión inválida o ausente. |
| 403 | El solicitante es el propietario de la unidad (no puede iniciar una conversación consigo mismo). |
| 404 | La unidad no existe o no está aprobada (mismo criterio que la ficha pública, EP-M04-04; no se distingue inexistente de no aprobada). |
| 503 | Falla de infraestructura (MongoDB no disponible). |

## Reglas de negocio

- **Identificador determinista sin backfill de datos:** el `_id` de `conversaciones` se deriva con SHA-256 de `unidad|propietario|solicitante` (`Conversation.ComputeId`). Dos solicitudes simultáneas para iniciar la misma conversación nunca duplican el documento: el upsert por `_id` en Mongo es atómico sobre un único documento (README.md, "Sprint 6 / M08: chat").
- Solo se puede contactar una unidad **aprobada** (`PublicationState.Aprobada`); si ya existe la conversación, se reutiliza sin reiniciar su resumen de último mensaje.
- No se puede iniciar una conversación con uno mismo (HU-067).

## Controles de seguridad aplicados

- Requiere sesión (`[Authorize]`, HU-086).
- `Cache-Control: no-store` por tratarse de datos ligados a una cuenta.
- La respuesta no expone correo ni teléfono de ninguna de las partes (HU-070), congruente con el resto del módulo.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/StartConversationServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/ChatEndpointTests.cs`, `tests/UniHome.IntegrationTests/MongoChatPersistenceTests.cs` (persistencia real del `_id` determinista).
- Seguridad: cubierta dinámicamente por el DAST (`tools/ci/zap_dast.py`, que recorre todas las rutas `/api/v1/*` del documento OpenAPI real) y por la gramática de RESTler generada desde el mismo documento (`tools/ci/restler/normalize_openapi.py`, `restler_prepare.py`); sin prueba de seguridad dedicada a este endpoint en particular.
