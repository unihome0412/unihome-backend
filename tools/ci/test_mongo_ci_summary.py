"""Pruebas del resumen TRX de MongoDB CI, sin red ni MongoDB."""
import io
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

from mongo_ci_summary import EXPECTED_TESTS, main

TRX_HEADER = (
    '<?xml version="1.0" encoding="utf-8"?>'
    '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
    '<Times start="2026-11-08T12:00:00.0000000Z" finish="2026-11-08T12:00:03.2500000Z" />'
    "<Results>"
)
TRX_FOOTER = "</Results></TestRun>"


def make_trx(results: list[tuple[str, str]]) -> str:
    body = "".join(
        f'<UnitTestResult testName="{name}" outcome="{outcome}" duration="00:00:01.0000000" />'
        for name, outcome in results
    )
    return f"{TRX_HEADER}{body}{TRX_FOOTER}"


class MongoSummaryTests(unittest.TestCase):
    def run_main(self, trx_path: Path, **kwargs):
        argv = [
            "--trx",
            str(trx_path),
            "--mongo-version",
            "8.0.34",
            "--database",
            "unihome_ci_verificacion",
            "--initialization",
            kwargs.get("initialization", "success"),
            "--tests-outcome",
            kwargs.get("tests_outcome", "success"),
        ]
        stdout = io.StringIO()
        stderr = io.StringIO()
        with redirect_stdout(stdout), redirect_stderr(stderr):
            result = main(argv)
        return result, stdout.getvalue(), stderr.getvalue()

    def test_aprueba_las_tres_pruebas_reales(self):
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "mongo-ci.trx"
            trx.write_text(
                make_trx([(name, "Passed") for name in EXPECTED_TESTS]),
                encoding="utf-8",
            )

            result, output, _ = self.run_main(trx)

            self.assertEqual(0, result)
            self.assertIn("| Pruebas ejecutadas | 3 |", output)
            self.assertIn("| Pruebas aprobadas | 3 |", output)
            self.assertIn("| Resultado final | **PASS** |", output)
            self.assertIn("| Tiempo de ejecución | 3.25 s |", output)

    def test_omision_no_se_considera_aprobada(self):
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "mongo-ci.trx"
            trx.write_text(
                make_trx([(EXPECTED_TESTS[0], "NotExecuted"), (EXPECTED_TESTS[1], "Passed")]),
                encoding="utf-8",
            )

            result, output, errors = self.run_main(trx)

            self.assertEqual(1, result)
            self.assertIn("| Pruebas omitidas | 1 |", output)
            self.assertIn("Prueba MongoDB omitida", output)
            self.assertIn("::error::", errors)

    def test_falta_una_prueba_esperada(self):
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "mongo-ci.trx"
            trx.write_text(make_trx([(EXPECTED_TESTS[0], "Passed")]), encoding="utf-8")

            result, output, _ = self.run_main(trx)

            self.assertEqual(1, result)
            self.assertIn("Falta la prueba MongoDB esperada", output)

    def test_trx_inexistente_falla_y_genera_reporte(self):
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "no-existe.trx"

            result, output, _ = self.run_main(trx)

            self.assertEqual(1, result)
            self.assertIn("No se encontró o no se pudo leer el TRX", output)
            self.assertIn("| Resultado final | **FAIL** |", output)

    def test_paso_de_pruebas_omitido_bloquea(self):
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "mongo-ci.trx"
            trx.write_text(
                make_trx([(name, "Passed") for name in EXPECTED_TESTS]),
                encoding="utf-8",
            )

            result, output, _ = self.run_main(trx, tests_outcome="skipped")

            self.assertEqual(1, result)
            self.assertIn("El paso de pruebas MongoDB no terminó correctamente", output)

    def test_inicializacion_fallida_bloquea(self):
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "mongo-ci.trx"
            trx.write_text(
                make_trx([(name, "Passed") for name in EXPECTED_TESTS]),
                encoding="utf-8",
            )

            result, output, _ = self.run_main(trx, initialization="failure")

            self.assertEqual(1, result)
            self.assertIn("La inicialización de la base efímera", output)


if __name__ == "__main__":
    unittest.main()
