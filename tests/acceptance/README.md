# Aceptación HTTPS — Sprints 1–5

Esta carpeta contiene la colección Postman/Newman para ejecutar los flujos
críticos de la matriz de
[`docs/aceptacion-sprints-1-5.md`](../../docs/aceptacion-sprints-1-5.md) en un
entorno autorizado. **No contiene credenciales, tokens, URLs productivas ni
datos personales.** Las 41 operaciones se cubren además con la suite local; esta
colección prioriza los flujos que necesitan una sesión y datos reales. Para
operaciones destructivas o de cambio de estado (borrado de cuenta, cambio de
estado de universidad) usa datos dedicados y ejecútalas por separado.

## Requisitos

- `baseUrl` HTTPS con TLS válido.
- Una cuenta sintética confirmada (`userEmail` / `userPassword`).
- Una cuenta administrativa autorizada (`adminEmail` / `adminPassword`).
- Código de confirmación y token de recuperación capturados del buzón de
  pruebas; nunca de un buzón productivo.
- Archivos sintéticos locales para `photoPath`, `inePath` y `proofPath`.
- Dos expedientes sintéticos en revisión para `approveExpedienteId` y
  `rejectExpedienteId`; no reutilizar el mismo para aprobar y rechazar.

## Ejecución

1. Copia la colección a un environment local (fuera de Git) y asigna las
   variables. No guardes el environment con secretos.
2. Ejecuta primero `00 Salud, cabeceras y Swagger protegido`. Si HSTS falla,
   revisa que `baseUrl` sea HTTPS y que el ingress envíe `X-Forwarded-Proto`.
3. Ejecuta `00` a `06` en orden. El login guarda `accessToken` y `refreshToken`;
   el alta de universidad/propiedad/unidad/expediente guarda sus IDs. La
   colección aprueba primero el expediente de aceptación para que la exploración
   pública y la ficha tengan una unidad `Aprobada` real.
4. Los fixtures de aprobación/rechazo separados (`approveExpedienteId`,
   `rejectExpedienteId`) también deben ser expedientes distintos. Si ejecutas la
   colección dos veces, vuelve a sembrar datos sintéticos y actualiza los IDs.
5. `06 Logout` invalida la sesión actual; no vuelvas a usar ese token después.

```powershell
newman run tests/acceptance/unihome-sprints-1-5.postman_collection.json `
  --environment <ruta-environment-local.json> `
  --reporters cli,json --reporter-json-export TestResults/acceptance.json
```

## Criterio

- Un `4xx` documentado en la matriz es un resultado válido; no se fuerza `2xx`.
- Cualquier `5xx`, una ruta no visitada o una cookie/JWT expuesto en reportes
  invalida la ejecución.
- El documento firmado se consume con sesión administrativa; el enlace no se
  comparte ni se publica.
- La aceptación remota solo se declara aprobada después de ejecutarla con una
  sesión autorizada y conservar la evidencia sin secretos.
