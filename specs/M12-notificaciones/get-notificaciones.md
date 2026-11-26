# `GET /api/v1/notificaciones`

| Campo | Valor |
|---|---|
| Módulo | M12 – Notificaciones |
| HU | HU-059 |
| Operación OpenAPI | `GET /api/v1/notificaciones` |
| Controlador | `src/UniHome.Api/Controllers/NotificationsController.cs` → `List` (EP-M12-01) |

## Autenticación y autorización
Requiere `Authorization: Bearer` (`[Authorize]`, sobre `AuthenticatedControllerBase`). Usa el `accountId` resuelto del propio claim de sesión; solo devuelve notificaciones del llamador.

## Request
Sin parámetros.

## Response
`200 OK` con arreglo de `NotificacionResponse`. Cabecera `Cache-Control: no-store` (fijada explícitamente por el controlador).

## Validaciones
- Si no se puede resolver el `accountId` desde el claim de sesión → `401`.

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Sesión inválida. |
| 503 | Dependencia (MongoDB) no disponible. |

## Reglas de negocio
- **Generado al consultar, nunca por un job en segundo plano.** Cada llamada recorre las unidades donde la cuenta es inquilino activo y, si corresponde, genera o renueva el recordatorio de renta del periodo en curso antes de devolver la lista completa (`ListMyNotificationsService`).
- `Notification.ComputeRentReminderId` deriva un id determinista de (destinatario, unidad, periodo, tipo): generarlo de nuevo en la misma ventana es un upsert idempotente, nunca un duplicado.
- **Ventana de 0 a 3 días antes del corte**: si al corte del periodo en curso le quedan más de 3 días, o si el corte ya pasó, no se genera ni renueva el recordatorio para esa unidad (decisión provisional sin aprobación de producto).
- **Se omite si la cuota ya está cubierta** (`RentPeriod.ComputePaymentState == Registrado`), o si la división del periodo está pendiente, o si el inquilino no tiene cuota asignada.
- También recibe notificaciones inmediatas de cambio de estado de reparaciones (HU-066, M11): a diferencia del recordatorio de renta, esas se generan en el momento del cambio, no al consultar.

## Controles de seguridad aplicados
- Requiere Bearer; usa el claim de sesión, nunca un id de cuenta recibido del cliente (previene IDOR sobre notificaciones de otra cuenta).
- `Cache-Control: no-store` fijado explícitamente por el controlador (datos propios).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ListMyNotificationsServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/NotificationsEndpointTests.cs`; cobertura cruzada de la notificación inmediata de reparaciones en `tests/UniHome.IntegrationTests/ReparacionesEndpointTests.cs`.
- Seguridad: recorrido dinámico de ZAP sobre el catálogo OpenAPI real (`tools/ci/zap_dast.py`); gramática RESTler generada del mismo documento (`tools/ci/restler/normalize_openapi.py`).
