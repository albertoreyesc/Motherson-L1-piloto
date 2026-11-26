namespace Motherson;

/// <summary>
/// Ajustes generales del portal que no dependen de la base de datos.
/// </summary>
public static class Entorno
{
    /// <summary>
    /// Indica si el portal trabaja con datos de prueba. Se lee de la opción "DatosSimulados"
    /// (appsettings.json o variable de entorno DatosSimulados). Por defecto es true.
    /// En false desaparecen las marcas "SIMULADO" de la pantalla, del Excel y de la API.
    /// </summary>
    public static bool DatosSimulados { get; set; } = true;
}
