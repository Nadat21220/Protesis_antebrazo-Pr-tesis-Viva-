using UnityEditor;

/// <summary>
/// Reemplazado por ProtesisSceneBuilder. Este menu solo redirige para no tener dos instaladores distintos.
/// </summary>
public static class ProtesisVivaSetup
{
    [MenuItem("Tools/Protesis Viva/1 - Preparar escena (usa Tools > Mano)")]
    public static void PrepararEscena()
    {
        ProtesisSceneBuilder.Construir();
    }
}
