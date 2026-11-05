# Persistencia MongoDB M01: inventario y operación

## Alcance y trazabilidad

Implementación técnica para HU-001/HU-012 (cuenta) y HU-004/HU-005
(sesión), EP-M01-08 (refresh), RF-01/RF-02 y C-03/C-09/C-10 de la referencia
SDD. No cierra M01 ni aprueba ADR o decisiones de producto pendientes.
La referencia `.local-sdd` no se modifica.

`M01-001` materializa los nombres que usan los adaptadores; no adapta los nombres
ingleses del catálogo M02–M13 ni cambia contratos REST. La API no ejecuta DDL
explícito. **Debe verificarse la migración antes de habilitar tráfico de la API**.
Readiness comprueba conectividad e índices M01. Cada repositorio verifica los
índices de su colección antes de operar, sin crear nada ni cachear la comprobación;
si falta un índice o hay divergencia crítica, falla como almacenamiento no disponible.
Esto añade una consulta `listIndexes` por operación; se requiere ese permiso de
lectura de metadatos, no permisos de DDL. La identidad operativa no debe poder
eliminar índices: la comprobación y la escritura no forman una transacción con DDL.

## Mapeo físico comprobado por serialización BSON

Fuentes: `AccountDocument`, `RefreshSessionDocument` y sus repositorios.
`MongoIdentityBsonMappingTests` verifica nombres y tipos reales del serializador.
Los identificadores se convierten de Guid a string de 32 hex (`N`) en los
repositorios: no son ObjectId ni UUID binario. Fechas de instante: BSON date UTC.

### `cuentas`

| Campo físico | Tipo BSON | Índice |
|---|---|---|
| `_id` | string (Guid N) | `_id_`, único nativo |
| `nombreCompleto` | string | — |
| `correoNormalizado` | string | `ux_cuentas_correo_normalizado`, único, ascendente |
| `fechaNacimiento` | string (`yyyy-MM-dd`) | — |
| `genero` | string | — |
| `hashContrasena` | string (PBKDF2 del adaptador Identity) | — |
| `aceptacionPrivacidad` | documento | — |
| `aceptacionPrivacidad.version` | string | — |
| `aceptacionPrivacidad.aceptadaEnUtc` | date | — |
| `estado` | string | — |
| `correoConfirmado` | bool | — |
| `creadoEnUtc` | date | — |
| `correoConfirmadoEnUtc` | date/null | — |
| `verificacionCorreo` | documento/null | — |
| `verificacionCorreo.hash` | string (HMAC) | — |
| `verificacionCorreo.emitidoEnUtc` | date | — |
| `verificacionCorreo.expiraEnUtc` | date | — |
| `verificacionCorreo.consumidoEnUtc` | date/null | — |
| `intentosReenvioUtc` | array de date | — |

### `sesiones_refresh`

| Campo físico | Tipo BSON | Índice |
|---|---|---|
| `_id` | string (Guid N) | `_id_`, único nativo |
| `cuentaId` | string (Guid N) | `ix_sesiones_refresh_cuenta`, ascendente |
| `hashRefreshActual` | string (SHA-256 hexadecimal) | `ux_sesiones_refresh_token_actual`, único, ascendente |
| `refreshEmitidoEnUtc` | date | — |
| `expiraEnUtc` | date | `ttl_sesiones_refresh_expiracion`, ascendente, TTL 0 s |
| `tokensUsados` | array de documentos | — |
| `tokensUsados[].hash` | string | — |
| `tokensUsados[].usadoEnUtc` | date | — |
| `revocadaEnUtc` | date/null | — |
| `motivoRevocacion` | string/null | — |
| `creadaEnUtc` | date | — |
| `ultimaRotacionEnUtc` | date | — |

La familia de refresh es la sesión identificada por `_id`: no hay campo de familia
adicional ni consulta por fecha de revocación que justifique otro índice.
La rotación usa un filtro atómico sobre hash vigente, revocación y expiración.
El backend rechaza sesiones vencidas aunque MongoDB aún no las haya eliminado:
TTL es limpieza eventual, no autorización.

## Inventario Atlas observado

Auditoría de solo lectura realizada sobre `unihome_modelo_pruebas` el 2026-11-04.
Las 20 colecciones preexistentes conservan validador con nivel efectivo `strict`
y acción `error`. **`error` es la acción correcta, no un fallo de auditoría**.
No se modificaron validadores ni índices M02–M13. Esta comprobación de modo no
certifica la correspondencia de cada JSON Schema con futuros DTOs.

Todas tienen `_id_`; índices adicionales observados:

| Colección | Índices adicionales |
|---|---|
| `auditoria_admin` | `ix_auditoria_actor_fecha`, `ix_auditoria_objetivo_fecha` |
| `conversaciones` | `ix_conversaciones_participantes` |
| `correos_salientes` | `ix_correos_pendientes`, `ux_correos_idempotencia` |
| `denuncias` | `ix_denuncias_estado_fecha` |
| `expedientes_verificacion` | `ix_expedientes_propiedad`, `ix_expedientes_revision` |
| `favoritos` | `ux_favoritos_cuenta_unidad` |
| `invitaciones_unidad` | `ix_invitaciones_unidad`, `ux_invitaciones_codigo` |
| `mensajes` | `ix_mensajes_conversacion_fecha` |
| `notificaciones` | `ix_notificaciones_destinatario` |
| `pagos_informativos` | `ix_pagos_periodo_inquilino` |
| `propiedades` | `geo_propiedades`, `ix_propiedades_propietario` |
| `rentas_periodo` | `ux_rentas_periodo` |
| `reparaciones` | `ix_reparaciones_estado`, `ix_reparaciones_unidad` |
| `restablecimientos_contrasena` | `ix_restablecimientos_cuenta`, `ttl_restablecimientos_expires`, `ux_restablecimientos_token` |
| `servicios_catalogo` | `ux_servicios_clave` |
| `solicitudes_eliminacion` | `ix_baja_cuenta_fecha`, `ix_baja_estado` |
| `tareas_eliminacion_archivos` | `ix_tarea_baja_cola`, `ux_tarea_baja_archivo` |
| `unidades` | `geo_unidades`, `ix_unidades_busqueda`, `ix_unidades_propiedad` |
| `universidades` | `geo_universidades`, `ix_universidades_nombre` |
| `vinculos_ocupacion` | `ix_ocupacion_inquilino_estado`, `ix_ocupacion_unidad_estado` |

Los índices `geo_*` observados son `2dsphere`; se mantienen GeoJSON,
los documentos privados de propiedad, Decimal128 y pagos externos informativos
como requisitos para las futuras implementaciones, sin declararlos implementados.

Después de `M01-001`: 23 colecciones. `cuentas` y `sesiones_refresh` conservan
los índices del inventario anterior; `migraciones` usa solo `_id_`. No se añadieron
validadores JSON a estas tres colecciones. Tras limpiar las pruebas se observaron
0 cuentas, 0 sesiones y 1 registro de migración (conteos estimados de auditoría).

## Operación segura

Consultar `tools/UniHome.MongoMigrator/README.md` para `audit`, `plan`, `apply` y
`verify`. La conexión se carga de User Secrets o entorno; no se pasa por argumentos.
El nombre de base debe indicarse explícitamente. `apply` exige `--confirm`.
La base `unihome_desarrollo` permanece bloqueada incluso si aparece en la allowlist.

La aplicación no es transaccional: si falla un paso puede quedar DDL parcial.
Detenerse, auditar, corregir la causa y revisar un nuevo plan antes de reintentar.
No borrar colecciones ni índices para reparar automáticamente. La versión se
registra al finalizar mediante inserción inmutable. Si otro proceso gana la
inserción, se compara su checksum y se rechaza uno diferente sin reemplazarlo.
No hay lock distribuido para el DDL: ejecutar un solo migrador por base desde la
ventana operativa; no aplicar definiciones competidoras en paralelo. Una definición
ya registrada no debe editarse: se crea una migración nueva.

En la primera ejecución se detectó el error 197 al intentar un índice redundante
sobre `_id` del ledger. Se corrigió usando su índice nativo y se completó el registro;
la verificación posterior no mostró escrituras pendientes.

La adopción en desarrollo o producción exige autorización humana, backup,
revisión de datos duplicados y del plan, credencial temporal de migración con
privilegios mínimos y verificación antes del tráfico. La herramienta actual no
autoriza migrar `unihome_desarrollo`.

## Pruebas y límites

La prueba Atlas opt-in usa la API y repositorios reales, pero captura el correo en
memoria. Requiere el esquema previamente migrado y no llama a `ApplyAsync`.
La auditoría de las 20 colecciones externas se ejecuta aparte con la herramienta,
no es un fixture exigido por la prueba de identidad. La confirmación se realiza mediante dominio/repositorio como precondición;
**no prueba un endpoint de confirmación**, aún pendiente de contrato.
Los datos ficticios se eliminan por identificador; no se eliminan colecciones,
índices ni ledger. No se envió correo SMTP real.
La prueba de unicidad intenta insertar directamente un duplicado con otro `_id`
y espera el código 11000, además del conflicto REST. Un coordinador de pruebas
espera a que ambas solicitudes alcancen el CAS antes de liberarlas; las operaciones
de persistencia y sus filtros no se sustituyen.
No se ha probado una identidad Atlas independiente sin permisos de DDL; no se
crearon usuarios ni se modificaron privilegios del clúster.

Los resultados de restore/build/test/format se informan por separado para no
confundir las pruebas con dobles con la ejecución contra Atlas. Pendientes:
M01-C, DEC-09/DEC-10, política de privacidad/retención y definición de privilegio
administrativo antes de M02/M03. Sin recursos nuevos de Azure, Docker, commits,
push ni interfaces Scalar/Swagger.

### Estado de la evidencia de ejecución

La versión anterior de la prueba pasó contra Atlas (55/55 integraciones de esa
etapa). Después de la revisión se añadieron comprobación fail-closed de índices,
ledger inmutable, sanitización del parser, rechazo de índices adicionales
peligrosos y barrera de concurrencia de pruebas; se retiró el DDL de la prueba.
La repetición Atlas de esta versión quedó **bloqueada por permisos de la herramienta**,
incluso después de la autorización expresa del usuario. No atribuir el resultado
Atlas anterior al código final. Ejecutar el comando opt-in del README en el entorno
local autorizado antes de dar por verificada esta revisión con base real.

Validación local de esta revisión final:

| Comando / comprobación | Resultado observado |
|---|---|
| `dotnet restore UniHome.Backend.slnx --locked-mode` | Correcto |
| `dotnet build UniHome.Backend.slnx -c Release --no-restore -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test UniHome.Backend.slnx -c Release --no-build --no-restore` | 173 unitarias + 56 integración correctas; 1 Atlas omitida por opt-in |
| `dotnet format UniHome.Backend.slnx --verify-no-changes --no-restore` | Correcto, sin cambios |
| `git diff --check` | Correcto |
| CLI con URI malformada exclusivamente sintética | Rechazo exit 5, sin URI/credenciales en salida |
| CLI `apply` sobre base prohibida con conexión local sintética | Rechazo exit 4 antes de conexión/DDL |

### Hallazgos de revisión y pendientes

| ID | Gravedad | Fuente | Impacto | Resolución / propuesta | Decisión requerida |
|---|---|---|---|---|---|
| M01-01 | Alta, corregida estáticamente | HU-001/RF-01, C-10; repositorios/readiness | Operar sin unicidad | Guard solo lectura, fail-closed y pruebas sin índice | Validar otra vez en Atlas |
| M01-02 | Alta, corregida estáticamente | C-10; ledger | Sobrescribir checksum concurrente | Inserción inmutable, comparación del ganador; sin lock DDL distribuido | Un migrador por base |
| M01-03 | Alta, corregida estáticamente | C-09/C-13; CLI y soporte de pruebas | Fuga ante URI malformada | Manejo sanitizado, sin inner exception sensible; pruebas sintéticas | Ninguna para esta corrección |
| M01-04 | Media, corregida estáticamente | C-10; catálogo de índices | TTL extra elimina datos o unique extra restringe cuentas | Rechazo compartido en API/migrador y pruebas | Revisar excepciones mediante migraciones futuras |
| E-01 | Evidencia pendiente | C-10; prueba Atlas final | No atribuir resultados antiguos al código final | Reejecutar opt-in fuera del bloqueo de herramientas | Habilitar ejecución local autorizada |
| E-02 | Evidencia pendiente | C-13; identidad de ejecución | Privilegios mínimos no demostrados con otro usuario Atlas | Probar usuario CRUD/metadatos sin DDL, sin crear usuarios automáticamente | Aprovisionamiento humano autorizado |
