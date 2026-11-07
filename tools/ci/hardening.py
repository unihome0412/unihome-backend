#!/usr/bin/env python3
"""Evalúa y resume jobs de CI/CD a partir de needs (sin token ni llamadas API)."""
from __future__ import annotations
import argparse
import json
import os
import sys
from pathlib import Path

MUST_RUN_ALWAYS = ("detect-changes", "dependency-scan", "secret-scan")
MUST_RUN_FOR_CODE = ("semgrep-sast", "build-test", "contract-test")
OPTIONAL = {
    "sonarcloud-sast": ("UNIHOME_ENABLE_SONAR", {"push", "workflow_dispatch"}),
    "mongo-db-test": ("UNIHOME_ENABLE_MONGO_CI", {"workflow_dispatch"}),
    "mutation-test": ("UNIHOME_ENABLE_MUTATION", {"workflow_dispatch"}),
    "pact-contract": ("UNIHOME_ENABLE_PACT", {"workflow_dispatch"}),
    "docker-build": ("UNIHOME_ENABLE_DOCKER", {"pull_request", "push", "workflow_dispatch"}),
    "container-lint": ("UNIHOME_ENABLE_DOCKER", {"pull_request", "push", "workflow_dispatch"}),
    "dast-zap": ("UNIHOME_ENABLE_ZAP", {"workflow_dispatch"}),
    "restler-fuzzing": ("UNIHOME_ENABLE_RESTLER", {"workflow_dispatch"}),
}


def evaluate(needs: dict, changed: bool, flags: dict, event: str) -> tuple[list, list]:
    required = set(MUST_RUN_ALWAYS)
    if changed:
        required.update(MUST_RUN_FOR_CODE)
        for job, (flag, events) in OPTIONAL.items():
            if str(flags.get(flag, "")).lower() == "true" and event in events:
                required.add(job)
    problems = []
    for job in sorted(required):
        status = needs.get(job, {}).get("result", "missing")
        if status != "success":
            problems.append(f"{job}: esperado success; encontrado {status}")
    return sorted(required), problems


def make_report(needs: dict, required: list, issues: list, changed: bool) -> str:
    rows = [
        "# UniHome — Hardening Report",
        "",
        f"- Cambios de código o CI: **{'sí' if changed else 'no (solo documentación)'}**",
        f"- Quality Gate: **{'FAIL' if issues else 'PASS'}**",
        "- `skipped` NO equivale a una prueba aprobada.",
        "",
        "| Job | Resultado | Obligatorio en esta ejecución |",
        "|---|---|---|",
    ]
    for job, val in sorted(needs.items()):
        if job == "pr-quality-gate":
            continue
        result = val.get("result", "missing")
        rows.append(f"| `{job}` | **{result}** | {'sí' if job in required else 'no'} |")
    if issues:
        rows += ["", "## Bloqueos", ""] + [f"- {x}" for x in issues]
    rows += ["", "**CD / despliegue en Azure: no incluido ni autorizado.**", ""]
    return "\n".join(rows)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--gate", action="store_true", help="Salir con error si falló un gate obligatorio")
    parser.add_argument("--output", help="Guardar reporte Markdown")
    args = parser.parse_args()
    needs = json.loads(os.environ.get("NEEDS_JSON", "{}"))
    flags = {k: os.environ.get(k, "") for k in {v[0] for v in OPTIONAL.values()}}
    changed = os.environ.get("CODE_CHANGED", "true").lower() == "true"
    required, problems = evaluate(needs, changed, flags, os.environ.get("EVENT_NAME", ""))
    report = make_report(needs, required, problems, changed)
    if args.output:
        path = Path(args.output)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(report, encoding="utf-8")
    summary_file = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_file:
        with open(summary_file, "a", encoding="utf-8") as file:
            file.write(report)
    else:
        print(report)
    if args.gate and problems:
        for p in problems:
            print("::error::" + p, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
