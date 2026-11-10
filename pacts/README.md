# Contratos Pact del consumidor

Esta carpeta es el punto de entrega previsto para contratos Pact **reales**
generados por las pruebas del cliente HTTP de la aplicación Android. No contiene
contratos en este momento y no se deben generar contratos desde OpenAPI ni
inventar interacciones para llenarla.

## Formato y origen

- Archivos JSON de Pact generados por pruebas de consumidor (Pact specification
  2, 3 o 4), producidos por el cliente Android real.
- El nombre del proveedor debe ser `UniHome API`.
- No se aceptan contratos escritos manualmente a partir de OpenAPI: eso no
  demuestra el comportamiento que el consumidor Android espera.
- Los contratos deben incluir sus `consumer`, `provider`, `interactions`,
  `request`, `response`, matchers y, si aplica, `providerStates`.

## Entrega

- Entregar los JSON como artefacto/PR del repositorio `unihome-mobile` o
  publicarlos en el Pact Broker acordado por el equipo.
- Copiarlos en esta carpeta o indicar otra ruta absoluta mediante
  `PACT_CONTRACTS_DIR` en el job `pact-contract`.
- El job permanece desactivado mediante `UNIHOME_ENABLE_PACT=true`; no se
  convierte en `success` por ausencia de contratos.
- La verificación requiere una API UniHome aislada en
  `PACT_PROVIDER_BASE_URL` (por defecto `http://127.0.0.1:5088`). No apuntar a
  Azure ni a producción.
- Si los contratos usan `providerStates`, el equipo debe entregar también el
  endpoint de estados acordado y configurarlo en `PACT_PROVIDER_STATE_URL`.
  Sin ese soporte, la verificación debe fallar de forma explícita.

## Estado actual

No hay contratos reales disponibles. La verificación Pact no se considera
aprobada y el job no debe habilitarse hasta recibirlos.
