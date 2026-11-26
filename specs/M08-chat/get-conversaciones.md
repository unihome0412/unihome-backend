# `GET /api/v1/conversaciones`

| Campo | Valor |
|---|---|
| Módulo | M08 – Chat |
| HU | HU-069 |
| Operación OpenAPI | `GET /api/v1/conversaciones` |
| Controlador | `src/UniHome.Api/Controllers/ChatController.cs` → `ListConversations` (`ListMyConversationsService.ListAsync`) |

## Autenticación y autorización

`[Authorize]` (Bearer JWT obligatorio, HU-086). Solo devuelve conversaciones donde la cuenta autenticada participa.

## Request

Sin parámetros de ruta ni query; sin cuerpo.

## Response

`200 OK` con `IReadOnlyList<ConversacionResumenResponse>`: nombre del otro participante, último mensaje, hora y contexto de la unidad (HU-069). `Cache-Control: no-store`.

## Validaciones

No aplica (sin entrada del cliente más allá del token).

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sesión inválida o ausente. |
| 503 | Falla de infraestructura (MongoDB no disponible). |

## Reglas de negocio

- Lista únicamente las conversaciones propias (filtradas por participación), ordenadas de forma consistente con la pantalla de bandeja de chat.
- Reutiliza el mismo modelo de conversación con `_id` determinista descrito en `post-unidades-unitId-conversaciones.md`.

## Controles de seguridad aplicados

- Requiere sesión (`[Authorize]`).
- `Cache-Control: no-store`.
- Nunca expone correo ni teléfono del otro participante (HU-070): solo `AccountId` y `FullName`.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/ListMyConversationsServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/ChatEndpointTests.cs`.
- Seguridad: cubierta dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real; sin prueba de seguridad dedicada a este endpoint en particular.
