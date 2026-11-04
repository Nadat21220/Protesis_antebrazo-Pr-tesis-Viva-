using System;
using UnityEngine;

public enum LadoVista { Dorso, Palma, Lateral }

/// <summary>
/// Selector de camara con 3 vistas + 2 tomas cinematograficas.
///  Vista 1: general · Vista 2: detalle de mano · Vista 3: mecanismo interno (Rayos X).
///  Toma de saludo (PoseSaludo): plano medio de la mano de frente a la palma, con orbita lenta y empuje (dolly in).
///  Toma de explicacion (PosePerfil): 3/4 de perfil (no de esquina) del lado del pulgar, con balanceo sutil.
/// Las poses se calculan solas a partir de los renderers del LOD0 y del lado de la palma.
/// </summary>
public class CamaraSelector : MonoBehaviour
{
    public const int PoseSaludo = 3;
    public const int PosePerfil = 4;

    public Camera camara;
    public ManoAgarre mano;
    public ManoRayosX rayos;

    [Min(0.1f)] public float tiempoTransicion = 0.9f;
    [Tooltip("Multiplica la distancia de la Vista 1. Sube este valor para probar LOD1/LOD2.")]
    public float margenV1 = 1.15f;
    public float margenV2 = 1.0f;
    public float margenV3 = 1.0f;
    [Tooltip("0 = usar el FOV original de la camara.")]
    public float fovV1 = 0f;
    public float fovDetalle = 40f;

    [Header("Desde que lado se ve cada vista")]
    [Tooltip("Dorso = el lado de la perilla de fuerza y la luz roja.")]
    public LadoVista ladoV1 = LadoVista.Dorso;
    [Tooltip("Palma = el lado de los sensores de presion y los cables.")]
    public LadoVista ladoV2 = LadoVista.Palma;
    public LadoVista ladoV3 = LadoVista.Lateral;

    [Header("Cinematicas")]
    public float fovCine = 36f;
    public float margenSaludo = 1.05f;
    public float margenPerfil = 1.25f;
    [Tooltip("Angulo respecto a la normal de la palma. 90 = perfil total (esquina). 50-60 = 3/4.")]
    [Range(20f, 80f)] public float anguloPerfil = 55f;
    [Tooltip("Recorrido de la orbita durante el saludo (grados).")]
    public float orbitaSaludo = 18f;
    [Tooltip("Fraccion de acercamiento (dolly in) durante la toma.")]
    [Range(0f, 0.4f)] public float empuje = 0.14f;
    [Tooltip("Balanceo de camara en mano, como fraccion de la distancia.")]
    [Range(0f, 0.05f)] public float balanceo = 0.012f;

    public string[] nombres =
    {
        "Vista 1 · General",
        "Vista 2 · Detalle de mano",
        "Vista 3 · Rayos X mecánico"
    };

    public int Vista { get; private set; }
    public string NombreVista { get { return nombres[Mathf.Clamp(Vista, 0, nombres.Length - 1)]; } }
    public bool EnCine { get; private set; }
    /// <summary>true cuando la camara ya llego (casi) a la pose de la toma.</summary>
    public bool Asentada { get; private set; }
    public event Action<int> VistaCambio;

    struct PoseCam { public Vector3 pos; public Quaternion rot; public float fov; }

    readonly PoseCam[] poses = new PoseCam[5];
    readonly Vector3[] centros = new Vector3[5];
    bool calculado;
    Vector3 vel;
    float fovVel;
    float fovOriginal = 66f;
    int cineIdx;
    float cineT, cineDur;

    void Start()
    {
        if (camara == null) camara = Camera.main;
        if (camara == null) camara = FindFirstObjectByType<Camera>();
        if (mano == null) mano = FindFirstObjectByType<ManoAgarre>();
        if (rayos == null && mano != null) rayos = mano.GetComponent<ManoRayosX>();
        if (camara != null) fovOriginal = camara.fieldOfView;
    }

    public void Siguiente() { Seleccionar((Vista + 1) % 3); }

    public void Seleccionar(int i)
    {
        Vista = Mathf.Clamp(i, 0, 2);
        EnCine = false;
        if (rayos != null) rayos.Activar(Vista == 2);
        if (VistaCambio != null) VistaCambio(Vista);
    }

    /// <summary>Lleva la camara a una toma cinematografica. duracion = 0 deja la toma fija (solo balanceo).</summary>
    public void IrACine(int pose, float duracion)
    {
        cineIdx = Mathf.Clamp(pose, PoseSaludo, PosePerfil);
        cineT = 0f;
        cineDur = Mathf.Max(0f, duracion);
        EnCine = true;
        Asentada = false;
    }

    public void SalirCine()
    {
        EnCine = false;
        Asentada = false;
    }

    void LateUpdate()
    {
        if (camara == null || mano == null) return;

        if (!calculado)
        {
            Calcular();
            if (!calculado) return;
            camara.transform.SetPositionAndRotation(poses[Vista].pos, poses[Vista].rot);
            camara.fieldOfView = poses[Vista].fov;
            return;
        }

        PoseCam p;
        float st = Mathf.Max(0.05f, tiempoTransicion * 0.5f);
        if (EnCine)
        {
            cineT += Time.deltaTime;
            p = PoseDeCine();
            st = Mathf.Max(0.08f, tiempoTransicion * 0.45f);
        }
        else p = poses[Vista];

        Transform ct = camara.transform;
        ct.position = Vector3.SmoothDamp(ct.position, p.pos, ref vel, st);
        ct.rotation = Quaternion.Slerp(ct.rotation, p.rot, 1f - Mathf.Exp(-4f / Mathf.Max(0.1f, tiempoTransicion) * Time.deltaTime));
        camara.fieldOfView = Mathf.SmoothDamp(camara.fieldOfView, p.fov, ref fovVel, st);

        if (EnCine)
            Asentada = (ct.position - p.pos).sqrMagnitude < 0.04f * (p.pos - centros[cineIdx]).sqrMagnitude * 0.1f;
    }

    // Toma en movimiento: orbita + empuje + balanceo. La camara sigue a esta pose con suavizado (se siente "de operador").
    PoseCam PoseDeCine()
    {
        PoseCam b = poses[cineIdx];
        Vector3 c = centros[cineIdx];
        Vector3 off = b.pos - c;
        float dist = off.magnitude;
        float u = cineDur > 0.01f ? Mathf.Clamp01(cineT / cineDur) : 0f;
        float e = Mathf.SmoothStep(0f, 1f, u);

        float ang, escala, fov = b.fov;
        if (cineIdx == PoseSaludo)
        {
            ang = Mathf.Lerp(-orbitaSaludo * 0.5f, orbitaSaludo * 0.5f, e);
            escala = 1f - empuje * e;
            fov = Mathf.Lerp(b.fov, b.fov - 4f, e);
        }
        else
        {
            ang = 3.5f * Mathf.Sin(cineT * 0.32f);
            escala = 1f;
        }

        Vector3 pos = c + Quaternion.AngleAxis(ang, Vector3.up) * off * escala;
        Vector3 der = Vector3.Cross(Vector3.up, (c - pos)).normalized;
        float bal = balanceo * dist;
        pos += der * (Mathf.Sin(cineT * 0.55f) * bal) + Vector3.up * (Mathf.Sin(cineT * 0.41f + 1.3f) * bal * 0.6f);

        Vector3 mira = c + Vector3.up * (Mathf.Sin(cineT * 0.37f) * bal * 0.4f);
        PoseCam p;
        p.pos = pos;
        p.rot = Quaternion.LookRotation(mira - pos, Vector3.up);
        p.fov = fov;
        return p;
    }

    void Calcular()
    {
        Transform lod0 = null;
        foreach (Transform h in mano.transform)
            if (h.name.EndsWith("_LOD0")) { lod0 = h; break; }
        if (lod0 == null) return;

        bool ok;
        Bounds total = BoundsDe(lod0, null, out ok);
        if (!ok) return;
        Bounds soloMano = BoundsDe(lod0, new[] { "Palma", "Indice", "Medio", "Anular", "Menique", "Pulgar" }, out ok);
        if (!ok) soloMano = total;
        Bounds mec = BoundsDe(lod0, new[] { "F1_", "F2_" }, out ok);
        if (!ok) mec = total;

        Vector3 up = Vector3.up;
        Vector3 palma = mano.LadoPalmaMundo;
        float fov1 = fovV1 > 0f ? fovV1 : fovOriginal;

        poses[0] = Armar(total, Direccion(ladoV1, palma), fov1, margenV1);
        poses[1] = Armar(soloMano, Direccion(ladoV2, palma), fovDetalle, margenV2);
        poses[2] = Armar(mec, Direccion(ladoV3, palma), fovDetalle, margenV3);
        for (int i = 0; i < 3; i++) centros[i] = Vector3.zero;

        // saludo: plano medio de la mano, de frente a la palma, un poco desde arriba
        Vector3 dorso = -palma;
        Vector3 lat = Vector3.Cross(up, dorso).normalized;
        Bounds medio = soloMano;
        medio.Expand(new Vector3(0f, 0f, 0f));
        Vector3 cSaludo = Vector3.Lerp(total.center, soloMano.center, 0.6f);
        medio.center = cSaludo;
        Vector3 dirSaludo = (palma - lat * 0.18f + up * 0.10f).normalized;
        poses[PoseSaludo] = Armar(medio, dirSaludo, fovCine, margenSaludo);
        centros[PoseSaludo] = cSaludo;

        // explicacion: 3/4 desde el lado del pulgar
        float signo = 1f;
        foreach (Renderer r in lod0.GetComponentsInChildren<Renderer>(true))
            if (r.name.StartsWith("Pulgar", StringComparison.Ordinal))
            {
                signo = Vector3.Dot(r.bounds.center - total.center, lat) >= 0f ? 1f : -1f;
                break;
            }
        float a = anguloPerfil * Mathf.Deg2Rad;
        Vector3 dirPerfil = (palma * Mathf.Cos(a) + lat * signo * Mathf.Sin(a) + up * 0.16f).normalized;
        poses[PosePerfil] = Armar(total, dirPerfil, fovCine, margenPerfil);
        centros[PosePerfil] = total.center;

        calculado = true;
    }

    static Vector3 Direccion(LadoVista lado, Vector3 palma)
    {
        Vector3 up = Vector3.up;
        Vector3 dorso = -palma;
        Vector3 lat = Vector3.Cross(up, dorso).normalized;
        switch (lado)
        {
            case LadoVista.Dorso: return (dorso + lat * 0.30f + up * 0.18f).normalized;
            case LadoVista.Palma: return (palma - lat * 0.30f + up * 0.22f).normalized;
            default: return (lat + dorso * 0.30f + up * 0.18f).normalized;
        }
    }

    static PoseCam Armar(Bounds b, Vector3 dir, float fov, float margen)
    {
        float radio = b.extents.magnitude;
        float dist = radio / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) * margen;
        Vector3 pos = b.center + dir * dist;
        PoseCam p;
        p.pos = pos;
        p.rot = Quaternion.LookRotation(b.center - pos, Vector3.up);
        p.fov = fov;
        return p;
    }

    static Bounds BoundsDe(Transform raiz, string[] prefijos, out bool hay)
    {
        Bounds b = new Bounds(raiz.position, Vector3.zero);
        hay = false;
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            if (prefijos != null)
            {
                bool coincide = false;
                for (int i = 0; i < prefijos.Length; i++)
                    if (r.name.StartsWith(prefijos[i], StringComparison.Ordinal)) { coincide = true; break; }
                if (!coincide) continue;
            }
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
