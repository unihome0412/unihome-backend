# Despliegue en Azure Container Apps

**Estado (2026-11-05):** desplegado en un entorno de desarrollo real, en la
suscripción "Azure subscription 1", región `eastus`, imagen `unihome-api:v2`.
`/health/live` y `/health/ready` responden `200`, HTTP redirige a HTTPS sin
bucle, y Swagger UI está disponible de forma opcional vía
`Api__EnableSwagger` (ver sección dedicada). **No se tocó `main`.**

| Recurso | Nombre | Tipo/plan |
|---|---|---|
| Grupo de recursos | `rg-unihome-dev` | — |
| Log Analytics | `log-unihome-dev` | Retención 30 días (por defecto) |
| Azure Container Registry | `acrunihomedev` | Basic |
| Entorno de Container Apps | `cae-unihome-dev` | Consumption |
| Container App | `unihome-api` | 0.25 vCPU / 0.5 GiB, 1–1 réplicas |

URL pública: `https://unihome-api.agreeablesky-4cd63e24.eastus.azurecontainerapps.io`
Swagger UI (demo, ver sección dedicada): `https://unihome-api.agreeablesky-4cd63e24.eastus.azurecontainerapps.io/swagger`

## Swagger UI (demostración)

Swagger UI renderiza el documento OpenAPI **nativo** de .NET 10
(`Microsoft.AspNetCore.OpenApi`, vía `MapOpenApi()`); no se usa el generador de
Swashbuckle, solo su paquete `Swashbuckle.AspNetCore.SwaggerUI` para la interfaz.
Controlado por `Api__EnableSwagger`, **desactivado por defecto**:

- `Api__EnableSwagger=false` (o ausente): `/swagger` y `/openapi/v1.json`
  responden `404`; `/` no tiene ninguna ruta mapeada.
- `Api__EnableSwagger=true`: `/swagger` y `/openapi/v1.json` responden `200`;
  `/` redirige (`302`) a `/swagger`.

El documento incluye un `servers` con la URL pública exacta en `https`
(detectada de la propia solicitud), así que el botón "Authorize" (esquema
Bearer, ya documentado en el propio OpenAPI) y cualquier solicitud de "Try it
out" desde Swagger UI usan HTTPS, nunca HTTP plano.

Activar/desactivar en el entorno ya desplegado:

```powershell
# Activar (demo)
az containerapp update --name unihome-api --resource-group rg-unihome-dev `
  --set-env-vars "Api__EnableSwagger=true"

# Desactivar despues de la demo
az containerapp update --name unihome-api --resource-group rg-unihome-dev `
  --set-env-vars "Api__EnableSwagger=false"
```

`--set-env-vars` agrega o actualiza variables sin tocar las demás ya
configuradas (secretos incluidos).

## Variables de entorno requeridas

Ninguna de estas variables tiene valor en este documento ni en el repositorio.
Deben configurarse en Container Apps como *secrets* (las sensibles) o variables
de entorno normales (las no sensibles), nunca en `appsettings.json` ni en la
imagen. Nombres verificados contra las clases de opciones correspondientes en
`src/UniHome.Infrastructure/Configuration/` y `src/UniHome.Api/Configuration/`.

| Variable | Sensible | Clase/sección | Obligatoria |
|---|---|---|---|
| `MongoDb__ConnectionString` | Sí | `MongoDbOptions` (`MongoDb`) | Sí, para que la API opere con persistencia |
| `MongoDb__DatabaseName` | No | `MongoDbOptions` (`MongoDb`) | Sí, junto con la anterior |
| `Jwt__SigningKey` | Sí | `JwtOptions` (`Jwt`) | Sí, mínimo 32 caracteres fuera de Development |
| `Jwt__Issuer` | No | `JwtOptions` (`Jwt`) | No (tiene valor por defecto `UniHome`) |
| `Jwt__Audience` | No | `JwtOptions` (`Jwt`) | No (tiene valor por defecto `UniHome.Mobile`) |
| `VerificationCode__HmacSecret` | Sí | `VerificationCodeOptions` (`VerificationCode`) | Sí, mínimo 32 caracteres fuera de Development |
| `Smtp__Host` | No | `SmtpOptions` (`Smtp`) | Solo si se configura envío de correo |
| `Smtp__Port` | No | `SmtpOptions` (`Smtp`) | Solo con `Smtp__Host`; debe ser `587` (STARTTLS) |
| `Smtp__UseStartTls` | No | `SmtpOptions` (`Smtp`) | Solo con `Smtp__Host`; debe ser `true` |
| `Smtp__Username` | Sí | `SmtpOptions` (`Smtp`) | Solo con `Smtp__Host` |
| `Smtp__Password` | Sí | `SmtpOptions` (`Smtp`) | Solo con `Smtp__Host` |
| `Smtp__FromEmail` | No | `SmtpOptions` (`Smtp`) | Solo con `Smtp__Host` |
| `Smtp__FromName` | No | `SmtpOptions` (`Smtp`) | Solo con `Smtp__Host` |
| `Proxy__TrustForwardedHeaders` | No | `ProxyOptions` (`Proxy`) | Sí, en `true`, solo si el contenedor corre detrás del ingress de Container Apps |
| `Api__EnableSwagger` | No | `ApiOptions` (`Api`) | No; `false` por defecto, activar solo para demostraciones puntuales |

Si no existe ninguna clave bajo `Smtp`, el registro funciona sin enviar correo
real (`correoConfirmacionEnviado: false`); si existe alguna, todas las
obligatorias de esa sección se validan al arrancar.

## Por qué `Proxy__TrustForwardedHeaders`

Container Apps termina TLS en su ingress y reenvía la solicitud al contenedor
por HTTP plano, agregando `X-Forwarded-For` y `X-Forwarded-Proto`. Sin esta
variable en `true`:

- `UseHttpsRedirection` vería siempre esquema `http` y redirigiría cada
  solicitud, incluidas las que ya llegaron por HTTPS al ingress (bucle de
  redirecciones).
- El limitador de tasa por IP (`AuthRateLimitOptions`) agruparía a todos los
  usuarios bajo la IP interna del ingress, no la IP real del cliente.

Por eso está desactivada por defecto (`false`) y solo debe activarse cuando el
contenedor efectivamente corre detrás de ese ingress, nunca si queda expuesto
directamente a Internet sin un proxy de por medio.

## Imagen de contenedor

El `Dockerfile` (raíz del repo) es multi-stage: compila con el SDK de .NET 10
(`mcr.microsoft.com/dotnet/sdk:10.0.403`, versión fija) y publica solo
`UniHome.Api` en Release con `dotnet restore --locked-mode`; la imagen final
usa el runtime ASP.NET (`mcr.microsoft.com/dotnet/aspnet:10.0.14`, versión
fija), escucha en el puerto `8080` y corre con el usuario sin privilegios ya
definido por la imagen base (`$APP_UID`), no con root. El `.dockerignore`
excluye `.git`, `.github`, `.local-sdd`, `.opencode`, `.vs`, `AGENTS.md`,
`opencode.json`, `docs`, `tests`, `TestResults`, `bin`, `obj` y cualquier
`.env`; la imagen no contiene secretos ni código de pruebas.

Construcción local, sin publicar a ningún registro (verificación, no despliegue):

```powershell
docker build -t unihome-api:local .
```

## Crear los recursos desde cero

Requiere sesión iniciada (`az login`) con la suscripción "Azure subscription 1"
activa, y el archivo de secretos local con las 9 variables no generadas aquí
(nunca en el repositorio). Ningún comando de esta sección imprime valores
sensibles: los secretos sensibles (`MongoDb__ConnectionString`,
`Smtp__Username`, `Smtp__Password`, `Jwt__SigningKey`,
`VerificationCode__HmacSecret`) se cargan desde un script externo al
repositorio y se fijan directamente como *secrets* de Container Apps.

```powershell
az group create --name rg-unihome-dev --location eastus

az monitor log-analytics workspace create `
  --resource-group rg-unihome-dev --workspace-name log-unihome-dev --location eastus

az acr create --resource-group rg-unihome-dev --name acrunihomedev `
  --sku Basic --location eastus --admin-enabled true

az acr login --name acrunihomedev
docker build -t acrunihomedev.azurecr.io/unihome-api:v1 .
docker push acrunihomedev.azurecr.io/unihome-api:v1

$logId = az monitor log-analytics workspace show --resource-group rg-unihome-dev `
  --workspace-name log-unihome-dev --query customerId -o tsv
$logKey = az monitor log-analytics workspace get-shared-keys --resource-group rg-unihome-dev `
  --workspace-name log-unihome-dev --query primarySharedKey -o tsv
az containerapp env create --name cae-unihome-dev --resource-group rg-unihome-dev `
  --location eastus --logs-workspace-id $logId --logs-workspace-key $logKey

$acrUser = az acr credential show --name acrunihomedev --query username -o tsv
$acrPass = az acr credential show --name acrunihomedev --query "passwords[0].value" -o tsv
az containerapp create --name unihome-api --resource-group rg-unihome-dev `
  --environment cae-unihome-dev --image acrunihomedev.azurecr.io/unihome-api:v1 `
  --registry-server acrunihomedev.azurecr.io --registry-username $acrUser --registry-password $acrPass `
  --target-port 8080 --ingress external --cpu 0.25 --memory 0.5Gi `
  --min-replicas 1 --max-replicas 1
```

`ACR Tasks` (`az acr build`) está bloqueado en esta suscripción
(`TasksOperationsNotAllowed`); por eso la imagen se construye localmente con
Docker y se sube con `docker push` en vez de `az acr build`.

Después, los *secrets* (valores reales, nunca en texto en comandos
compartidos ni en este documento — usar variables en memoria de un script
externo al repositorio) y las variables de entorno:

```powershell
az containerapp secret set --name unihome-api --resource-group rg-unihome-dev --secrets `
  "mongodb-connection-string=<valor real>" `
  "smtp-username=<valor real>" `
  "smtp-password=<valor real>" `
  "jwt-signing-key=<aleatorio >= 64 bytes, generado en el script>" `
  "verification-code-hmac-secret=<aleatorio >= 64 bytes, generado en el script>"

az containerapp update --name unihome-api --resource-group rg-unihome-dev --set-env-vars `
  "ASPNETCORE_ENVIRONMENT=Production" `
  "Proxy__TrustForwardedHeaders=true" `
  "Jwt__Issuer=UniHome" `
  "Jwt__Audience=UniHome.Mobile" `
  "MongoDb__DatabaseName=<valor real>" `
  "Smtp__Host=<valor real>" `
  "Smtp__Port=<valor real>" `
  "Smtp__UseStartTls=<valor real>" `
  "Smtp__FromEmail=<valor real>" `
  "Smtp__FromName=<valor real>" `
  "MongoDb__ConnectionString=secretref:mongodb-connection-string" `
  "Smtp__Username=secretref:smtp-username" `
  "Smtp__Password=secretref:smtp-password" `
  "Jwt__SigningKey=secretref:jwt-signing-key" `
  "VerificationCode__HmacSecret=secretref:verification-code-hmac-secret"
```

**Nota de PowerShell:** si alguno de estos valores (en particular
`MongoDb__ConnectionString`) contiene `&`, invocar `az.cmd` directo desde
PowerShell puede hacer que `cmd.exe` lo interprete como separador de
comandos. La forma confiable es invocar el intérprete real del CLI sin pasar
por el envoltorio `.cmd`:

```powershell
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\python.exe" -IBm azure.cli containerapp secret set ...
```

### Probes de liveness y readiness

El CLI de `containerapp` no tiene una bandera directa para probes; se
exportan y se vuelven a aplicar vía YAML (sin tocar nada más del recurso):

```powershell
az containerapp show --name unihome-api --resource-group rg-unihome-dev -o yaml > app.yaml
# Insertar en properties.template.containers[0], junto a "resources":
#   probes:
#   - type: Liveness
#     httpGet: { path: /health/live, port: 8080 }
#     initialDelaySeconds: 5
#     periodSeconds: 10
#     failureThreshold: 3
#   - type: Readiness
#     httpGet: { path: /health/ready, port: 8080 }
#     initialDelaySeconds: 5
#     periodSeconds: 10
#     failureThreshold: 3
az containerapp update --name unihome-api --resource-group rg-unihome-dev --yaml app.yaml
Remove-Item app.yaml
```

## Redesplegar una nueva versión de la imagen

```powershell
az acr login --name acrunihomedev
docker build -t acrunihomedev.azurecr.io/unihome-api:v3 .
docker push acrunihomedev.azurecr.io/unihome-api:v3
az containerapp update --name unihome-api --resource-group rg-unihome-dev `
  --image acrunihomedev.azurecr.io/unihome-api:v3
```

Container Apps crea una revisión nueva y la activa automáticamente
(`activeRevisionsMode: Single`); la anterior queda inactiva, no se elimina de
inmediato.

## Verificar

```powershell
curl.exe https://unihome-api.agreeablesky-4cd63e24.eastus.azurecontainerapps.io/health/live
curl.exe https://unihome-api.agreeablesky-4cd63e24.eastus.azurecontainerapps.io/health/ready
curl.exe -I http://unihome-api.agreeablesky-4cd63e24.eastus.azurecontainerapps.io/health/live
```

## Eliminar todos los recursos (control de costo)

Un solo comando elimina el grupo completo (ACR, entorno, Container App y Log
Analytics) y detiene cualquier cargo asociado:

```powershell
az group delete --name rg-unihome-dev --yes --no-wait
```

## Costo esperado

- **Container Apps (plan Consumption):** con 1 réplica mínima fija de 0.25
  vCPU / 0.5 GiB corriendo permanentemente, se cobra por ese consumo continuo
  (no hay escala a cero porque `--min-replicas 1`); el nivel gratuito mensual
  de Consumption normalmente no cubre una réplica encendida 24/7 completa.
- **Azure Container Registry (Basic):** costo fijo mensual bajo (~5 USD/mes a
  precios públicos de referencia), más el almacenamiento de la imagen (pocos
  MB en este caso).
- **Log Analytics:** las primeras GB de ingestión por mes suelen estar en el
  nivel gratuito; el consumo de un solo contenedor pequeño es bajo.
- No se activó pago por uso adicional ni se cambió el plan de la suscripción;
  solo se usaron los recursos explícitamente autorizados. Verificar el costo
  real en Azure Cost Management, no asumir estas cifras como facturación
  definitiva.

## Pendiente

- Decidir y documentar la estrategia de CI/CD de imagen (build + push
  automatizado); hoy el build/push es manual desde esta máquina.
- Evaluar reemplazar las credenciales admin del ACR por una identidad
  administrada con rol `AcrPull`, en vez de usuario/contraseña del registro.
- Registrar una cuenta real de prueba contra este despliegue (explícitamente
  fuera de alcance de esta tarea).
- `ACR Tasks` sigue bloqueado en esta suscripción; si se necesita build remoto
  más adelante, abrir un caso de soporte de Azure.
- **Escalado a 2 réplicas y estado compartido (propuesta ADR-006 en el SDD compartido: `UniHome_Backend_SDD_v3_propuesta_redis/docs/adr/006-signalr-rate-limit-estado.md`):**
  - **Estado actual del despliegue (Verificado):** El despliegue actual en Azure (entorno de desarrollo `rg-unihome-dev`) opera con **1 sola réplica** (`minReplicas = 1`, `maxReplicas = 1`, 0.25 vCPU / 0.5 GiB). Ni Redis ni las 2 réplicas están desplegados en la nube. La arquitectura distribuida es una propuesta documental planeada sujeta a aprobación del equipo.
  - **Objetivo de la propuesta:** Operar 2 réplicas compartiendo estado en Redis exclusivamente para rate limiting distribuido (BE-SEC-009) y evitar la evasión de límites de autenticación por balanceo de carga.
  - **MongoDB como única fuente de verdad:** MongoDB Atlas permanece como la única fuente de verdad inmutable para la suspensión de cuentas (M13), eliminación de cuentas (EP-M01-09 / `DeleteAccountService.cs:69`), revocación de sesiones (`RefreshToken`) y el estado persistente de las cuentas. El caché de estado de cuenta en Redis queda expresamente pospuesto; Redis no es ni será fuente de verdad para identidades ni permisos.
  - **Rate limiting distribuido, atomicidad y proxies confiables:**
    - Los contadores compartidos operan mediante script Lua atómico (1 comando por intento) con TTL de expiración estricto para cada ventana temporal.
    - Las peticiones se identifican mediante hash criptográfico SHA-256 de la IP del cliente junto con una sal dedicada (`Auth:RateLimit:IpHashSalt`).
    - Para la extracción de la IP real detrás del ingress de Azure Container Apps, la propuesta contempla el uso de `ForwardedHeadersMiddleware`. Se enfatiza que fijar `ForwardLimit = 1` limita la cantidad de encabezados procesados pero **NO** constituye una garantía suficiente contra spoofing por sí solo; es indispensable configurar la confianza explícita en proxies mediante `KnownProxies` o `KnownIPNetworks` según la topología real de red de Container Apps, rechazando encabezados `X-Forwarded-For` de fuentes no confiables (configuración y validación técnica pendientes de implementación).
  - **Comportamiento ante fallos de Redis y modo degradado:**
    - Se contemplan cinco condiciones de falla: (1) Redis no disponible, (2) caída de conexión, (3) agotamiento de cuota de comandos o ancho de banda en Upstash, (4) latencia elevada en Upstash (se aplicará timeout preventivo estricto de comando, p. ej. 250 ms), y (5) fallo de ejecución del script Lua.
    - **Aclaración crítica sobre límites y semántica de códigos HTTP:**
      * Un fallback local en memoria RAM **NO** mantiene un límite global; si el tráfico se reparte entre 2 réplicas, cada réplica contabiliza con contadores independientes, lo que duplica la cuota efectiva admisible o penaliza asimétricamente a usuarios bajo redes NAT.
      * El código **HTTP 429 (Too Many Requests)** se reserva exclusivamente para solicitudes que han sido evaluadas correctamente por el limitador y superan el cupo o límite de peticiones configurado dentro de la ventana de tiempo.
      * El agotamiento de la cuota mensual de comandos en Upstash (500,000 comandos) o una caída de red **NO** significa que el usuario haya excedido su límite de solicitudes; se trata de una indisponibilidad técnica de la dependencia externa.
      * El código **HTTP 503 (Service Unavailable)** se contempla para rechazar temporalmente solicitudes cuando Redis no esté disponible, se produzca timeout o falle la dependencia, *únicamente si el equipo ratifica la política Fail-Closed*.
    - **Alternativas de contingencia para endpoints críticos de autenticación:** Actualmente tienen política de límite de intentos solo confirm-email, resend, login y refresh (Program.cs:196-295); Registro, logout y mi-cuenta no la tienen, y el límite de Registro está planeado.
      * *Opción Fail-Closed:* Rechazar peticiones con código **HTTP 503 (Service Unavailable)** ante indisponibilidad técnica o timeout de Redis, preservando la protección estricta contra fuerza bruta; el código **HTTP 429 (Too Many Requests)** queda reservado exclusivamente para solicitudes que exceden el cupo o límite de peticiones configurado.
      * *Opción Fail-Open / Degradación local:* Permitir la continuidad del servicio degradando a contadores locales en memoria por réplica (asumiendo que el límite global no se garantiza y emitiendo HTTP 429 si el contador local de la instancia se satura).
      * *Estado de la decisión:* **[DECISIÓN PENDIENTE DE VALIDACIÓN Y CONSENSO DEL EQUIPO POR JESUI Y BRIDGET]**. No se asumirá una decisión preaprobada hasta contar con el consenso del equipo.
  - **Variables y seguridad de secretos:** Las variables planeadas `Redis__ConnectionString` y `Auth__RateLimit__IpHashSalt` son de carácter estrictamente confidencial. Se configurarán únicamente como secretos en Azure Container Apps y GitHub Secrets / Environments; queda prohibido almacenar credenciales reales o ejemplos en el repositorio, archivos de configuración o registros de ejecución.

## Presupuesto, costos y límites gratuitos

*Fecha de consulta y fuentes oficiales: 2026-11-09 y 2026-11-10.*

- **Upstash Redis plan gratuito:** El plan gratuito indica 250 MB de almacenamiento en la página oficial de precios de Upstash consultada el 2026-11-09; el equipo mencionó 256 MB; la cifra definitiva se confirma en el panel de Upstash al crear la base de datos [NO VERIFICADO]. El límite de comandos del plan gratuito es de 500,000 al mes. Incluye 10 GB de transferencia de datos mensual, 1 sola base de datos, sin listas de control de acceso (ACLs) ni cifrado en reposo. No requiere ingresar tarjeta bancaria para operar en el nivel gratuito.
- **Presupuesto mensual en Azure (rg-unihome-dev):** Se encuentra configurado un presupuesto formal en Azure Cost Management denominado `unihome-dev-budget` por un monto de **US$25.00 mensuales**, con alertas automáticas configuradas al 50% (US$12.50), 80% (US$20.00) y 100% (US$25.00) notificadas por correo al equipo.
- **Separación estricta de presupuestos y costos:**
  1. El presupuesto mensual de US$25 de Azure supervisa únicamente los recursos desplegados en el Resource Group de desarrollo (`rg-unihome-dev`) y **NO** cubre, limita ni supervisa los consumos de Upstash Redis en su nube independiente.
  2. El presupuesto de Azure no es un tope de gasto para Upstash ni garantiza la ausencia de cobros si se sobrepasan los límites del proveedor externo.
  3. Para Upstash, no se autoriza una actualización automática a un plan de pago; la regla preventiva de control de costos consiste en **no registrar tarjeta bancaria** en la cuenta.
  4. [NO VERIFICADO]: El comportamiento exacto de Upstash al alcanzar el tope mensual gratuito (rechazo de comandos con código de error, limitación de tasa/throttling o suspensión temporal de la base hasta el siguiente ciclo de facturación) debe verificarse en la documentación oficial vigente y mediante pruebas controladas; no se asume como hecho garantizado que la falta de tarjeta fuerce una detención sin confirmar previamente la política del proveedor.
- **Cálculo teórico de consumo en Azure Container Apps (entorno de desarrollo):**
  - Cuota mensual gratuita por suscripción en plan Consumption: 180,000 vCPU-segundos, 360,000 GiB-segundos y 2 millones de peticiones (fuente: https://learn.microsoft.com/azure/container-apps/billing).
  - **Escenario teórico de escalado a 2 réplicas continuas (24/7):**
    * Consumo vCPU: 2 réplicas × 0.25 vCPU × 30 días × 24 horas × 3,600 segundos = **1,296,000 vCPU-segundos mensuales**.
    * Consumo memoria RAM: 2 réplicas × 0.5 GiB × 30 días × 24 horas × 3,600 segundos = **2,592,000 GiB-segundos mensuales**.
  - **Aclaraciones y supuestos del cálculo:**
    * Este cálculo representa un **escenario teórico de actividad continua (24/7 sin escala a cero)**; no constituye una factura exacta ni una predicción garantizada.
    * El modelo de facturación de Container Apps distingue entre consumo activo (procesando peticiones) y consumo inactivo (idle), por lo que si las réplicas escalan o no reciben carga continua, el consumo real puede variar.
    * Ambos valores teóricos (1.29M vCPU-s y 2.59M GiB-s) superan las cuotas mensuales gratuitas (180k vCPU-s y 360k GiB-s), consumiendo saldo del crédito de la suscripción (que vence el 3 de diciembre de 2026).
    * [PENDIENTE DE VALIDACIÓN]: La tasa de facturación residual neta una vez agotada la cuota mensual gratuita frente al crédito disponible antes de fijar `minReplicas = 2` permanentemente en el entorno de desarrollo.
- **Estimación de comandos de Redis para rate limiting:**
  - Supuesto de tráfico de evaluación: entre 100 y 500 intentos de autenticación diarios (~3,000 a 15,000 comandos mensuales con 1 invocación Lua atómica por intento).
  - Proyección: ~15,000 comandos al mes representan aproximadamente el 3% del cupo mensual de 500,000 comandos de Upstash.
  - [NO VERIFICADO]: Cuántos comandos contabiliza internamente Upstash por ejecución de script Lua (`EVALSHA` / `EVAL`).
  - [NO VERIFICADO]: Comportamiento exacto de Upstash al superar el tope mensual gratuito (rechazo de comandos con código de error, limitación de tasa o suspensión temporal de la base hasta el siguiente ciclo de facturación).

## Estado real de las pruebas CI/CD

Para asegurar la transparencia en los pipelines de GitHub Actions, se diferencia formalmente entre pruebas con evidencia real de ejecución y pruebas planeadas:

- **Pruebas implementadas y verificadas (con ejecuciones exitosas en GitHub Actions):**
  1. *Build, formato y pruebas unitarias (.NET 10):* Ejecución obligatoria en push y PRs con paso limpio (verificado en Run #128 y Run #121).
  2. *Análisis estático de seguridad (SAST) y dependencias:* Semgrep, Gitleaks, Dependabot / dotnet list package, CodeQL (verificado en Run #95) y SonarCloud (.NET) (verificado en Run #128 y Run #119).
  3. *Database Test (MongoDB efímero):* Job `mongo-db-test` con contenedor `mongo:8.0.34` fijado por digest en el runner de CI, base aislada `unihome_ci_verificacion` y validación de pruebas TRX mediante `tools/ci/mongo_ci_summary.py` (verificado en verde en Run #121 y Run #119).
  4. *Contract Test (OpenAPI):* 10/10 pruebas en `OpenApiGenderContractTests.cs` verificadas (verificado en Run #128 y Run #121).
  5. *DAST pasivo (OWASP ZAP):* Job `dast-zap` ejecutando `zaproxy:2.17.0` con API Kestrel local, MongoDB y SMTP mock local (`tools/ci/zap_dast.py`, verificado en verde en Run #121 y ejecución manual `42772939746`).
  6. *RESTler Fuzzing:* Job `restler-fuzzing` con Microsoft RESTler v9.3.1 en modo fuzz-lean aislado sobre 7 operaciones de OpenAPI (verificado en verde en Run #121 con 2,054 peticiones y 0 errores 500).
- **Pruebas planeadas o pendientes (sin ejecución real o bloqueadas):**
  1. *Pact Contract Testing:* El proyecto proveedor `UniHome.PactTests` está compilado, pero la suite está bloqueada e inactiva (`vars.UNIHOME_ENABLE_PACT`) debido a que la carpeta `pacts/` no contiene contratos JSON reales por parte del equipo del cliente Android (`unihome-mobile`).
  2. *Pruebas de concurrencia y consistencia de rate limiting entre 2 instancias:* **Trabajo planeado**. Requieren levantar en CI dos instancias simultáneas de la API conectadas al mismo Redis efímero para validar que el límite global es compartido y atómico.
  3. *Pruebas de integración de Redis:* **Trabajo planeado**. No existe actualmente contenedor ni suite de pruebas de Redis en los pipelines activos.
