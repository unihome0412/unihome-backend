# UniHome Backend — Pipeline DevSecOps

Referencia de diseño: grafo de trabajos GitHub Actions compartido el 6 de noviembre de 2026. Adaptado a **ASP.NET Core 10 y MongoDB Atlas**. Esta entrega reemplaza el **contenido** de `.github/workflows/ci.yml`, conserva `.github/workflows/codeql.yml` y `.github/dependabot.yml`, y no crea otro workflow paralelo que duplicaría builds.

## Qué funciona al integrarlo

| Job | Qué verifica | Cuándo |
|---|---|---|
| Detect changes | Detecta cambios relevantes mediante Git | Todos los PR y push a main, manual |
| Semgrep SAST | Reglas `p/security-audit` de C#/seguridad | Código y CI modificados |
| Build & Test | Restore locked, build Release, formato, unit/integration local y test del gate | Código y CI modificados |
| Dependency scan | NuGet High/Critical, preserva configuración existente | Siempre |
| Secret scan | Gitleaks, preserva el análisis del historial existente | Siempre |
| Contract Test (OpenAPI) | Tests existentes de OpenAPI y Swagger | Código y CI; después de build |
| PR Quality Gate | Analiza realmente los resultados; **skipped no es passed** | Todos los runs, aunque fallen anteriores |
| Hardening Report | Tabla Markdown y artefacto con resultados reales | Todos los runs salvo cancelación |

El análisis **CodeQL existente** sigue ejecutándose de manera independiente en `.github/workflows/codeql.yml`; para proteger `main`, configurar **ambos** estados requeridos (CodeQL y el gate DevSecOps). Las pruebas de integración que necesitan MongoDB están protegidas con un opt-in en el proyecto y se omiten por defecto en las pruebas locales: el pipeline registra dicho `skipped` de forma honesta. El job manual de MongoDB CI no acepta una omisión como aprobación.

## Trabajos visibles pero desactivados por defecto

Estos trabajos aparecen como `skipped` en el grafo hasta configurar las variables indicadas. **No se consideran pruebas superadas**.

| Job | Variable de repositorio | Otro requisito | Evento permitido |
|---|---|---|---|
| SonarCloud SAST | `UNIHOME_ENABLE_SONAR=true` | Secret `SONAR_TOKEN` y vars `SONAR_PROJECT_KEY`, `SONAR_ORGANIZATION`; proyecto Sonar | PR interno a main, push main o manual desde main |
| Database Test (MongoDB efímero) | `UNIHOME_ENABLE_MONGO_CI=true` | MongoDB Community 8.0.34 fijado por digest como servicio temporal del runner; base **exclusiva `unihome_ci_verificacion`**, inicializada con M01-001 en el propio job; sin Atlas, secretos ni credenciales | Solo manual |
| Mutation Test (Stryker.NET) | `UNIHOME_ENABLE_MUTATION=true` | Stryker.NET 5.0.0 local desde `dotnet-tools.json`; configuración en `tests/UniHome.UnitTests/stryker-config.json`; reportes HTML/JSON | Solo manual |
| Docker Build + Dockle | `UNIHOME_ENABLE_DOCKER=true` | Aprobar uso de Docker en CI, cuotas de acciones/artefactos; **no** publica imagen | PR, push main, manual |
| DAST ZAP | `UNIHOME_ENABLE_ZAP=true` | Solo instancia efímera local `/health/live`, sin secretos; escaneo **pasivo limitado**, no prueba toda la API | Solo manual |
| Pact | `UNIHOME_ENABLE_PACT=true` | Primero desarrollar `tests/UniHome.PactTests` con contratos de Mobile; en su ausencia falla al activarse | Solo manual |
| RESTler | `UNIHOME_ENABLE_RESTLER=true` | Primero implementar `tools/ci/restler/run-isolated.sh` y un harness 100 % aislado; si falta, **falla** | Solo manual |

**Ninguno de estos trabajos usa `unihome_desarrollo`, ejecuta deploy ni escribe en Azure.** El trabajo MongoDB no se conecta a Atlas: usa un contenedor efímero del runner, sin credenciales y con la URI restringida a loopback. La migración `M01-001` se aplica únicamente a `unihome_ci_verificacion`; no se ejecuta `promote` ni se escribe en bases compartidas.

ZAP: el escaneo inicial alcanza únicamente `http://127.0.0.1:5088/health/live`. Esto demuestra conexión, no cobertura DAST del flujo autenticado. RESTler / Pact requieren contratos y datos de prueba que aún no están implementados. No activar como simples interruptores para aparentar cobertura.

## SonarQube Cloud

- **PR interno hacia `main`**: se ejecuta análisis real de Pull Request. `SonarScanner for .NET` detecta automáticamente los metadatos desde el evento de GitHub Actions; no se pasan `sonar.branch.name` ni parámetros de rama manuales. El checkout usa `fetch-depth: 0`.
- **Push a `main` y `workflow_dispatch` desde `main`**: se ejecuta análisis de la rama principal.
- **`workflow_dispatch` desde una rama feature**: Sonar no se ejecuta y el gate no lo exige, porque el plan Free no admite análisis de ramas no principales. Stryker puede seguir ejecutándose en su evento manual.
- **PR desde fork**: el job no se ejecuta porque GitHub no expone `SONAR_TOKEN` a esos PR; el gate falla cerrado si Sonar está habilitado y el job quedó `skipped`. No se usa `pull_request_target`.
- **Cobertura para el Quality Gate**: el job de Sonar ejecuta las pruebas unitarias .NET con `XPlat Code Coverage` (Cobertura) y las pruebas Python de `tools/ci` con `coverage.py`. Los reportes se pasan al scanner; no se desactiva la condición de cobertura nueva.
- Se mantiene `sonar.qualitygate.wait=true`: si el Quality Gate real falla, el job de Sonar falla y bloquea el PR.

## Mutation testing (Stryker.NET)

- Herramienta local fijada en `dotnet-tools.json` (`dotnet-stryker` 5.0.0); no se instala una versión global variable.
- Configuración versionada: `tests/UniHome.UnitTests/stryker-config.json`. Muta únicamente `src/UniHome.Application/Identity/Login/LoginService.cs`.
- Ejecución local reproducible:

```bash
dotnet tool restore
cd tests/UniHome.UnitTests
dotnet stryker --config-file stryker-config.json
```

- Reportes: `tests/UniHome.UnitTests/StrykerOutput/<timestamp>/reports/mutation-report.html` y `mutation-report.json`. No se versionan.
- El job manual `mutation-test` restaura la herramienta desde el manifiesto, ejecuta pruebas unitarias, corre Stryker y publica ambos reportes. Falla si no hay mutaciones evaluadas, si hay errores de ejecución o si faltan los reportes.
- Línea base verificada localmente: 57 mutantes en el archivo objetivo, 46 evaluados, 44 detectados, 2 sobrevivientes en `ConfigureAwait(false)` (sin efecto observable en el entorno de prueba) y mutation score de 95.65 %.

## MongoDB CI efímero

- Job manual `mongo-db-test`, condicionado por `UNIHOME_ENABLE_MONGO_CI=true`.
- Servicio de GitHub Actions con MongoDB Community **8.0.34**, fijado por digest
  `sha256:b9924c330a8bbc5800fe9c69f04a84e221eca7b28fd049a0354147d9908fa142`.
  No se usa `latest`, Atlas ni Docker local.
- La URI es `mongodb://127.0.0.1:27017`, directa, sin credenciales. El workflow
  rechaza hosts no loopback, SRV, credenciales y una base distinta de
  `unihome_ci_verificacion`. Las pruebas también aplican esta comprobación cuando
  `UNIHOME_MONGO_CI_LOOPBACK_ONLY=1`.
- No se configura replica set: las pruebas de identidad y bloqueo usan índices y
  operaciones atómicas sobre un documento; no requieren transacciones.
- Se espera la disponibilidad real del contenedor, se aplica únicamente `M01-001`
  con `UniHome.MongoMigrator` y se verifica el ledger, checksum e índices antes de
  las pruebas.
- `MongoIdentityPersistenceTests` y `MongoPromotionLockPersistenceTests` se
  ejecutan contra MongoDB real; el correo se sustituye por un doble en memoria.
  No se envían correos.
- Un script valida el TRX: exige las dos pruebas, rechaza `NotExecuted`, falla si
  hay cero resultados, si la inicialización no terminó o si el paso de pruebas no
  fue exitoso. El resumen incluye versión, base, conteos, duración, inicialización
  y resultado final.
- Evidencia: `mongodb-ci-results` con el TRX, la salida de migración/verificación
  y el resumen Markdown. El contenedor se destruye al finalizar el job y la base
  temporal se elimina explícitamente.
- Verificación real (9 de noviembre de 2026):
  [ejecución manual 42570584187](https://github.com/unihome0412/unihome-backend/actions/runs/42570584187),
  commit `9f4b4b5`, job `mongo-db-test` en verde, TRX con 2 pruebas ejecutadas y
  aprobadas, 0 fallidas, 0 omitidas, M01-001 válida y limpieza confirmada.

## Flujo

```text
pull_request -> main / push -> main / ejecución manual
                    |
              Detect Changes
             /      |        \
      Semgrep   Build & Test   Escaneos de dependencias / secretos
                    |
            Contract Test OpenAPI
                    |
            (Pact, opcional)

En paralelo, con habilitación explícita:
  SonarCloud / Mongo CI / Stryker / Docker Build -> Dockle
  OWASP ZAP local / RESTler aislado

                        |
                  PR Quality Gate
                        |
                 Hardening Report
```

Las pruebas activadas en un contexto admisible pasan a ser **obligatorias** para el gate. Un job opcional que se omite cuando se ha habilitado en ese evento **bloquea la integración**. El workflow usa `if: !cancelled()` en los dos últimos jobs para que no desaparezca el reporte cuando falla una prueba.

## Instalación y verificaciones

1. Guardar el paquete fuera del repositorio. Ejecutar `instalar-ci.ps1` desde PowerShell 7. Hace un preflight del árbol Git limpio y del workflow original y crea `feature/ci-devsecops`, con respaldo externo.
2. Revisar `git diff`, crear un commit de trabajo propio y subir **solo** la rama de CI para un PR hacia `main`.
3. Revisar el primer run. No declarar que ha pasado hasta ver **Build**, **Semgrep**, **OpenAPI**, **Dependency**, **Secret** y **PR Quality Gate** en verde.
4. En GitHub > Settings > Rules > Rulesets, configurar revisión requerida, checks requeridos `PR Quality Gate (controles obligatorios)` y `CodeQL - análisis C#`; validar los **nombres exactos** que GitHub muestre, porque pueden depender del workflow/repo.
5. No activar despliegue Azure todavía. Una fase CD posterior debe usar `workflow_dispatch`, ambiente protegido con aprobadores, autenticación OIDC y presupuestos/alertas revisados.

## Advertencias

- Esta entrega se validó sintácticamente (YAML), con pruebas de la lógica del quality gate y comprobación de archivos y referencias. La integración de Stryker.NET se validó localmente con .NET 10 y una ejecución real de mutaciones; el primer run de GitHub Actions sigue siendo la validación del pipeline completo.
- Semgrep usa reglas externas y una versión de CLI instalada al ejecutar: para un pipeline reproducible de producción habrá que fijar versiones/digests y comprobar la licencia/costos aplicables.
- Dockle usa un binario oficial descargado de GitHub; fijar y validar su checksum antes de exigirlo en producción.
- Pact y RESTler aún requieren su harness/configuración y deben quedar desactivados hasta ser verificados. Stryker.NET ya tiene configuración versionada y línea base local verificada. MongoDB CI fue verificado en la ejecución manual `42570584187`: job `mongo-db-test` en verde, 2/2 pruebas reales, 0 omitidas y artefacto `mongodb-ci-results`; la base temporal y el contenedor se destruyeron al finalizar.
- Si el CI que actualmente está en GitHub fue modificado **después** del ZIP del 6 de noviembre, no aplicar el paquete a ciegas: reconciliar las diferencias antes.
- Conservar `.local-sdd/` únicamente en local. `AGENTS.md`, `opencode.json`, `memoria.md` del SDD tampoco deben subir. La documentación técnica de CI en este paquete sí puede versionarse.
