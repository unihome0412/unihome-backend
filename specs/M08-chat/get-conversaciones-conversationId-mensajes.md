# `GET /api/v1/conversaciones/{conversationId}/mensajes`

| Campo | Valor |
|---|---|
| Módulo | M08 – Chat |
| HU | HU-068 |
| Operación OpenAPI | `GET /api/v1/conversaciones/{conversationId}/mensajes` |
| Controlador | `src/UniHome.Api/Controllers/ChatController.cs` → `ListMessages` (`ListConversationMessagesService.ListAsync`) |

## Autenticación y autorización

`[Authorize]` (Bearer JWT obligatorio, HU-086). Autorización por participación: **solo participantes leen** la conversación (HU-068); cualquier otra cuenta recibe `403`.

## Request

- Parámetro de ruta: `conversationId` (`Guid`).
- Query: `antes` (`DateTimeOffset?`), `antesId` (`Guid?`), `limite` (`int?`).
- El cursor es compuesto (fecha + id): si falta `antes` o `antesId`, se trata como primera página en vez de aplicar un corte parcial ambiguo (comentario en el controlador).

## Response

`200 OK` con `PaginaMensajesResponse`: mensajes más recientes primero y, si la página viene llena, `siguienteCursor` (`{fecha, mensajeId}`) para pedir el historial anterior. Tamaño de página por defecto 20, máximo 50 (README.md, "Sprint 6 / M08: chat"). `Cache-Control: no-store`.

## Validaciones

- `conversationId` debe ser GUID válido (restricción de ruta).
- El cursor exige ambos componentes (`antes` y `antesId`) juntos; si solo llega uno, se ignora y se devuelve la primera página.

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no es parte de la conversación. |
| 404 | La conversación no existe. |
| 503 | Falla de infraestructura (MongoDB no disponible). |

## Reglas de negocio

- **Paginación por cursor compuesto (fecha + id):** un cursor de solo fecha se saltaría mensajes con la misma fecha en el corte de página; el `mensajeId` desempata (README.md).
- Solo participantes leen y envían (HU-068).

## Controles de seguridad aplicados

- Requiere sesión y verificación explícita de participación antes de exponer cualquier mensaje (mitiga BOLA/IDOR sobre `conversationId`).
- `Cache-Control: no-store`.
- Nunca expone correo ni teléfono (HU-070).

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/ListConversationMessagesServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/ChatEndpointTests.cs`.
- Seguridad: la autorización por participación (BOLA/IDOR) está cubierta por `tests/UniHome.UnitTests/Application/ListConversationMessagesServiceTests.cs::ListAsync_NoParticipante_DebeDevolverForbidden`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real; sin prueba de seguridad dedicada adicional a lo anterior.
