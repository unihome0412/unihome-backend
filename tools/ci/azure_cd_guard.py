"""Reglas puras de autorización y recuperación del CD Azure.

Las pruebas de este módulo simulan condiciones sin autenticar Azure ni ejecutar
comandos. El workflow sigue siendo la fuente ejecutable; estas funciones fijan
las invariantes que no deben perderse.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable

DEPLOY_JOB_DEPENDENCIES = (
    "detect-changes",
    "build-test",
    "contract-test",
    "docker-build",
    "container-lint",
    "pr-quality-gate",
    "hardening-report",
)


@dataclass(frozen=True)
class DeployDecision:
    allowed: bool
    reason: str


def deploy_allowed(
    *,
    event_name: str,
    ref: str,
    deploy_input: bool,
    enabled: bool,
    code_changed: bool,
    needs: dict[str, str],
    required_jobs: Iterable[str] = DEPLOY_JOB_DEPENDENCIES,
) -> DeployDecision:
    if event_name != "workflow_dispatch":
        return DeployDecision(False, "el deploy solo se permite en workflow_dispatch")
    if ref != "refs/heads/main":
        return DeployDecision(False, "el deploy solo se permite desde main")
    if deploy_input is not True:
        return DeployDecision(False, "falta el input manual deploy_to_azure=true")
    if enabled is not True:
        return DeployDecision(False, "UNIHOME_ENABLE_AZURE_CD no está activo")
    if not code_changed:
        return DeployDecision(False, "no hay cambios de código que desplegar")
    for job in required_jobs:
        if needs.get(job) != "success":
            return DeployDecision(False, f"{job} no está en success")
    return DeployDecision(True, "autorización y gates completos")


def _is_manual_deploy(event_name: str, deploy_input: object) -> bool:
    return event_name == "workflow_dispatch" and str(deploy_input).lower() == "true"


def concurrency_key(event_name: str, ref: str, deploy_input: object = None) -> str:
    lane = "cd" if _is_manual_deploy(event_name, deploy_input) else "ci"
    return f"unihome-devsecops-ci.yml-{event_name}-{lane}-{ref}"


def cancel_in_progress(event_name: str, deploy_input: object = None) -> bool:
    return not _is_manual_deploy(event_name, deploy_input)


def recovery_actions(
    *,
    update_started: bool,
    revision_running: bool,
    fqdn_available: bool,
    health_ok: bool,
) -> list[str]:
    if not update_started:
        return []
    if not revision_running:
        return ["rollback"]
    if not fqdn_available:
        return ["rollback"]
    if not health_ok:
        return ["rollback"]
    return []


def rollback_outcome(
    *,
    image_update_ok: bool,
    fqdn_available: bool,
    health_ok: bool,
) -> str:
    if not image_update_ok:
        return "rollback_failed"
    if not fqdn_available:
        return "rollback_unverified"
    if not health_ok:
        return "rollback_unhealthy"
    return "rollback_verified"


def cancellation_limits() -> str:
    return (
        "Un trap EXIT cubre fallos de query de FQDN, revisión o health dentro "
        "del paso. Una cancelación externa del job o la pérdida del runner "
        "puede impedir el rollback; requiere revisión manual de la revisión "
        "activa."
    )
