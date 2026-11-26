# `POST /api/v1/conversaciones/{conversationId}/mensajes`

| Campo | Valor |
|---|---|
| Módulo | M08 – Chat |
| HU | HU-068 / HU-089 |
| Operación OpenAPI | `POST /api/v1/conversaciones/{conversationId}/mensajes` |
| Controlador | `src/UniHome.Api/Controllers/ChatController.cs` → `SendMessage` (`SendMessageService.SendAsync`) |

## Autenticación y autorización

`[Authorize]` (Bearer JWT obligatorio, HU-086). Autorización por participación: **solo participantes envían** (HU-068); cualquier otra cuenta recibe `403`.

## Request

- Parámetro de ruta: `conversationId` (`Guid`).
- Body JSON `EnviarMensajeRequest`: `texto` (string).
- `[RequestSizeLimit(8 * 1024)]`: la solicitud se limita a 8 KB; si `Request.ContentLength` lo excede, responde `413` antes de leer el cuerpo.

## Response

`201 Created` con `MensajeResponse`. `Cache-Control: no-store`.

## Validaciones

- `texto` no vacío y de hasta `Message.MaxBodyLength` caracteres; fuera de ese rango responde `400` (`ValidationProblemDetails`).
- Límite de tamaño de solicitud de 8 KB (`413` si se excede).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | `texto` vacío o excede `Message.MaxBodyLength`. |
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no es parte de la conversación. |
| 404 | La conversación no existe. |
| 413 | El cuerpo de la solicitud excede 8 KB. |
| 429 | Se superó el límite de frecuencia (ver controles de seguridad). |
| 503 | Falla de infraestructura (MongoDB o el limitador distribuido no disponibles). |

## Reglas de negocio

- Solo participantes leen y envían (HU-068); el cuerpo exige `texto` no vacío y hasta `Message.MaxBodyLength` caracteres (README.md, "Sprint 6 / M08: chat").
- **Aviso de cifrado en tránsito (HU-089):** toda respuesta de envío o lectura de mensajes incluye el aviso `"Comunicación cifrada en tránsito."` (`MensajeResponse`).

## Controles de seguridad aplicados

- **Límite de frecuencia por cuenta:** 30 mensajes por minuto (umbral provisional), con el `IDistributedRateLimiter` compartido entre réplicas. Al excederse responde `429`. Si el almacén distribuido no está disponible, el envío se permite (fail-open): a diferencia del cupo de ocupación (HU-049), este límite no protege una invariante de negocio crítica (README.md).
- Verificación de participación antes de aceptar el mensaje (mitiga BOLA/IDOR sobre `conversationId`).
- `RequestSizeLimit` de 8 KB como mitigación adicional de abuso de payload.
- `Cache-Control: no-store`; nunca expone correo ni teléfono (HU-070).

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/SendMessageServiceTests.cs` (`SendAsync_NoParticipante_DebeDevolverForbidden`, `SendAsync_ExcedeElLimiteDeFrecuencia_DebeDevolverRateLimited`, caso de texto que excede `Message.MaxBodyLength`).
- Integración: `tests/UniHome.IntegrationTests/ChatEndpointTests.cs`.
- Seguridad: la autorización por participación y el rate limiting están cubiertos por las pruebas unitarias citadas. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real (incluye `429`/consumo de recursos en el catálogo fuzzeado).
