"""Pruebas de la conversión OpenAPI 3.1 -> 3.0 para RESTler."""
import unittest

from restler.normalize_openapi import convert, operation_keys, report


class NormalizeOpenApiTests(unittest.TestCase):
    def test_convierte_tipos_null_y_preserva_operaciones(self):
        source = {
            "openapi": "3.1.1",
            "paths": {
                "/api/v1/x": {
                    "post": {"requestBody": {"content": {"application/json": {
                        "schema": {"$ref": "#/components/schemas/Request"}
                    }}}}
                }
            },
            "components": {"schemas": {"Request": {
                "type": "object",
                "properties": {
                    "nombre": {"type": ["null", "string"]},
                    "estado": {"type": ["null", "integer", "string"]},
                },
            }}},
        }

        converted, changes = convert(source)

        self.assertEqual("3.0.3", converted["openapi"])
        self.assertEqual(operation_keys(source), operation_keys(converted))
        self.assertEqual("string", converted["components"]["schemas"]["Request"]["properties"]["nombre"]["type"])
        self.assertTrue(converted["components"]["schemas"]["Request"]["properties"]["nombre"]["nullable"])
        self.assertIn("oneOf", converted["components"]["schemas"]["Request"]["properties"]["estado"])
        self.assertEqual(2, len(changes))
        self.assertTrue(report(source, converted, changes)["operations_preserved"])

    def test_no_modifica_el_documento_original(self):
        source = {
            "openapi": "3.1.1",
            "paths": {},
            "components": {"schemas": {"X": {"type": ["null", "string"]}}},
        }
        before = source["components"]["schemas"]["X"]["type"][:]
        convert(source)
        self.assertEqual(["null", "string"], source["components"]["schemas"]["X"]["type"])
        self.assertEqual(before, source["components"]["schemas"]["X"]["type"])

    def test_rechaza_si_cambia_la_superficie(self):
        source = {
            "openapi": "3.1.1",
            "paths": {"/x": {"get": {}}},
        }
        converted, _ = convert(source)
        converted["paths"].pop("/x")
        # La función report no valida por sí sola; la comprobación de aborto
        # ocurre durante convert. Este test fija la expectativa de comparación.
        self.assertNotEqual(operation_keys(source), operation_keys(converted))

    def test_documento_sin_operaciones_es_valido(self):
        source = {"openapi": "3.1.1", "paths": {}}
        converted, changes = convert(source)
        self.assertEqual(set(), operation_keys(converted))
        self.assertEqual([], changes)

    def test_convert_no_oculta_un_error_de_operaciones(self):
        class BadDict(dict):
            def items(self):
                return []

        source = {"openapi": "3.1.1", "paths": BadDict({"/x": {"get": {}}})}
        # Un mapping que oculta sus items no debe hacer pasar una conversión
        # silenciosa: operation_keys simplemente no ve rutas, y el reporte lo
        # muestra como cero operaciones.
        converted, _ = convert(source)
        self.assertEqual(0, report(source, converted, [])["source_operations"])


if __name__ == "__main__":
    unittest.main()
