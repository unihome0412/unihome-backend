"""Pruebas del reporte y del gate de RESTler."""
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import restler.restler_report as report

OPERATIONS = [
    ("POST", "/api/v1/auth/registro"),
    ("POST", "/api/v1/auth/confirmacion-correo"),
    ("POST", "/api/v1/auth/confirmacion-correo/reenvio"),
    ("POST", "/api/v1/auth/login"),
    ("POST", "/api/v1/auth/refresh"),
    ("POST", "/api/v1/auth/logout"),
    ("DELETE", "/api/v1/cuentas/mi-cuenta"),
]


def build_spec():
    return {
        "paths": {
            path: {
                method.lower(): {
                    "responses": {"200": {}, "204": {}},
                }
            }
            for method, path in OPERATIONS
        }
    }



def write_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding="utf-8")


def write_task(root: Path, task: str, operations, *, name: str = "speccov.json") -> None:
    entries = {}
    for index, (method, endpoint) in enumerate(operations):
        entries[f"{task}-{index}"] = {
            "verb": method,
            "endpoint": endpoint,
            "valid": 1,
            "status_code": "204" if method == "DELETE" else "200",
            "invalid_due_to_500": 0,
            "sample_request": {"request_headers": ["Authorization: Bearer abc.def.ghi"]},
        }
    write_json(
        root / task / "RestlerResults" / "exp" / "logs" / name,
        entries)
    write_json(
        root / task / "ResponseBuckets" / "runSummary.json",
        {"codeCounts": {"200": 1}, "bugCount": 0})
    write_json(
        root / task / "RestlerResults" / "exp" / "logs" / "testing_summary.json",
        {"bug_buckets": {}})
    (root / task / "RestlerResults" / "exp" / "logs" / "main.txt").write_text(
        "Sending: GET /x\nReceived: 200\n", encoding="utf-8")


def build_work(root: Path, *, omit: tuple[str, str] | None = None) -> None:
    compile_dir = root / "Compile"
    compile_dir.mkdir(parents=True)
    (compile_dir / "grammar.py").write_text("# grammar\n", encoding="utf-8")
    (compile_dir / "config.json").write_text("{}\n", encoding="utf-8")
    (compile_dir / "StdOut.txt").write_text("compiled\n", encoding="utf-8")

    main_operations = [
        op for op in OPERATIONS
        if op not in {("POST", "/api/v1/auth/logout"),
                      ("DELETE", "/api/v1/cuentas/mi-cuenta")}
        and op != omit
    ]
    fuzz_operations = [
        op for op in main_operations
        if op != omit
    ]
    write_task(root, "TestMain", main_operations)
    write_task(root, "FuzzLean", fuzz_operations, name="speccov-min.json")
    if omit != ("POST", "/api/v1/auth/logout"):
        write_task(root, "TestLogout", [("POST", "/api/v1/auth/logout")])
    if omit != ("DELETE", "/api/v1/cuentas/mi-cuenta"):
        write_task(root, "TestDelete", [("DELETE", "/api/v1/cuentas/mi-cuenta")])

    for name in ("compile", "test-main", "test-logout", "test-delete", "fuzz-lean"):
        (root / f"{name}-exit-code.txt").write_text("0\n", encoding="utf-8")


class RestlerReportTests(unittest.TestCase):
    def test_sanitize_redacta_jwt_headers_y_campos_escapados(self):
        text = (
            'Authorization: Bearer abc.def.ghi\n'
            '{"accessToken":"uno","refreshToken":"dos","contrasena":"tres"}\n'
            r'{\"contraseña\":\"no\",\"codigo\":\"123456\",\"token\":\"reset-secret\"}'
        )
        sanitized = report.sanitize_text(text, {"synthetic-secret"})
        self.assertNotIn("abc.def.ghi", sanitized)
        self.assertNotIn('"uno"', sanitized)
        self.assertNotIn('"dos"', sanitized)
        self.assertNotIn('"tres"', sanitized)
        self.assertNotIn("123456", sanitized)
        self.assertNotIn("reset-secret", sanitized)

    def test_coverage_y_gate_pass(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            build_work(root)
            spec = build_spec()
            write_json(root / "artifacts" / "openapi-original.json", spec)
            with (
                patch.object(report, "WORK_DIR", root),
                patch.object(report, "STATE_DIR", root / "state"),
                patch.object(report, "CI_STATE_DIR", root / "ci-state"),
                patch.object(report, "ARTIFACTS_DIR", root / "artifacts"),
            ):
                result = report.main()
            self.assertEqual(0, result)
            summary = json.loads(
                (root / "artifacts" / "restler-results.json").read_text(encoding="utf-8"))
            self.assertEqual("PASS", summary["status"])
            self.assertEqual(7, len(summary["routes"]))
            self.assertTrue(all(row["valid"] for row in summary["routes"]))
            self.assertEqual([], summary["missing_operations"])

    def test_speccov_min_es_evidencia_valida_para_fuzz_lean(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            build_work(root)
            spec = build_spec()
            write_json(root / "artifacts" / "openapi-original.json", spec)
            with (
                patch.object(report, "WORK_DIR", root),
                patch.object(report, "STATE_DIR", root / "state"),
                patch.object(report, "CI_STATE_DIR", root / "ci-state"),
                patch.object(report, "ARTIFACTS_DIR", root / "artifacts"),
            ):
                result = report.main()
            self.assertEqual(0, result)
            summary = json.loads(
                (root / "artifacts" / "restler-results.json").read_text(encoding="utf-8"))
            self.assertTrue(any(
                row["path"] == "/api/v1/auth/login" and row["attempted_fuzz_lean"]
                for row in summary["routes"]))

    def test_gate_falla_por_500(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            build_work(root)
            spec = build_spec()
            write_json(root / "artifacts" / "openapi-original.json", spec)
            write_json(
                root / "TestMain" / "ResponseBuckets" / "runSummary.json",
                {"codeCounts": {"500": 1}, "bugCount": 1})
            with (
                patch.object(report, "WORK_DIR", root),
                patch.object(report, "STATE_DIR", root / "state"),
                patch.object(report, "CI_STATE_DIR", root / "ci-state"),
                patch.object(report, "ARTIFACTS_DIR", root / "artifacts"),
            ):
                result = report.main()
            self.assertEqual(1, result)
            summary = json.loads(
                (root / "artifacts" / "restler-results.json").read_text(encoding="utf-8"))
            self.assertEqual("FAIL", summary["status"])
            self.assertEqual([500], summary["http_5xx"])

    def test_gate_error_si_falta_una_operacion(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            build_work(root, omit=("POST", "/api/v1/auth/logout"))
            spec = build_spec()
            write_json(root / "artifacts" / "openapi-original.json", spec)
            with (
                patch.object(report, "WORK_DIR", root),
                patch.object(report, "STATE_DIR", root / "state"),
                patch.object(report, "CI_STATE_DIR", root / "ci-state"),
                patch.object(report, "ARTIFACTS_DIR", root / "artifacts"),
            ):
                result = report.main()
            self.assertEqual(1, result)
            summary = json.loads(
                (root / "artifacts" / "restler-results.json").read_text(encoding="utf-8"))
            self.assertEqual("ERROR", summary["status"])
            self.assertIn("POST /api/v1/auth/logout", summary["missing_operations"])

    def test_gate_error_si_falta_pasada_destructiva(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            build_work(root, omit=("DELETE", "/api/v1/cuentas/mi-cuenta"))
            spec = build_spec()
            write_json(root / "artifacts" / "openapi-original.json", spec)
            with (
                patch.object(report, "WORK_DIR", root),
                patch.object(report, "STATE_DIR", root / "state"),
                patch.object(report, "CI_STATE_DIR", root / "ci-state"),
                patch.object(report, "ARTIFACTS_DIR", root / "artifacts"),
            ):
                result = report.main()
            self.assertEqual(1, result)
            summary = json.loads(
                (root / "artifacts" / "restler-results.json").read_text(encoding="utf-8"))
            self.assertEqual("ERROR", summary["status"])
            self.assertIn("DELETE /api/v1/cuentas/mi-cuenta", summary["missing_operations"])

    def test_gate_warn_si_hay_operacion_sin_2xx(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            build_work(root)
            spec = build_spec()
            write_json(root / "artifacts" / "openapi-original.json", spec)
            for task, name in (("TestMain", "speccov.json"),
                               ("FuzzLean", "speccov-min.json")):
                speccov = (root / task / "RestlerResults" / "exp" / "logs" / name)
                data = json.loads(speccov.read_text(encoding="utf-8"))
                first = next(iter(data.values()))
                first["valid"] = 0
                first["status_code"] = "400"
                write_json(speccov, data)
            with (
                patch.object(report, "WORK_DIR", root),
                patch.object(report, "STATE_DIR", root / "state"),
                patch.object(report, "CI_STATE_DIR", root / "ci-state"),
                patch.object(report, "ARTIFACTS_DIR", root / "artifacts"),
            ):
                result = report.main()
            self.assertEqual(0, result)
            summary = json.loads(
                (root / "artifacts" / "restler-results.json").read_text(encoding="utf-8"))
            self.assertEqual("WARN", summary["status"])
            markdown = (root / "artifacts" / "restler-summary.md").read_text(
                encoding="utf-8")
            self.assertIn("## Operaciones sin 2xx", markdown)
            self.assertIn("se conserva como WARN", markdown)


if __name__ == "__main__":
    unittest.main()
