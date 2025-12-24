using static XiloAdventures.Engine.Engine.RandomMessageHelper;

namespace XiloAdventures.Engine.Engine;

public static partial class RandomMessages
{
    /// <summary>
    /// Mensaje cuando algo no se puede leer.
    /// </summary>
    public static string CannotRead => Pick(
        "No puedes leer {0}.",
        "{0} no tiene texto que leer.",
        "No hay nada escrito en {0}.",
        "{0} no es legible.",
        "No encuentras texto en {0}.",
        "{0} no contiene nada que leer.",
        "Es imposible leer {0}.",
        "No hay escritura en {0}.",
        "{0} no tiene nada escrito.",
        "No se puede leer {0}."
    );

    /// <summary>
    /// Mensaje cuando algo está en blanco.
    /// </summary>
    public static string IsBlank => Pick(
        "{0} está en blanco.",
        "Las páginas de {0} están vacías.",
        "No hay nada escrito en {0}.",
        "{0} no contiene texto alguno.",
        "{0} está completamente en blanco.",
        "El contenido de {0} brilla por su ausencia.",
        "{0} no tiene nada que leer.",
        "Las hojas de {0} están vírgenes.",
        "{0} está vacío de contenido.",
        "No hay texto en {0}."
    );
}
