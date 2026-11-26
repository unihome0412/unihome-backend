# Matriz de pruebas por operación

Las 69 operaciones del catálogo OpenAPI real (`/openapi/v1.json`, confirmado
por `tests/UniHome.IntegrationTests/OpenApiTests.cs::OpenApi_DebeExponerLas69OperacionesDelCatalogoVigente`
y, por construcción, por `tests/UniHome.IntegrationTests/SpecsCoverageTests.cs`),
con su cobertura real de pruebas unitarias, de integración y de seguridad. El
detalle completo de cada fila vive en `specs/<modulo>/<archivo>.md`; esta
tabla resume y enlaza, no sustituye la spec.

## Metodología

- La columna **Unitarias** es "Sí" solo si existe un método de prueba en
  `tests/UniHome.UnitTests` que instancia directamente el servicio de
  `Application` (o, cuando no hay uno, el componente equivalente) que
  implementa la operación — verificado con `Grep` contra el repo real, no
  inferido. **"N/A (arquitectura)"** marca la única excepción real
  encontrada: `GET /api/v1/fotos/{photoRef}`, cuya lógica vive directamente
  en el controlador y no en un servicio de `Application`;
  `tests/UniHome.UnitTests/UniHome.UnitTests.csproj` no referencia
  `UniHome.Api` por convención del proyecto (ningún controlador de este
  repo tiene prueba unitaria directa — todos se prueban por integración),
  así que forzar una prueba de controlador aquí rompería esa convención sin
  cerrar ningún hueco real: la integración ya cubre la ruta de punta a
  punta (ver su spec).
- La columna **Integración** es "Sí" si existe una prueba HTTP real contra
  `WebApplicationFactory` (`tests/UniHome.IntegrationTests`) para la ruta.
- La columna **Seguridad dedicada** solo marca pruebas *adicionales* a la
  cobertura dinámica de base (ver abajo): casos como un claim de admin
  falsificado (`*_ConClaimAdminFalso_DebeResponder403`) o acceso cruzado a
  un recurso ajeno (`OperacionesSobreRecursoAjeno_DebenResponder403`), que
  ejercitan directamente un riesgo OWASP concreto. `—` no significa "sin
  seguridad": significa que esa operación solo tiene la cobertura dinámica
  de base, igual que las demás.
- **Resultado de este bloque: 0 operaciones sin alguno de los tres tipos de
  prueba.** Un hueco real se encontró y se cerró aquí mismo: `GET
  /api/v1/unidades/{unitId}/inquilinos` no tenía prueba unitaria dedicada a
  `ListUnitTenantsService`; se agregó
  `tests/UniHome.UnitTests/Application/ListUnitTenantsServiceTests.cs` (4
  casos: propio/200, ajeno/403, inexistente/404, llamante no elegible/403).

### Por qué todas las filas tienen cobertura de seguridad dinámica

`tools/ci/zap_dast.py` (DAST pasivo con OWASP ZAP) y
`tools/ci/restler/normalize_openapi.py` + `restler_prepare.py` (fuzzing
RESTler) construyen su recorrido/gramática a partir del documento OpenAPI
real en runtime, no de una lista fija — por construcción cubren las 69
operaciones. `POST /api/v1/auth/logout` y `DELETE /api/v1/cuentas/mi-cuenta`
se aíslan en pasadas RESTler separadas (`logout-settings`/`delete-settings`
en `restler_prepare.py`, con `destructive_operations_isolated: true` en su
resumen) para no invalidar la sesión/cuenta que usa el resto de la
secuencia principal; igual quedan cubiertas, solo en una corrida distinta
del mismo job. `tools/ci/test_zap_dast.py` tiene una prueba real
(`test_openapi_operations_incluyen_69_operaciones_y_auth`,
`test_run_scan_genera_evidencia_y_cobertura_69_de_69`) que fija esta
cobertura en 69/69 para el fixture local del repo — eso es verificable hoy
ejecutando esa suite Python. Es cobertura **por diseño de la herramienta**,
no el resultado de un run de CI ya ejecutado contra el catálogo completo en
GitHub Actions: ese run real, su *run id* y su reporte son evidencia del
**Bloque 4** de esta auditoría, no de este bloque.

### Pact (contrato de consumidor)

Ninguna de las 69 operaciones tiene hoy un contrato Pact real: `pacts/`
solo contiene `pacts/README.md`, que documenta que los contratos deben
venir de pruebas de consumidor reales de la app Android (nunca generados
desde el propio OpenAPI) y que el job `pact-contract` permanece
desactivado (`UNIHOME_ENABLE_PACT=true` sin activar) hasta recibirlos. No
se inventó ningún contrato para esta auditoría.

## Tabla

| Módulo | Operación | Unitarias | Integración | Seguridad dedicada* | Spec |
|---|---|---|---|---|---|
| M01-autenticacion | `POST /api/v1/auth/confirmacion-correo` | Sí | Sí | — | [M01-autenticacion/post-auth-confirmacion-correo.md](../specs/M01-autenticacion/post-auth-confirmacion-correo.md) |
| M01-autenticacion | `POST /api/v1/auth/confirmacion-correo/reenvio` | Sí | Sí | — | [M01-autenticacion/post-auth-confirmacion-correo-reenvio.md](../specs/M01-autenticacion/post-auth-confirmacion-correo-reenvio.md) |
| M01-autenticacion | `POST /api/v1/auth/login` | Sí | Sí | — | [M01-autenticacion/post-auth-login.md](../specs/M01-autenticacion/post-auth-login.md) |
| M01-autenticacion | `POST /api/v1/auth/logout` | Sí | Sí | ruta incluida en `AUTHENTICATED_OPERATIONS` de `tools/ci/zap_dast.py` (caso explícito de operación autenticada para el DAST) | [M01-autenticacion/post-auth-logout.md](../specs/M01-autenticacion/post-auth-logout.md) |
| M01-autenticacion | `POST /api/v1/auth/recuperacion/confirmacion` | Sí | Sí | — | [M01-autenticacion/post-auth-recuperacion-confirmacion.md](../specs/M01-autenticacion/post-auth-recuperacion-confirmacion.md) |
| M01-autenticacion | `POST /api/v1/auth/recuperacion/solicitud` | Sí | Sí | — | [M01-autenticacion/post-auth-recuperacion-solicitud.md](../specs/M01-autenticacion/post-auth-recuperacion-solicitud.md) |
| M01-autenticacion | `POST /api/v1/auth/refresh` | Sí | Sí | — | [M01-autenticacion/post-auth-refresh.md](../specs/M01-autenticacion/post-auth-refresh.md) |
| M01-autenticacion | `POST /api/v1/auth/registro` | Sí | Sí | — | [M01-autenticacion/post-auth-registro.md](../specs/M01-autenticacion/post-auth-registro.md) |
| M01-autenticacion | `DELETE /api/v1/cuentas/mi-cuenta` | Sí | Sí | ruta incluida en `AUTHENTICATED_OPERATIONS` de `tools/ci/zap_dast.py` | [M01-autenticacion/delete-cuentas-mi-cuenta.md](../specs/M01-autenticacion/delete-cuentas-mi-cuenta.md) |
| M02-perfil | `GET /api/v1/perfil/mi-perfil` | Sí | Sí | — | [M02-perfil/get-perfil-mi-perfil.md](../specs/M02-perfil/get-perfil-mi-perfil.md) |
| M02-perfil | `PUT /api/v1/perfil/mi-universidad` | Sí | Sí | — | [M02-perfil/put-perfil-mi-universidad.md](../specs/M02-perfil/put-perfil-mi-universidad.md) |
| M03-universidades | `GET /api/v1/admin/universidades` | Sí | Sí | — | [M03-universidades/get-admin-universidades.md](../specs/M03-universidades/get-admin-universidades.md) |
| M03-universidades | `POST /api/v1/admin/universidades` | Sí | Sí | `Crear_ConClaimAdminFalso_DebeResponder403` (escalamiento de privilegio, OWASP API5) | [M03-universidades/post-admin-universidades.md](../specs/M03-universidades/post-admin-universidades.md) |
| M03-universidades | `PUT /api/v1/admin/universidades/{id}` | Sí | Sí | — | [M03-universidades/put-admin-universidades-id.md](../specs/M03-universidades/put-admin-universidades-id.md) |
| M03-universidades | `PUT /api/v1/admin/universidades/{id}/estado` | Sí | Sí | `CambiarEstado_ConClaimAdminFalso_DebeResponder403` (escalamiento de privilegio, OWASP API5) | [M03-universidades/put-admin-universidades-id-estado.md](../specs/M03-universidades/put-admin-universidades-id-estado.md) |
| M03-universidades | `GET /api/v1/universidades` | Sí | Sí | — | [M03-universidades/get-universidades.md](../specs/M03-universidades/get-universidades.md) |
| M04-exploracion | `GET /api/v1/exploracion/lista` | Sí | Sí | — | [M04-exploracion/get-exploracion-lista.md](../specs/M04-exploracion/get-exploracion-lista.md) |
| M04-exploracion | `GET /api/v1/exploracion/mapa` | Sí | Sí | — | [M04-exploracion/get-exploracion-mapa.md](../specs/M04-exploracion/get-exploracion-mapa.md) |
| M04-exploracion | `GET /api/v1/unidades/{unitId}/ficha` | Sí | Sí | — | [M04-exploracion/get-unidades-unitId-ficha.md](../specs/M04-exploracion/get-unidades-unitId-ficha.md) |
| M04-exploracion | `GET /api/v1/unidades/{unitId}/tarjeta` | Sí | Sí | — | [M04-exploracion/get-unidades-unitId-tarjeta.md](../specs/M04-exploracion/get-unidades-unitId-tarjeta.md) |
| M05-propiedades-unidades | `GET /api/v1/fotos/{photoRef}` | N/A (arquitectura) | Sí | — | [M05-propiedades-unidades/get-fotos-photoRef.md](../specs/M05-propiedades-unidades/get-fotos-photoRef.md) |
| M05-propiedades-unidades | `GET /api/v1/mi-resumen` | Sí | Sí | — | [M05-propiedades-unidades/get-mi-resumen.md](../specs/M05-propiedades-unidades/get-mi-resumen.md) |
| M05-propiedades-unidades | `GET /api/v1/mis-propiedades` | Sí | Sí | — | [M05-propiedades-unidades/get-mis-propiedades.md](../specs/M05-propiedades-unidades/get-mis-propiedades.md) |
| M05-propiedades-unidades | `POST /api/v1/propiedades` | Sí | Sí | — | [M05-propiedades-unidades/post-propiedades.md](../specs/M05-propiedades-unidades/post-propiedades.md) |
| M05-propiedades-unidades | `POST /api/v1/propiedades/{propertyId}/unidades` | Sí | Sí | `OperacionesSobreRecursoAjeno_DebenResponder403` (BOLA/IDOR) | [M05-propiedades-unidades/post-propiedades-propertyId-unidades.md](../specs/M05-propiedades-unidades/post-propiedades-propertyId-unidades.md) |
| M05-propiedades-unidades | `POST /api/v1/publicaciones/primera/terminos` | Sí | Sí | — | [M05-propiedades-unidades/post-publicaciones-primera-terminos.md](../specs/M05-propiedades-unidades/post-publicaciones-primera-terminos.md) |
| M05-propiedades-unidades | `GET /api/v1/unidades/{unitId}/detalle` | Sí | Sí | — | [M05-propiedades-unidades/get-unidades-unitId-detalle.md](../specs/M05-propiedades-unidades/get-unidades-unitId-detalle.md) |
| M05-propiedades-unidades | `POST /api/v1/unidades/{unitId}/fotos` | Sí | Sí | — | [M05-propiedades-unidades/post-unidades-unitId-fotos.md](../specs/M05-propiedades-unidades/post-unidades-unitId-fotos.md) |
| M05-propiedades-unidades | `PUT /api/v1/unidades/{unitId}/servicios` | Sí | Sí | — | [M05-propiedades-unidades/put-unidades-unitId-servicios.md](../specs/M05-propiedades-unidades/put-unidades-unitId-servicios.md) |
| M05-propiedades-unidades | `GET /api/v1/unidades/{unitId}/vista-previa` | Sí | Sí | — | [M05-propiedades-unidades/get-unidades-unitId-vista-previa.md](../specs/M05-propiedades-unidades/get-unidades-unitId-vista-previa.md) |
| M05-propiedades-unidades | `PUT /api/v1/unidades/{unitId}/zona-reglas` | Sí | Sí | — | [M05-propiedades-unidades/put-unidades-unitId-zona-reglas.md](../specs/M05-propiedades-unidades/put-unidades-unitId-zona-reglas.md) |
| M06-verificacion | `GET /api/v1/admin/expedientes` | Sí | Sí | — | [M06-verificacion/get-admin-expedientes.md](../specs/M06-verificacion/get-admin-expedientes.md) |
| M06-verificacion | `GET /api/v1/admin/expedientes/{id}` | Sí | Sí | — | [M06-verificacion/get-admin-expedientes-id.md](../specs/M06-verificacion/get-admin-expedientes-id.md) |
| M06-verificacion | `POST /api/v1/admin/expedientes/{id}/aprobar` | Sí | Sí | — | [M06-verificacion/post-admin-expedientes-id-aprobar.md](../specs/M06-verificacion/post-admin-expedientes-id-aprobar.md) |
| M06-verificacion | `GET /api/v1/admin/expedientes/{id}/documentos/{tipo}` | Sí | Sí | — | [M06-verificacion/get-admin-expedientes-id-documentos-tipo.md](../specs/M06-verificacion/get-admin-expedientes-id-documentos-tipo.md) |
| M06-verificacion | `POST /api/v1/admin/expedientes/{id}/rechazar` | Sí | Sí | — | [M06-verificacion/post-admin-expedientes-id-rechazar.md](../specs/M06-verificacion/post-admin-expedientes-id-rechazar.md) |
| M06-verificacion | `GET /api/v1/expedientes/documentos/{token}` | Sí | Sí | — | [M06-verificacion/get-expedientes-documentos-token.md](../specs/M06-verificacion/get-expedientes-documentos-token.md) |
| M06-verificacion | `GET /api/v1/expedientes/mi-expediente` | Sí | Sí | — | [M06-verificacion/get-expedientes-mi-expediente.md](../specs/M06-verificacion/get-expedientes-mi-expediente.md) |
| M06-verificacion | `POST /api/v1/expedientes/mi-expediente/comprobante` | Sí | Sí | — | [M06-verificacion/post-expedientes-mi-expediente-comprobante.md](../specs/M06-verificacion/post-expedientes-mi-expediente-comprobante.md) |
| M06-verificacion | `POST /api/v1/expedientes/mi-expediente/ine` | Sí | Sí | — | [M06-verificacion/post-expedientes-mi-expediente-ine.md](../specs/M06-verificacion/post-expedientes-mi-expediente-ine.md) |
| M06-verificacion | `GET /api/v1/mi-publicacion/estado-mapa` | Sí | Sí | — | [M06-verificacion/get-mi-publicacion-estado-mapa.md](../specs/M06-verificacion/get-mi-publicacion-estado-mapa.md) |
| M06-verificacion | `POST /api/v1/publicaciones/primera/enviar` | Sí | Sí | — | [M06-verificacion/post-publicaciones-primera-enviar.md](../specs/M06-verificacion/post-publicaciones-primera-enviar.md) |
| M07-favoritos | `GET /api/v1/favoritos` | Sí | Sí | — | [M07-favoritos/get-favoritos.md](../specs/M07-favoritos/get-favoritos.md) |
| M07-favoritos | `DELETE /api/v1/favoritos/{unitId}` | Sí | Sí | — | [M07-favoritos/delete-favoritos-unitId.md](../specs/M07-favoritos/delete-favoritos-unitId.md) |
| M07-favoritos | `PUT /api/v1/favoritos/{unitId}` | Sí | Sí | — | [M07-favoritos/put-favoritos-unitId.md](../specs/M07-favoritos/put-favoritos-unitId.md) |
| M08-chat | `GET /api/v1/conversaciones` | Sí | Sí | — | [M08-chat/get-conversaciones.md](../specs/M08-chat/get-conversaciones.md) |
| M08-chat | `GET /api/v1/conversaciones/{conversationId}/mensajes` | Sí | Sí | — | [M08-chat/get-conversaciones-conversationId-mensajes.md](../specs/M08-chat/get-conversaciones-conversationId-mensajes.md) |
| M08-chat | `POST /api/v1/conversaciones/{conversationId}/mensajes` | Sí | Sí | — | [M08-chat/post-conversaciones-conversationId-mensajes.md](../specs/M08-chat/post-conversaciones-conversationId-mensajes.md) |
| M08-chat | `POST /api/v1/unidades/{unitId}/conversaciones` | Sí | Sí | — | [M08-chat/post-unidades-unitId-conversaciones.md](../specs/M08-chat/post-unidades-unitId-conversaciones.md) |
| M09-ocupacion | `POST /api/v1/unidades/unirse` | Sí | Sí | — | [M09-ocupacion/post-unidades-unirse.md](../specs/M09-ocupacion/post-unidades-unirse.md) |
| M09-ocupacion | `GET /api/v1/unidades/{unitId}/inquilinos` | Sí (agregada en este bloque) | Sí | — | [M09-ocupacion/get-unidades-unitId-inquilinos.md](../specs/M09-ocupacion/get-unidades-unitId-inquilinos.md) |
| M09-ocupacion | `DELETE /api/v1/unidades/{unitId}/inquilinos/{tenantAccountId}` | Sí | Sí | — | [M09-ocupacion/delete-unidades-unitId-inquilinos-tenantAccountId.md](../specs/M09-ocupacion/delete-unidades-unitId-inquilinos-tenantAccountId.md) |
| M09-ocupacion | `GET /api/v1/unidades/{unitId}/invitacion` | Sí | Sí | — | [M09-ocupacion/get-unidades-unitId-invitacion.md](../specs/M09-ocupacion/get-unidades-unitId-invitacion.md) |
| M09-ocupacion | `PUT /api/v1/unidades/{unitId}/mantenimiento` | Sí | Sí | — | [M09-ocupacion/put-unidades-unitId-mantenimiento.md](../specs/M09-ocupacion/put-unidades-unitId-mantenimiento.md) |
| M09-ocupacion | `DELETE /api/v1/unidades/{unitId}/mi-vinculo` | Sí | Sí | — | [M09-ocupacion/delete-unidades-unitId-mi-vinculo.md](../specs/M09-ocupacion/delete-unidades-unitId-mi-vinculo.md) |
| M10-renta | `GET /api/v1/mi-resumen-renta/{periodo}` | Sí | Sí | — | [M10-renta/get-mi-resumen-renta-periodo.md](../specs/M10-renta/get-mi-resumen-renta-periodo.md) |
| M10-renta | `GET /api/v1/unidades/{unitId}/renta/mis-pagos` | Sí | Sí | — | [M10-renta/get-unidades-unitId-renta-mis-pagos.md](../specs/M10-renta/get-unidades-unitId-renta-mis-pagos.md) |
| M10-renta | `PUT /api/v1/unidades/{unitId}/renta/{periodo}` | Sí | Sí | — | [M10-renta/put-unidades-unitId-renta-periodo.md](../specs/M10-renta/put-unidades-unitId-renta-periodo.md) |
| M10-renta | `PUT /api/v1/unidades/{unitId}/renta/{periodo}/division` | Sí | Sí | — | [M10-renta/put-unidades-unitId-renta-periodo-division.md](../specs/M10-renta/put-unidades-unitId-renta-periodo-division.md) |
| M10-renta | `GET /api/v1/unidades/{unitId}/renta/{periodo}/mi-renta` | Sí | Sí | — | [M10-renta/get-unidades-unitId-renta-periodo-mi-renta.md](../specs/M10-renta/get-unidades-unitId-renta-periodo-mi-renta.md) |
| M10-renta | `GET /api/v1/unidades/{unitId}/renta/{periodo}/pagos` | Sí | Sí | — | [M10-renta/get-unidades-unitId-renta-periodo-pagos.md](../specs/M10-renta/get-unidades-unitId-renta-periodo-pagos.md) |
| M10-renta | `POST /api/v1/unidades/{unitId}/renta/{periodo}/pagos/{tenantAccountId}` | Sí | Sí | — | [M10-renta/post-unidades-unitId-renta-periodo-pagos-tenantAccountId.md](../specs/M10-renta/post-unidades-unitId-renta-periodo-pagos-tenantAccountId.md) |
| M11-reparaciones | `GET /api/v1/unidades/{unitId}/reparaciones` | Sí | Sí | — | [M11-reparaciones/get-unidades-unitId-reparaciones.md](../specs/M11-reparaciones/get-unidades-unitId-reparaciones.md) |
| M11-reparaciones | `POST /api/v1/unidades/{unitId}/reparaciones` | Sí | Sí | — | [M11-reparaciones/post-unidades-unitId-reparaciones.md](../specs/M11-reparaciones/post-unidades-unitId-reparaciones.md) |
| M11-reparaciones | `PUT /api/v1/unidades/{unitId}/reparaciones/{reparacionId}/estado` | Sí | Sí | — | [M11-reparaciones/put-unidades-unitId-reparaciones-reparacionId-estado.md](../specs/M11-reparaciones/put-unidades-unitId-reparaciones-reparacionId-estado.md) |
| M12-notificaciones | `GET /api/v1/notificaciones` | Sí | Sí | — | [M12-notificaciones/get-notificaciones.md](../specs/M12-notificaciones/get-notificaciones.md) |
| M13-administracion | `GET /api/v1/admin/cuentas` | Sí | Sí | — | [M13-administracion/get-admin-cuentas.md](../specs/M13-administracion/get-admin-cuentas.md) |
| M13-administracion | `PUT /api/v1/admin/cuentas/{accountId}` | Sí | Sí | — | [M13-administracion/put-admin-cuentas-accountId.md](../specs/M13-administracion/put-admin-cuentas-accountId.md) |
| M13-administracion | `PUT /api/v1/admin/cuentas/{accountId}/estado` | Sí | Sí | — | [M13-administracion/put-admin-cuentas-accountId-estado.md](../specs/M13-administracion/put-admin-cuentas-accountId-estado.md) |

## Hallazgos de seguridad detectados al construir esta matriz (no son huecos de prueba; son de producto/código, para el Bloque 4)

Al redactar `specs/M13-administracion/`, se encontró con evidencia de código
(`SetAccountStatusService.SetStatusAsync`, `EditAccountService.EditAsync`)
que **hoy no existe ninguna restricción que impida que un administrador se
autosuspenda o se autoedite** vía `PUT /api/v1/admin/cuentas/{accountId}` o
`.../estado`, y que ninguna operación de M13 registra qué administrador
ejecutó la acción. Esto coincide con uno de los pendientes de seguridad que
el Bloque 4 de esta auditoría debe cerrar explícitamente ("que un
administrador no pueda autoeliminarse ni autosuspenderse"). No se oculta
aquí: se documenta en las specs de esas dos operaciones y se deja para su
cierre en la rama `security/auditoria`.
