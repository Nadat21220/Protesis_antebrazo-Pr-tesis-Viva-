using System;
using UnityEngine;

/// <summary>
/// Feedback visual de los subsistemas. Va en el mismo objeto que ManoAgarre.
///  - Sensores azules de las yemas: brillan con la presion (F1).
///  - Motor de vibracion azul + temblor mecanico de F1_Motor_Vibracion: agarre firme o alerta de calor.
///  - Luz roja de advertencia: parpadea solo cuando el objeto esta muy caliente (F3).
///  - Sensor naranja: se calienta segun la temperatura medida (F3).
///
/// Para no romper el SRP Batcher NO se usa MaterialPropertyBlock: se crea UNA instancia de material por canal
/// (4 en total), compartida por todas las piezas y LODs. Los assets originales nunca se modifican.
/// </summary>
[DisallowMultipleComponent]
public class ManoSensoresFeedback : MonoBehaviour
{
    public ManoAgarre agarre;

    [Header("Nombres de propiedad")]
    [Tooltip("URP/Lit usa _EmissionColor. En un Shader Graph, usa el Reference de tu propiedad Color HDR.")]
    public string propiedadColor = "_EmissionColor";
    [Tooltip("Opcional. Si el material la tiene (p. ej. un Shader Graph con _Intensidad) se usa en vez de multiplicar el color.")]
    public string propiedadIntensidad = "_Intensidad";

    [Serializable]
    public class Canal
    {
        public string material;
        public Color color = Color.white;
        public float intensidadMin = 0.2f;
        public float intensidadMax = 6f;
        [NonSerialized] public Material instancia;
    }

    [Header("Canales (nombre exacto del material en el FBX)")]
    public Canal presion = new Canal { material = "Azul_Sensor_Presion", color = new Color(0.10f, 0.45f, 1.00f), intensidadMin = 0.4f, intensidadMax = 7f };
    public Canal vibracion = new Canal { material = "Azul_Motor_Vibracion", color = new Color(0.10f, 0.40f, 1.00f), intensidadMin = 0.3f, intensidadMax = 6f };
    public Canal alerta = new Canal { material = "Luz_Roja_Advertencia", color = new Color(1.00f, 0.05f, 0.02f), intensidadMin = 0.2f, intensidadMax = 12f };
    public Canal temperatura = new Canal { material = "Naranja_Sensor_Temp", color = new Color(1.00f, 0.45f, 0.02f), intensidadMin = 0.4f, intensidadMax = 6f };

    [Header("Vibración mecánica del motor (F1_Motor_Vibracion)")]
    public float amplitudVibracion = 0.012f;

    Transform motor;
    Vector3 motorReposo;
    float alertaNivel;

    void Start()
    {
        if (agarre == null) agarre = GetComponent<ManoAgarre>();

        Preparar(presion);
        Preparar(vibracion);
        Preparar(alerta);
        Preparar(temperatura);

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == "F1_Motor_Vibracion_L0") { motor = t; motorReposo = t.localPosition; break; }

        if (alerta.instancia != null && !ManoMaterialUtil.TieneEmision(alerta.instancia, propiedadColor, propiedadIntensidad))
            Debug.LogWarning("ManoSensoresFeedback: el material '" + alerta.material + "' no expone '" + propiedadColor +
                             "' ni '" + propiedadIntensidad + "'. Expón esas propiedades en Emision_Luz_Roja.shadergraph (ver guía).");
    }

    void Preparar(Canal c)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool cambio = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || m.name != c.material) continue;
                if (c.instancia == null)
                {
                    c.instancia = new Material(m);
                    c.instancia.name = m.name + " (runtime)";
                }
                mats[i] = c.instancia;
                cambio = true;
            }
            if (cambio) r.sharedMaterials = mats;
        }

        if (c.instancia == null)
            Debug.LogWarning("ManoSensoresFeedback: ningun renderer usa el material '" + c.material + "'.");
    }

    void Update()
    {
        if (agarre == null) return;

        float t = Time.time;
        float dt = Time.deltaTime;

        // F1: sensores de presion de las yemas
        Aplicar(presion, agarre.Presion);

        // F1 / F3: motor de vibracion
        float vib = agarre.Vibracion;
        float frecuencia = Mathf.Lerp(6f, 28f, vib);
        float osc = 0.5f + 0.5f * Mathf.Sin(t * frecuencia * 6.2832f);
        Aplicar(vibracion, vib * Mathf.Lerp(1f, osc, vib));

        // F3: luz roja solo con objeto muy caliente
        float objetivo = agarre.AlertaCalor ? 0.45f + 0.55f * Mathf.PingPong(t * 4f, 1f) : 0f;
        alertaNivel = Mathf.MoveTowards(alertaNivel, objetivo, 10f * dt);
        Aplicar(alerta, alertaNivel);

        // F3: sensor de temperatura
        Aplicar(temperatura, Mathf.InverseLerp(agarre.tempAmbiente, agarre.tempObjeto, agarre.Temperatura));

        if (motor != null)
            motor.localPosition = motorReposo + UnityEngine.Random.insideUnitSphere * (amplitudVibracion * vib);
    }

    void Aplicar(Canal c, float nivel)
    {
        if (c.instancia == null) return;
        float intensidad = Mathf.Lerp(c.intensidadMin, c.intensidadMax, Mathf.Clamp01(nivel));
        ManoMaterialUtil.AplicarEmision(c.instancia, propiedadColor, propiedadIntensidad, c.color, intensidad);
    }
}
