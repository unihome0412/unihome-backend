namespace UniHome.Domain.Units;

public sealed record UnitTemplateDefaults(
    UnitType Type,
    Occupancy Occupancy,
    int Places,
    ForWhom ForWhom,
    BathroomType Bathroom,
    FurnitureType Furniture);

/// <summary>
/// Valores típicos de HU-079. El cliente puede sobrescribir los campos que envíe;
/// precio y tamaño siempre se capturan explícitamente.
/// </summary>
public static class UnitTemplates
{
    public static UnitTemplateDefaults Get(UnitTemplate template) => template switch
    {
        UnitTemplate.HabitacionIndividual => new(
            UnitType.Habitacion,
            Occupancy.Individual,
            1,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado),
        UnitTemplate.HabitacionCompartida => new(
            UnitType.Habitacion,
            Occupancy.Compartida,
            2,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado),
        UnitTemplate.DepartamentoCompleto => new(
            UnitType.Departamento,
            Occupancy.Individual,
            1,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado),
        UnitTemplate.Estudio => new(
            UnitType.Estudio,
            Occupancy.Individual,
            1,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado),
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };
}
