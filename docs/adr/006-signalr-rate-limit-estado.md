# ADR-006 · Estado compartido para rate limiting distribuido (propuesta documental acotada)

- Estado: **Propuesto — pendiente de ratificación y validación por el equipo (Jesui y Bridget)**
- Fecha: 2026-11-10 (actualizado tras revisión técnica)
- Propone: Leslie Neri. Revisión: Jesui, Bridget (Equipo backend).

> **Nota de trazabilidad:** El nombre del archivo (`006-signalr-rate-limit-estado.md`) se conserva por trazabilidad con las referencias documentales previas del SDD y la constitución; su alcance vigente está acotado exclusivamente al rate limiting distribuido.

## 1. Contexto

El requerimiento de infraestructura del curso (PDF v3, rúbrica de arquitectura y escalabilidad) exige desplegar el backend en al menos 2 réplicas (instancias) en Azure Container Apps.

En la implementación actual del repositorio (`main`), la API opera con **1 sola réplica** (`minReplicas = 1`, `maxReplicas = 1`) y los contadores de intentos de autenticación por IP residen exclusivamente en la memoria RAM del proceso (`src/UniHome.Api/Program.cs:196-295` y `src/UniHome.Api/Configuration/AuthRateLimitOptions.cs:8-35`). Con 2 réplicas y un balanceador de carga, los límites por IP se fragmentan o duplican al repartirse el tráfico entre réplicas, comprometiendo la defensa contra fuerza bruta (BE-SEC-009).

### MongoDB como única fuente de verdad
MongoDB Atlas continúa siendo la **única fuente de verdad** inmutable para:
- Suspensión de cuentas (M13).
- Eliminación de cuentas (EP-M01-09 / `DeleteAccountService.cs:69`).
- Revocación de sesiones (`RefreshToken`).
- Estado persistente de las cuentas (activo, bloqueado, confirmado).

La validación de cuenta activa en cada petición autenticada (BE-SEC-008, `src/UniHome.Api/Program.cs:150-164`) se ejecuta directamente contra MongoDB Atlas (`MongoAccountRepository.cs`). La caché del estado de cuenta en Redis permanece **expresamente pospuesta**; Redis no debe convertirse en fuente de verdad ni sustituir los mecanismos de persistencia y autorización de MongoDB.

## 2. Alternativas evaluadas

1. **Alternativa A:** Mantener instancia única del API. (Descartada para producción final: incumple el requisito del PDF v3 de correr al menos 2 réplicas).
2. **Alternativa B completa:** Redis para SignalR (M08), rate limiting y caché de datos. (Pospuesta: M08 no está codificado aún y el caché de cuenta no se requiere en esta fase).
3. **Alternativa C (Propuesta):** Adopción acotada de Redis exclusivamente para rate limiting distribuido, con costo $0 y modo degradado seguro.

## 3. Decisión propuesta

Se propone la adopción de Redis con **alcance estrictamente acotado**:

### 3.1. Rate limiting distribuido
- **Operaciones atómicas:** Los contadores por IP para endpoints de autenticación (`src/UniHome.Api/Controllers/AuthController.cs`) se sincronizan en Redis mediante un script Lua atómico (1 comando por intento con `EVALSHA` / `EVAL`), garantizando lectura, incremento y validación sin condiciones de carrera.
- **TTL obligatorio:** Cada clave en Redis se almacena con un TTL de expiración estricto coincidente con la ventana temporal configurada (p. ej. 60 segundos), evitando la acumulación indefinida de claves en memoria.
- **Identificación segura:** Seudonimización de la IP del cliente con hash SHA-256 junto con una sal criptográfica dedicada (`Auth:RateLimit:IpHashSalt`).
- **Tratamiento seguro de cabeceras:** La IP real del cliente se extrae mediante `ForwardedHeadersMiddleware` configurado con `ForwardLimit = 1` para confiar únicamente en el proxy de ingress de Azure Container Apps.

### 3.2. Comportamiento ante fallos de Redis
Se contemplan cinco escenarios de contingencia:
1. **Redis no disponible:** Interrupción del servicio de Upstash.
2. **Pérdida de conexión:** Caídas intermitentes de red o desconexión del cliente.
3. **Agotamiento de cuota:** Superación del límite gratuito de 500,000 comandos o 10 GB de ancho de banda.
4. **Latencia elevada:** Picos de latencia en la nube; se aplicará un timeout estricto de conexión/comando (p. ej. 250 ms) para evitar el agotamiento de hilos en Kestrel.
5. **Fallo en script Lua:** Error de compilación o ejecución en el motor de Redis.

**Aclaración crítica sobre límites en modo degradado:**
Un fallback local en memoria RAM **NO** mantiene un límite global compartido. Al particionarse el tráfico entre 2 réplicas, cada réplica contabiliza con contadores locales independientes, lo que en la práctica duplica el límite total admisible de peticiones o provoca bloqueos prematuros en usuarios legítimos bajo redes NAT compartidas si el balanceador distribuye las solicitudes de forma asimétrica.

**Política de seguridad para endpoints sensibles:**
Para los endpoints críticos de autenticación (Login, Registro, Refresh, Confirmación de correo), el equipo debe formalizar la política aplicable:
- *Opción Fail-Closed:* Rechazar peticiones de autenticación con código **HTTP 503 (Service Unavailable)** ante indisponibilidad técnica o timeout de Redis para garantizar protección estricta contra ataques de fuerza bruta; el código **HTTP 429 (Too Many Requests)** queda reservado exclusivamente para solicitudes legítimamente evaluadas que exceden el cupo o límite de tasa permitido.
- *Opción Fail-Open / Degradación local:* Permitir la continuidad del servicio degradando temporalmente a contadores locales en memoria por réplica (asumiendo que el límite global no se garantiza y emitiendo HTTP 429 si el contador local de la réplica se satura).
- *Estado de la decisión:* **[DECISIÓN PENDIENTE DE VALIDACIÓN Y CONSENSO DEL EQUIPO POR JESUI Y BRIDGET]**. No se asumirá una decisión preaprobada hasta contar con el consenso del equipo.

### 3.3. Independencia de Health Checks
La disponibilidad de Redis no forma parte del probe de preparación (`/health/ready`). La API se considera lista mientras el proceso esté activo y MongoDB Atlas responda.

## 4. Qué queda fuera explícitamente

1. **Caché de estado de cuenta (pospuesto):** Queda fuera de alcance. La validación de estado de cuenta (BE-SEC-008) continúa consultando directamente a MongoDB Atlas en cada request autenticado. Esto garantiza revocación inmediata e incontestable de sesiones ante suspensión (M13) o eliminación de cuenta (`src/UniHome.Application/Identity/DeleteAccount/DeleteAccountService.cs:69`), sin depender de Redis ni de lógica de invalidación con `DEL`.
2. **SignalR (ConversacionesHub de M08):** Queda fuera de alcance. El módulo M08 no está codificado (programado para Sprint 6 según `docs/plan-sprints-backend.md`). En esta fase no se configura backplane de SignalR en Redis. Cuando se diseñe M08, si el backend continúa con >= 2 réplicas, se abrirá un ADR futuro para evaluar el mecanismo de transporte distribuido.
3. **Caché de lectura distribuido:** Los datos de lectura pública frecuente quedan formalmente diseñados en el ADR-008 para su posterior implementación.

## 5. Presupuesto, costos y límites gratuitos (planeado)

*Fecha de consulta y fuentes: 2026-11-09 y 2026-11-10.*

- **Upstash Redis plan gratuito:** 500,000 comandos al mes, **256 MB** de almacenamiento (conforme a los límites oficiales del plan Free en https://upstash.com/pricing/redis, consultado el 2026-11-10; los 250 MB corresponden al plan de pago Fixed/Pay-as-you-go, no al nivel gratuito), 10 GB de transferencia de datos mensual, 1 base de datos, sin listas de control de acceso (ACLs) ni cifrado en reposo. No requiere tarjeta bancaria para operar en el nivel gratuito.
- **Presupuesto mensual en Azure (rg-unihome-dev):** Se encuentra configurado un presupuesto formal en Azure Cost Management denominado `unihome-dev-budget` por un monto de **US$25.00 mensuales**, con alertas automáticas al 50% (US$12.50), 80% (US$20.00) y 100% (US$25.00) notificadas al correo del proyecto `unihome0412@gmail.com`. El gasto acumulado actual verificado es de **US$0.00**.
- **Separación estricta de presupuestos y costos:**
  1. El presupuesto mensual de US$25 de Azure supervisa únicamente los recursos desplegados en el Resource Group de Azure (`rg-unihome-dev`) y **NO** cubre, limita ni supervisa los consumos de Upstash Redis en su nube independiente.
  2. El presupuesto de Azure no es un tope de gasto para Upstash ni garantiza la ausencia de cobros si se sobrepasan los límites del proveedor externo.
  3. Para Upstash, la garantía preventiva de costo cero consiste en **no registrar tarjeta de crédito** en la plataforma, forzando la detención del servicio en lugar de cobros por exceso.
- **Auditoría de consumo antes de escalar:** Operar 2 réplicas permanentes 24/7 en Azure Container Apps consume 1,296,000 vCPU-segundos/mes, superando la cuota mensual gratuita de 180,000 vCPU-segundos del plan Consumption. Por tanto, antes de activar 2 réplicas continuas se debe auditar el crédito restante de la suscripción (que vence el 3 de diciembre de 2026).
- **Estimación de comandos de Redis para rate limiting:**
  - Supuesto de tráfico de evaluación: 100 a 500 intentos de autenticación diarios (~3,000 a 15,000 comandos al mes con 1 invocación Lua atómica por intento).
  - Proyección: ~15,000 comandos al mes representan aproximadamente el 3% del cupo mensual de 500,000 comandos de Upstash.
  - [NO VERIFICADO]: Cuántos comandos contabiliza internamente Upstash por ejecución de script Lua (`EVALSHA` / `EVAL`).
  - [NO VERIFICADO]: Comportamiento exacto de Upstash al superar el tope mensual gratuito (rechazo con código de error, limitación de tasa o suspensión temporal de la base hasta el siguiente ciclo).

## 6. Estado real de las pruebas CI/CD

- **Pruebas implementadas y verificadas (con ejecuciones reales en GitHub Actions):**
  1. *Build, formato y pruebas unitarias (.NET 10):* Ejecución obligatoria en push y PRs con paso limpio (verificado en Run #128 y Run #121).
  2. *Análisis estático de seguridad (SAST) y dependencias:* Semgrep, Gitleaks, Dependabot / dotnet list package, CodeQL (verificado en Run #95) y SonarCloud (.NET) (verificado en Run #128 y Run #119).
  3. *Database Test (MongoDB efímero):* Job `mongo-db-test` con contenedor `mongo:8.0.34` fijado por digest en runner, base de datos `unihome_ci_verificacion` y validador TRX `tools/ci/mongo_ci_summary.py` (verificado en verde en Run #121 y Run #119).
  4. *Contract Test (OpenAPI):* 10 pruebas en `OpenApiGenderContractTests.cs` verificadas (verificado en Run #128 y Run #121).
  5. *DAST pasivo (OWASP ZAP):* Job `dast-zap` ejecutando `zaproxy:2.17.0` con API Kestrel local, MongoDB y SMTP mock local (`tools/ci/zap_dast.py`, verificado en verde en Run #121 y ejecución manual `42772939746`).
  6. *RESTler Fuzzing:* Job `restler-fuzzing` con Microsoft RESTler v9.3.1 en modo fuzz-lean aislado sobre 7 operaciones OpenAPI (verificado en verde en Run #121 con 2,054 peticiones y 0 errores 500).
  1. *Build, formato y pruebas unitarias (.NET 10):* Ejecución limpia en cada commit/PR.
  2. *Análisis estático de seguridad (SAST) y dependencias:* Semgrep, Gitleaks, Dependabot, CodeQL y SonarCloud en verde.
  3. *Database Test (MongoDB efímero):* Job `mongo-db-test` con contenedor `mongo:8.0.34` fijado por digest en runner, base de datos `unihome_ci_verificacion` y validador TRX `tools/ci/mongo_ci_summary.py` (verificado en verde en Run #121).
  4. *Contract Test (OpenAPI):* 10 pruebas en `OpenApiGenderContractTests.cs` verificadas.
  5. *DAST pasivo (OWASP ZAP):* Job `dast-zap` ejecutando `zaproxy:2.17.0` con API Kestrel local, MongoDB y SMTP mock local (`tools/ci/zap_dast.py`, verificado en verde en Run #121).
  6. *RESTler Fuzzing:* Job `restler-fuzzing` con Microsoft RESTler v9.3.1 en modo fuzz-lean aislado sobre 7 operaciones OpenAPI (verificado en verde en Run #121 con 2,054 peticiones y 0 errores 500).
- **Pruebas planeadas o pendientes (sin ejecución real o bloqueadas):**
  1. *Pact Contract Testing:* El proyecto proveedor `UniHome.PactTests` está compilado, pero la suite está bloqueada e inactiva (`vars.UNIHOME_ENABLE_PACT`) debido a que la carpeta `pacts/` no contiene contratos JSON reales entregados por el equipo del cliente Android (`unihome-mobile`).
  2. *Pruebas de concurrencia y consistencia de rate limiting entre 2 instancias:* **Trabajo planeado**. Requieren levantar en CI dos instancias simultáneas de la API conectadas al mismo Redis efímero para validar que el límite global es compartido y atómico.
  3. *Pruebas de integración de Redis:* **Trabajo planeado**. No existe actualmente contenedor ni suite de pruebas de Redis en los pipelines activos.

## 7. Seguridad de variables y secretos

- Las variables planeadas `Redis__ConnectionString` y `Auth__RateLimit__IpHashSalt` son de carácter confidencial.
- Se configurarán únicamente como secretos en Azure Container Apps y GitHub Secrets / Environments.
- Queda estrictamente prohibido incluir valores reales o ejemplos que puedan confundirse con credenciales válidas en código fuente, archivos markdown, documentación pública o registros de ejecución.
- Los logs de la aplicación deben sanitizar las cadenas de conexión para evitar fugas en Log Analytics.

## 8. Estado actual del despliegue

- **Redis NO está desplegado** en ninguna instancia de Upstash asociada a producción.
- **Las 2 réplicas NO están desplegadas**: la API `unihome-api` en `rg-unihome-dev` tiene exactamente **1 sola réplica activa** (`minReplicas = 1`, `maxReplicas = 1`).
- La arquitectura distribuida con Redis y el escalado a 2 réplicas constituyen una **mejora planeada para una etapa posterior** (posterior a la ratificación del ADR y a su implementación en código).

## 9. Criterios de aprobación

- [ ] Jesui y Bridget (Equipo backend) validan y ratifican la política de contingencia ante caídas de Redis (Fail-Closed vs. Degradación local).
- [ ] Equipo y Product Owner ratifican la adopción acotada de Redis bajo costo $0.
- [ ] Se revisan dependencias SDD (ARQ-002 BE-SEC-009, INF-001, INF-002, INF-004).
- [ ] Se registra la ratificación y actualización de la constitución cuando aplique.
