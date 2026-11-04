using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Conmutador Dia / Laboratorio nocturno.
/// Anima la Directional Light, la luz ambiente, la exposicion del skybox y la intensidad del Bloom del Global Volume.
/// En modo noche el ambiente casi desaparece y solo las emisiones HDR (sensores, motor, luz roja) brillan con el Bloom.
/// </summary>
public class ModoIluminacion : MonoBehaviour
{
    public Light sol;
    public Volume volumen;
    public Camera camara;

    [Header("Noche")]
    public float intensidadSolNoche = 0.03f;
    public Color colorSolNoche = new Color(0.35f, 0.45f, 0.85f);
    [Range(0f, 1f)] public float ambienteNoche = 0.04f;
    public Color fondoNoche = new Color(0.01f, 0.012f, 0.025f);
    [Tooltip("Color del ambiente (modo Flat, p. ej. la tienda) de noche.")]
    public Color ambienteColorNoche = new Color(0.03f, 0.03f, 0.06f);

    [Header("Bloom")]
    [Tooltip("Intensidad del Bloom de dia. De noche se usa la que ya tenia el Volume (15.66).")]
    public float bloomDia = 3f;

    [Min(0.1f)] public float duracion = 0.9f;

    public bool Noche { get; private set; }
    public event Action<bool> Cambio;

    // valores originales (dia)
    float solInt;
    Color solColor;
    float ambInt;
    float reflInt;
    float bloomNoche;
    float expoDia = 1f;
    Bloom bloom;
    Material skyInst;
    bool tieneExpo;
    CameraClearFlags clearDia;
    Color fondoDia;
    float t;       // 0 = dia, 1 = noche
    bool ambienteFlat;
    Color ambColorDia;

    void Start()
    {
        if (sol == null) sol = RenderSettings.sun;
        if (sol == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sol = l; break; }
        }
        if (camara == null) camara = Camera.main;
        if (camara == null) camara = FindFirstObjectByType<Camera>();

        if (volumen == null)
        {
            foreach (Volume v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                Bloom b;
                if (v.isGlobal && v.profile != null && v.profile.TryGet(out b)) { volumen = v; break; }
            }
        }
        if (volumen != null && volumen.profile != null) volumen.profile.TryGet(out bloom);

        if (sol != null) { solInt = sol.intensity; solColor = sol.color; }
        ambInt = RenderSettings.ambientIntensity;
        ambienteFlat = RenderSettings.ambientMode == AmbientMode.Flat;
        ambColorDia = RenderSettings.ambientLight;
        reflInt = RenderSettings.reflectionIntensity;
        if (bloom != null) bloomNoche = bloom.intensity.value;

        if (RenderSettings.skybox != null)
        {
            skyInst = new Material(RenderSettings.skybox);
            tieneExpo = skyInst.HasProperty("_Exposure");
            if (tieneExpo) expoDia = skyInst.GetFloat("_Exposure");
            RenderSettings.skybox = skyInst;
        }
        if (camara != null) { clearDia = camara.clearFlags; fondoDia = camara.backgroundColor; }

        Aplicar();
    }

    public void Alternar() { Noche = !Noche; if (Cambio != null) Cambio(Noche); }
    public void Fijar(bool noche) { if (Noche == noche) return; Noche = noche; if (Cambio != null) Cambio(Noche); }

    void Update()
    {
        float objetivo = Noche ? 1f : 0f;
        if (Mathf.Approximately(t, objetivo)) return;
        t = Mathf.MoveTowards(t, objetivo, Time.deltaTime / duracion);
        Aplicar();
    }

    void Aplicar()
    {
        float k = Mathf.SmoothStep(0f, 1f, t);

        if (sol != null)
        {
            sol.intensity = Mathf.Lerp(solInt, intensidadSolNoche, k);
            sol.color = Color.Lerp(solColor, colorSolNoche, k);
        }

        RenderSettings.ambientIntensity = Mathf.Lerp(ambInt, ambienteNoche, k);
        if (ambienteFlat) RenderSettings.ambientLight = Color.Lerp(ambColorDia, ambienteColorNoche, k);
        RenderSettings.reflectionIntensity = Mathf.Lerp(reflInt, 0.05f, k);

        if (tieneExpo) skyInst.SetFloat("_Exposure", Mathf.Lerp(expoDia, 0.02f, k));

        if (camara != null && !tieneExpo)
        {
            // Sin propiedad _Exposure en el skybox: se cambia a color solido cuando ya esta oscuro.
            if (k > 0.5f) { camara.clearFlags = CameraClearFlags.SolidColor; camara.backgroundColor = fondoNoche; }
            else { camara.clearFlags = clearDia; camara.backgroundColor = fondoDia; }
        }

        if (bloom != null) bloom.intensity.value = Mathf.Lerp(bloomDia, bloomNoche, k);
    }

    void OnDestroy()
    {
        // Deja los valores de dia para que el Editor no guarde un estado nocturno.
        t = 0f;
        Noche = false;
        if (sol != null) Aplicar();
    }
}
