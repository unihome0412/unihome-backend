# `PUT /api/v1/perfil/mi-universidad`

| Campo | Valor |
|---|---|
| Módulo | M02 – Perfil (cruza con M03 – Universidades) |
| HU | HU-087 |
| Operación OpenAPI | `PUT /api/v1/perfil/mi-universidad` |
| Controlador | `src/UniHome.Api/Controllers/ProfileController.cs` → `ChangeMyUniversity` (EP-M02/M03), bajo `[Authorize]` a nivel de controlador |

## Autenticación y autorización
Requiere `Authorization: Bearer`. Usa el claim `sub` del propio token; solo modifica la universidad de la cuenta autenticada.

## Request
Body JSON (`CambiarMiUniversidadRequest`): `universidadId` (`Guid`, `[JsonRequired]`). Límite de 8 KB. Mismo contrato tanto si el cliente entra desde el perfil como desde el mapa (un único endpoint para ambos puntos de entrada).

## Response
`200 OK` con `PerfilResponse` completo (mismo contrato que `GET /perfil/mi-perfil`).

## Validaciones
- `ChangeMyUniversityService` (`src/UniHome.Application/Identity/` vía `ChangeMyUniversityService`) valida que `universidadId` exista **y** esté activa en el catálogo; si no, `400`.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | `universidadId` inexistente o universidad inactiva. |
| 401 | Sesión inválida. |
| 413 | Cuerpo > 8 KB. |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- Solo se acepta una universidad **activa** del catálogo administrado por M03.
- Un único endpoint sirve dos puntos de entrada de UI (perfil y mapa); el backend no distingue el origen.
- No agrega una migración nueva: `universidadActualId` es un campo flexible sobre `M01-001`, igual criterio que `lugaresOcupados` en `unidades`.

## Controles de seguridad aplicados
- Requiere Bearer; usa el claim de sesión, nunca un id de cuenta recibido del cliente.
- Cabecera `Cache-Control: no-store` aplicada por el middleware global para el prefijo `/api/v1/perfil` (`src/UniHome.Api/Program.cs:814-823`).
- Límite de tamaño de cuerpo.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ChangeMyUniversityServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/ProfileEndpointTests.cs`.
- Seguridad: recorrido dinámico de ZAP sobre el catálogo OpenAPI real; gramática RESTler generada del mismo documento.
