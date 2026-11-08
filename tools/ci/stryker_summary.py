#!/usr/bin/env python3
"""Resume el reporte JSON de Stryker.NET y falla si no hay mutaciones evaluadas."""
from __future__ import annotations

import json
import sys
from pathlib import Path

REPORT_ROOT = Path("tests/UniHome.UnitTests/StrykerOutput")
TESTED_STATUSES = ("Killed", "Survived", "Timeout", "NoCoverage", "RuntimeError")


def find_latest_report(root: Path) -> Path | None:
    reports = sorted(
        root.rglob("mutation-report.json"),
        key=lambda path: path.stat().st_mtime,
        reverse=True,
    )
    return reports[0] if reports else None


def summarize(report: dict) -> dict:
    project_root = report.get("projectRoot")
    mutants: list[tuple[str, dict]] = []

    for file_path, file_data in report.get("files", {}).items():
        for mutant in file_data.get("mutants", []):
            mutants.append((file_path, mutant))

    counts: dict[str, int] = {}
    for _, mutant in mutants:
        status = mutant.get("status", "Unknown")
        counts[status] = counts.get(status, 0) + 1

    tested = sum(counts.get(status, 0) for status in TESTED_STATUSES)
    detected = counts.get("Killed", 0) + counts.get("Timeout", 0)
    undetected = counts.get("Survived", 0) + counts.get("NoCoverage", 0)
    score = (detected / (detected + undetected) * 100) if detected + undetected else 0.0

    survivors = []
    for file_path, mutant in mutants:
        if mutant.get("status") != "Survived":
            continue

        location = mutant.get("location", {}).get("start", {})
        survivors.append(
            {
                "file": _relative_file(file_path, project_root),
                "line": location.get("line", "?"),
                "mutator": mutant.get("mutatorName", "?"),
                "replacement": str(mutant.get("replacement", "")).replace("\n", " "),
            }
        )

    return {
        "mutants": len(mutants),
        "tested": tested,
        "detected": detected,
        "survived": counts.get("Survived", 0),
        "no_coverage": counts.get("NoCoverage", 0),
        "timeout": counts.get("Timeout", 0),
        "ignored": counts.get("Ignored", 0),
        "compile_error": counts.get("CompileError", 0),
        "runtime_error": counts.get("RuntimeError", 0),
        "score": score,
        "counts": counts,
        "survivors": survivors,
    }


def render(summary: dict) -> str:
    lines = [
        "## Stryker.NET — resumen de mutaciones",
        "",
        "| Métrica | Valor |",
        "|---|---:|",
        f"| Mutantes en el reporte | {summary['mutants']} |",
        f"| Evaluados | {summary['tested']} |",
        f"| Detectados (killed + timeout) | {summary['detected']} |",
        f"| Sobrevivientes | {summary['survived']} |",
        f"| Sin cobertura | {summary['no_coverage']} |",
        f"| Ignorados | {summary['ignored']} |",
        f"| Errores de compilación | {summary['compile_error']} |",
        f"| Errores de ejecución | {summary['runtime_error']} |",
        f"| Mutation score | {summary['score']:.2f}% |",
    ]

    if summary["survivors"]:
        lines += [
            "",
            "### Mutantes sobrevivientes",
            "",
            "| Archivo | Línea | Mutador | Reemplazo |",
            "|---|---:|---|---|",
        ]
        for survivor in summary["survivors"][:20]:
            lines.append(
                f"| `{_escape(survivor['file'])}` | {survivor['line']} | "
                f"{_escape(survivor['mutator'])} | `{_escape(survivor['replacement'])}` |"
            )
        if len(summary["survivors"]) > 20:
            lines.append(f"| ... | | | {len(summary['survivors']) - 20} más |")

    lines += ["", "_El reporte completo HTML/JSON se conserva como artefacto del job._", ""]
    return "\n".join(lines)


def _relative_file(file_path: str, project_root: str | None) -> str:
    if project_root:
        try:
            return Path(file_path).relative_to(project_root).as_posix()
        except ValueError:
            pass
    return Path(file_path).name


def _escape(value: str) -> str:
    return value.replace("|", "\\|")


def main(root: Path = REPORT_ROOT) -> int:
    report_path = find_latest_report(root)
    if report_path is None or not report_path.is_file():
        print(
            f"::error::No se encontró mutation-report.json en {root}",
            file=sys.stderr,
        )
        return 1

    report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    summary = summarize(report)
    text = render(summary)
    print(text)

    if summary["mutants"] == 0:
        print(
            "::error::El reporte no contiene mutaciones; no se puede aprobar la validación.",
            file=sys.stderr,
        )
        return 1
    if summary["tested"] == 0:
        print(
            "::error::No hay mutaciones evaluadas; no se puede aprobar la validación.",
            file=sys.stderr,
        )
        return 1
    if summary["runtime_error"] > 0:
        print(
            f"::error::Stryker registró {summary['runtime_error']} errores de ejecución.",
            file=sys.stderr,
        )
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
