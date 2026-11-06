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
