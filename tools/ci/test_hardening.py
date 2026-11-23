"""Pruebas del quality gate, sin dependencias ni red."""
import json
import os
import sys
import unittest
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from pathlib import Path
from unittest.mock import patch

from hardening import evaluate, main

class GateTests(unittest.TestCase):
    def test_obligatorios_aprobados(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}
        required, issues = evaluate(needs, True, {}, "pull_request")
        self.assertEqual([], issues)
        self.assertIn("contract-test", required)

    def test_mongo_skipped_en_pr_bloquea(self):
        needs = self._base_needs()
        needs["mongo-db-test"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_MONGO_CI": "true"}, "pull_request")
        self.assertIn("mongo-db-test", required)
        self.assertTrue(any("mongo-db-test" in issue for issue in issues))

    def test_habilitado_y_omitido_bloquea(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}
        needs["docker-build"] = {"result": "skipped"}
        needs["container-lint"] = {"result": "skipped"}
        needs["container-smoke"] = {"result": "skipped"}
        _, issues = evaluate(needs, True, {"UNIHOME_ENABLE_DOCKER": "true"}, "pull_request")
        self.assertEqual(3, len(issues))

    def test_container_smoke_exitoso_cumple(self):
        needs = self._base_needs()
        needs["docker-build"] = {"result": "success"}
        needs["container-lint"] = {"result": "success"}
        needs["container-smoke"] = {"result": "success"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_DOCKER": "true"}, "pull_request")
        self.assertEqual([], issues)
        self.assertIn("container-smoke", required)

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

    def test_mongo_success_en_manual_cumple(self):
        needs = self._base_needs()
        needs["mongo-db-test"] = {"result": "success"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_MONGO_CI": "true"}, "workflow_dispatch")
        self.assertEqual([], issues)
        self.assertIn("mongo-db-test", required)

    def test_mongo_skipped_en_manual_bloquea(self):
        needs = self._base_needs()
        needs["mongo-db-test"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_MONGO_CI": "true"}, "workflow_dispatch")
        self.assertIn("mongo-db-test", required)
        self.assertTrue(any("mongo-db-test" in issue for issue in issues))

    def test_mongo_success_en_pr_cumple(self):
        needs = self._base_needs()
        needs["mongo-db-test"] = {"result": "success"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_MONGO_CI": "true"}, "pull_request")
        self.assertEqual([], issues)
        self.assertIn("mongo-db-test", required)

    def test_mongo_failure_en_push_bloquea(self):
        needs = self._base_needs()
        needs["mongo-db-test"] = {"result": "failure"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_MONGO_CI": "true"}, "push")
        self.assertIn("mongo-db-test", required)
        self.assertTrue(any("mongo-db-test" in issue for issue in issues))

    def test_mutation_success_en_manual_cumple(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}
        needs["mutation-test"] = {"result": "success"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_MUTATION": "true"}, "workflow_dispatch")
        self.assertEqual([], issues)
        self.assertIn("mutation-test", required)

    def test_mutation_skipped_en_manual_bloquea(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}
        needs["mutation-test"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_MUTATION": "true"}, "workflow_dispatch")
        self.assertIn("mutation-test", required)
        self.assertTrue(any("mutation-test" in issue for issue in issues))

    def test_mutation_failure_en_manual_bloquea(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}
        needs["mutation-test"] = {"result": "failure"}
        _, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_MUTATION": "true"}, "workflow_dispatch")
        self.assertTrue(any("mutation-test" in issue for issue in issues))

    def test_mutation_skipped_en_pull_request_no_bloquea(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}
        needs["mutation-test"] = {"result": "skipped"}
        _, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_MUTATION": "true"}, "pull_request")
        self.assertEqual([], issues)

    def test_cloudinary_manual_skipped_bloquea_cuando_esta_habilitado(self):
        needs = self._base_needs()
        needs["cloudinary-integration"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True,
            {"UNIHOME_ENABLE_CLOUDINARY_INTEGRATION": "true"},
            "workflow_dispatch", "refs/heads/feature/cloudinary")
        self.assertIn("cloudinary-integration", required)
        self.assertTrue(any("cloudinary-integration" in issue for issue in issues))

    def test_cloudinary_pr_skipped_no_bloquea(self):
        needs = self._base_needs()
        needs["cloudinary-integration"] = {"result": "skipped"}
        _, issues = evaluate(
            needs, True,
            {"UNIHOME_ENABLE_CLOUDINARY_INTEGRATION": "true"},
            "pull_request", "refs/pull/15/merge")
        self.assertEqual([], issues)

    def test_dast_manual_feature_success_cumple(self):
        needs = self._base_needs()
        needs["dast-zap"] = {"result": "success"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_ZAP": "true"},
            "workflow_dispatch", "refs/heads/feature/ci-dast-zap")
        self.assertEqual([], issues)
        self.assertIn("dast-zap", required)

    def test_dast_manual_feature_skipped_bloquea(self):
        needs = self._base_needs()
        needs["dast-zap"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_ZAP": "true"},
            "workflow_dispatch", "refs/heads/feature/ci-dast-zap")
        self.assertIn("dast-zap", required)
        self.assertTrue(any("dast-zap" in issue for issue in issues))

    def test_dast_pr_skipped_no_bloquea(self):
        needs = self._base_needs()
        needs["dast-zap"] = {"result": "skipped"}
        _, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_ZAP": "true"},
            "pull_request", "refs/pull/15/merge")
        self.assertEqual([], issues)

    def test_dast_push_skipped_no_bloquea(self):
        needs = self._base_needs()
        needs["dast-zap"] = {"result": "skipped"}
        _, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_ZAP": "true"},
            "push", "refs/heads/main")
        self.assertEqual([], issues)

    def test_dast_manual_main_skipped_bloquea(self):
        needs = self._base_needs()
        needs["dast-zap"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_ZAP": "true"},
            "workflow_dispatch", "refs/heads/main")
        self.assertIn("dast-zap", required)
        self.assertTrue(any("dast-zap" in issue for issue in issues))

    def test_dast_manual_rama_no_habilitada_no_exige(self):
        needs = self._base_needs()
        needs["dast-zap"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_ZAP": "true"},
            "workflow_dispatch", "refs/heads/docs/ci")
        self.assertNotIn("dast-zap", required)
        self.assertEqual([], issues)

    def test_dast_manual_sin_cambios_no_exige(self):
        needs = {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan")}
        required, issues = evaluate(
            needs, False, {"UNIHOME_ENABLE_ZAP": "true"},
            "workflow_dispatch", "refs/heads/feature/ci-dast-zap")
        self.assertNotIn("dast-zap", required)
        self.assertEqual([], issues)

    def test_restler_manual_feature_skipped_bloquea(self):
        needs = self._base_needs()
        needs["restler-fuzzing"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_RESTLER": "true"},
            "workflow_dispatch", "refs/heads/feature/ci-restler")
        self.assertIn("restler-fuzzing", required)
        self.assertTrue(any("restler-fuzzing" in issue for issue in issues))

    def test_restler_pr_skipped_no_bloquea(self):
        needs = self._base_needs()
        needs["restler-fuzzing"] = {"result": "skipped"}
        _, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_RESTLER": "true"},
            "pull_request", "refs/pull/16/merge")
        self.assertEqual([], issues)

    def test_restler_manual_success_cumple(self):
        needs = self._base_needs()
        needs["restler-fuzzing"] = {"result": "success"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_RESTLER": "true"},
            "workflow_dispatch", "refs/heads/main")
        self.assertEqual([], issues)
        self.assertIn("restler-fuzzing", required)

    def test_mongo_only_no_exige_controles_manuales_omitidos(self):
        needs = self._base_needs()
        needs["mongo-db-test"] = {"result": "success"}
        suppressed = (
            "mutation-test",
            "pact-contract",
            "cloudinary-integration",
            "dast-zap",
            "restler-fuzzing",
        )
        for job in suppressed:
            needs[job] = {"result": "skipped"}
        required, issues = evaluate(
            needs,
            True,
            {
                "UNIHOME_ENABLE_MONGO_CI": "true",
                "UNIHOME_ENABLE_MUTATION": "true",
                "UNIHOME_ENABLE_PACT": "true",
                "UNIHOME_ENABLE_CLOUDINARY_INTEGRATION": "true",
                "UNIHOME_ENABLE_ZAP": "true",
                "UNIHOME_ENABLE_RESTLER": "true",
            },
            "workflow_dispatch",
            "refs/heads/feature/hu-006-recuperacion-contrasena",
            run_mongo_only=True,
        )
        self.assertEqual([], issues)
        self.assertIn("mongo-db-test", required)
        for job in suppressed:
            self.assertNotIn(job, required)

    def test_mongo_only_exige_mongo_aprobado(self):
        needs = self._base_needs()
        needs["mongo-db-test"] = {"result": "failure"}
        required, issues = evaluate(
            needs,
            True,
            {"UNIHOME_ENABLE_MONGO_CI": "true"},
            "workflow_dispatch",
            "refs/heads/feature/hu-006-recuperacion-contrasena",
            run_mongo_only=True,
        )
        self.assertIn("mongo-db-test", required)
        self.assertTrue(any("mongo-db-test" in issue for issue in issues))

    def test_mongo_only_no_omite_controles_automaticos(self):
        needs = self._base_needs()
        needs["mongo-db-test"] = {"result": "success"}
        needs["build-test"] = {"result": "failure"}
        _, issues = evaluate(
            needs,
            True,
            {"UNIHOME_ENABLE_MONGO_CI": "true"},
            "workflow_dispatch",
            "refs/heads/feature/hu-006-recuperacion-contrasena",
            run_mongo_only=True,
        )
        self.assertTrue(any("build-test" in issue for issue in issues))

    def test_mongo_only_sin_input_sigue_exigiendo_manuales(self):
        needs = self._base_needs()
        needs["mongo-db-test"] = {"result": "success"}
        needs["mutation-test"] = {"result": "skipped"}
        required, issues = evaluate(
            needs,
            True,
            {"UNIHOME_ENABLE_MONGO_CI": "true", "UNIHOME_ENABLE_MUTATION": "true"},
            "workflow_dispatch",
            "refs/heads/feature/hu-006-recuperacion-contrasena",
        )
        self.assertIn("mutation-test", required)
        self.assertTrue(any("mutation-test" in issue for issue in issues))

    @staticmethod
    def _base_needs():
        return {x: {"result": "success"} for x in (
            "detect-changes", "dependency-scan", "secret-scan", "semgrep-sast", "build-test", "contract-test")}

    def test_container_smoke_mantiene_proxies_y_evita_url_http(self):
        workflow = Path(__file__).resolve().parents[2] / ".github" / "workflows" / "ci.yml"
        text = workflow.read_text(encoding="utf-8")
        start = text.index("  container-smoke:")
        end = text.index("\n  dast-zap:", start)
        block = text[start:end]

        # La imagen ya define el binding interno; fijar ASPNETCORE_URLS aquí
        # vuelve a introducir la vulnerabilidad githubactions:S5332 de Sonar.
        proxy_0 = "Proxy__KnownProxies__0"
        proxy_1 = "Proxy__KnownProxies__1"
        self.assertNotIn("ASPNETCORE_URLS", block)
        self.assertIn(proxy_0, block)
        self.assertIn(proxy_1, block)
        self.assertIn("Reproducir el fallo sin Proxy__KnownProxies", block)
        self.assertIn("Proxy:TrustForwardedHeaders", block)

        # Readiness evalúa Redis: el smoke levanta un stub HTTPS local aislado,
        # no usa Upstash real ni desactiva la comprobación.
        self.assertIn("tools/ci/smoke_upstash_stub.py", block)
        self.assertIn("SSL_CERT_FILE=/tmp/unihome-smoke-upstash-ca.crt", block)
        self.assertIn(
            'Redis__RestUrl="https://127.0.0.1:${SMOKE_UPSTASH_PORT}"',
            block,
        )

        # El CD debe seguir fijando los proxies loopback y el fail-fast.
        deploy = text[text.index("  deploy-azure:"):]
        self.assertIn("Proxy__TrustForwardedHeaders=true", deploy)
        self.assertIn(proxy_0, deploy)
        self.assertIn(proxy_1, deploy)

    def test_sonar_pr_interno_aprobado(self):
        needs = self._base_needs()
        needs["sonarcloud-sast"] = {"result": "success"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_SONAR": "true"}, "pull_request", "refs/pull/12/merge")
        self.assertEqual([], issues)
        self.assertIn("sonarcloud-sast", required)

    def test_sonar_pr_fallido_bloquea(self):
        needs = self._base_needs()
        needs["sonarcloud-sast"] = {"result": "failure"}
        _, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_SONAR": "true"}, "pull_request", "refs/pull/12/merge")
        self.assertTrue(any("sonarcloud-sast" in issue for issue in issues))

    def test_sonar_pr_omitido_bloquea(self):
        needs = self._base_needs()
        needs["sonarcloud-sast"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_SONAR": "true"}, "pull_request", "refs/pull/12/merge")
        self.assertIn("sonarcloud-sast", required)
        self.assertTrue(any("sonarcloud-sast" in issue for issue in issues))

    def test_sonar_manual_main_aprobado(self):
        needs = self._base_needs()
        needs["sonarcloud-sast"] = {"result": "success"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_SONAR": "true"}, "workflow_dispatch", "refs/heads/main")
        self.assertEqual([], issues)
        self.assertIn("sonarcloud-sast", required)

    def test_sonar_manual_feature_omitido_no_bloquea(self):
        needs = self._base_needs()
        needs["sonarcloud-sast"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_SONAR": "true"}, "workflow_dispatch", "refs/heads/feature/ci-stryker")
        self.assertEqual([], issues)
        self.assertNotIn("sonarcloud-sast", required)

    def test_sonar_push_main_omitido_bloquea(self):
        needs = self._base_needs()
        needs["sonarcloud-sast"] = {"result": "skipped"}
        required, issues = evaluate(
            needs, True, {"UNIHOME_ENABLE_SONAR": "true"}, "push", "refs/heads/main")
        self.assertIn("sonarcloud-sast", required)
        self.assertTrue(any("sonarcloud-sast" in issue for issue in issues))

    def test_main_manual_feature_no_exige_sonar(self):
        needs = self._base_needs()
        needs["sonarcloud-sast"] = {"result": "skipped"}
        env = {
            "NEEDS_JSON": json.dumps(needs),
            "CODE_CHANGED": "true",
            "EVENT_NAME": "workflow_dispatch",
            "EVENT_REF": "refs/heads/feature/ci-stryker",
            "UNIHOME_ENABLE_SONAR": "true",
        }
        with (
            patch.dict(os.environ, env, clear=True),
            patch.object(sys, "argv", ["hardening.py", "--gate"]),
            redirect_stdout(StringIO()) as stdout,
            redirect_stderr(StringIO()),
        ):
            result = main()
        self.assertEqual(0, result)
        self.assertIn("| `sonarcloud-sast` | **skipped** | no |", stdout.getvalue())

    def test_main_pr_sonar_omitido_bloquea(self):
        needs = self._base_needs()
        needs["sonarcloud-sast"] = {"result": "skipped"}
        env = {
            "NEEDS_JSON": json.dumps(needs),
            "CODE_CHANGED": "true",
            "EVENT_NAME": "pull_request",
            "EVENT_REF": "refs/pull/12/merge",
            "UNIHOME_ENABLE_SONAR": "true",
        }
        with (
            patch.dict(os.environ, env, clear=True),
            patch.object(sys, "argv", ["hardening.py", "--gate"]),
            redirect_stdout(StringIO()),
            redirect_stderr(StringIO()) as stderr,
        ):
            result = main()
        self.assertEqual(1, result)
        self.assertIn(
            "sonarcloud-sast: esperado success; encontrado skipped",
            stderr.getvalue(),
        )

    def test_main_dast_omitido_en_feature_bloquea(self):
        needs = self._base_needs()
        needs["dast-zap"] = {"result": "skipped"}
        env = {
            "NEEDS_JSON": json.dumps(needs),
            "CODE_CHANGED": "true",
            "EVENT_NAME": "workflow_dispatch",
            "EVENT_REF": "refs/heads/feature/ci-dast-zap",
            "UNIHOME_ENABLE_ZAP": "true",
        }
        with (
            patch.dict(os.environ, env, clear=True),
            patch.object(sys, "argv", ["hardening.py", "--gate"]),
            redirect_stdout(StringIO()),
            redirect_stderr(StringIO()) as stderr,
        ):
            result = main()
        self.assertEqual(1, result)
        self.assertIn(
            "dast-zap: esperado success; encontrado skipped",
            stderr.getvalue(),
        )

    def test_main_mongo_only_reporta_modo_y_omite_manuales(self):
        needs = self._base_needs()
        needs["mongo-db-test"] = {"result": "success"}
        needs["mutation-test"] = {"result": "skipped"}
        needs["dast-zap"] = {"result": "skipped"}
        needs["restler-fuzzing"] = {"result": "skipped"}
        env = {
            "NEEDS_JSON": json.dumps(needs),
            "CODE_CHANGED": "true",
            "EVENT_NAME": "workflow_dispatch",
            "EVENT_REF": "refs/heads/feature/hu-006-recuperacion-contrasena",
            "RUN_MONGO_ONLY": "true",
            "UNIHOME_ENABLE_MONGO_CI": "true",
            "UNIHOME_ENABLE_MUTATION": "true",
            "UNIHOME_ENABLE_ZAP": "true",
            "UNIHOME_ENABLE_RESTLER": "true",
        }
        with (
            patch.dict(os.environ, env, clear=True),
            patch.object(sys, "argv", ["hardening.py", "--gate"]),
            redirect_stdout(StringIO()) as stdout,
            redirect_stderr(StringIO()),
        ):
            result = main()
        self.assertEqual(0, result)
        report = stdout.getvalue()
        self.assertIn("Modo solo MongoDB: **sí**", report)
        self.assertIn("| `mutation-test` | **skipped** | no |", report)
        self.assertIn("| `dast-zap` | **skipped** | no |", report)
        self.assertIn("| `restler-fuzzing` | **skipped** | no |", report)

    def test_main_dast_exitoso_muestra_evento_y_rama(self):
        needs = self._base_needs()
        needs["dast-zap"] = {"result": "success"}
        env = {
            "NEEDS_JSON": json.dumps(needs),
            "CODE_CHANGED": "true",
            "EVENT_NAME": "workflow_dispatch",
            "EVENT_REF": "refs/heads/feature/ci-dast-zap",
            "UNIHOME_ENABLE_ZAP": "true",
        }
        with (
            patch.dict(os.environ, env, clear=True),
            patch.object(sys, "argv", ["hardening.py", "--gate"]),
            redirect_stdout(StringIO()) as stdout,
            redirect_stderr(StringIO()),
        ):
            result = main()
        self.assertEqual(0, result)
        report = stdout.getvalue()
        self.assertIn("workflow_dispatch / refs/heads/feature/ci-dast-zap", report)
        self.assertIn("DAST ZAP manual: **obligatorio**", report)

if __name__ == '__main__':
    unittest.main()
