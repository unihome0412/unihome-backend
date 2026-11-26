# `GET /api/v1/unidades/{unitId}/tarjeta`

| Campo | Valor |
|---|---|
| Módulo | M04 – Exploración pública |
| HU | HU-038 / HU-046 / HU-047 |
| Operación OpenAPI | `GET /api/v1/unidades/{unitId}/tarjeta` |
| Controlador | `src/UniHome.Api/Controllers/ExplorationController.cs` → `Card` (EP-M04-03) |

## Autenticación y autorización
Pública (`[AllowAnonymous]`). Con Bearer válido incluye `esFavorito`; sin sesión ese campo es `null`. Sin verificación de propiedad: solo se sirve si la unidad es públicamente visible.

## Request
Parámetro de ruta `unitId` (Guid, con restricción de ruta `:guid`).

## Response
`200 OK` con `UnidadTarjetaResponse` (mismos campos que la lista). `Cache-Control: no-store` si hay sesión.

## Validaciones
Ninguna de entrada más allá del formato `Guid` de la ruta (lo valida el framework antes de llegar al controlador).

## Códigos de error
| Código | Causa |
|---|---|
| 404 | La unidad no existe o no es públicamente visible (`GetUnitCardService`: no es `Aprobada`+`Disponible`). |
| 503 | Falla de dependencia. |

## Reglas de negocio
- `ExplorationProjection.IsPublic(unit)` exige `PublicationState.Aprobada` y `UnitAvailability.Disponible`; cualquier otro estado responde `404`, sin distinguir "no existe" de "no visible".
- `arrendadorVerificado` refleja el expediente M06 aprobado del propietario.

## Controles de seguridad aplicados
- No expone datos del propietario distintos a los de la tarjeta pública.
- `Cache-Control: no-store` cuando el campo `esFavorito` depende de la sesión del llamador.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ExplorationAndFavoritesServiceTests.cs::Tarjeta_ConSesion_DebeIncluirFavoritoYSinPii`, `::Tarjeta_NoPublica_DebeDevolver404`.
- Integración: `tests/UniHome.IntegrationTests/ExplorationEndpointTests.cs::Tarjeta_SinSesion_EsFavoritoNullYConSesionEsBooleano`, `::EstadosNoPublicos_NoAparecenEnM04YFichaDevuelve404`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
