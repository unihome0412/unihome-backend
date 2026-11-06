# UniHome.SmtpProbe

Utilidad manual de desarrollo para comprobar la configuración SMTP y enviar un
mensaje de prueba con asunto `UniHome - Prueba de correo`.

No forma parte de la API, no publica rutas HTTP, no se ejecuta durante
`dotnet test`, no escribe credenciales ni mensajes en disco y no imprime valores
de configuración.

## Configuración

La utilidad reutiliza el mismo `UserSecretsId` que `UniHome.Api`
(`unihome-backend-api`). Configura los valores con placeholders propios:

```powershell
dotnet user-secrets set "Smtp:Host" "<host-smtp>" --project src/UniHome.Api
dotnet user-secrets set "Smtp:Port" "587" --project src/UniHome.Api
dotnet user-secrets set "Smtp:UseStartTls" "true" --project src/UniHome.Api
dotnet user-secrets set "Smtp:Username" "<usuario-smtp>" --project src/UniHome.Api
dotnet user-secrets set "Smtp:Password" "<clave-smtp>" --project src/UniHome.Api
dotnet user-secrets set "Smtp:FromEmail" "<correo-remitente>" --project src/UniHome.Api
dotnet user-secrets set "Smtp:FromName" "UniHome" --project src/UniHome.Api
```

`Smtp:TimeoutSeconds` es opcional (1 a 120; predeterminado 15).

No agregues estos valores a `appsettings.json` ni a archivos versionados.

## Ejecución

```powershell
dotnet run --project tools/UniHome.SmtpProbe/UniHome.SmtpProbe.csproj --configuration Release
```

La utilidad solicita un correo de destino controlado por el desarrollador y una
confirmación antes de enviar. Muestra únicamente:

- Si el servidor SMTP aceptó o rechazó el mensaje.
- Un motivo seguro (`Authentication`, `Timeout`, `Transient`, `Tls`, etc.) si
  no fue posible completar el envío.

## Entrega

Que el servidor SMTP acepte el mensaje no confirma que el buzón final lo haya
recibido. Verifica la entrega real en el panel de Brevo.
