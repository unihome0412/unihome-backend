from pymongo import MongoClient

URI = ("mongodb+srv://unihome0412_db_user:0d18wEzlRHuDkPor"
       "@unihome.gelwqtb.mongodb.net/?retryWrites=true&w=majority")
c = MongoClient(URI, serverSelectionTimeoutMS=8000)
print("Bases:", c.list_database_names())
for db_name in ["unihome_verificacion", "unihome_desarrollo"]:
    db = c[db_name]
    print(f"\n== {db_name} ==")
    for col in sorted(db.list_collection_names()):
        print(f"  {col}: {db[col].estimated_document_count()}")
    for u in db["universidades"].find({}, {"nombre": 1, "siglas": 1, "estado": 1, "activa": 1}):
        print("  univ:", u)
    for un in db["unidades"].find({}, {"_id": 1, "universidadId": 1, "estado": 1, "disponibilidad": 1, "precioMensual": 1}).limit(10):
        print("  unidad:", un)
