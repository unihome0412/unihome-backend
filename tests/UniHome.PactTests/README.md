# UniHome.PactTests — verificación del proveedor

Proyecto de verificación Pact para el proveedor `UniHome API`. No forma parte de
`UniHome.Backend.slnx` deliberadamente: las pruebas normales del backend no deben
depender de una entrega de contratos del consumidor Android todavía inexistente.

## Qué verifica

- Verifica archivos Pact del consumidor con `PactVerifier` de PactNet 5.0.1
  contra una API UniHome en ejecución sobre Kestrel/TCP.
- No usa `WebApplicationFactory`/TestServer: el verificador nativo de Pact no
  puede alcanzar un servidor en memoria.
- Falla si `PACT_CONTRACTS_DIR` no existe o no contiene archivos `.json`.

## Entradas

| Variable | Uso |
|---|---|
| `PACT_CONTRACTS_DIR` | Directorio con contratos Pact reales; por defecto `pacts`. |
| `PACT_PROVIDER_BASE_URL` | URL de la API aislada en ejecución; por defecto `http://127.0.0.1:5088`. |
| `PACT_PROVIDER_STATE_URL` | URL opcional del endpoint de estados del proveedor. |

El job `pact-contract` exige que la API esté disponible; puede ser la misma
instancia efímera de loopback usada por otros harnesses, o una instancia aislada
proporcionada por el Pipeline. No se debe apuntar a Azure ni a producción.

Los contratos deben ser generados por las pruebas del cliente HTTP real del
repositorio `unihome-mobile`, no desde OpenAPI. Ver `pacts/README.md`.

## Ejecución futura

```powershell
$env:PACT_CONTRACTS_DIR = '<ruta a contratos reales>'
dotnet test tests/UniHome.PactTests/UniHome.PactTests.csproj -c Release
```

El job `pact-contract` solo se ejecuta con `UNIHOME_ENABLE_PACT=true` y exige
contratos reales antes de ejecutar este proyecto. Si no hay contratos, falla y
no se interpreta como aprobación.
