# Sprint 7 — Cierre backend M10/M12 (renta, pagos informativos y recordatorio)

**Rama:** `feature/sprint-07-backend-integral`
**Base:** `34c69ae` (Sprint 6 en `main`, PR #58)
**Alcance:** HU-035, 050, 051, 052, 053, 054, 055, 056, 057, 058, 059, 060,
061 y 090 (14 HU de la hoja de Sprint Planning). **Fuera de alcance:** M11 y
el resto de M12 (notificaciones de otros módulos), quedan para un sprint
posterior.

## 1. Estado de las 14 HU

| HU | Módulo | Estado | Entrega backend | Pruebas |
|---|---|---|---|---|
| HU-035 | M05 | Implementada | `GET /unidades/{unitId}/detalle`: detalle completo de una unidad propia (contrato `UnidadResponse`, el mismo que ya devuelven alta/fotos/servicios/zona-reglas), en cualquier estado de publicación | `GetUnitDetailForOwnerServiceTests` |
| HU-050/051/052 | M10 | Implementada | `PUT /unidades/{unitId}/renta/{periodo}`: renta total, 4 conceptos fijo/variable y día de corte; siempre reinicia la división a pendiente | `ConfigureRentPeriodServiceTests` + dominio |
| HU-053/054 | M10 | Implementada | `PUT /unidades/{unitId}/renta/{periodo}/division`: partes iguales (redondeo exacto) o personalizada (suma = renta total) | `SplitRentPeriodServiceTests` + `RentPeriodResolverTests` + dominio |
| HU-055 | M10 | Implementada | `POST /unidades/{unitId}/renta/{periodo}/pagos/{tenantAccountId}`: registra un pago informativo de un inquilino con vínculo activo | `RegisterRentPaymentServiceTests` |
| HU-056 | M10 | Implementada | `GET /unidades/{unitId}/renta/{periodo}/pagos`: estado de pago (pendiente/parcial/registrado) de cada inquilino frente a su cuota | `ListRentPaymentsServiceTests` |
| HU-057 | M10 | Implementada | `GET /unidades/{unitId}/renta/{periodo}/mi-renta`: el propio inquilino ve su cuota, lo pagado y su saldo; exige vínculo activo | `GetMyRentShareServiceTests` |
| HU-058 | M10 | Implementada | `GET /unidades/{unitId}/renta/mis-pagos`: historial propio de pagos a través de todos los periodos; no exige vínculo activo | `GetMyPaymentHistoryServiceTests` |
| HU-060/061 | M10 | Implementada | `GET /mi-resumen-renta/{periodo}`: resumen mensual combinado, como titular (por unidad propia) y como inquilino (por unidad con vínculo activo), en una sola consulta | `GetMyRentSummaryServiceTests` |
| HU-059 | M12 | Implementada | `GET /notificaciones`: genera/renueva el recordatorio de renta (ventana de 0-3 días antes del corte, omitido si la cuota ya está cubierta) al consultar, nunca por un job | `ListMyNotificationsServiceTests` |
| HU-090 | Transversal (M10/M12) | Implementada | Toda respuesta de renta/pagos incluye el aviso "UniHome no procesa pagos ni interviene en transacciones económicas entre las partes" | Cubierto por los contratos `*Response` de cada endpoint |

## 2. Endpoints nuevos

| Método | Ruta | Autorización | Éxito | Errores |
|---|---|---|---|---|
| GET | `/api/v1/unidades/{unitId}/detalle` | Bearer titular | 200 | 401, 403, 404, 503 |
| PUT | `/api/v1/unidades/{unitId}/renta/{periodo}` | Bearer titular | 200 | 400, 401, 403, 404, 413, 503 |
| PUT | `/api/v1/unidades/{unitId}/renta/{periodo}/division` | Bearer titular | 200 | 400, 401, 403, 404, 413, 503 |
| POST | `/api/v1/unidades/{unitId}/renta/{periodo}/pagos/{tenantAccountId}` | Bearer titular | 201 | 400, 401, 403, 404, 413, 503 |
| GET | `/api/v1/unidades/{unitId}/renta/{periodo}/pagos` | Bearer titular | 200 | 401, 403, 404, 422, 503 |
| GET | `/api/v1/unidades/{unitId}/renta/{periodo}/mi-renta` | Bearer inquilino | 200 | 401, 403, 404, 422, 503 |
| GET | `/api/v1/unidades/{unitId}/renta/mis-pagos` | Bearer inquilino | 200 | 401, 404, 503 |
| GET | `/api/v1/mi-resumen-renta/{periodo}` | Bearer | 200 | 400, 401, 503 |
| GET | `/api/v1/notificaciones` | Bearer | 200 | 401, 503 |

El catálogo OpenAPI pasa de 54 a 61 operaciones (9 nuevas: 1 de M05, 6 de
M10, 1 transversal de resumen y 1 de M12).

## 3. Decisiones provisionales (sin aprobación de producto)

Sin un valor fijado por la spec, igual estatus que otros umbrales de Sprints
anteriores:

- **Herencia entre periodos.** Si el periodo consultado, dividido, pagado o
  resumido todavía no existe, se crea copiando el periodo anterior más
  reciente de la misma unidad: renta total, conceptos y día de corte
  siempre; división y cuotas solo si el conjunto de inquilinos activos no
  cambió. Si cambió, la división queda pendiente para que el titular la
  rehaga — **las cuotas se "congelan" (no se recalculan solas) en cuanto
  cambia quién vive en la unidad**, porque una división calculada contra un
  grupo de inquilinos que ya no es el actual dejaría de repartir
  correctamente la renta total. Si no hay periodo anterior, no hay nada que
  heredar (`404`, "la renta no está configurada"). El endpoint de
  configuración (HU-050/051/052) nunca hereda: siempre usa los valores
  explícitos de la solicitud y reinicia la división a pendiente.
- **Recordatorio a 3 días o menos del corte (HU-059).** Se genera o renueva
  el recordatorio solo si al corte del periodo en curso le quedan entre 0 y
  3 días (`ListMyNotificationsService.ReminderWindowDays`). Un corte a más
  de 3 días no genera nada todavía; un corte ya pasado tampoco (se trata
  como vencido, no como próximo a vencer — un recordatorio de "está por
  vencer" no es el lugar para avisar que ya venció). Es una ventana corta
  a propósito: el recordatorio se regenera en cada consulta dentro de la
  ventana (upsert idempotente por id determinista), así que no hace falta
  una ventana larga para no perder el aviso.
- **El recordatorio se omite si la cuota ya está cubierta.** Usa el mismo
  `RentPeriod.ComputePaymentState` de HU-056/057: si el estado ya es
  `Registrado` (pagado ≥ cuota, incluido sobrepago), no se genera ni renueva
  el recordatorio — recordarle a alguien que pague algo que ya pagó no
  tiene sentido. Tampoco se genera si la división del periodo está
  pendiente o si el inquilino no tiene cuota asignada en ese periodo (p.
  ej., se unió a la unidad después de que el titular dividió la renta).
- **Saldo a favor si el pago supera la cuota (HU-055).** Un pago informativo
  mayor a la cuota del inquilino no se rechaza: `InformativePayment.Register`
  solo exige monto positivo y fecha de recepción no futura, nunca que el
  monto quepa dentro de la cuota. El exceso simplemente deja un saldo a
  favor (`saldoMxn` positivo) en HU-056/057/060/061, calculado como
  `pagado − cuota`. Es coherente con que UniHome no procesa el dinero
  (HU-090): no hay nada que "rechazar", solo una bitácora de lo que el
  titular dice haber recibido.
- **Redondeo de la división en partes iguales (HU-053), con ejemplo.** Se
  trabaja en centavos (`renta × 100`, entero) y el residuo se reparte de un
  centavo en centavo a los inquilinos ordenados por `accountId` (formato
  `"N"`, orden ordinal), para que la suma cierre exacto contra la renta
  total. Ejemplo: **$1000.00 entre 3 inquilinos → $333.34 + $333.33 +
  $333.33** (el primero en el orden ordinal del `accountId` absorbe el
  centavo sobrante; sin este ajuste, `1000 / 3 = 333.33...` repetido tres
  veces solo suma $999.99, un centavo menos que la renta real).
- **Resumen mensual combinado en un solo endpoint (HU-060/061), no dos.**
  `GET /mi-resumen-renta/{periodo}` devuelve `comoPropietario` y
  `comoInquilino` en la misma respuesta en vez de dos rutas separadas, para
  no duplicar el desglose por inquilino que ya da HU-056 (como titular) ni
  el detalle de cuota propia que ya da HU-057 (como inquilino): el resumen
  solo agrega totales por unidad. Una cuenta sin unidades propias ni
  vínculos activos recibe ambas listas vacías, nunca un error.

Ninguna de estas seis decisiones tiene aprobación de producto registrada. Si
el equipo de producto fija otro valor, requiere una historia de cambio
explícita: hoy están codificadas en `RentPeriod.InheritFrom`,
`ListMyNotificationsService.ReminderWindowDays`,
`RentPeriod.ComputePaymentState`, `InformativePayment.Register`,
`RentPeriod.ApplyEqualSplit` y la forma de respuesta de
`GetMyRentSummaryService`/`MiResumenRentaResponse`.

## 4. M10: diseño y cobertura

- **UniHome no procesa pagos (HU-090).** Todo el módulo es informativo: el
  titular lleva una bitácora manual de lo que dice haber recibido
  (`InformativePayment`), y UniHome nunca mueve dinero ni valida contra un
  proveedor de pagos. El aviso `RentNotices.PagosInformativos` va en toda
  respuesta de renta/pagos, incluida HU-059.
- **Resolución de periodo compartida.** `RentPeriodResolver` (de la
  configuración/división) se reutiliza sin cambios en HU-055/056/057/058/
  060/061/059: todos resuelven "el periodo existente o heredado del
  anterior más reciente" de la misma manera, para que la herencia sea
  consistente sin importar qué endpoint la dispare primero.
- **HU-057 exige vínculo activo; HU-058 no.** `GetMyRentShareService`
  responde `403` si el llamador no es inquilino activo de la unidad hoy.
  `GetMyPaymentHistoryService` no hace esa validación: recorre todos los
  periodos de la unidad (`IRentPeriodRepository.ListByUnitAsync`, nuevo) y
  filtra solo los pagos del llamador, visibles aunque ya haya dejado la
  unidad — es historial, no un derecho de ocupación.
- **`IOccupancyLinkRepository.ListActiveByTenantAsync` (nuevo).** Para
  HU-060/061 y HU-059 se necesitaba, por primera vez, "en qué unidades es
  esta cuenta inquilino activa" (antes solo existía "qué inquilinos están
  activos en esta unidad", al revés). Reutiliza el índice ya existente
  `ix_ocupacion_inquilino_estado` (M09-001); no se necesitó una migración
  nueva.
- Migración `M10-001` (ya aplicada desde el commit de configuración/
  división): `rentas_periodo` (`ux_rentas_periodo` único sobre
  `unitId`+`periodo`) y `pagos_informativos` (`ix_pagos_periodo_inquilino`,
  no único). Ningún endpoint de este cierre modificó su esquema declarado.

## 5. M12: diseño y cobertura (HU-059)

- **Generado al consultar, nunca por un job en segundo plano.** Esto estaba
  ya decidido en el modelo de dominio desde el commit que creó
  `Notification` (antes de este cierre): `Notification.ComputeRentReminderId`
  deriva un id determinista de (destinatario, unidad, periodo, tipo), así
  que volver a generarlo en la misma ventana es un upsert idempotente, no
  un duplicado. Este cierre implementa el repositorio
  (`MongoNotificationRepository`, antes solo existían esquema/índices/
  migración) y el servicio que lo dispara.
- **`ListMyNotificationsService`** recorre las unidades donde la cuenta es
  inquilino activo, resuelve el periodo del mes en curso para cada una (con
  herencia, igual que el resto de M10) y decide si generar el recordatorio
  según la ventana de 3 días y el estado de pago (sección 3). Después
  siempre devuelve la lista completa de notificaciones del destinatario,
  no solo la recién generada.
- Migración `M12-001` (ya aplicada desde el commit de dominio/migraciones):
  `notificaciones` (`ix_notificaciones_destinatario`); alcance limitado al
  recordatorio de renta. El resto de M12 (notificaciones de otros módulos)
  queda fuera de este sprint.

## 6. HU-035: por qué un endpoint nuevo y no reutilizar otro

La unidad ya tenía dos vistas desde Sprint 2/6: la "ficha" pública (HU-044/
045, M04, pensada para un prospecto, oculta datos administrativos) y la
"vista previa" (HU-080, una tarjeta con precio/distancia/favorito). Ninguna
de las dos sirve para que el titular revise el detalle completo de su propia
unidad sin modificar nada: los endpoints de alta/edición (fotos, servicios,
zona-reglas) devuelven el contrato completo `UnidadResponse` tras cada
cambio, pero no existía una ruta de solo lectura para volver a consultarlo.
`GetUnitDetailForOwnerService` llena ese hueco reutilizando el mismo
contrato, sin dominio ni infraestructura nuevos.

## 7. Cambios MongoDB

- **`M10-001`** y **`M12-001`**: ya declaradas y aplicadas/verificadas en
  `mongo-db-test`, `dast-zap` y `restler-fuzzing` desde el primer commit de
  este sprint. Ningún endpoint de este cierre agregó un campo, colección o
  índice nuevo: HU-035 reutiliza `unidades` (M05-001), HU-057/058/060/061
  reutilizan `rentas_periodo`/`pagos_informativos` (M10-001), y HU-059
  reutiliza `notificaciones` (M12-001).
- Ninguna migración de este sprint se aplicó ni promovió a Atlas. El orden
  completo para un entorno que parte de Sprint 6 es `M01-001`, `M01-002`,
  `M03-001`, `M05-001`, `M06-001`, `M07-001`, `M08-001`, `M09-001`,
  `M10-001`, `M12-001`. No saltar `M10-001` ni `M12-001`.

## 8. Pendientes

1. **CI:** `.github/workflows/ci.yml` ya aplica/verifica `M10-001` y
   `M12-001` en `mongo-db-test`, `dast-zap` y `restler-fuzzing` desde el
   primer commit de este sprint; sin cambios adicionales en los commits de
   este cierre. No se ejecutó el workflow en GitHub; queda **PENDING_CI**
   hasta que se disparen esos jobs en el PR.
2. Las seis decisiones provisionales de la sección 3 no tienen aprobación
   de producto.
3. `M10-001` y `M12-001` no se aplicaron ni promovieron a Atlas.
4. Mismo alcance que sprints previos: Docker build/Container Smoke no
   ejecutados localmente; ZAP, RESTler, Sonar, CodeQL, Semgrep y Pact
   remotos: **PENDING_CI**.
5. Pruebas Mongo reales de sprints anteriores siguen **BLOCKED**
   localmente sin `mongod`; este sprint no agregó una prueba real opt-in
   nueva (ningún endpoint nuevo introdujo un patrón de persistencia que no
   estuviera ya cubierto por `MongoRentPeriodRepository`/
   `MongoInformativePaymentRepository`/`MongoOccupancyLinkRepository`/
   `MongoNotificationRepository`, todos con la misma forma de
   `ReplaceOneAsync`+`IsUpsert`/`Find` que el resto del proyecto).

## 9. Pruebas locales

| Control | Resultado |
|---|---|
| `dotnet build -c Release -warnaserror` | PASS, 0 avisos/errores |
| `dotnet format --verify-no-changes` | PASS |
| Unitarias | **967/967 PASS** (4 omitidas: OCR/PDF reales, requieren binarios locales) |
| Integración | **299/299 ejecutadas PASS**; 13 opt-in Mongo/Cloudinary omitidas explícitamente |
| OpenAPI | PASS: 61 operaciones (52 de Sprints 1–6 + 9 de Sprint 7) |
| Python CI (`tools/ci`) | **167/167 PASS** |
| Mongo real (`mongo-db-test`) | **BLOCKED**: no hay `mongod` local |
| ZAP/RESTler/Sonar/CodeQL/Semgrep remotos | **PENDING_CI** |

## 10. Archivos principales

**Nuevos (Sprint 7)**
- `src/UniHome.Domain/Rentals/{RentPeriod,InformativePayment,RentEnums}.cs`,
  `src/UniHome.Domain/Notifications/Notification.cs`
- `src/UniHome.Application/Rentals/**` (resolver, configuración, división,
  pagos, vista del inquilino, resumen mensual) y
  `src/UniHome.Application/Notifications/ListMyNotificationsService.cs`
- `src/UniHome.Application/Units/Detail/GetUnitDetailForOwnerService.cs`
- `src/UniHome.Infrastructure/Persistence/Mongo/{Rentals,Notifications}/**`
  (esquema, índices, migraciones `M10-001`/`M12-001`, repositorios)
- `src/UniHome.Api/Controllers/{RentController,NotificationsController}.cs`;
  endpoint nuevo en `UnitsController` (HU-035)
- `src/UniHome.Api/Contracts/{Rentals,Notifications}/**`
- Tests de dominio, Application (con dobles en memoria) e integración HTTP
  (conteo OpenAPI) para las 14 HU.
- `docs/sprint-07-cierre.md` (este documento).

**Modificados**
- `MongoMigrationRegistry`, `Program.cs`, `DependencyInjection.cs`.
- `IOccupancyLinkRepository`/`MongoOccupancyLinkRepository`: nuevo método
  `ListActiveByTenantAsync`.
- `IRentPeriodRepository`/`MongoRentPeriodRepository`: nuevo método
  `ListByUnitAsync`.
- `.github/workflows/ci.yml`: `M10-001`/`M12-001` en `mongo-db-test`,
  `dast-zap` y `restler-fuzzing` (desde el primer commit del sprint).
- `tools/ci/zap_dast.py` y `tools/ci/test_zap_dast.py`: catálogo de 61
  operaciones, flujo real de las 14 HU.
- `README.md`: secciones de Sprint 7 (M10, M12) y detalle de unidad (M05).
