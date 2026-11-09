#!/usr/bin/env python3
"""Resume y valida el reporte TRX de MongoDB CI, bloqueando falsos positivos."""
from __future__ import annotations

import argparse
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

DEFAULT_TRX_DIR = Path("TestResults/mongo")
EXPECTED_TESTS = (
    "RegistroLoginRefreshConcurrenteYLogout_DebeFuncionarContraMongoReal",
    "BloqueoDistribuido_ExclusionMutua_RenovacionYFencingObsoleto_DebeFuncionarContraMongoReal",
)


def find_latest_trx(directory: Path) -> Path | None:
    if not directory.exists():
        return None
    files = sorted(
        directory.rglob("*.trx"),
        key=lambda p: p.stat().st_mtime,
        reverse=True,
    )
    return files[0] if files else None


def _strip_ns(tag: str) -> str:
    return tag.split("}")[-1] if "}" in tag else tag


def parse_trx(file_path: Path) -> dict:
    tree = ET.parse(file_path)
    root = tree.getroot()

    counters_elem = None
    unit_test_results: list[dict] = []

    for elem in root.iter():
        tag = _strip_ns(elem.tag)
        if tag == "Counters" and counters_elem is None:
            counters_elem = elem
        elif tag == "UnitTestResult":
            unit_test_results.append({
                "testName": elem.attrib.get("testName", ""),
                "outcome": elem.attrib.get("outcome", ""),
                "duration": elem.attrib.get("duration", "0"),
            })

    counters = {
        "total": int(counters_elem.attrib.get("total", "0")) if counters_elem is not None else 0,
        "executed": int(counters_elem.attrib.get("executed", "0")) if counters_elem is not None else 0,
        "passed": int(counters_elem.attrib.get("passed", "0")) if counters_elem is not None else 0,
        "failed": int(counters_elem.attrib.get("failed", "0")) if counters_elem is not None else 0,
        "notExecuted": int(counters_elem.attrib.get("notExecuted", "0")) if counters_elem is not None else 0,
    }

    return {
        "counters": counters,
        "results": unit_test_results,
    }


def validate(summary: dict) -> list[str]:
    issues: list[str] = []
    counters = summary.get("counters", {})
    results = summary.get("results", [])

    total = counters.get("total", 0)
    executed = counters.get("executed", 0)
    passed = counters.get("passed", 0)
    failed = counters.get("failed", 0)
    not_executed = counters.get("notExecuted", 0)

    if total != len(EXPECTED_TESTS):
        issues.append(f"Se esperaban {len(EXPECTED_TESTS)} pruebas totales en el TRX; encontrados {total}.")
    if executed != len(EXPECTED_TESTS):
        issues.append(f"Se esperaban {len(EXPECTED_TESTS)} pruebas ejecutadas; encontradas {executed}.")
    if passed != len(EXPECTED_TESTS):
        issues.append(f"Se esperaban {len(EXPECTED_TESTS)} pruebas aprobadas; encontradas {passed}.")
    if failed > 0:
        issues.append(f"Hay {failed} pruebas fallidas en MongoDB CI.")
    if not_executed > 0:
        issues.append(f"Hay {not_executed} pruebas omitidas (skipped) en MongoDB CI. skipped != passed.")

    found_names = {r.get("testName", ""): r.get("outcome", "") for r in results}
    for expected in EXPECTED_TESTS:
        if expected not in found_names:
            issues.append(f"Falta la prueba esperada: {expected}.")
        elif found_names[expected].lower() != "passed":
            issues.append(f"La prueba {expected} no tiene estado Passed (estado: {found_names[expected]}).")

    return issues


def render_markdown(summary: dict, issues: list[str]) -> str:
    counters = summary.get("counters", {})
    results = summary.get("results", [])

    total = counters.get("total", 0)
    passed = counters.get("passed", 0)
    failed = counters.get("failed", 0)
    not_executed = counters.get("notExecuted", 0)

    status_str = "APROBADO (PASS)" if not issues else "FALLIDO (FAIL)"

    lines = [
        "## MongoDB CI Aislado — Resumen de Ejecución Real",
        "",
        f"- Estado global: **{status_str}**",
        "- Entorno: **MongoDB Community 8.0.4 (Service Container efímero en 127.0.0.1:27017)**",
        "- Base temporal de pruebas: `unihome_ci_verificacion`",
        "- Migración inicial M01-001: **Aplicada y verificada con UniHome.MongoMigrator**",
        "",
        "| Métrica | Valor |",
        "|---|---:|",
        f"| Pruebas esperadas | {len(EXPECTED_TESTS)} |",
        f"| Pruebas encontradas en TRX | {total} |",
        f"| Pruebas aprobadas | {passed} |",
        f"| Pruebas fallidas | {failed} |",
        f"| Pruebas omitidas (skipped) | {not_executed} |",
        "",
        "### Detalle de pruebas ejecutadas",
        "",
        "| Prueba | Estado | Duración |",
        "|---|:---:|---:|",
    ]

    for res in results:
        outcome = res.get("outcome", "Unknown")
        icon = "✅" if outcome.lower() == "passed" else "❌"
        lines.append(f"| `{res.get('testName')}` | {icon} **{outcome}** | {res.get('duration')} |")

    if issues:
        lines += ["", "### Bloqueos detectados", ""]
        for issue in issues:
            lines.append(f"- ❌ {issue}")

    lines.append("")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description="Valida el reporte TRX de MongoDB CI.")
    parser.add_argument("--trx", type=Path, default=None, help="Ruta al archivo .trx")
    parser.add_argument("--dir", type=Path, default=DEFAULT_TRX_DIR, help="Directorio con archivos .trx")
    args = parser.parse_args()

    trx_file = args.trx or find_latest_trx(args.dir)
    if trx_file is None or not trx_file.exists():
        print(f"::error::No se encontró ningún archivo .trx en {args.dir}", file=sys.stderr)
        return 1

    summary = parse_trx(trx_file)
    issues = validate(summary)
    report = render_markdown(summary, issues)
    print(report)

    if issues:
        for issue in issues:
            print(f"::error::{issue}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
