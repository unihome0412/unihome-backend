from pymongo import MongoClient
URI = ("mongodb+srv://unihome0412_db_user:0d18wEzlRHuDkPor"
       "@unihome.gelwqtb.mongodb.net/?retryWrites=true&w=majority")
db = MongoClient(URI, serverSelectionTimeoutMS=8000)["unihome_verificacion"]
for m in db["migraciones"].find({}, {"_id": 1, "version": 1, "estado": 1, "aplicadaEn": 1}):
    print("migracion:", m)
for col in ["conversaciones", "mensajes", "notificaciones", "vinculos_ocupacion", "invitaciones_unidad"]:
    print(col, [i["name"] for i in db[col].list_indexes()])
    print("  options:", list(db.list_collections(filter={"name": col}))[0].get("options", {}).keys())
