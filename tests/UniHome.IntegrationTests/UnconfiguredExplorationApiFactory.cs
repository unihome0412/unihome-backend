namespace UniHome.IntegrationTests;

/// <summary>
/// Mantiene la identidad en memoria para emitir un JWT válido, pero deja los
/// puertos de exploración/favoritos con sus adaptadores MongoDB reales. Como la
/// conexión no está configurada, los endpoints M04/M07 deben fallar cerrados con
/// 503 en lugar de inventar resultados.
/// </summary>
public sealed class UnconfiguredExplorationApiFactory : UniHomeApiFactory
{
    protected override bool UseInMemoryExploration => false;
}
