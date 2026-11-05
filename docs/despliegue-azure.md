# Despliegue en Azure Container Apps

**Estado:** preparación de la imagen y de la configuración. **No se creó ningún
recurso en Azure, no se inició sesión en Azure CLI y no se desplegó nada.** Este
documento solo describe las variables de entorno requeridas y referencia el
`Dockerfile` ya existente en la raíz del repositorio.

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

## Pendiente antes de desplegar (fuera de alcance de este documento)

- Crear el entorno de Container Apps, el Container App y el registro de
  contenedor (ACR u otro) — requiere autorización explícita y sesión de Azure,
  ninguna de las dos existe todavía.
- Configurar los *secrets* de Container Apps con los valores reales de la
  tabla anterior; nunca escribir esos valores en este repositorio.
- Decidir y documentar la estrategia de CI/CD de imagen (build + push) una vez
  autorizado el despliegue; hoy el `Dockerfile` solo se construye y prueba
  localmente.
- Revisar `/health/live` y `/health/ready` como *probes* de Container Apps
  (liveness/readiness) cuando se configure el recurso.
