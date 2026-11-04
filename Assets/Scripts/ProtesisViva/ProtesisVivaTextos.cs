/// <summary>
/// Todos los textos de la app en un solo lugar. Editalos aqui sin tocar la UI.
/// Se evitaron cifras estadisticas concretas a proposito: si quieres citar datos (cuantas personas, precios exactos),
/// agrega la fuente en pantalla o en la presentacion.
/// </summary>
public static class ProtesisVivaTextos
{
    public const string Titulo = "Prótesis Viva";
    public const string Subtitulo = "Una mano accesible para recuperar el agarre";

    public const string Banner =
        "Prueba las 3 funciones: cierra la mano, elige la fuerza y acerca un objeto caliente.";

    public static readonly string[] FrasesMenu =
    {
        "Una amputación transradial cambia la forma de comer, escribir, trabajar y abrazar. Cada gesto cotidiano se vuelve a aprender.",
        "Las prótesis de mano avanzadas pueden costar tanto como un automóvil, y para muchas personas siguen fuera de alcance.",
        "Cuando el diseño parte de la persona (comodidad, peso, mantenimiento y costo), la tecnología deja de ser un lujo.",
        "Sostener un vaso, tomar la mano de alguien, volver a sentirse capaz: eso es lo que esta prótesis quiere devolver."
    };

    public const string BotonComenzar = "Comenzar";
    public const string PieMenu = "Simulación educativa · no sustituye una valoración clínica";

    public static readonly string[] TabsInfo = { "Mano", "Antebrazo", "Subsistemas" };

    public static readonly string[] TitulosFicha =
    {
        "Mano: autonomía en la vida diaria",
        "Antebrazo: comodidad, fuerza y movilidad",
        "Subsistemas F1 · F2 · F3 y funcionamiento biomecánico"
    };

    public static readonly string[] CuerposFicha =
    {
        "<b>Qué es.</b> Una mano de cinco dedos (índice, medio, anular, meñique y pulgar), cada uno con falange proximal, media y distal, diseñada para recuperar el agarre de objetos cotidianos.\n\n" +
        "<b>Qué cambia en la vida diaria.</b>\n" +
        "• Sostener un vaso, un cubierto o un celular sin depender de otra persona.\n" +
        "• Sujetar objetos con firmeza suave o fuerte, según la tarea.\n" +
        "• Volver a participar en el trabajo, el estudio y los gestos sociales.\n\n" +
        "<b>Sensores en las yemas.</b> Cada punta de dedo tiene un sensor de presión (azul) que indica cuánta fuerza se aplica sobre el objeto, para no soltarlo ni aplastarlo.\n\n" +
        "<b>Idea de diseño.</b> Pocas piezas, mecanismo sencillo y mantenimiento accesible, para que la autonomía no dependa de un costo prohibitivo.",

        "<b>Qué es.</b> La estructura que conecta la prótesis con el cuerpo y aloja el mecanismo. En una amputación transradial es la pieza que se vive todo el día.\n\n" +
        "<b>Comodidad.</b>\n" +
        "• Forma ergonómica que reparte la carga en el antebrazo.\n" +
        "• Peso contenido para que no canse el hombro ni el codo.\n" +
        "• Carcasa que protege el mecanismo del polvo y los golpes.\n\n" +
        "<b>Fuerza.</b> Aloja el servomotor y el riel de fuerza (marcas Suave, Medio y Fuerte), de modo que la persona elija cuánta fuerza usar.\n\n" +
        "<b>Movilidad articular.</b> El diseño conserva el movimiento de muñeca y codo del usuario; la prótesis acompaña el movimiento natural y no lo reemplaza.\n\n" +
        "<b>Tip.</b> Prueba la Vista 3 (rayos X) para ver el interior sin quitar la carcasa.",

        "<b>F1 · Vibración al agarrar bien.</b> Un sensor de presión en cada yema mide qué tan fuerte aprietas. Cuando el agarre es firme, un motor de vibración en la muñeca vibra contra el brazo, así la persona sabe que el objeto no se le va a caer. Al soltar, la vibración se detiene.\n\n" +
        "<b>F2 · Fuerza ajustable.</b> Un servomotor jala un cable hacia cada yema. La perilla del dorso elige el nivel (Suave para un vaso, Fuerte para una bolsa) y el servomotor nunca aprieta más allá de ese límite.\n\n" +
        "<b>F3 · Alerta de objeto caliente.</b> Un sensor de temperatura en la yema compara lo que toca con un límite de seguridad. Si lo pasa, se enciende una luz roja y vibra un pequeño motor. Al soltar el objeto y enfriarse el sensor, la alerta se apaga sola.\n\n" +
        "<b>Cómo probarlo.</b>\n" +
        "1. Mueve el control de cierre: las yemas tocan el objeto y los sensores azules brillan.\n" +
        "2. Cambia entre Suave, Medio y Fuerte: la perilla se desliza y el apretón cambia.\n" +
        "3. Con agarre firme vibra el motor (F1).\n" +
        "4. Activa «Objeto: Caliente» y agarra: la luz roja y la vibración avisan (F3).\n\n" +
        "<b>Nota.</b> Esta aplicación es una simulación didáctica del concepto."
    };

    public const string EstadoReposo = "Reposo · Mano abierta";
    public const string EstadoCerrando = "Cerrando la mano";
    public const string EstadoAgarre = "Agarre firme";
    public const string EstadoSobrecarga = "¡Sobrecarga de fuerza!";

    public const string EtiquetaFuerza = "Fuerza de agarre";
    public const string EtiquetaCierre = "Cierre de la mano";
    public const string EtiquetaNivel = "Fuerza del agarre (F2)";
    public static readonly string[] Niveles = { "Suave", "Medio", "Fuerte" };
    public const string BotonObjetoFrio = "Objeto: Frío";
    public const string BotonObjetoCaliente = "Objeto: Caliente";
    public const string ChipPresion = "Presión";
    public const string ChipTemp = "Temperatura";
    public const string ChipVibracion = "Vibración";
    public const string ChipAlerta = "Alerta";
    public const string Si = "ON";
    public const string No = "OFF";
    public const string EstadoAlertaCalor = "¡Objeto muy caliente!";
    public const string BotonLuzDia = "Luz: Día";
    public const string BotonLuzNoche = "Luz: Noche";
    public const string BotonVista = "Cámara";
    public const string BotonInfo = "Info";
    public const string BotonCerrar = "Cerrar";
    public const string BotonControles = "Controles";
    public const string BotonAbrirMano = "Abrir mano";
    public const string BotonInicio = "Inicio";
    public const string TituloCine = "Cinemáticas";
    public const string BotonSaludar = "Saludar";
    public const string BotonAmorPaz = "Amor y paz";
    public const string BotonExplicar = "Explicar";
    public const string CineSaludando = "Cinemática · Saludo";
    public const string CineAmorPaz = "Cinemática · Amor y paz";
    public const string CineExplicando = "Modo explicación";
    public const string CineCerrar = "X";

    public static readonly string[] AnotTitulos =
    {
        "Sensor de presión · F1",
        "Sensor de temperatura · F3",
        "Luz roja de alerta · F3",
        "Motor de vibración · F1",
        "Perilla de fuerza · F2",
        "Servomotor y cables · F2"
    };

    public static readonly string[] AnotCuerpos =
    {
        "En cada yema mide cuánto aprieta la mano. Si el agarre es firme, activa la vibración.",
        "Compara el calor del objeto con un límite de seguridad. Solo mide cuando la yema toca el objeto.",
        "Se enciende si el objeto está demasiado caliente. Se apaga sola al soltarlo y enfriarse.",
        "Vibra en el antebrazo cuando el agarre es firme o hay alerta de calor, para sentirlo sin mirar.",
        "Se desliza por el riel entre Suave, Medio y Fuerte y fija hasta dónde puede apretar la mano.",
        "El servomotor jala los cables amarillos que cierran los dedos, solo hasta el límite elegido."
    };
}
