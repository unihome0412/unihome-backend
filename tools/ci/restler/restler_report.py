#!/usr/bin/env python3
"""Analiza resultados reales de RESTler, genera cobertura y artefactos seguros."""
from __future__ import annotations

import json
import re
import shutil
from pathlib import Path
from typing import Any, Iterable

ROOT_DIR = Path(__file__).resolve().parents[3]
WORK_DIR = ROOT_DIR / ".restler-work"
STATE_DIR = ROOT_DIR / ".restler-state"
ARTIFACTS_DIR = ROOT_DIR / "artifacts/restler"
RESTLER_TAG = "v9.3.1"
RESTLER_COMMIT = "eaacd92733771d35e90acfb2040fe37d4e5d09b1"
RISK_ORDER = ("Critical", "High", "Medium", "Low", "Informational")
AUTH_OPERATIONS = {
    ("POST", "/api/v1/auth/logout"),
    ("DELETE", "/api/v1/cuentas/mi-cuenta"),
}
JWT_PATTERN = re.compile(r"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}")
HEADER_PATTERN = re.compile(r"(?i)(authorization\s*:\s*bearer\s+)[^\r\n]+")
JSON_SECRET_PATTERN = re.compile(
    r'(?i)(\\?"(?:accessToken|refreshToken|password|contrasena|codigo)\\?"\s*:\s*\\?")[^"\\]+')
BEARER_PATTERN = re.compile(r"(?i)(bearer\s+)[A-Za-z0-9._~+/\-]+=*")
SECRET_KEYS = {"password", "contrasena", "refreshToken", "accessToken", "codigo"}


def read_json(path: Path) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None


def find_files(root: Path, name: str) -> list[Path]:
    if not root.exists():
        return []
    return sorted(root.rglob(name))


def first_file(root: Path, name: str) -> Path | None:
    files = find_files(root, name)
    return files[0] if files else None


def route_key(entry: dict[str, Any]) -> tuple[str, str] | None:
    method = entry.get("verb")
    endpoint = entry.get("endpoint")
    if not isinstance(method, str) or not isinstance(endpoint, str):
        return None
    return method.upper(), endpoint


def load_speccov(root: Path) -> dict[tuple[str, str], list[dict[str, Any]]]:
    result: dict[tuple[str, str], list[dict[str, Any]]] = {}
    for path in find_files(root, "speccov.json"):
        data = read_json(path)
        if not isinstance(data, dict):
            continue
        for entry in data.values():
            if not isinstance(entry, dict):
                continue
            key = route_key(entry)
            if key is not None:
                result.setdefault(key, []).append(entry)
    return result


def operations_from_spec(spec: dict[str, Any]) -> list[tuple[str, str]]:
    operations: list[tuple[str, str]] = []
    for path, item in (spec.get("paths") or {}).items():
        if not isinstance(item, dict):
            continue
        for method in ("get", "post", "put", "patch", "delete", "head", "options"):
            if isinstance(item.get(method), dict):
                operations.append((method.upper(), path))
    return sorted(operations)


def _statuses(entries: Iterable[dict[str, Any]]) -> list[int]:
    statuses: set[int] = set()
    for entry in entries:
        raw = entry.get("status_code")
        try:
            statuses.add(int(raw))
        except (TypeError, ValueError):
            continue
    return sorted(statuses)


def _has_five_hundred(entries: Iterable[dict[str, Any]]) -> bool:
    return any(int(entry.get("invalid_due_to_500") or 0) == 1 for entry in entries)


def _recursive_codes(value: Any) -> set[int]:
    codes: set[int] = set()
    if isinstance(value, dict):
        for key, child in value.items():
            if isinstance(key, str) and re.fullmatch(r"\d{3}", key):
                codes.add(int(key))
            codes.update(_recursive_codes(child))
    elif isinstance(value, list):
        for child in value:
            codes.update(_recursive_codes(child))
    return codes


def collect_http_codes(root: Path) -> set[int]:
    codes: set[int] = set()
    for name in ("runSummary.json", "errorBuckets.json"):
        for path in find_files(root, name):
            codes.update(_recursive_codes(read_json(path)))
    for entries in load_speccov(root).values():
        codes.update(_statuses(entries))
    return codes


def collect_bugs(root: Path) -> list[dict[str, Any]]:
    bugs: list[dict[str, Any]] = []
    for path in find_files(root, "testing_summary.json"):
        summary = read_json(path)
        if not isinstance(summary, dict):
            continue
        buckets = summary.get("bug_buckets")
        if isinstance(buckets, dict):
            for name, count in buckets.items():
                bugs.append({"name": str(name), "count": int(count), "source": str(path)})
    for path in find_files(root, "bug_buckets.txt"):
        try:
            text = path.read_text(encoding="utf-8", errors="replace").strip()
        except OSError:
            continue
        if text and not re.search(r"Total Buckets:\s*0\b", text):
            bugs.append({
                "name": "bug_buckets.txt",
                "count": len([line for line in text.splitlines() if line.strip()]),
                "source": str(path),
            })
    for path in find_files(root, "*.replay.txt"):
        bugs.append({"name": path.name, "count": 1, "source": str(path)})
    return bugs


def _evidence_texts(root: Path) -> Iterable[str]:
    for path in list(find_files(root, "*.txt")) + list(find_files(root, "*.log")):
        try:
            yield path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue


def count_requests(root: Path) -> int:
    return sum(
        len(re.findall(r"(?m)^.*Sending:", text))
        for text in _evidence_texts(root)
    )


def count_sequences(root: Path) -> int:
    sequences: set[str] = set()
    for text in _evidence_texts(root):
        sequences.update(re.findall(
            r"(?im)^.*x-restler-sequence-id:\s*([0-9a-f-]{8,})", text))
    return len(sequences)


def compact_coverage(
    operations: list[tuple[str, str]],
    test: dict[tuple[str, str], list[dict[str, Any]]],
    fuzz: dict[tuple[str, str], list[dict[str, Any]]],
) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for key in operations:
        method, endpoint = key
        test_entries = test.get(key, [])
        fuzz_entries = fuzz.get(key, [])
        all_entries = test_entries + fuzz_entries
        rows.append({
            "method": method,
            "path": endpoint,
            "auth_required": key in AUTH_OPERATIONS,
            "attempted_test": bool(test_entries),
            "attempted_fuzz_lean": bool(fuzz_entries),
            "attempted": bool(all_entries),
            "valid_test": any(int(entry.get("valid") or 0) == 1 for entry in test_entries),
            "valid_fuzz_lean": any(int(entry.get("valid") or 0) == 1 for entry in fuzz_entries),
            "valid": any(int(entry.get("valid") or 0) == 1 for entry in all_entries),
            "status_codes": _statuses(all_entries),
            "http_500": _has_five_hundred(all_entries),
        })
    return rows


def classify(
    *,
    technical_errors: list[str],
    missing_operations: list[str],
    rows: list[dict[str, Any]],
    http_codes: set[int],
    bugs: list[dict[str, Any]],
) -> str:
    if technical_errors or missing_operations:
        return "ERROR"
    if any(code >= 500 for code in http_codes) or bugs:
        return "FAIL"
    if any(row["auth_required"] and not row["valid"] for row in rows):
        return "ERROR"
    if any(not row["valid"] for row in rows):
        return "WARN"
    return "PASS"


def collect_state_secrets(root: Path = STATE_DIR) -> set[str]:
    secrets: set[str] = set()

    def walk(value: Any) -> None:
        if isinstance(value, dict):
            for key, child in value.items():
                if key in SECRET_KEYS and isinstance(child, str) and len(child) >= 4:
                    secrets.add(child)
                walk(child)
        elif isinstance(value, list):
            for child in value:
                walk(child)

    if root.exists():
        for path in root.glob("*.json"):
            walk(read_json(path))
    return secrets


def sanitize_text(text: str, secrets: Iterable[str] = ()) -> str:
    for secret in sorted((value for value in secrets if value), key=len, reverse=True):
        text = text.replace(secret, "[REDACTED]")
    text = JWT_PATTERN.sub("[REDACTED_JWT]", text)
    text = HEADER_PATTERN.sub(r"\1[REDACTED]", text)
    text = JSON_SECRET_PATTERN.sub(r"\1[REDACTED]", text)
    text = BEARER_PATTERN.sub(r"\1[REDACTED]", text)
    return text


def copy_sanitized(source: Path, destination: Path, secrets: Iterable[str] = ()) -> None:
    try:
        text = source.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(sanitize_text(text, secrets), encoding="utf-8")


def copy_evidence_tree(
    source_root: Path,
    destination_root: Path,
    secrets: Iterable[str] = (),
) -> list[str]:
    copied: list[str] = []
    if not source_root.exists():
        return copied
    for source in source_root.rglob("*"):
        if not source.is_file():
            continue
        relative = source.relative_to(source_root)
        if source.name in {"engine_settings.json", "dict.json"}:
            continue
        if source.suffix.lower() not in {".txt", ".log", ".json", ".md", ".py"}:
            continue
        destination = destination_root / relative
        copy_sanitized(source, destination, secrets)
        copied.append(str(relative).replace("\\", "/"))
    return copied


def render_markdown(
    *,
    status: str,
    compile_exit: int | None,
    test_exit: int | None,
    fuzz_exit: int | None,
    rows: list[dict[str, Any]],
    technical_errors: list[str],
    http_codes: set[int],
    bugs: list[dict[str, Any]],
    requests: int,
    sequences: int,
    transformation: dict[str, Any] | None,
) -> str:
    lines = [
        "# UniHome RESTler Fuzzing",
        "",
        f"- **Estado:** **{status}**",
        f"- **RESTler:** `{RESTLER_TAG}` (`{RESTLER_COMMIT}`)",
        "- **Modo:** Compile + Test / Smoke + Fuzz-lean; sin fuzzing exhaustivo.",
        "- **Límites:** `max_combinations=3`, `max_sequence_length=2`, "
        "`max_request_execution_time=20s`, presupuesto de shell acotado.",
        "- **Checkers:** conjunto pasivo por defecto de RESTler; no se desactivan.",
        f"- **Compile:** {'success' if compile_exit == 0 else 'ERROR'} "
        f"(exit `{compile_exit}`).",
        f"- **Test/Smoke:** {'success' if test_exit == 0 else 'ERROR'} "
        f"(exit `{test_exit}`).",
        f"- **Fuzz-lean:** {'success' if fuzz_exit == 0 else 'ERROR'} "
        f"(exit `{fuzz_exit}`).",
        "",
        "## Cobertura real",
        "",
        f"- Operaciones OpenAPI: **{len(rows)}**.",
        f"- Operaciones intentadas: **{sum(1 for row in rows if row['attempted'])}**.",
        f"- Operaciones con al menos una respuesta 2xx: **{sum(1 for row in rows if row['valid'])}**.",
        f"- Solicitudes observadas en logs: **{requests}**.",
        f"- Secuencias RESTler observadas: **{sequences}**.",
        "",
        "| Método | Ruta | Auth | Test | Fuzz-lean | Válida | HTTP |",
        "|---|---|---:|---|---|---:|---|",
    ]
    for row in rows:
        lines.append(
            f"| {row['method']} | `{row['path']}` | "
            f"{'sí' if row['auth_required'] else 'no'} | "
            f"{'sí' if row['attempted_test'] else 'no'} / "
            f"{'2xx' if row['valid_test'] else 'sin 2xx'} | "
            f"{'sí' if row['attempted_fuzz_lean'] else 'no'} / "
            f"{'2xx' if row['valid_fuzz_lean'] else 'sin 2xx'} | "
            f"{'sí' if row['valid'] else '**NO**'} | "
            f"{', '.join(str(code) for code in row['status_codes']) or '—'} |")
    lines += [
        "",
        "## Errores HTTP",
        "",
        f"- Códigos observados: {', '.join(str(code) for code in sorted(http_codes)) or 'ninguno'}.",
        f"- HTTP 5xx: **{sum(1 for code in http_codes if code >= 500)}**.",
        "",
        "## Bugs y checkers",
        "",
    ]
    if bugs:
        for bug in bugs:
            lines.append(f"- `{bug['name']}`: {bug['count']} ocurrencia(s).")
    else:
        lines.append("No se detectaron bug buckets ni violaciones de checkers.")
    lines += [
        "",
        "## Compatibilidad OpenAPI",
        "",
    ]
    if transformation:
        lines.append(
            f"- Original `{transformation.get('source_openapi')}` -> derivado "
            f"`{transformation.get('target_openapi')}`.")
        lines.append(
            f"- Cambios de tipos union: {transformation.get('change_count', 0)}; "
            f"operaciones preservadas: {'sí' if transformation.get('operations_preserved') else 'NO'}.")
    else:
        lines.append("- No se encontró el reporte de transformación.")
    lines += [
        "",
        "## Criterio",
        "",
        "- ERROR si la compilación, el test/fuzz o la cobertura de operaciones falla.",
        "- FAIL si aparece un HTTP 5xx o un bug bucket; no se ocultan ni se desactivan checkers.",
        "- WARN si una operación fue intentada pero no obtuvo 2xx (por ejemplo, datos fuera de banda).",
        "- PASS solo con las 7 operaciones intentadas, autenticación válida y sin 5xx/bugs.",
    ]
    if technical_errors:
        lines += ["", "## Errores técnicos", ""] + [f"- {error}" for error in technical_errors]
    lines.append("")
    return "\n".join(lines)


def _exit_code(name: str) -> int | None:
    path = WORK_DIR / f"{name}-exit-code.txt"
    try:
        return int(path.read_text(encoding="utf-8").strip())
    except (OSError, ValueError):
        return None


def main() -> int:
    ARTIFACTS_DIR.mkdir(parents=True, exist_ok=True)
    original = read_json(ARTIFACTS_DIR / "openapi-original.json")
    transformation = read_json(ARTIFACTS_DIR / "openapi-transformation.json")
    if not isinstance(original, dict):
        original = read_json(WORK_DIR / "openapi-restler-30.json")
    operations = operations_from_spec(original) if isinstance(original, dict) else []
    test = load_speccov(WORK_DIR / "Test")
    fuzz = load_speccov(WORK_DIR / "FuzzLean")
    rows = compact_coverage(operations, test, fuzz)
    http_codes = collect_http_codes(WORK_DIR)
    bugs = collect_bugs(WORK_DIR)
    requests = count_requests(WORK_DIR)
    sequences = count_sequences(WORK_DIR)
    secrets = collect_state_secrets(STATE_DIR)

    technical_errors: list[str] = []
    prepare_error = WORK_DIR / "prepare-error.txt"
    if prepare_error.exists():
        technical_errors.append(prepare_error.read_text(encoding="utf-8", errors="replace")[:2000])
    compile_exit = _exit_code("compile")
    if compile_exit not in (0, None):
        technical_errors.append(f"RESTler compile terminó con código {compile_exit}.")
    if not (WORK_DIR / "Compile" / "grammar.py").exists():
        technical_errors.append("No se generó Compile/grammar.py.")
    if compile_exit is None:
        technical_errors.append("No hay código de salida de RESTler compile.")
    test_exit = _exit_code("test")
    fuzz_exit = _exit_code("fuzz-lean")
    if not test:
        technical_errors.append("Test no produjo speccov.json.")
    if not fuzz:
        technical_errors.append("Fuzz-lean no produjo speccov.json.")
    if test_exit not in (0, None):
        technical_errors.append(f"RESTler test terminó con código {test_exit}.")
    if fuzz_exit not in (0, None):
        technical_errors.append(f"RESTler fuzz-lean terminó con código {fuzz_exit}.")

    missing = [
        f"{row['method']} {row['path']}"
        for row in rows if not row["attempted"]
    ]
    status = classify(
        technical_errors=technical_errors,
        missing_operations=missing,
        rows=rows,
        http_codes=http_codes,
        bugs=bugs,
    )
    summary = {
        "status": status,
        "restler": {"tag": RESTLER_TAG, "commit": RESTLER_COMMIT},
        "compile_exit": compile_exit,
        "test_exit": test_exit,
        "fuzz_lean_exit": fuzz_exit,
        "routes": rows,
        "missing_operations": missing,
        "http_codes": sorted(http_codes),
        "http_5xx": sorted(code for code in http_codes if code >= 500),
        "bugs": bugs,
        "requests_observed": requests,
        "sequences_observed": sequences,
        "openapi_transformation": transformation,
        "technical_errors": technical_errors,
    }
    (ARTIFACTS_DIR / "restler-results.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    (ARTIFACTS_DIR / "route-coverage.json").write_text(
        json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
    (ARTIFACTS_DIR / "http-errors.json").write_text(
        json.dumps({
            "codes": sorted(http_codes),
            "http_5xx": sorted(code for code in http_codes if code >= 500),
        }, ensure_ascii=False, indent=2), encoding="utf-8")
    (ARTIFACTS_DIR / "bug-buckets.json").write_text(
        json.dumps(bugs, ensure_ascii=False, indent=2), encoding="utf-8")

    markdown = render_markdown(
        status=status,
        compile_exit=compile_exit,
        test_exit=test_exit,
        fuzz_exit=fuzz_exit,
        rows=rows,
        technical_errors=technical_errors,
        http_codes=http_codes,
        bugs=bugs,
        requests=requests,
        sequences=sequences,
        transformation=transformation if isinstance(transformation, dict) else None,
    )
    (ARTIFACTS_DIR / "restler-summary.md").write_text(markdown, encoding="utf-8")

    copied = []
    copied += copy_evidence_tree(WORK_DIR / "Compile", ARTIFACTS_DIR / "compile", secrets)
    copied += copy_evidence_tree(WORK_DIR / "Test", ARTIFACTS_DIR / "test", secrets)
    copied += copy_evidence_tree(WORK_DIR / "FuzzLean", ARTIFACTS_DIR / "fuzz-lean", secrets)
    for name in ("compile-console.txt", "test-console.txt", "fuzz-lean-console.txt"):
        copy_sanitized(WORK_DIR / name, ARTIFACTS_DIR / "logs" / name, secrets)
    (ARTIFACTS_DIR / "request-logs.json").write_text(
        json.dumps({
            "requests_observed": requests,
            "sequences_observed": sequences,
            "files": copied,
        }, ensure_ascii=False, indent=2),
        encoding="utf-8")

    for file in ARTIFACTS_DIR.rglob("*"):
        if file.is_file() and file.suffix.lower() in {".txt", ".log", ".json", ".md", ".py"}:
            copy_sanitized(file, file, secrets)

    print(f"RESTler status: {status}")
    print(
        f"Operaciones: {sum(1 for row in rows if row['attempted'])}/{len(rows)} intentadas; "
        f"{sum(1 for row in rows if row['valid'])}/{len(rows)} con 2xx; "
        f"5xx={summary['http_5xx']}; bugs={len(bugs)}")
    if technical_errors:
        print("\n".join(technical_errors))
    return 1 if status in {"ERROR", "FAIL"} else 0


if __name__ == "__main__":  # pragma: no cover - entrada de infraestructura
    raise SystemExit(main())
