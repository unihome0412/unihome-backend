#!/usr/bin/env python3
"""DAST pasivo y determinista con OWASP ZAP.

La API efímera se ejecuta en loopback. Este proceso:

1. envía tráfico real a través del proxy de ZAP (salud, OpenAPI, Swagger y
   todas las operaciones documentadas, incluidas las autenticadas);
2. espera a que ZAP termine el análisis pasivo;
3. genera reportes HTML/JSON de ZAP y un resumen Markdown con cobertura;
4. falla si faltan rutas, reportes o si hay alertas High/Critical.

No se ejecuta Active Scan, no se usa `-I`, no se aplican exclusiones y no se
desactivan reglas pasivas. Las credenciales y los tokens son sintéticos y
efímeros; el código de confirmación se obtiene de un SMTP de captura local.
"""
from __future__ import annotations

import argparse
import email
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from email import policy
from pathlib import Path
from typing import Any

PASSWORD = "ClaveSegura1"
PRIVACY_VERSION = "2026-01"
EXPECTED_ZAP_VERSION = "2.17.0"
RISK_ORDER = ("Critical", "High", "Medium", "Low", "Informational")
RISK_BY_CODE = {"3": "High", "2": "Medium", "1": "Low", "0": "Informational"}
CONFIDENCE_BY_CODE = {
    "0": "False Positive",
    "1": "Low",
    "2": "Medium",
    "3": "High",
    "4": "Confirmed",
}
HTTP_METHODS = ("get", "post", "put", "patch", "delete", "head", "options")
AUTHENTICATED_OPERATIONS = {
    ("POST", "/api/v1/auth/logout"),
    ("DELETE", "/api/v1/cuentas/mi-cuenta"),
}
SENSITIVE_CACHE_PATHS = {
    "/api/v1/auth/registro",
    "/api/v1/auth/confirmacion-correo",
    "/api/v1/auth/confirmacion-correo/reenvio",
    "/api/v1/auth/login",
    "/api/v1/auth/refresh",
    "/api/v1/auth/logout",
    "/api/v1/cuentas/mi-cuenta",
}
SECURITY_HEADERS = {
    "x-content-type-options": ("nosniff",),
    "x-frame-options": ("DENY", "SAMEORIGIN"),
    "referrer-policy": None,
    "permissions-policy": None,
    "content-security-policy": None,
}


class DastError(RuntimeError):
    """Error controlado que impide considerar válido el escaneo."""


@dataclass
class ResponseRecord:
    method: str
    path: str
    status: int
    headers: dict[str, str]
    auth: bool = False
    openapi: bool = True
    note: str = ""

    def as_dict(self) -> dict[str, Any]:
        return {
            "method": self.method,
            "path": self.path,
            "status": self.status,
            "auth": self.auth,
            "openapi": self.openapi,
            "note": self.note,
            "headers": self.headers,
        }


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class HttpClient:
    """Cliente HTTP que puede enviar todo su tráfico por el proxy de ZAP."""

    def __init__(self, target: str, proxy: str | None) -> None:
        self.target = target.rstrip("/")
        self.records: list[ResponseRecord] = []
        for key in ("NO_PROXY", "no_proxy"):
            os.environ.pop(key, None)

        handlers: list[Any] = [_NoRedirect()]
        if proxy:
            handlers.append(urllib.request.ProxyHandler({"http": proxy, "https": proxy}))
        else:
            handlers.append(urllib.request.ProxyHandler({}))
        self._opener = urllib.request.build_opener(*handlers)

    def request(
        self,
        method: str,
        path: str,
        *,
        body: dict[str, Any] | None = None,
        token: str | None = None,
        expected: set[int] | None = None,
        auth: bool = False,
        openapi: bool = True,
        note: str = "",
    ) -> tuple[int, bytes, dict[str, str]]:
        headers = {
            "Accept": "application/json, text/html;q=0.9, */*;q=0.1",
            "User-Agent": "UniHome-DAST/1.0",
        }
        data = None
        if body is not None:
            data = json.dumps(body).encode("utf-8")
            headers["Content-Type"] = "application/json"
        if token:
            headers["Authorization"] = f"Bearer {token}"

        request = urllib.request.Request(
            self.target + path, data=data, headers=headers, method=method)
        try:
            with self._opener.open(request, timeout=30) as response:
                status = response.status
                payload = response.read()
                response_headers = _header_dict(response.headers)
        except urllib.error.HTTPError as error:
            status = error.code
            payload = error.read()
            response_headers = _header_dict(error.headers)
        except Exception as error:  # pragma: no cover - se valida en Actions
            raise DastError(f"No se pudo ejecutar {method} {path}: {error}") from error

        self.records.append(ResponseRecord(
            method=method,
            path=path,
            status=status,
            headers=response_headers,
            auth=auth,
            openapi=openapi,
            note=note,
        ))

        if expected is not None and status not in expected:
            preview = payload[:240].decode("utf-8", errors="replace")
            raise DastError(
                f"{method} {path}: se esperaba {sorted(expected)} y se recibió "
                f"{status}. Cuerpo: {preview!r}")

        return status, payload, response_headers


class ZapApi:
    """Cliente mínimo de la API local de ZAP, sin dependencias externas."""

    def __init__(self, base_url: str, api_key: str) -> None:
        self._base = base_url.rstrip("/")
        self._key = api_key
        self._opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

    def _url(self, path: str) -> str:
        normalized = path.strip("/")
        return f"{self._base}/{normalized}/?apikey={urllib.parse.quote(self._key)}"

    def raw(self, path: str) -> str:
        try:
            with self._opener.open(self._url(path), timeout=30) as response:
                return response.read().decode("utf-8", errors="replace")
        except urllib.error.HTTPError as error:
            raise DastError(f"La API de ZAP respondió HTTP {error.code} en {path}.") from error
        except urllib.error.URLError as error:
            raise DastError(f"No fue posible contactar la API de ZAP: {error.reason}.") from error

    def json(self, path: str) -> dict[str, Any]:
        return json.loads(self.raw(path))

    def version(self) -> str:
        try:
            return str(self.json("JSON/core/view/version")["version"])
        except (KeyError, TypeError, json.JSONDecodeError) as error:
            raise DastError("ZAP no devolvió una versión válida.") from error

    def prepare_passive_scan(self) -> None:
        # Habilita todas las reglas pasivas instaladas; no se excluye ninguna.
        self.raw("JSON/pscan/action/enableAllScanners")

    def message_count(self) -> int:
        return int(self.json("JSON/core/view/numberOfMessages")["numberOfMessages"])

    def records_to_scan(self) -> int:
        return int(self.json("JSON/pscan/view/recordsToScan")["recordsToScan"])

    def wait_for_passive(
        self,
        expected_messages: int,
        timeout_seconds: int = 120,
    ) -> tuple[int, int]:
        deadline = time.time() + timeout_seconds
        messages = 0
        records = 0
        zeros = 0
        time.sleep(2)

        # ZAP procesa el historial de forma asíncrona. Se exige que haya visto
        # el tráfico esperado y que el contador pasivo permanezca en cero.
        while time.time() < deadline:
            messages = self.message_count()
            records = self.records_to_scan()
            if messages >= expected_messages and records == 0:
                zeros += 1
                if zeros >= 2:
                    return messages, records
            else:
                zeros = 0
            time.sleep(1)

        raise DastError(
            "ZAP no terminó el análisis pasivo: "
            f"mensajes={messages}, pendientes={records}, esperados={expected_messages}.")

    def write_reports(self, output: Path) -> dict[str, Any]:
        html = _unwrap_report(self.raw("OTHER/core/other/htmlreport"), "htmlreport")
        if "<html" not in html.lower():
            raise DastError("El reporte HTML de ZAP está vacío o no es HTML.")
        (output / "zap-report.html").write_text(html, encoding="utf-8")

        json_text = _unwrap_report(self.raw("OTHER/core/other/jsonreport"), "jsonreport")
        try:
            report = json.loads(json_text)
        except json.JSONDecodeError as error:
            raise DastError("El reporte JSON de ZAP no es JSON válido.") from error
        if not isinstance(report, dict):
            raise DastError("El reporte JSON de ZAP no tiene el formato esperado.")
        (output / "zap-report.json").write_text(
            json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

        # El reporte Markdown de ZAP es complementario; el resumen propio es
        # siempre obligatorio y contiene cobertura y evidencia del job.
        try:
            md = _unwrap_report(self.raw("OTHER/core/other/mdreport"), "mdreport")
            if md.strip():
                (output / "zap-report.md").write_text(md, encoding="utf-8")
        except (urllib.error.URLError, DastError):
            pass

        return report


def _header_dict(headers: Any) -> dict[str, str]:
    return {key.lower(): value for key, value in headers.items()}


def _unwrap_report(raw: str, name: str) -> str:
    """Acepta tanto el cuerpo crudo como el envoltorio JSON de la API."""
    value = raw.strip()
    if not value.startswith("{"):
        return raw
    try:
        parsed = json.loads(value)
    except json.JSONDecodeError:
        return raw
    if isinstance(parsed, dict) and isinstance(parsed.get(name), str):
        return parsed[name]
    return raw


def parse_openapi_operations(spec: dict[str, Any]) -> list[dict[str, Any]]:
    operations: list[dict[str, Any]] = []
    paths = spec.get("paths")
    if not isinstance(paths, dict) or not paths:
        raise DastError("OpenAPI no contiene rutas; no hay cobertura que verificar.")

    root_security = spec.get("security")
    for path, item in paths.items():
        if not isinstance(item, dict):
            continue
        for method in HTTP_METHODS:
            operation = item.get(method)
            if not isinstance(operation, dict):
                continue
            security = operation.get("security", item.get("security", root_security))
            operations.append({
                "method": method.upper(),
                "path": path,
                "auth": bool(security),
            })

    if not operations:
        raise DastError("OpenAPI no contiene operaciones HTTP.")
    return sorted(operations, key=lambda value: (value["path"], value["method"]))


def build_coverage(
    operations: list[dict[str, Any]],
    records: list[ResponseRecord],
) -> dict[str, Any]:
    operation_keys = {(item["method"], item["path"]) for item in operations}
    routes: list[dict[str, Any]] = []
    missing: list[str] = []

    for operation in operations:
        key = (operation["method"], operation["path"])
        matches = [
            record for record in records
            if (record.method, record.path) == key
        ]
        statuses = sorted({record.status for record in matches})
        visited = any(200 <= status < 300 for status in statuses)
        if not visited:
            missing.append(f"{operation['method']} {operation['path']}")
        routes.append({
            "method": operation["method"],
            "path": operation["path"],
            "auth_required": bool(operation["auth"]) or key in AUTHENTICATED_OPERATIONS,
            "openapi": True,
            "visited": visited,
            "statuses": statuses,
            "notes": sorted({record.note for record in matches if record.note}),
        })

    extras: dict[tuple[str, str], list[ResponseRecord]] = {}
    for record in records:
        key = (record.method, record.path)
        if key not in operation_keys:
            extras.setdefault(key, []).append(record)

    support_routes = []
    for (method, path), matches in sorted(extras.items()):
        support_routes.append({
            "method": method,
            "path": path,
            "openapi": False,
            "statuses": sorted({record.status for record in matches}),
            "notes": sorted({record.note for record in matches if record.note}),
        })

    return {
        "openapi_operations": len(operations),
        "openapi_visited": sum(1 for route in routes if route["visited"]),
        "routes": routes,
        "support_routes": support_routes,
        "missing": missing,
    }


def parse_zap_report(report: dict[str, Any]) -> dict[str, Any]:
    unique: dict[tuple[str, str], dict[str, Any]] = {}
    severity_counts = {level: 0 for level in RISK_ORDER}
    instance_counts = {level: 0 for level in RISK_ORDER}
    sites = report.get("site") or []
    if isinstance(sites, dict):
        sites = [sites]

    for site in sites:
        if not isinstance(site, dict):
            continue
        for alert in site.get("alerts") or []:
            if not isinstance(alert, dict):
                continue
            risk = _risk_label(alert)
            name = str(alert.get("name") or "Sin nombre")
            key = (risk, name)
            instances = alert.get("instances") or []
            if not isinstance(instances, list):
                instances = []
            item = unique.setdefault(key, {
                "risk": risk,
                "name": name,
                "confidence": _confidence_label(alert),
                "instances": 0,
                "uris": set(),
            })
            item["instances"] += len(instances)
            for instance in instances:
                if isinstance(instance, dict) and instance.get("uri"):
                    item["uris"].add(str(instance["uri"]))

    alerts = sorted(unique.values(), key=lambda value: (
        RISK_ORDER.index(value["risk"]) if value["risk"] in RISK_ORDER else 99,
        value["name"],
    ))
    for alert in alerts:
        severity_counts[alert["risk"]] += 1
        instance_counts[alert["risk"]] += alert["instances"]
        alert["uris"] = sorted(alert["uris"])

    return {
        "severity_counts": severity_counts,
        "instance_counts": instance_counts,
        "alerts": alerts,
        "unique_alerts": sum(severity_counts.values()),
        "instances": sum(instance_counts.values()),
    }


def _risk_label(alert: dict[str, Any]) -> str:
    riskdesc = str(alert.get("riskdesc") or "").split(" ", 1)[0]
    if riskdesc in RISK_ORDER:
        return riskdesc
    code = str(alert.get("riskcode") or "")
    return RISK_BY_CODE.get(code, "Informational")


def _confidence_label(alert: dict[str, Any]) -> str:
    confidence = str(alert.get("confidence") or "").strip()
    if confidence in CONFIDENCE_BY_CODE:
        return CONFIDENCE_BY_CODE[confidence]
    if confidence:
        return confidence
    riskdesc = str(alert.get("riskdesc") or "")
    match = re.search(r"\(([^)]+)\)", riskdesc)
    return match.group(1) if match else "n/d"


def extract_verification_code(message_bytes: bytes, recipient: str) -> str | None:
    message = email.message_from_bytes(message_bytes, policy=policy.default)
    recipients = " ".join(
        str(value) for field in ("To", "Cc") for value in message.get_all(field, [])
    ).lower()
    if recipient.lower() not in recipients:
        return None

    for part in message.walk():
        if part.get_content_type() == "text/plain":
            try:
                text = part.get_content()
            except Exception:  # pragma: no cover - mensajes MIME dañados
                continue
            match = re.search(r"(?<!\d)(\d{6})(?!\d)", str(text))
            if match:
                return match.group(1)

    for part in message.walk():
        if part.get_content_type() == "text/html":
            try:
                text = part.get_content()
            except Exception:  # pragma: no cover - mensajes MIME dañados
                continue
            text = re.sub(r"<[^>]+>", " ", str(text))
            match = re.search(r"(?<!\d)(\d{6})(?!\d)", text)
            if match:
                return match.group(1)
    return None


def wait_for_verification_code(
    mailbox: Path,
    recipient: str,
    timeout_seconds: int = 45,
) -> str:
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        for path in sorted(mailbox.glob("*.eml")):
            try:
                code = extract_verification_code(path.read_bytes(), recipient)
            except OSError:
                continue
            if code:
                return code
        time.sleep(0.5)
    raise DastError(
        f"El SMTP de captura no recibió un código para {recipient} en "
        f"{timeout_seconds}s. El flujo de registro no puede continuar.")


def security_findings(records: list[ResponseRecord]) -> list[dict[str, str]]:
    findings: list[dict[str, str]] = []
    representative = [
        record for record in records
        if record.path in {"/health/live", "/openapi/v1.json",
                           "/api/v1/auth/registro", "/api/v1/auth/login"}
    ]
    reported: set[tuple[str, str, str]] = set()
    for record in representative:
        for header, expected in SECURITY_HEADERS.items():
            key = (header, record.method, record.path)
            if key in reported:
                continue
            reported.add(key)
            value = record.headers.get(header)
            if value is None:
                findings.append({
                    "severity": "WARN",
                    "header": header,
                    "finding": f"ausente en {record.method} {record.path}",
                })
            elif expected and value.strip().lower() not in {
                item.lower() for item in expected
            }:
                findings.append({
                    "severity": "WARN",
                    "header": header,
                    "finding": f"valor inesperado en {record.method} {record.path}: {value}",
                })

    cache_paths: set[str] = set()
    for record in records:
        if record.path not in SENSITIVE_CACHE_PATHS or record.path in cache_paths:
            continue
        cache = record.headers.get("cache-control", "")
        if "no-store" not in cache.lower():
            cache_paths.add(record.path)
            findings.append({
                "severity": "WARN",
                "header": "cache-control",
                "finding": f"sin no-store en {record.path}",
            })

    cookies = [
        (record, value) for record in records
        for value in [record.headers.get("set-cookie")] if value
    ]
    if not cookies:
        findings.append({
            "severity": "INFO",
            "header": "set-cookie",
            "finding": "no se observaron cookies; la sesión usa Bearer JWT",
        })
    else:
        for record, value in cookies:
            lower = value.lower()
            missing = [name for name in ("httponly", "secure", "samesite") if name not in lower]
            findings.append({
                "severity": "WARN" if missing else "INFO",
                "header": "set-cookie",
                "finding": f"{record.path}: faltan atributos {', '.join(missing)}" if missing
                else f"{record.path}: atributos de cookie presentes",
            })

    server = next((record.headers.get("server") for record in records if record.headers.get("server")), None)
    if server:
        severity = "WARN" if re.search(r"\d", server) else "INFO"
        findings.append({
            "severity": severity,
            "header": "server",
            "finding": server,
        })
    powered_by = next((record.headers.get("x-powered-by") for record in records
                       if record.headers.get("x-powered-by")), None)
    if powered_by:
        findings.append({
            "severity": "WARN",
            "header": "x-powered-by",
            "finding": powered_by,
        })

    return findings


def classify_status(
    technical_errors: list[str],
    missing_routes: list[str],
    severity_counts: dict[str, int],
    warning_findings: int = 0,
) -> str:
    if technical_errors or missing_routes:
        return "ERROR"
    if severity_counts.get("Critical", 0) or severity_counts.get("High", 0):
        return "FAIL"
    if severity_counts.get("Medium", 0) or warning_findings:
        return "WARN"
    return "PASS"


def render_markdown(
    *,
    status: str,
    target: str,
    zap_version: str,
    zap_image: str,
    generated_at: str,
    health: dict[str, Any],
    coverage: dict[str, Any],
    scan: dict[str, Any],
    headers: list[dict[str, str]],
    technical_errors: list[str],
) -> str:
    lines = [
        "# UniHome DAST — OWASP ZAP Baseline pasivo",
        "",
        f"- **Estado:** **{status}**",
        f"- **Fecha UTC:** {generated_at}",
        f"- **Objetivo:** `{target}`",
        f"- **ZAP:** `{zap_version}`",
        f"- **Imagen fijada:** `{zap_image}`",
        "- **Modo:** Baseline pasivo; no se ejecuta Active Scan ni Full Scan.",
        "- **Controles:** sin `-I`, sin `continue-on-error`, sin listas de exclusión "
        "y sin desactivar reglas pasivas.",
        "- **Criterio:** High/Critical bloquean; Medium produce WARN; un fallo "
        "técnico o una ruta no visitada produce ERROR.",
        "",
        "## Arranque y salud de la API",
        "",
    ]
    for name, item in health.items():
        lines.append(
            f"- `/{name}`: HTTP {item.get('status_code')} / estado "
            f"`{item.get('status')}`")
    lines += [
        "",
        "## Cobertura de rutas OpenAPI",
        "",
        f"- Operaciones documentadas: **{coverage.get('openapi_operations', 0)}**.",
        f"- Operaciones visitadas realmente: **{coverage.get('openapi_visited', 0)}**.",
        f"- Rutas faltantes: **{len(coverage.get('missing', []))}**.",
        "",
        "| Método | Ruta | Auth | OpenAPI | Respuestas | Visitada |",
        "|---|---|---|---|---|---|",
    ]
    for route in coverage.get("routes", []):
        statuses = ", ".join(str(value) for value in route.get("statuses", [])) or "—"
        lines.append(
            f"| {route['method']} | `{route['path']}` | "
            f"{'sí' if route.get('auth_required') else 'no'} | sí | "
            f"{statuses} | {'sí' if route.get('visited') else '**NO**'} |")
    for route in coverage.get("support_routes", []):
        statuses = ", ".join(str(value) for value in route.get("statuses", [])) or "—"
        lines.append(
            f"| {route['method']} | `{route['path']}` | no | no | "
            f"{statuses} | sí (soporte) |")
    if coverage.get("missing"):
        lines += ["", "Rutas no visitadas: " + ", ".join(
            f"`{value}`" for value in coverage["missing"])]

    lines += [
        "",
        "## Alertas ZAP por severidad",
        "",
        "| Severidad | Alertas únicas | Instancias |",
        "|---|---:|---:|",
    ]
    for risk in RISK_ORDER:
        lines.append(
            f"| {risk} | {scan['severity_counts'].get(risk, 0)} | "
            f"{scan['instance_counts'].get(risk, 0)} |")
    if scan.get("alerts"):
        lines += [
            "",
            "| Severidad | Regla | Confianza | Instancias |",
            "|---|---|---|---:|",
        ]
        for alert in scan["alerts"]:
            lines.append(
                f"| {alert['risk']} | {alert['name']} | {alert['confidence']} | "
                f"{alert['instances']} |")
    else:
        lines += ["", "No se reportaron alertas ZAP."]

    lines += [
        "",
        "## Hallazgos de cabeceras y sesión",
        "",
        "| Severidad | Cabecera | Hallazgo |",
        "|---|---|---|",
    ]
    if headers:
        for finding in headers:
            lines.append(
                f"| {finding['severity']} | `{finding['header']}` | "
                f"{finding['finding']} |")
    else:
        lines.append("| INFO | — | Sin hallazgos adicionales |")

    lines += [
        "",
        "## Alcance y evidencia",
        "",
        "- La API se ejecutó en loopback con MongoDB efímero y un SMTP de "
        "captura local; no se usaron Atlas, Brevo, Azure ni secretos reales.",
        "- Las cuentas se crearon y confirmaron mediante los endpoints reales de "
        "la API. Los tokens no se guardaron en los artefactos.",
        "- El HTTP local es intencional para el runner aislado; no hay puertos "
        "públicos ni despliegue. HSTS/HTTPS no aplica a este loopback.",
        "- Fuera de alcance: Active Scan, Full Scan, pruebas de carga, datos "
        "reales, Atlas, Brevo y Azure.",
        "- Reportes: `zap-report.html`, `zap-report.json`, "
        "`dast-report.md`, `route-coverage.json` y `security-headers.json`.",
    ]
    if technical_errors:
        lines += ["", "## Errores técnicos", ""]
        lines += [f"- {error}" for error in technical_errors]
    lines.append("")
    return "\n".join(lines)


def _write_json(path: Path, value: Any) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def _register_payload(email: str) -> dict[str, Any]:
    return {
        "nombreCompleto": "Cuenta DAST",
        "correo": email,
        "fechaNacimiento": "2004-05-10",
        "genero": "Mujer",
        "contrasena": PASSWORD,
        "confirmarContrasena": PASSWORD,
        "aceptoAvisoPrivacidad": True,
        "versionAvisoPrivacidad": PRIVACY_VERSION,
    }


def _json_body(payload: bytes) -> dict[str, Any]:
    try:
        value = json.loads(payload)
    except json.JSONDecodeError as error:
        raise DastError("La API devolvió un cuerpo que no es JSON.") from error
    if not isinstance(value, dict):
        raise DastError("La API devolvió un JSON con formato inesperado.")
    return value


def run_scan(args: argparse.Namespace) -> int:
    target = args.target.rstrip("/")
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    mailbox = Path(args.mailbox)
    generated_at = datetime.now(timezone.utc).isoformat()
    technical_errors: list[str] = []
    health: dict[str, Any] = {}

    http = HttpClient(target, args.proxy)
    zap = ZapApi(args.zap_api, args.zap_api_key)

    version = zap.version()
    if version != EXPECTED_ZAP_VERSION:
        technical_errors.append(
            f"Versión ZAP inesperada: {version}; se esperaba {EXPECTED_ZAP_VERSION}.")
    zap.prepare_passive_scan()

    for name in ("health/live", "health/ready"):
        status, payload, _ = http.request(
            "GET", f"/{name}", expected={200}, openapi=False, note="salud")
        body = _json_body(payload)
        health[name.replace("/", "-")] = {
            "status_code": status,
            "status": body.get("status"),
        }
        if body.get("status") != "Healthy":
            raise DastError(f"/{name} no reportó Healthy: {body.get('status')!r}")
    _write_json(output / "api-health.json", health)

    _, spec_payload, _ = http.request(
        "GET", "/openapi/v1.json", expected={200}, openapi=False, note="OpenAPI")
    spec = _json_body(spec_payload)
    operations = parse_openapi_operations(spec)
    _write_json(output / "openapi-operations.json", operations)

    http.request("GET", "/swagger/index.html", expected={200}, openapi=False,
                 note="Swagger UI")
    http.request("GET", "/", expected={302}, openapi=False, note="redirección raíz")

    def register_account(role: str) -> str:
        account_email = f"dast.{role}.{uuid.uuid4().hex}@uni.edu"
        _, payload, _ = http.request(
            "POST", "/api/v1/auth/registro", body=_register_payload(account_email),
            expected={201}, note=f"registro {role}")
        response = _json_body(payload)
        if response.get("correoConfirmacionEnviado") is not True:
            raise DastError(
                f"El registro {role} no confirmó el envío por SMTP; "
                "el doble de captura no está operativo.")
        code = wait_for_verification_code(mailbox, account_email)
        http.request(
            "POST", "/api/v1/auth/confirmacion-correo",
            body={"correo": account_email, "codigo": code},
            expected={200}, note=f"confirmación {role}")
        return account_email

    account_a = register_account("a")
    _, login_payload, _ = http.request(
        "POST", "/api/v1/auth/login",
        body={"correo": account_a, "contrasena": PASSWORD},
        expected={200}, note="login A")
    tokens_a = _json_body(login_payload)
    access_a = str(tokens_a.get("accessToken") or "")
    refresh_a = str(tokens_a.get("refreshToken") or "")
    if not access_a or not refresh_a:
        raise DastError("El login A no devolvió accessToken y refreshToken.")

    account_c = register_account("c")
    _, login_c_payload, _ = http.request(
        "POST", "/api/v1/auth/login",
        body={"correo": account_c, "contrasena": PASSWORD},
        expected={200}, note="login C")
    tokens_c = _json_body(login_c_payload)
    access_c = str(tokens_c.get("accessToken") or "")
    if not access_c:
        raise DastError("El login C no devolvió accessToken.")

    http.request(
        "POST", "/api/v1/auth/confirmacion-correo/reenvio",
        body={"correo": account_a}, expected={202}, note="reenvío A")
    _, refresh_payload, _ = http.request(
        "POST", "/api/v1/auth/refresh",
        body={"refreshToken": refresh_a}, expected={200}, note="refresh A")
    refreshed = _json_body(refresh_payload)
    access_refreshed = str(refreshed.get("accessToken") or "")
    refresh_refreshed = str(refreshed.get("refreshToken") or "")
    if not access_refreshed or not refresh_refreshed:
        raise DastError("El refresh A no devolvió accessToken y refreshToken.")

    http.request(
        "POST", "/api/v1/auth/logout", token=access_refreshed,
        expected={204}, auth=True, note="logout A")
    http.request(
        "POST", "/api/v1/auth/refresh",
        body={"refreshToken": refresh_refreshed},
        expected={401}, note="refresh tras logout")
    http.request(
        "DELETE", "/api/v1/cuentas/mi-cuenta",
        body={"contrasena": PASSWORD, "confirmacionPerdidaInformacion": True},
        token=access_c, expected={204}, auth=True, note="borrado C")
    http.request(
        "POST", "/api/v1/auth/login",
        body={"correo": account_c, "contrasena": PASSWORD},
        expected={401, 403}, note="login C tras borrado")

    messages, records = zap.wait_for_passive(expected_messages=len(http.records))
    report = zap.write_reports(output)
    scan = parse_zap_report(report)
    coverage = build_coverage(operations, http.records)
    headers = security_findings(http.records)

    header_warnings = sum(1 for finding in headers if finding["severity"] == "WARN")
    status = classify_status(
        technical_errors, coverage["missing"], scan["severity_counts"], header_warnings)
    summary = {
        "status": status,
        "target": target,
        "generated_at": generated_at,
        "zap_version": version,
        "zap_image": args.zap_image,
        "messages_scanned": messages,
        "records_to_scan": records,
        "health": health,
        "coverage": coverage,
        "scan": {key: value for key, value in scan.items() if key != "alerts"},
        "alerts": scan["alerts"],
        "headers": headers,
        "header_warnings": header_warnings,
        "technical_errors": technical_errors,
        "mode": "baseline-passive",
    }
    _write_json(output / "route-coverage.json", coverage)
    _write_json(output / "security-headers.json", headers)
    _write_json(output / "dast-summary.json", summary)
    _write_json(output / "zap-evidence.json", {
        "version": version,
        "image": args.zap_image,
        "messages": messages,
        "recordsToScan": records,
    })
    markdown = render_markdown(
        status=status,
        target=target,
        zap_version=version,
        zap_image=args.zap_image,
        generated_at=generated_at,
        health=health,
        coverage=coverage,
        scan=scan,
        headers=headers,
        technical_errors=technical_errors,
    )
    (output / "dast-report.md").write_text(markdown, encoding="utf-8")
    summary_file = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_file:
        with open(summary_file, "a", encoding="utf-8") as file:
            file.write(markdown)

    print(f"DAST status: {status}")
    print(
        "Alertas por severidad: "
        + ", ".join(f"{risk}={scan['severity_counts'].get(risk, 0)}" for risk in RISK_ORDER))
    print(
        f"Cobertura OpenAPI: {coverage['openapi_visited']}/"
        f"{coverage['openapi_operations']}; mensajes={messages}")
    if coverage["missing"]:
        print("Rutas no visitadas: " + ", ".join(coverage["missing"]), file=sys.stderr)
    if status in {"FAIL", "ERROR"}:
        return 1
    return 0


def write_error_report(args: argparse.Namespace, error: Exception) -> None:
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    markdown = "\n".join([
        "# UniHome DAST — OWASP ZAP Baseline pasivo",
        "",
        "- **Estado:** **ERROR**",
        f"- **Objetivo:** `{args.target}`",
        f"- **Imagen:** `{args.zap_image}`",
        "",
        "## Error técnico",
        "",
        f"- {type(error).__name__}: {error}",
        "",
        "El escaneo no se considera ejecutado ni aprobado.",
        "",
    ])
    (output / "dast-report.md").write_text(markdown, encoding="utf-8")
    (output / "dast-error.txt").write_text(
        f"{type(error).__name__}: {error}\n", encoding="utf-8")
    summary_file = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_file:
        with open(summary_file, "a", encoding="utf-8") as file:
            file.write(markdown)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--target", required=True)
    parser.add_argument("--proxy", required=True)
    parser.add_argument("--zap-api", required=True)
    parser.add_argument("--zap-api-key", required=True)
    parser.add_argument("--zap-image", required=True)
    parser.add_argument("--mailbox", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        return run_scan(args)
    except Exception as error:
        write_error_report(args, error)
        print(f"::error::DAST no completado: {type(error).__name__}: {error}",
              file=sys.stderr)
        return 1


if __name__ == "__main__":  # pragma: no cover - entrada del proceso
    raise SystemExit(main())
