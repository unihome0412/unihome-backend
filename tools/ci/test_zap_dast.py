"""Pruebas sin red de la lógica del escaneo DAST."""
import json
import tempfile
import unittest
import uuid
from email.message import EmailMessage
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
from urllib.error import HTTPError

import zap_dast
from zap_dast import (
    DastError,
    HttpClient,
    ResponseRecord,
    ZapApi,
    build_coverage,
    classify_status,
    extract_password_reset_token,
    extract_verification_code,
    parse_openapi_operations,
    parse_zap_report,
    render_markdown,
    security_findings,
    wait_for_password_reset_token,
    wait_for_verification_code,
    write_error_report,
)

OPERATIONS = [
    ("GET", "/api/v1/admin/universidades", True),
    ("POST", "/api/v1/admin/universidades", True),
    ("PUT", "/api/v1/admin/universidades/{id}", True),
    ("PUT", "/api/v1/admin/universidades/{id}/estado", True),
    ("POST", "/api/v1/auth/confirmacion-correo", False),
    ("POST", "/api/v1/auth/confirmacion-correo/reenvio", False),
    ("POST", "/api/v1/auth/login", False),
    ("POST", "/api/v1/auth/logout", True),
    ("POST", "/api/v1/auth/recuperacion/confirmacion", False),
    ("POST", "/api/v1/auth/recuperacion/solicitud", False),
    ("POST", "/api/v1/auth/refresh", False),
    ("POST", "/api/v1/auth/registro", False),
    ("DELETE", "/api/v1/cuentas/mi-cuenta", True),
    ("GET", "/api/v1/fotos/{photoRef}", False),
    ("GET", "/api/v1/perfil/mi-perfil", True),
    ("POST", "/api/v1/propiedades", True),
    ("POST", "/api/v1/propiedades/{propertyId}/unidades", True),
    ("POST", "/api/v1/publicaciones/primera/terminos", True),
    ("POST", "/api/v1/unidades/{unitId}/fotos", True),
    ("PUT", "/api/v1/unidades/{unitId}/servicios", True),
    ("GET", "/api/v1/unidades/{unitId}/vista-previa", True),
    ("PUT", "/api/v1/unidades/{unitId}/zona-reglas", True),
    ("GET", "/api/v1/universidades", False),
]


def build_spec() -> dict:
    paths: dict = {}
    for method, path, auth in OPERATIONS:
        operation = {"security": [{"Bearer": []}]} if auth else {}
        paths.setdefault(path, {})[method.lower()] = operation
    return {"openapi": "3.0.0", "paths": paths}


SPEC = build_spec()

SAFE_HEADERS = {
    "x-content-type-options": "nosniff",
    "x-frame-options": "DENY",
    "referrer-policy": "no-referrer",
    "permissions-policy": "camera=()",
    "content-security-policy": "default-src 'none'",
    "cache-control": "no-store",
}


class FakeResponse:
    def __init__(self, status=200, payload=b"{}", headers=None):
        self.status = status
        self._payload = payload
        self.headers = headers or {"Content-Type": "application/json"}

    def read(self):
        return self._payload

    def __enter__(self):
        return self

    def __exit__(self, *_):
        return False


class FakeOpener:
    def __init__(self, response=None):
        self.response = response or FakeResponse()
        self.requests = []

    def open(self, request, timeout=30):
        del timeout
        self.requests.append(request)
        return self.response


class FakeHttpClient:
    def __init__(self, target, proxy):
        del target, proxy
        self.records = []
        self.multipart_calls = []

    def request(
        self,
        method,
        path,
        *,
        body=None,
        token=None,
        expected=None,
        auth=False,
        openapi=True,
        note="",
    ):
        status, payload = self._response(method, path, body, note)
        if expected is not None and status not in expected:
            raise AssertionError(
                f"Respuesta simulada {status} no esperada para {method} {path}: {expected}")
        self.records.append(ResponseRecord(
            method=method,
            path=path.split("?", 1)[0],
            status=status,
            headers=dict(SAFE_HEADERS),
            auth=auth,
            openapi=openapi,
            note=note,
        ))
        return status, payload, dict(SAFE_HEADERS)

    def request_multipart(
        self,
        method,
        path,
        *,
        files,
        fields=None,
        token=None,
        expected=None,
        note="",
    ):
        del fields
        self.multipart_calls.append((method, path, files))
        status, payload = self._response(method, path, None, note)
        if expected is not None and status not in expected:
            raise AssertionError(
                f"Respuesta simulada {status} no esperada para {method} {path}: {expected}")
        self.records.append(ResponseRecord(
            method=method,
            path=path.split("?", 1)[0],
            status=status,
            headers=dict(SAFE_HEADERS),
            auth=bool(token),
            openapi=True,
            note=note,
        ))
        return status, payload, dict(SAFE_HEADERS)

    @staticmethod
    def _response(method, path, body, note):
        del body
        base = path.split("?", 1)[0]
        if base == "/health/live":
            return 200, b'{"status":"Healthy"}'
        if base == "/health/ready":
            return 200, b'{"status":"Healthy"}'
        if base == "/openapi/v1.json":
            return 200, json.dumps(SPEC).encode("utf-8")
        if base == "/swagger/index.html":
            return 200, b"<html></html>"
        if base == "/":
            return 302, b""
        if base == "/api/v1/auth/registro":
            return 201, json.dumps({
                "id": str(uuid.uuid4()),
                "correoConfirmacionEnviado": True,
            }).encode("utf-8")
        if base == "/api/v1/auth/confirmacion-correo":
            return 200, b'{"correoConfirmado":true}'
        if base == "/api/v1/auth/confirmacion-correo/reenvio":
            return 202, b""
        if base == "/api/v1/auth/login":
            if note == "login C tras borrado":
                return 401, b"{}"
            return 200, json.dumps({
                "accessToken": f"access-{uuid.uuid4().hex}",
                "refreshToken": f"refresh-{uuid.uuid4().hex}",
            }).encode("utf-8")
        if base == "/api/v1/auth/refresh":
            if note == "refresh tras logout":
                return 401, b"{}"
            return 200, json.dumps({
                "accessToken": f"access-{uuid.uuid4().hex}",
                "refreshToken": f"refresh-{uuid.uuid4().hex}",
            }).encode("utf-8")
        if base == "/api/v1/auth/logout":
            return 204, b""
        if base == "/api/v1/auth/recuperacion/solicitud":
            return 200, b'{"mensaje":"Si el correo esta registrado recibiras instrucciones"}'
        if base == "/api/v1/auth/recuperacion/confirmacion":
            return 200, b"{}"
        if base == "/api/v1/cuentas/mi-cuenta":
            return 204, b""
        if base == "/api/v1/perfil/mi-perfil":
            return 200, b'{"nombreCompleto":"Cuenta DAST"}'
        if base == "/api/v1/universidades":
            return 200, b"[]"
        if base == "/api/v1/admin/universidades":
            if method == "POST":
                return 201, json.dumps({"id": str(uuid.uuid4())}).encode("utf-8")
            return 200, b"[]"
        if base.startswith("/api/v1/admin/universidades/"):
            return 200, json.dumps({"id": str(uuid.uuid4())}).encode("utf-8")
        if base == "/api/v1/propiedades":
            return 201, json.dumps({"id": str(uuid.uuid4())}).encode("utf-8")
        if base.startswith("/api/v1/propiedades/") and base.endswith("/unidades"):
            return 201, json.dumps({"id": str(uuid.uuid4())}).encode("utf-8")
        if base.startswith("/api/v1/unidades/") and base.endswith("/servicios"):
            return 200, json.dumps({"id": str(uuid.uuid4())}).encode("utf-8")
        if base.startswith("/api/v1/unidades/") and base.endswith("/zona-reglas"):
            return 200, json.dumps({"id": str(uuid.uuid4())}).encode("utf-8")
        if base.startswith("/api/v1/unidades/") and base.endswith("/fotos"):
            return 200, b'{"totalFotos":1}'
        if base.startswith("/api/v1/unidades/") and base.endswith("/vista-previa"):
            return 200, b'{"fotoPrincipalUrl":"/api/v1/fotos/ref"}'
        if base.startswith("/api/v1/fotos/"):
            return 200, zap_dast.DAST_PHOTO_PNG
        if base == "/api/v1/publicaciones/primera/terminos":
            return 200, b'{"aceptada":true}'
        raise AssertionError(f"Ruta simulada no definida: {method} {path}")


class FakeZapApi:
    report = {"site": []}

    def __init__(self, base_url, api_key):
        del base_url, api_key

    def version(self):
        return "2.17.0"

    def prepare_passive_scan(self):
        return None

    def wait_for_passive(self, expected_messages, timeout_seconds=120):
        del timeout_seconds
        return expected_messages, 0

    def write_reports(self, output):
        Path(output / "zap-report.html").write_text("<html></html>", encoding="utf-8")
        Path(output / "zap-report.json").write_text(
            json.dumps(self.report), encoding="utf-8")
        return self.report


class ZapDastTests(unittest.TestCase):
    def test_openapi_operations_incluyen_23_operaciones_y_auth(self):
        operations = parse_openapi_operations(SPEC)
        self.assertEqual(23, len(operations))
        auth = {(item["method"], item["path"]) for item in operations if item["auth"]}
        self.assertIn(("POST", "/api/v1/auth/logout"), auth)
        self.assertIn(("DELETE", "/api/v1/cuentas/mi-cuenta"), auth)
        self.assertIn(("POST", "/api/v1/admin/universidades"), auth)
        self.assertIn(("POST", "/api/v1/unidades/{unitId}/fotos"), auth)
        self.assertNotIn(("POST", "/api/v1/auth/login"), auth)

    def test_openapi_sin_rutas_falla(self):
        with self.assertRaises(DastError):
            parse_openapi_operations({"paths": {}})

    def test_cobertura_resuelve_placeholders_de_rutas(self):
        operations = parse_openapi_operations(SPEC)
        records = [
            ResponseRecord("POST", "/api/v1/auth/registro", 201, {}),
            ResponseRecord(
                "PUT", "/api/v1/admin/universidades/123e4567-e89b-12d3-a456-426614174000/estado",
                200, {}),
            ResponseRecord(
                "POST", "/api/v1/unidades/123e4567-e89b-12d3-a456-426614174000/fotos",
                200, {}),
        ]
        coverage = build_coverage(operations, records)
        self.assertEqual(3, coverage["openapi_visited"])
        self.assertEqual(20, len(coverage["missing"]))
        self.assertEqual(0, len(coverage["support_routes"]))

    def test_cobertura_detecta_ruta_no_visitada(self):
        operations = parse_openapi_operations(SPEC)
        records = [
            ResponseRecord("POST", "/api/v1/auth/registro", 201, {}),
            ResponseRecord("POST", "/api/v1/auth/logout", 401, {}),
        ]
        coverage = build_coverage(operations, records)
        self.assertEqual(1, coverage["openapi_visited"])
        self.assertIn("POST /api/v1/auth/logout", coverage["missing"])
        self.assertEqual(0, len(coverage["support_routes"]))

    def test_cobertura_incluye_rutas_de_soporte(self):
        operations = parse_openapi_operations(SPEC)
        records = [ResponseRecord("GET", "/health/live", 200, {})]
        coverage = build_coverage(operations, records)
        self.assertEqual(1, len(coverage["support_routes"]))
        self.assertEqual("GET", coverage["support_routes"][0]["method"])

    def test_reporte_zap_cuenta_severidades_e_instancias(self):
        report = {
            "site": [{
                "alerts": [
                    {
                        "name": "CSP ausente",
                        "riskdesc": "Medium (Medium)",
                        "riskcode": "2",
                        "instances": [{"uri": "http://localhost/a"}],
                    },
                    {
                        "name": "Cabecera crítica",
                        "riskdesc": "High (High)",
                        "riskcode": "3",
                        "instances": [
                            {"uri": "http://localhost/b"},
                            {"uri": "http://localhost/c"},
                        ],
                    },
                ],
            }]
        }
        scan = parse_zap_report(report)
        self.assertEqual(1, scan["severity_counts"]["Medium"])
        self.assertEqual(1, scan["severity_counts"]["High"])
        self.assertEqual(2, scan["instance_counts"]["High"])
        self.assertEqual(2, scan["unique_alerts"])
        self.assertEqual(3, scan["instances"])

    def test_clasificacion_de_estado(self):
        self.assertEqual("ERROR", classify_status(["fallo"], [], {}))
        self.assertEqual("ERROR", classify_status([], ["ruta"], {}))
        self.assertEqual("FAIL", classify_status([], [], {"High": 1}))
        self.assertEqual("WARN", classify_status([], [], {"Medium": 1}))
        self.assertEqual("WARN", classify_status([], [], {}, 1))
        self.assertEqual("PASS", classify_status([], [], {}))

    def test_codigo_de_confirmacion_desde_texto(self):
        message = EmailMessage()
        message["To"] = "ana@uni.edu"
        message["From"] = "no-reply@uni.edu"
        message.set_content("Usa este código: 123456")
        self.assertEqual(
            "123456", extract_verification_code(message.as_bytes(), "ana@uni.edu"))
        self.assertIsNone(
            extract_verification_code(message.as_bytes(), "otra@uni.edu"))

    def test_codigo_de_confirmacion_desde_html_ignora_colores(self):
        message = EmailMessage()
        message["To"] = "ana@uni.edu"
        message["From"] = "no-reply@uni.edu"
        message.set_content(
            '<div style="color:#f4f6f8">Código</div><div>654321</div>',
            subtype="html",
        )
        self.assertEqual(
            "654321", extract_verification_code(message.as_bytes(), "ana@uni.edu"))

    def test_token_de_recuperacion_desde_enlace(self):
        message = EmailMessage()
        message["To"] = "ana@uni.edu"
        message["From"] = "no-reply@uni.edu"
        message.set_content(
            "Abre http://127.0.0.1:5088/restablecer?token=abc-DEF_123 para continuar")
        self.assertEqual(
            "abc-DEF_123",
            extract_password_reset_token(message.as_bytes(), "ana@uni.edu"))
        self.assertIsNone(
            extract_password_reset_token(message.as_bytes(), "otra@uni.edu"))

    def test_espera_token_de_recuperacion_desde_buzon(self):
        with tempfile.TemporaryDirectory() as directory:
            mailbox = Path(directory)
            message = EmailMessage()
            message["To"] = "ana@uni.edu"
            message["From"] = "no-reply@uni.edu"
            message.set_content("https://app.test/reset?token=token-123")
            (mailbox / "uno.eml").write_bytes(message.as_bytes())
            self.assertEqual(
                "token-123",
                wait_for_password_reset_token(mailbox, "ana@uni.edu", timeout_seconds=1))

    def test_espera_codigo_desde_buzon(self):
        with tempfile.TemporaryDirectory() as directory:
            mailbox = Path(directory)
            message = EmailMessage()
            message["To"] = "ana@uni.edu"
            message["From"] = "no-reply@uni.edu"
            message.set_content("123456")
            (mailbox / "uno.eml").write_bytes(message.as_bytes())
            self.assertEqual("123456", wait_for_verification_code(
                mailbox, "ana@uni.edu", timeout_seconds=1))

    def test_espera_codigo_sin_mensaje_falla(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(DastError):
                wait_for_verification_code(
                    Path(directory), "ana@uni.edu", timeout_seconds=0)

    def test_hallazgos_de_cabeceras_y_cookies(self):
        records = [
            ResponseRecord(
                "GET", "/health/live", 200,
                {"x-content-type-options": "nosniff", "server": "Kestrel"}),
            ResponseRecord(
                "POST", "/api/v1/auth/login", 200,
                {"cache-control": "no-store", "set-cookie": "sid=1; HttpOnly"}),
        ]
        findings = security_findings(records)
        headers = {item["header"] for item in findings}
        self.assertIn("x-frame-options", headers)
        self.assertIn("set-cookie", headers)
        self.assertIn("server", headers)
        self.assertTrue(any("faltan atributos" in item["finding"] for item in findings))

    def test_resumen_markdown_contiene_estado_y_cobertura(self):
        coverage = build_coverage(
            parse_openapi_operations(SPEC),
            [ResponseRecord("POST", "/api/v1/auth/registro", 201, {})],
        )
        scan = parse_zap_report({"site": []})
        markdown = render_markdown(
            status="WARN",
            target="http://127.0.0.1:5088",
            zap_version="2.17.0",
            zap_image="imagen@sha256:abc",
            generated_at="2026-11-09T00:00:00Z",
            health={"health-live": {"status_code": 200, "status": "Healthy"}},
            coverage=coverage,
            scan=scan,
            headers=[],
            technical_errors=[],
        )
        self.assertIn("**WARN**", markdown)
        self.assertIn("Cobertura de rutas OpenAPI", markdown)
        self.assertIn("sin `-I`", markdown)
        self.assertIn("/api/v1/auth/registro", markdown)

    def test_http_client_registra_respuesta_y_valida_estado(self):
        client = HttpClient("http://127.0.0.1:5088", "http://127.0.0.1:8090")
        opener = FakeOpener(FakeResponse(201, b'{"ok":true}', {"X-Test": "1"}))
        client._opener = opener
        status, payload, headers = client.request("POST", "/x", expected={201})
        self.assertEqual(201, status)
        self.assertEqual(b'{"ok":true}', payload)
        self.assertEqual("1", headers["x-test"])
        self.assertEqual(1, len(client.records))

    def test_http_client_falla_si_estado_no_es_el_esperado(self):
        client = HttpClient("http://127.0.0.1:5088", None)
        client._opener = FakeOpener(FakeResponse(500, b"error"))
        with self.assertRaises(DastError):
            client.request("GET", "/x", expected={200})

    def test_http_client_envolver_error_de_red(self):
        class BrokenOpener:
            def open(self, request, timeout=30):
                del request, timeout
                raise OSError("sin conexión")

        client = HttpClient("http://127.0.0.1:5088", None)
        client._opener = BrokenOpener()
        with self.assertRaises(DastError):
            client.request("GET", "/x")

    def test_http_client_multipart_envia_archivo_real(self):
        client = HttpClient("http://127.0.0.1:5088", None)
        opener = FakeOpener(FakeResponse(200, b'{"totalFotos":1}'))
        client._opener = opener
        status, _, _ = client.request_multipart(
            "POST", "/api/v1/unidades/u/fotos",
            files=[("Fotos", "foto.png", "image/png", b"\x89PNG")],
            expected={200},
        )
        self.assertEqual(200, status)
        request = opener.requests[0]
        self.assertIn("multipart/form-data; boundary=", request.get_header("Content-type"))
        self.assertIn(b'name="Fotos"; filename="foto.png"', request.data)
        self.assertIn(b"\x89PNG", request.data)

    def test_desenvuelve_reportes_de_la_api(self):
        self.assertEqual("<html></html>", zap_dast._unwrap_report(
            json.dumps({"htmlreport": "<html></html>"}), "htmlreport"))
        self.assertEqual('{"site":[]}', zap_dast._unwrap_report(
            '{"site":[]}', "jsonreport"))
        self.assertEqual("texto", zap_dast._unwrap_report("texto", "x"))
        self.assertEqual("{no-json", zap_dast._unwrap_report("{no-json", "x"))

    def test_zap_version_y_reportes(self):
        zap = object.__new__(ZapApi)
        zap.raw = lambda path: {
            "JSON/core/view/version": '{"version":"2.17.0"}',
            "JSON/core/view/numberOfMessages": '{"numberOfMessages":"2"}',
            "JSON/pscan/view/recordsToScan": '{"recordsToScan":"0"}',
            "OTHER/core/other/htmlreport": "<html>ok</html>",
            "OTHER/core/other/jsonreport": '{"site":[]}',
            "OTHER/core/other/mdreport": "# Reporte",
        }[path]
        self.assertEqual("2.17.0", zap.version())
        self.assertEqual(2, zap.message_count())
        self.assertEqual(0, zap.records_to_scan())
        with tempfile.TemporaryDirectory() as directory:
            report = zap.write_reports(Path(directory))
            self.assertEqual({"site": []}, report)
            self.assertTrue((Path(directory) / "zap-report.html").exists())
            self.assertTrue((Path(directory) / "zap-report.md").exists())

    def test_zap_error_http_no_expone_url(self):
        class ErrorOpener:
            def open(self, url, timeout=30):
                del timeout
                raise HTTPError(url, 404, "Not Found", {}, None)

        zap = ZapApi("http://127.0.0.1:8090", "clave-sintetica")
        zap._opener = ErrorOpener()
        with self.assertRaises(DastError) as context:
            zap.raw("JSON/core/view/version")
        self.assertNotIn("clave-sintetica", str(context.exception))

    def test_riesgo_y_confianza_desconocidos(self):
        self.assertEqual("Informational", zap_dast._risk_label({"riskcode": "9"}))
        self.assertEqual("n/d", zap_dast._confidence_label({}))
        self.assertEqual("High", zap_dast._confidence_label({"confidence": "3"}))
        self.assertEqual("Medium", zap_dast._confidence_label({"confidence": "2"}))

    def test_reporte_con_sitio_unico_y_alerta_invalida(self):
        scan = parse_zap_report({"site": {
            "alerts": [None, {"name": "Baja", "riskdesc": "Low (Low)", "riskcode": "1"}]
        }})
        self.assertEqual(1, scan["unique_alerts"])
        self.assertEqual(1, scan["severity_counts"]["Low"])

    def test_hallazgo_x_powered_by(self):
        records = [ResponseRecord(
            "GET", "/health/live", 200,
            {"x-powered-by": "ASP.NET", "server": "Kestrel/10.0"})]
        findings = security_findings(records)
        self.assertTrue(any(item["header"] == "x-powered-by" for item in findings))
        self.assertTrue(any(item["severity"] == "WARN" and item["header"] == "server"
                            for item in findings))

    def test_parse_args_driver(self):
        with patch.object(zap_dast.sys, "argv", [
            "zap_dast.py", "--target", "http://127.0.0.1:5088",
            "--proxy", "http://127.0.0.1:8090",
            "--zap-api", "http://127.0.0.1:8090",
            "--zap-api-key", "key",
        ]):
            args = zap_dast.parse_args()
        self.assertEqual("key", args.zap_api_key)

    def test_zap_espera_fin_de_analisis_pasivo(self):
        zap = object.__new__(ZapApi)
        zap.message_count = lambda: 3
        zap.records_to_scan = lambda: 0
        with patch("zap_dast.time.sleep", return_value=None):
            messages, records = zap.wait_for_passive(3, timeout_seconds=1)
        self.assertEqual(3, messages)
        self.assertEqual(0, records)

    def _run_scan(self, zap_class=FakeZapApi):
        output = Path(tempfile.mkdtemp(prefix="unihome-dast-test-"))
        mailbox = output / "mailbox"
        mailbox.mkdir()
        args = SimpleNamespace(
            target="http://127.0.0.1:5088",
            proxy="http://127.0.0.1:8090",
            zap_api="http://127.0.0.1:8090",
            zap_api_key="key",
        )
        with (
            patch.object(zap_dast, "HttpClient", FakeHttpClient),
            patch.object(zap_dast, "ZapApi", zap_class),
            patch.object(zap_dast, "OUTPUT_DIR", output),
            patch.object(zap_dast, "MAILBOX_DIR", mailbox),
            patch.object(
                zap_dast, "load_admin_state",
                return_value={"email": "admin@uni.edu", "password": "synthetic"}),
            patch.object(zap_dast, "wait_for_verification_code",
                         return_value="123456"),
            patch.object(zap_dast, "wait_for_password_reset_token",
                         return_value="reset-token"),
        ):
            result = zap_dast.run_scan(args)
        return result, output

    def test_run_scan_genera_evidencia_y_cobertura_23_de_23(self):
        result, output = self._run_scan()
        self.assertEqual(0, result)
        summary = json.loads((output / "dast-summary.json").read_text(encoding="utf-8"))
        self.assertEqual("PASS", summary["status"])
        self.assertEqual(23, summary["coverage"]["openapi_operations"])
        self.assertEqual(23, summary["coverage"]["openapi_visited"])
        self.assertEqual([], summary["coverage"]["missing"])
        self.assertTrue((output / "dast-report.md").exists())
        self.assertTrue((output / "zap-report.html").exists())
        self.assertNotIn("access-", (output / "dast-report.md").read_text(encoding="utf-8"))

    def test_run_scan_high_falla(self):
        class HighZap(FakeZapApi):
            report = {"site": [{"alerts": [{
                "name": "Hallazgo alto",
                "riskdesc": "High (High)",
                "riskcode": "3",
                "instances": [{"uri": "http://127.0.0.1:5088/x"}],
            }]}]}

        result, output = self._run_scan(HighZap)
        self.assertEqual(1, result)
        summary = json.loads((output / "dast-summary.json").read_text(encoding="utf-8"))
        self.assertEqual("FAIL", summary["status"])

    def test_run_scan_version_inesperada_es_error(self):
        class OldZap(FakeZapApi):
            def version(self):
                return "2.16.0"

        result, output = self._run_scan(OldZap)
        self.assertEqual(1, result)
        summary = json.loads((output / "dast-summary.json").read_text(encoding="utf-8"))
        self.assertEqual("ERROR", summary["status"])
        self.assertTrue(summary["technical_errors"])

    def test_error_tecnico_genera_reporte_error(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            args = SimpleNamespace(target="http://127.0.0.1:5088")
            with patch.object(zap_dast, "OUTPUT_DIR", output):
                write_error_report(args, DastError("sin reporte"))
            report = (output / "dast-report.md").read_text(encoding="utf-8")
            self.assertIn("**ERROR**", report)
            self.assertTrue((output / "dast-error.txt").exists())


if __name__ == "__main__":
    unittest.main()
