using UnityEngine;

/// <summary>
/// OBSOLETO. Esta version construia la interfaz al dar Play con texto legacy (se veia pixelado).
/// La UI ahora se construye en el Editor (Tools > Mano > Construir Escena Completa y UI) y la maneja ProtesisVivaControles.
/// Este componente se elimina solo; se conserva el archivo para no romper escenas antiguas.
/// </summary>
public class ProtesisVivaUI : MonoBehaviour
{
    public ManoAgarre agarre;
    public CamaraSelector camaras;
    public ModoIluminacion luz;

    void Awake()
    {
        Debug.LogWarning("ProtesisVivaUI está obsoleto. Ejecuta Tools > Mano > Construir Escena Completa y UI.");
        Destroy(this);
    }
}
