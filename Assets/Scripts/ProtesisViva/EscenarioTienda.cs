using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ambiente de la tienda: la mano esta en una vitrina de cristal. Lo construye ProtesisSceneBuilder (EscenarioTiendaBuilder).
/// En ejecucion este script:
///  - crea un Volume propio (viñeta, color, tonemapping ACES y, en PC, profundidad de campo) para un look de fotografia de producto,
///  - hace que focos y neones sigan el boton Dia / Noche (de noche la tienda se apaga y brillan los neones y los focos de la vitrina),
///  - da un parpadeo muy sutil al neon para que se sienta vivo,
///  - sube la distancia de sombras de URP (la mano mide ~7 unidades y el valor por defecto son 3 m) y lo devuelve al salir.
/// </summary>
[DisallowMultipleComponent]
public class EscenarioTienda : MonoBehaviour
{
    public ModoIluminacion luz;
    public Camera camara;
    [Tooltip("Punto que debe quedar enfocado (centro de la mano).")]
    public Transform foco;
    public float alturaMano = 7.3f;

    [Header("Luces de la tienda")]
    public Light[] focosVitrina;
    public Light[] lucesAmbiente;
    public Renderer[] neones;
    public Color colorNeon = new Color(1f, 0.10f, 0.12f);

    [Header("Intensidades (multiplican las del Editor)")]
    public float vitrinaDia = 1f;
    public float vitrinaNoche = 1.35f;
    public float ambienteDia = 1f;
    public float ambienteNoche = 0.05f;
    public float neonDia = 1.4f;
    public float neonNoche = 5f;

    [Header("Post-proceso")]
    public bool crearVolumen = true;
    [Range(0f, 0.6f)] public float vineta = 0.28f;
    [Range(-1f, 1f)] public float exposicion = 0.1f;
    public bool profundidadDeCampo = true;
    public bool suavizarBordes = true;
    public float distanciaSombras = 40f;

    float k;                 // 0 = dia, 1 = noche
    float[] focosInt, ambInt;
    Material neonMat;
    GameObject volGo;
    DepthOfField dof;
    float sombrasOriginal = -1f;
    UniversalRenderPipelineAsset asset;
    AntialiasingMode aaOriginal;
    UniversalAdditionalCameraData camData;
    bool aaCambiado;

    void Start()
    {
        if (luz == null) luz = FindFirstObjectByType<ModoIluminacion>();
        if (camara == null) camara = Camera.main;

        focosInt = new float[focosVitrina != null ? focosVitrina.Length : 0];
        for (int i = 0; i < focosInt.Length; i++) if (focosVitrina[i] != null) focosInt[i] = focosVitrina[i].intensity;
        ambInt = new float[lucesAmbiente != null ? lucesAmbiente.Length : 0];
        for (int i = 0; i < ambInt.Length; i++) if (lucesAmbiente[i] != null) ambInt[i] = lucesAmbiente[i].intensity;

        // una sola instancia de material para todos los tubos de neon (compatible con SRP Batcher)
        if (neones != null && neones.Length > 0 && neones[0] != null && neones[0].sharedMaterial != null)
        {
            neonMat = new Material(neones[0].sharedMaterial) { name = "Neon (runtime)" };
            foreach (Renderer r in neones) if (r != null) r.sharedMaterial = neonMat;
        }

        asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset != null && asset.shadowDistance < distanciaSombras)
        {
            sombrasOriginal = asset.shadowDistance;
            asset.shadowDistance = distanciaSombras;
        }

        if (camara != null && suavizarBordes)
        {
            camData = camara.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null && camData.antialiasing == AntialiasingMode.None)
            {
                aaOriginal = camData.antialiasing;
                camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                aaCambiado = true;
            }
        }

        if (crearVolumen) CrearVolumen();
        if (luz != null) luz.Cambio += AlCambiarLuz;
        k = luz != null && luz.Noche ? 1f : 0f;
        Aplicar();
    }

    void CrearVolumen()
    {
        volGo = new GameObject("Volume_Tienda (runtime)");
        volGo.transform.SetParent(transform, false);
        Volume v = volGo.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = 5f;
        v.weight = 1f;
        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        v.sharedProfile = perfil;

        Vignette vig = perfil.Add<Vignette>(true);
        vig.intensity.Override(vineta);
        vig.smoothness.Override(0.55f);

        ColorAdjustments ca = perfil.Add<ColorAdjustments>(true);
        ca.postExposure.Override(exposicion);
        ca.contrast.Override(10f);
        ca.saturation.Override(6f);

        Tonemapping tm = perfil.Add<Tonemapping>(true);
        tm.mode.Override(TonemappingMode.ACES);

        if (profundidadDeCampo && !Application.isMobilePlatform)
        {
            dof = perfil.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(20f);
            dof.gaussianEnd.Override(40f);
            dof.gaussianMaxRadius.Override(1.4f);
            dof.highQualitySampling.Override(false);
        }
    }

    void AlCambiarLuz(bool noche) { }

    void Update()
    {
        float objetivo = luz != null && luz.Noche ? 1f : 0f;
        if (!Mathf.Approximately(k, objetivo))
            k = Mathf.MoveTowards(k, objetivo, Time.deltaTime / Mathf.Max(0.1f, luz != null ? luz.duracion : 0.9f));
        Aplicar();
    }

    void LateUpdate()
    {
        // el fondo se desenfoca a partir de unos centimetros detras de la mano
        if (dof != null && camara != null && foco != null)
        {
            float d = Vector3.Distance(camara.transform.position, foco.position);
            dof.gaussianStart.value = d + alturaMano * 0.55f;
            dof.gaussianEnd.value = d + alturaMano * 1.8f;
        }
    }

    void Aplicar()
    {
        float s = Mathf.SmoothStep(0f, 1f, k);

        float mv = Mathf.Lerp(vitrinaDia, vitrinaNoche, s);
        for (int i = 0; i < focosInt.Length; i++)
            if (focosVitrina[i] != null) focosVitrina[i].intensity = focosInt[i] * mv;

        float ma = Mathf.Lerp(ambienteDia, ambienteNoche, s);
        for (int i = 0; i < ambInt.Length; i++)
            if (lucesAmbiente[i] != null) lucesAmbiente[i].intensity = ambInt[i] * ma;

        if (neonMat != null)
        {
            float parpadeo = 1f + 0.04f * Mathf.Sin(Time.time * 23f) * Mathf.Sin(Time.time * 1.7f);
            float inten = Mathf.Lerp(neonDia, neonNoche, s) * parpadeo;
            ManoMaterialUtil.AplicarEmision(neonMat, "_EmissionColor", null, colorNeon, inten);
        }
    }

    void OnDestroy()
    {
        if (luz != null) luz.Cambio -= AlCambiarLuz;
        if (asset != null && sombrasOriginal >= 0f) asset.shadowDistance = sombrasOriginal;
        if (aaCambiado && camData != null) camData.antialiasing = aaOriginal;
        if (volGo != null)
        {
            Volume v = volGo.GetComponent<Volume>();
            if (v != null && v.sharedProfile != null) Destroy(v.sharedProfile);
            Destroy(volGo);
        }
        if (neonMat != null) Destroy(neonMat);
    }
}
