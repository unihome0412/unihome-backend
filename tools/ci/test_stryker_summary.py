"""Pruebas del resumen de Stryker, sin dependencias ni red."""
import json
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from pathlib import Path

from stryker_summary import main, render, summarize


class StrykerSummaryTests(unittest.TestCase):
    def test_resume_conteos_y_score(self):
        report = {
            "projectRoot": "/repo/src/UniHome.Application",
            "files": {
                "/repo/src/UniHome.Application/Identity/Login/LoginService.cs": {
                    "mutants": [
                        {"status": "Killed"},
                        {"status": "Killed"},
                        {
                            "status": "Survived",
                            "location": {"start": {"line": 10}},
                            "mutatorName": "Boolean mutation",
                            "replacement": "true",
                        },
                        {"status": "NoCoverage"},
                        {"status": "Ignored"},
                        {"status": "CompileError"},
                    ]
                }
            },
        }

        summary = summarize(report)

        self.assertEqual(6, summary["mutants"])
        self.assertEqual(4, summary["tested"])
        self.assertEqual(2, summary["detected"])
        self.assertEqual(1, summary["survived"])
        self.assertEqual(1, summary["no_coverage"])
        self.assertEqual(1, summary["ignored"])
        self.assertEqual(1, summary["compile_error"])
        self.assertEqual(50.0, summary["score"])
        self.assertEqual(
            "Identity/Login/LoginService.cs",
            summary["survivors"][0]["file"],
        )

    def test_render_incluye_sobrevivientes(self):
        summary = {
            "mutants": 3,
            "tested": 2,
            "detected": 1,
            "survived": 1,
            "no_coverage": 0,
            "timeout": 0,
            "ignored": 1,
            "compile_error": 0,
            "runtime_error": 0,
            "score": 50.0,
            "counts": {"Killed": 1, "Survived": 1, "Ignored": 1},
            "survivors": [
                {
                    "file": "Identity/Login/LoginService.cs",
                    "line": 61,
                    "mutator": "Boolean mutation",
                    "replacement": "true",
                }
            ],
        }

        text = render(summary)

        self.assertIn("Mutation score", text)
        self.assertIn("Boolean mutation", text)
        self.assertIn("Identity/Login/LoginService.cs", text)

    def test_reporte_vacio_no_tiene_mutantes(self):
        summary = summarize({"files": {}})

        self.assertEqual(0, summary["mutants"])
        self.assertEqual(0, summary["tested"])
        self.assertEqual(0, summary["score"])

    @staticmethod
    def _write_report(root: Path, report: dict) -> None:
        reports = root / "2026-01-01.00-00-00" / "reports"
        reports.mkdir(parents=True)
        (reports / "mutation-report.json").write_text(
            json.dumps(report), encoding="utf-8")

    def test_main_sin_reporte_falla(self):
        with tempfile.TemporaryDirectory() as tmp:
            with redirect_stdout(StringIO()), redirect_stderr(StringIO()) as stderr:
                result = main(Path(tmp))
        self.assertEqual(1, result)
        self.assertIn("No se encontró mutation-report.json", stderr.getvalue())

    def test_main_reporte_valido_aprueba(self):
        report = {"files": {"a.cs": {"mutants": [{"status": "Killed"}]}}}
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._write_report(root, report)
            with redirect_stdout(StringIO()) as stdout, redirect_stderr(StringIO()):
                result = main(root)
        self.assertEqual(0, result)
        self.assertIn("Mutation score", stdout.getvalue())

    def test_main_reporte_vacio_falla(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._write_report(root, {"files": {}})
            with redirect_stdout(StringIO()), redirect_stderr(StringIO()):
                result = main(root)
        self.assertEqual(1, result)

    def test_main_sin_mutaciones_evaluadas_falla(self):
        report = {"files": {"a.cs": {"mutants": [{"status": "Ignored"}]}}}
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._write_report(root, report)
            with redirect_stdout(StringIO()), redirect_stderr(StringIO()):
                result = main(root)
        self.assertEqual(1, result)

    def test_main_error_de_ejecucion_falla(self):
        report = {"files": {"a.cs": {"mutants": [
            {"status": "Killed"}, {"status": "RuntimeError"}]}}}
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._write_report(root, report)
            with redirect_stdout(StringIO()), redirect_stderr(StringIO()):
                result = main(root)
        self.assertEqual(1, result)


if __name__ == "__main__":
    unittest.main()
