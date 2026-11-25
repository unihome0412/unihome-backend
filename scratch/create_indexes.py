"""
Crea los índices oficiales de UniHome en 'unihome_desarrollo' exactamente
como los define UniHome.Infrastructure:
  - M01: cuentas, sesiones_refresh, restablecimientos_contrasena
  - M03: universidades
  - M05: propiedades, unidades, aceptaciones_terminos
  - M07: favoritos
"""
from pymongo import MongoClient, ASCENDING, DESCENDING, IndexModel

CONN = (
    "mongodb+srv://unihome0412_db_user:0d18wEzlRHuDkPor"
    "@unihome.gelwqtb.mongodb.net/"
    "?retryWrites=true&w=majority"
)
DB_NAME = "unihome_desarrollo"

def create_indexes():
    client = MongoClient(CONN, serverSelectionTimeoutMS=10000)
    db = client[DB_NAME]
    print(f"Creando índices en {DB_NAME}...")

    # 1. Cuentas (M01-001)
    db["cuentas"].create_indexes([
        IndexModel([("correoNormalizado", ASCENDING)], name="ux_cuentas_correo_normalizado", unique=True)
    ])
    print("✓ Cuentas: ux_cuentas_correo_normalizado")

    # 2. Sesiones Refresh (M01-001)
    db["sesiones_refresh"].create_indexes([
        IndexModel([("hashRefreshActual", ASCENDING)], name="ux_sesiones_refresh_token_actual", unique=True),
        IndexModel([("cuentaId", ASCENDING)], name="ix_sesiones_refresh_cuenta"),
        IndexModel([("expiraEnUtc", ASCENDING)], name="ttl_sesiones_refresh_expiracion", expireAfterSeconds=0)
    ])
    print("✓ Sesiones Refresh: ux_sesiones_refresh_token_actual, ix_sesiones_refresh_cuenta, ttl")

    # 3. Restablecimientos Contraseña (M01-002)
    db["restablecimientos_contrasena"].create_indexes([
        IndexModel([("tokenHash", ASCENDING)], name="ux_restablecimientos_token", unique=True),
        IndexModel([("accountId", ASCENDING)], name="ix_restablecimientos_cuenta"),
        IndexModel([("expiresAt", ASCENDING)], name="ttl_restablecimientos_expires", expireAfterSeconds=0)
    ])
    print("✓ Restablecimientos: ux_restablecimientos_token, ix_restablecimientos_cuenta, ttl")

    # 4. Universidades (M03-001)
    db["universidades"].create_indexes([
        IndexModel([("nombreNormalizado", ASCENDING)], name="ux_universidades_nombre_normalizado", unique=True)
    ])
    print("✓ Universidades: ux_universidades_nombre_normalizado")

    # 5. Propiedades (M05-001)
    db["propiedades"].create_indexes([
        IndexModel([
            ("ownerAccountId", ASCENDING),
            ("direccionNormalizada", ASCENDING),
            ("coloniaNormalizada", ASCENDING),
            ("nombreNormalizado", ASCENDING)
        ], name="ux_propiedades_propietario_direccion", unique=True),
        IndexModel([
            ("ownerAccountId", ASCENDING),
            ("createdAt", DESCENDING)
        ], name="ix_propiedades_propietario")
    ])
    print("✓ Propiedades: ux_propiedades_propietario_direccion, ix_propiedades_propietario")

    # 6. Unidades (M05-001)
    db["unidades"].create_indexes([
        IndexModel([
            ("propertyId", ASCENDING),
            ("createdAt", DESCENDING)
        ], name="ix_unidades_propiedad"),
        IndexModel([
            ("estadoPublicacion", ASCENDING),
            ("precioMensualPorLugarMxn", ASCENDING)
        ], name="ix_unidades_busqueda")
    ])
    print("✓ Unidades: ix_unidades_propiedad, ix_unidades_busqueda")

    # 7. Aceptaciones de Términos (M05-001)
    db["aceptaciones_terminos"].create_indexes([
        IndexModel([
            ("accountId", ASCENDING),
            ("versionTerminos", ASCENDING)
        ], name="ux_aceptaciones_cuenta_version", unique=True),
        IndexModel([("accountId", ASCENDING)], name="ix_aceptaciones_cuenta")
    ])
    print("✓ Aceptaciones Términos: ux_aceptaciones_cuenta_version, ix_aceptaciones_cuenta")

    # 8. Favoritos (M07-001)
    db["favoritos"].create_indexes([
        IndexModel([
            ("cuentaId", ASCENDING),
            ("unidadId", ASCENDING)
        ], name="ux_favoritos_cuenta_unidad", unique=True),
        IndexModel([
            ("cuentaId", ASCENDING),
            ("creadaEnUtc", DESCENDING)
        ], name="ix_favoritos_cuenta_fecha")
    ])
    print("✓ Favoritos: ux_favoritos_cuenta_unidad, ix_favoritos_cuenta_fecha")

    print("\n¡Todos los índices oficiales fueron creados exitosamente!")

if __name__ == "__main__":
    create_indexes()
