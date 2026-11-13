namespace UniHome.Domain.Units;

public enum UnitType
{
    Habitacion,
    Departamento,
    Estudio
}

public enum Occupancy
{
    Individual,
    Compartida
}

public enum ForWhom
{
    SoloMujeres,
    SoloHombres,
    Mixto
}

public enum BathroomType
{
    Privado,
    Compartido
}

public enum FurnitureType
{
    Amueblado,
    Parcial,
    SinMuebles
}

public enum UnitTemplate
{
    HabitacionIndividual,
    HabitacionCompartida,
    DepartamentoCompleto,
    Estudio
}

public enum ServiceName
{
    Agua,
    Luz,
    Gas,
    Internet,
    Lavadora,
    CocinaEquipada,
    Estacionamiento,
    LimpiezaAreasComunes
}

public enum ServiceMode
{
    Incluido,
    Aparte
}
