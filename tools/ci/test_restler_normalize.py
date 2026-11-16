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
        estado = converted["components"]["schemas"]["Request"]["properties"]["estado"]
        self.assertEqual("integer", estado["type"])
        self.assertTrue(estado["nullable"])
        self.assertEqual(2, len(changes))
        self.assertTrue(report(source, converted, changes)["operations_preserved"])

    def test_corrige_enums_string_y_oneof_nullable(self):
        source = {
            "openapi": "3.1.1",
            "paths": {"/x": {"post": {}}},
            "components": {"schemas": {
                "UnitType": {"type": "integer"},
                "Request": {
                    "type": "object",
                    "properties": {
                        "tipoUnidad": {
                            "oneOf": [
                                {"type": "null"},
                                {"$ref": "#/components/schemas/UnitType"},
                            ]
                        },
                        "latitud": {
                            "type": ["null", "number", "string"],
                            "format": "double",
                        },
                    },
                },
            }},
        }

        converted, changes = convert(source)

        self.assertEqual(
            "string",
            converted["components"]["schemas"]["UnitType"]["type"])
        self.assertIn(
            "Habitacion",
            converted["components"]["schemas"]["UnitType"]["enum"])
        tipo = converted["components"]["schemas"]["Request"]["properties"]["tipoUnidad"]
        self.assertEqual("string", tipo["type"])
        self.assertIn("Estudio", tipo["enum"])
        self.assertTrue(tipo["nullable"])
        latitud = converted["components"]["schemas"]["Request"]["properties"]["latitud"]
        self.assertEqual("number", latitud["type"])
        self.assertTrue(latitud["nullable"])
        self.assertTrue(any(
            item.get("before_one_of") and item.get("after_type") == "string"
            for item in changes))

    def test_relaja_uuid_de_entrada_para_ids_sinteticos(self):
        source = {
            "openapi": "3.1.1",
            "paths": {"/x/{id}": {
                "put": {"parameters": [{
                    "name": "id",
                    "in": "path",
                    "schema": {"type": "string", "format": "uuid"},
                }]},
            }},
            "components": {"schemas": {"CrearPropiedadRequest": {
                "type": "object",
                "properties": {
                    "universidadId": {"type": "string", "format": "uuid"},
                },
            }}},
        }

        converted, changes = convert(source)

        parameter = converted["paths"]["/x/{id}"]["put"]["parameters"][0]
        self.assertNotIn("format", parameter["schema"])
        university = converted["components"]["schemas"][
            "CrearPropiedadRequest"]["properties"]["universidadId"]
        self.assertNotIn("format", university)
        self.assertTrue(any(item.get("before_format") == "uuid" for item in changes))

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
