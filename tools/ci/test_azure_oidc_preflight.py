"""Contrato estático del workflow de preflight OIDC read-only."""
from __future__ import annotations

import re
import unittest
from pathlib import Path

WORKFLOW = Path(".github/workflows/azure-oidc-preflight.yml")


class AzureOidcPreflightContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.text = WORKFLOW.read_text(encoding="utf-8")
        cls.on_block = cls.text.split("on:", 1)[1].split("permissions:", 1)[0]
        cls.job = cls.text.split("  oidc-preflight:", 1)[1]

    def test_solo_es_manual_y_no_tiene_triggers_automaticos(self):
        self.assertIn("workflow_dispatch:", self.on_block)
        self.assertNotIn("push:", self.on_block)
        self.assertNotIn("pull_request:", self.on_block)
        self.assertNotIn("pull_request_target:", self.on_block)
        self.assertNotIn("schedule:", self.on_block)

    def test_solo_permite_main_y_usa_environment_azure_dev(self):
        self.assertIn("if: ${{ github.ref == 'refs/heads/main' }}", self.job)
        self.assertIn("environment: azure-dev", self.job)

    def test_permisos_minimos(self):
        top = self.text.split("jobs:", 1)[0]
        self.assertIn("permissions:\n  contents: read", top)
        self.assertIn("contents: read", self.job)
        self.assertIn("id-token: write", self.job)
        self.assertNotIn("contents: write", self.text)
        self.assertNotIn("actions: write", self.text)
        self.assertNotIn("pull-requests: write", self.text)

    def test_autenticacion_oidc_pinned_y_sin_credenciales_estaticas(self):
        self.assertIn(
            "uses: azure/login@a641126d1b8aa4d1fa005f4f92df94a3a4c4c906 # v3.1.0",
            self.text)
        self.assertIn("client-id: ${{ secrets.AZURE_CLIENT_ID }}", self.text)
        self.assertIn("tenant-id: ${{ secrets.AZURE_TENANT_ID }}", self.text)
        self.assertIn("subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}", self.text)
        lower = self.text.lower()
        self.assertNotIn("client-secret", lower)
        self.assertNotIn("azure_client_secret", lower)
        self.assertNotIn("creds:", lower)

    def test_no_usa_codigo_del_repositorio(self):
        self.assertNotIn("actions/checkout", self.text)

    def test_solo_hay_consultas_de_lectura(self):
        required_reads = (
            "az account show",
            "az containerapp show",
            "az containerapp revision list",
            "az acr show",
            "az acr login --name acrunihomedev --expose-token",
        )
        for command in required_reads:
            self.assertIn(command, self.text)

        forbidden = (
            "docker push",
            "docker build",
            "docker login",
            "az containerapp update",
            "az containerapp create",
            "az containerapp delete",
            "az containerapp revision restart",
            "az acr build",
            "az acr import",
            "az acr login --password",
            "az acr login --username",
            "az acr login --resource-group",
            "az role assignment create",
            "az group create",
            "az group delete",
            "az deployment",
            "terraform apply",
            "git push",
            "gh pr merge",
            "curl -x post",
            "curl -x put",
            "curl -x patch",
            "curl -x delete",
        )
        lower = self.text.lower()
        for command in forbidden:
            self.assertNotIn(command, lower)

    def test_publica_resumen_y_distingue_login_de_rbac(self):
        self.assertIn("GITHUB_STEP_SUMMARY", self.text)
        self.assertIn("steps.azure_login.outcome", self.text)
        self.assertIn("autenticación OIDC", self.text)
        self.assertIn("AuthorizationFailed", self.text)
        self.assertIn("RBAC", self.text)
        self.assertIn("RBAC_LIMITED", self.text)
        self.assertIn("ACR auth", self.text)
        self.assertIn("ACR_AUTH_RESULT", self.text)
        self.assertIn("Causa: $cause", self.text)
        self.assertIn("no ejecutado", self.text)

    def test_acr_auth_es_aislada_sin_docker_ni_tokens(self):
        self.assertIn("az acr login --name acrunihomedev --expose-token", self.text)
        self.assertIn(">/dev/null 2>\"$acr_auth_log\"", self.text)
        self.assertIn('ACR_AUTH_RESULT=%s', self.text)
        self.assertNotIn("docker login", self.text.lower())
        self.assertNotIn("docker push", self.text.lower())
        self.assertNotIn("--password", self.text.lower())
        self.assertNotIn("--username", self.text.lower())

    def test_concurrencia_separada_y_sin_cancelacion(self):
        self.assertIn("group: azure-oidc-preflight-", self.text)
        self.assertIn("cancel-in-progress: false", self.text)
        self.assertNotIn("cancel-in-progress: true", self.text)

    def test_no_expone_tokens_ni_respuestas_sensibles(self):
        lower = self.text.lower()
        self.assertNotIn("echo $actions_id_token", lower)
        self.assertNotIn("authorization:", lower)
        self.assertNotIn("bearer ", lower)
        self.assertNotIn("az account get-access-token", lower)
        secret_refs = set(re.findall(r"secrets\.([A-Z0-9_]+)", self.text))
        self.assertEqual(
            secret_refs,
            {"AZURE_CLIENT_ID", "AZURE_TENANT_ID", "AZURE_SUBSCRIPTION_ID"},
        )


if __name__ == "__main__":
    unittest.main()
