#!/usr/bin/env python3
"""Valida y resume las evidencias TRX de la base MongoDB efímera de CI.

No realiza llamadas de red ni imprime cadenas de conexión. El objetivo es que
una ejecución no se considere aprobada si las pruebas MongoDB se omitieron, no
se ejecutaron o fallaron, aunque dotnet test hubiera devuelto cero.
"""
from __future__ import annotations

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

TRX_NAMESPACE = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
PASSED = "Passed"
SKIPPED = {"NotExecuted", "NotRunnable"}
EXPECTED_TESTS = (
    "MongoIdentityPersistenceTests.RegistroLoginRefreshConcurrenteYLogout_DebeFuncionarContraMongoReal",
    "MongoPromotionLockPersistenceTests.BloqueoDistribuido_ExclusionMutua_RenovacionYFencingObsoleto_DebeFuncionarContraMongoReal",
)


@dataclass(frozen=True)
class TestCase:
    name: str
    outcome: str
    duration_seconds: float


@dataclass(frozen=True)
class Evidence:
    tests: tuple[TestCase, ...]
    duration_seconds: float
    passed: int
    failed: int
    skipped: int

    @property
    def executed(self) -> int:
        return self.passed + self.failed


def _parse_duration(value: str | None) -> float:
    if not value:
        return 0.0
    try:
        hours, minutes, seconds = value.split(":")
        return int(hours) * 3600 + int(minutes) * 60 + float(seconds)
    except (TypeError, ValueError):
        return 0.0


def _parse_instant(value: str | None) -> datetime | None:
    if not value:
        return None
    normalized = value.strip().replace("Z", "+00:00")
    # .NET/TRX puede emitir siete decimales; fromisoformat espera como máximo seis.
    normalized = re.sub(r"(\.\d{6})\d+", r"\1", normalized)
    try:
        return datetime.fromisoformat(normalized)
    except ValueError:
        return None


def inspect_trx(path: Path) -> Evidence:
    """Lee un TRX y devuelve solo metadatos seguros de las pruebas."""
    root = ET.parse(path).getroot()
    tests: list[TestCase] = []

    for node in root.findall(f".//{TRX_NAMESPACE}UnitTestResult"):
        tests.append(
            TestCase(
                name=node.get("testName", ""),
                outcome=node.get("outcome", ""),
                duration_seconds=_parse_duration(node.get("duration")),
            )
        )

    duration_seconds = 0.0
    times = root.find(f"{TRX_NAMESPACE}Times")
    if times is not None:
        start = _parse_instant(times.get("start"))
        finish = _parse_instant(times.get("finish"))
        if start is not None and finish is not None:
            duration_seconds = max(0.0, (finish - start).total_seconds())
    if duration_seconds == 0.0:
        duration_seconds = sum(test.duration_seconds for test in tests)

    passed = sum(test.outcome == PASSED for test in tests)
    skipped = sum(test.outcome in SKIPPED for test in tests)
    failed = len(tests) - passed - skipped
    return Evidence(tuple(tests), duration_seconds, passed, failed, skipped)


def validate(
    evidence: Evidence,
    initialization: str,
    tests_outcome: str,
) -> list[str]:
    issues: list[str] = []

    if not evidence.tests:
        issues.append("El TRX no contiene resultados de pruebas MongoDB.")

    names = [test.name for test in evidence.tests]
    for expected in EXPECTED_TESTS:
        if not any(name.endswith(expected) for name in names):
            issues.append(f"Falta la prueba MongoDB esperada: {expected}")

    for test in evidence.tests:
        if test.outcome == PASSED:
            continue
        if test.outcome in SKIPPED:
            issues.append(f"Prueba MongoDB omitida: {test.name}")
        else:
            issues.append(f"Prueba MongoDB no aprobada ({test.outcome}): {test.name}")

    if initialization != "success":
        issues.append(
            "La inicialización de la base efímera no terminó correctamente "
            f"(estado: {initialization or 'desconocido'})."
        )
    if tests_outcome != "success":
        issues.append(
            "El paso de pruebas MongoDB no terminó correctamente "
            f"(estado: {tests_outcome or 'desconocido'})."
        )

    return issues


def render_report(
    evidence: Evidence,
    issues: list[str],
    mongo_version: str,
    database: str,
    initialization: str,
) -> str:
    status = "PASS" if not issues else "FAIL"
    lines = [
        "# MongoDB CI — Evidencia de pruebas reales",
        "",
        "| Dato | Valor |",
        "|---|---|",
        f"| Versión de MongoDB | {mongo_version} |",
        f"| Base temporal | `{database}` |",
        f"| Inicialización M01-001 | {initialization or 'desconocida'} |",
        f"| Pruebas ejecutadas | {evidence.executed} |",
        f"| Pruebas aprobadas | {evidence.passed} |",
        f"| Pruebas fallidas | {evidence.failed} |",
        f"| Pruebas omitidas | {evidence.skipped} |",
        f"| Tiempo de ejecución | {evidence.duration_seconds:.2f} s |",
        f"| Resultado final | **{status}** |",
    ]
    if issues:
        lines += ["", "## Bloqueos", ""] + [f"- {issue}" for issue in issues]
    lines += ["", "`skipped` no se considera una prueba aprobada.", ""]
    return "\n".join(lines)


def _write_summary(report: str, summary_path: Path | None) -> None:
    if summary_path is not None:
        summary_path.parent.mkdir(parents=True, exist_ok=True)
        summary_path.write_text(report + "\n", encoding="utf-8")

    github_summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if github_summary:
        with open(github_summary, "a", encoding="utf-8") as file:
            file.write(report + "\n")

    print(report)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--trx", required=True, help="Ruta al TRX de las pruebas MongoDB")
    parser.add_argument("--mongo-version", required=True)
    parser.add_argument("--database", required=True)
    parser.add_argument("--initialization", default="unknown")
    parser.add_argument("--tests-outcome", default="unknown")
    parser.add_argument("--summary", help="Ruta opcional para el resumen Markdown")
    args = parser.parse_args(argv)

    summary_path = Path(args.summary) if args.summary else None
    trx_path = Path(args.trx)
    issues: list[str] = []

    try:
        evidence = inspect_trx(trx_path)
    except (OSError, ET.ParseError):
        evidence = Evidence((), 0.0, 0, 0, 0)
        issues.append(f"No se encontró o no se pudo leer el TRX: {trx_path}")

    issues.extend(validate(evidence, args.initialization, args.tests_outcome))
    report = render_report(
        evidence,
        issues,
        args.mongo_version,
        args.database,
        args.initialization,
    )
    _write_summary(report, summary_path)

    if issues:
        for issue in issues:
            print(f"::error::{issue}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
