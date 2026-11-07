"""Pruebas del quality gate, sin dependencias ni red."""
import unittest
from hardening import evaluate

class GateTests(unittest.TestCase):
    def test_obligatorios_aprobados(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}
        required, issues = evaluate(needs, True, {}, "pull_request")
        self.assertEqual([], issues)
        self.assertIn("contract-test", required)

    def test_optional_omitido_no_impide_pr(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}
        needs["mongo-db-test"] = {"result": "skipped"}
        _, issues = evaluate(needs, True, {"UNIHOME_ENABLE_MONGO_CI": "true"}, "pull_request")
        self.assertEqual([], issues)

    def test_habilitado_y_omitido_bloquea(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}
        needs["docker-build"] = {"result": "skipped"}
        needs["container-lint"] = {"result": "skipped"}
        _, issues = evaluate(needs, True, {"UNIHOME_ENABLE_DOCKER": "true"}, "pull_request")
        self.assertEqual(2, len(issues))

    def test_fallo_en_prueba_obligatoria_bloquea(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}
        needs["build-test"] = {"result": "failure"}
        _, issues = evaluate(needs, True, {}, "push")
        self.assertEqual(1, len(issues))

    def test_solo_documentos_no_necesita_build(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan")}
        _, issues = evaluate(needs, False, {}, "pull_request")
        self.assertEqual([], issues)

if __name__ == '__main__':
    unittest.main()
