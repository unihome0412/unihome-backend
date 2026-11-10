"""Pruebas estáticas y simuladas del CD Azure, sin red ni Azure."""
import re
import unittest
from pathlib import Path

from azure_cd_guard import (
    DEPLOY_JOB_DEPENDENCIES,
    cancel_in_progress,
    cancellation_limits,
    concurrency_key,
    deploy_allowed,
    recovery_actions,
    rollback_outcome,
)


class AzureCdWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = Path(".github/workflows/ci.yml").read_text(encoding="utf-8")
        cls.before_jobs = cls.workflow.split("jobs:", 1)[0]
        cls.deploy = cls.workflow.split("  deploy-azure:", 1)[1]

    def test_ci_y_cd_tienen_grupos_de_concurrencia_separados(self):
        self.assertIn(
            "(github.event_name == 'workflow_dispatch' && github.event.inputs.deploy_to_azure == 'true') && 'cd' || 'ci'",
            self.before_jobs)
        self.assertIn(
            "cancel-in-progress: ${{ !(github.event_name == 'workflow_dispatch' && github.event.inputs.deploy_to_azure == 'true') }}",
            self.before_jobs)
        self.assertIn("group: azure-dev-deploy-${{ github.ref }}", self.deploy)
        self.assertIn("cancel-in-progress: false", self.deploy)

    def test_push_y_deploy_no_comparten_grupo_ni_cancelacion(self):
        push_key = concurrency_key("push", "refs/heads/main")
        deploy_key = concurrency_key(
            "workflow_dispatch", "refs/heads/main", "true")
        self.assertNotEqual(push_key, deploy_key)
        self.assertTrue(cancel_in_progress("push"))
        self.assertFalse(cancel_in_progress("workflow_dispatch", "true"))
        self.assertEqual(
            deploy_key,
            concurrency_key("workflow_dispatch", "refs/heads/main", True))

    def test_deploy_solo_es_manual_desde_main_con_todas_las_autorizaciones(self):
        self.assertIn("github.event_name == 'workflow_dispatch'", self.deploy)
        self.assertIn("github.ref == 'refs/heads/main'", self.deploy)
        self.assertIn("inputs.deploy_to_azure == true", self.deploy)
        self.assertIn("vars.UNIHOME_ENABLE_AZURE_CD == 'true'", self.deploy)
        for job in DEPLOY_JOB_DEPENDENCIES:
            self.assertIn(job, self.deploy)

    def test_deploy_tiene_oidc_environment_y_permisos_minimos(self):
        self.assertIn("environment: azure-dev", self.deploy)
        self.assertIn("id-token: write", self.deploy)
        self.assertIn("contents: read", self.deploy)
        self.assertIn("actions: read", self.deploy)
        self.assertNotIn("client-secret", self.deploy)

    def test_descarga_artifact_esta_fijada_por_sha(self):
        self.assertIn(
            "actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093",
            self.workflow)
        self.assertNotIn("actions/download-artifact@v4", self.workflow)

    def test_trazabilidad_y_rollback_estan_presentes(self):
        self.assertIn("IMAGE_DIGEST", self.deploy)
        self.assertIn("digest: \\(sha256:[0-9a-f]*\\)", self.deploy)
        self.assertIn("PREVIOUS_FQDN", self.deploy)
        self.assertIn("properties.runningState", self.deploy)
        self.assertIn("activeRevisionsMode", self.deploy)
        self.assertIn("trap on_exit EXIT", self.deploy)
        self.assertIn("PREVIOUS_IMAGE", self.deploy)
        self.assertIn("rollback()", self.deploy)
        self.assertIn("previous_healthy", self.deploy)
        self.assertIn("rollback_healthy", self.deploy)
        self.assertIn("Rollback verificado", self.deploy)

    def test_no_se_declara_inmutable_el_tag(self):
        self.assertNotIn("imagen inmutable", self.workflow)
        self.assertIn("etiquetada por SHA", self.deploy)


class DeployGuardTests(unittest.TestCase):
    def setUp(self):
        self.needs = {job: "success" for job in DEPLOY_JOB_DEPENDENCIES}

    def test_pr_y_push_nunca_autorizan(self):
        self.assertFalse(deploy_allowed(
            event_name="pull_request", ref="refs/heads/main", deploy_input=False,
            enabled=True, code_changed=True, needs=self.needs).allowed)
        self.assertFalse(deploy_allowed(
            event_name="push", ref="refs/heads/main", deploy_input=True,
            enabled=True, code_changed=True, needs=self.needs).allowed)

    def test_manual_completo_autoriza(self):
        decision = deploy_allowed(
            event_name="workflow_dispatch", ref="refs/heads/main",
            deploy_input=True, enabled=True, code_changed=True, needs=self.needs)
        self.assertTrue(decision.allowed)

    def test_manual_sin_input_variable_o_gate_no_autoriza(self):
        cases = (
            dict(deploy_input=False, enabled=True, code_changed=True),
            dict(deploy_input=True, enabled=False, code_changed=True),
            dict(deploy_input=True, enabled=True, code_changed=False),
        )
        for case in cases:
            self.assertFalse(deploy_allowed(
                event_name="workflow_dispatch", ref="refs/heads/main",
                needs=self.needs, **case).allowed)

    def test_un_gate_fallido_bloquea(self):
        needs = dict(self.needs)
        needs["hardening-report"] = "skipped"
        self.assertFalse(deploy_allowed(
            event_name="workflow_dispatch", ref="refs/heads/main",
            deploy_input=True, enabled=True, code_changed=True, needs=needs).allowed)

    def test_rama_no_main_bloquea(self):
        self.assertFalse(deploy_allowed(
            event_name="workflow_dispatch", ref="refs/heads/feature/ci-cd-azure-prep",
            deploy_input=True, enabled=True, code_changed=True, needs=self.needs).allowed)


class RecoverySimulationTests(unittest.TestCase):
    def test_sin_update_no_hay_rollback(self):
        self.assertEqual([], recovery_actions(
            update_started=False, revision_running=False,
            fqdn_available=False, health_ok=False))

    def test_fallo_de_revision_fqdn_o_health_programa_rollback(self):
        for case in (
            dict(revision_running=False, fqdn_available=True, health_ok=True),
            dict(revision_running=True, fqdn_available=False, health_ok=True),
            dict(revision_running=True, fqdn_available=True, health_ok=False),
        ):
            self.assertEqual(["rollback"], recovery_actions(
                update_started=True, **case))

    def test_exito_no_programa_rollback(self):
        self.assertEqual([], recovery_actions(
            update_started=True, revision_running=True,
            fqdn_available=True, health_ok=True))

    def test_rollback_solo_se_declara_verificado_si_imagen_fqdn_y_salud_pasan(self):
        self.assertEqual("rollback_verified", rollback_outcome(
            image_update_ok=True, fqdn_available=True, health_ok=True))
        self.assertEqual("rollback_failed", rollback_outcome(
            image_update_ok=False, fqdn_available=True, health_ok=True))
        self.assertEqual("rollback_unverified", rollback_outcome(
            image_update_ok=True, fqdn_available=False, health_ok=True))
        self.assertEqual("rollback_unhealthy", rollback_outcome(
            image_update_ok=True, fqdn_available=True, health_ok=False))

    def test_limites_de_cancelacion_documentados(self):
        self.assertIn("cancelación externa", cancellation_limits())


if __name__ == "__main__":
    unittest.main()
