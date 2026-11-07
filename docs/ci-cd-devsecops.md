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

El análisis **CodeQL existente** sigue ejecutándose de manera independiente en `.github/workflows/codeql.yml`; para proteger `main`, configurar **ambos** estados requeridos (CodeQL y el gate DevSecOps). Las pruebas de integración que necesitan MongoDB Atlas están protegidas con un opt-in en el proyecto y se omiten por defecto: el pipeline registra dicho `skipped` de forma honesta.

## Trabajos visibles pero desactivados por defecto

Estos trabajos aparecen como `skipped` en el grafo hasta configurar las variables indicadas. **No se consideran pruebas superadas**.

| Job | Variable de repositorio | Otro requisito | Evento permitido |
|---|---|---|---|
| SonarCloud SAST | `UNIHOME_ENABLE_SONAR=true` | Secret `SONAR_TOKEN` y vars `SONAR_PROJECT_KEY`, `SONAR_ORGANIZATION`; proyecto Sonar | push main o manual |
| Database Test (MongoDB) | `UNIHOME_ENABLE_MONGO_CI=true` | Secret `UNIHOME_CI_MONGO_URI`; base **exclusiva `unihome_ci_verificacion`**, preparada previamente con M01-001; usuario con permisos limitados; acceso por IP del runner | Solo manual |
| Mutation Test (Stryker.NET) | `UNIHOME_ENABLE_MUTATION=true` | Validar previamente soporte `.slnx` y definir umbral de mutación | Solo manual |
| Docker Build + Dockle | `UNIHOME_ENABLE_DOCKER=true` | Aprobar uso de Docker en CI, cuotas de acciones/artefactos; **no** publica imagen | PR, push main, manual |
| DAST ZAP | `UNIHOME_ENABLE_ZAP=true` | Solo instancia efímera local `/health/live`, sin secretos; escaneo **pasivo limitado**, no prueba toda la API | Solo manual |
| Pact | `UNIHOME_ENABLE_PACT=true` | Primero desarrollar `tests/UniHome.PactTests` con contratos de Mobile; en su ausencia falla al activarse | Solo manual |
| RESTler | `UNIHOME_ENABLE_RESTLER=true` | Primero implementar `tools/ci/restler/run-isolated.sh` y un harness 100 % aislado; si falta, **falla** | Solo manual |

**Ninguno de estos trabajos usa `unihome_desarrollo`, crea migraciones, ejecuta deploy o escribe en Azure.** El trabajo MongoDB solo debe habilitarse después de autorización explícita de la base de CI. Los runners de GitHub tienen IPs variables; para Atlas se recomienda un runner de red controlada si no existe acceso seguro. **No abrir Atlas a `0.0.0.0/0`** para simplificar pruebas.

ZAP: el escaneo inicial alcanza únicamente `http://127.0.0.1:5088/health/live`. Esto demuestra conexión, no cobertura DAST del flujo autenticado. RESTler / Pact requieren contratos y datos de prueba que aún no están implementados. No activar como simples interruptores para aparentar cobertura.

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

- Esta entrega se validó sintácticamente (YAML), con pruebas de la lógica del quality gate y comprobación de archivos y referencias, **no se ejecutó en GitHub Actions ni compiló .NET 10 en este entorno**. El primer PR será la validación real.
- Semgrep usa reglas externas y una versión de CLI instalada al ejecutar: para un pipeline reproducible de producción habrá que fijar versiones/digests y comprobar la licencia/costos aplicables.
- Dockle usa un binario oficial descargado de GitHub; fijar y validar su checksum antes de exigirlo en producción.
- Algunos jobs opcionales (Pact, RESTler, pruebas Mongo, Stryker) aún requieren su harness/configuración y deben quedar desactivados hasta ser verificados.
- Si el CI que actualmente está en GitHub fue modificado **después** del ZIP del 6 de noviembre, no aplicar el paquete a ciegas: reconciliar las diferencias antes.
- Conservar `.local-sdd/` únicamente en local. `AGENTS.md`, `opencode.json`, `memoria.md` del SDD tampoco deben subir. La documentación técnica de CI en este paquete sí puede versionarse.
