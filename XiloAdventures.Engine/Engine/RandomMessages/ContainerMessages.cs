using static XiloAdventures.Engine.Engine.RandomMessageHelper;

namespace XiloAdventures.Engine.Engine;

public static partial class RandomMessages
{
    /// <summary>
    /// Mensaje cuando el contenedor está cerrado.
    /// </summary>
    public static string ContainerIsClosed => Pick(
        "{0} está cerrado.",
        "{0} no se puede abrir, está cerrado.",
        "Primero tendrás que abrir {0}.",
        "{0} permanece cerrado.",
        "El acceso a {0} está bloqueado.",
        "{0} está firmemente cerrado.",
        "No puedes acceder a {0}, está cerrado.",
        "{0} tiene la tapa cerrada.",
        "Necesitas abrir {0} primero.",
        "{0} está sellado."
    );

    /// <summary>
    /// Mensaje cuando el contenedor está vacío.
    /// </summary>
    public static string ContainerEmpty => Pick(
        "{0} está vacío.",
        "No hay nada dentro de {0}.",
        "{0} no contiene nada.",
        "El interior de {0} está vacío.",
        "No encuentras nada en {0}.",
        "{0} está completamente vacío.",
        "Dentro de {0} no hay nada.",
        "{0} no tiene nada dentro.",
        "El contenido de {0} brilla por su ausencia.",
        "No hay nada que ver en {0}."
    );

    /// <summary>
    /// Mensaje cuando no hay contenedor con ese nombre.
    /// </summary>
    public static string NoSuchContainer => Pick(
        "No hay ningún contenedor con ese nombre.",
        "No ves ningún contenedor así.",
        "¿Qué contenedor? No veo ninguno con ese nombre.",
        "Aquí no hay nada donde meter o sacar cosas con ese nombre.",
        "No encuentras ningún contenedor así.",
        "No existe tal contenedor por aquí.",
        "No hay nada así que pueda contener objetos.",
        "No ves ningún receptáculo con ese nombre.",
        "¿Un contenedor? No veo ninguno así.",
        "Aquí no hay nada parecido a eso."
    );
}
