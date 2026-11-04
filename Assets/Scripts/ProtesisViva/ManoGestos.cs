using System;
using System.Collections.Generic;
using UnityEngine;

public enum Gesto { Ninguno, Saludar, AmorYPaz }

/// <summary>
/// Gestos de la mano: Saludar y Amor y paz. Va en el MISMO objeto que ManoAgarre.
///
///  - En Awake crea un pivote "Muneca_Pivote_Ln" en la rotula de la muneca de cada LOD y le cuelga la mano
///    (palma, dedos, sensores, perilla, riel, luz roja). Asi la mano completa gira sobre la muneca sin mover el antebrazo.
///    Los cables F2 NO se mueven de su sitio: se deforman con los dedos (ManoCablesF2), asi que se estiran solos.
///  - Los dedos se controlan con el modo manual de ManoAgarre (cierre y apertura lateral por dedo).
///  - La camara la maneja ProtesisVivaControles (CamaraSelector.IrACine); este script solo anima la mano.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(ManoAgarre))]
public class ManoGestos : MonoBehaviour
{
    [Header("Saludar")]
    public float duracionSaludo = 6.5f;
    [Tooltip("Giro maximo de la muneca a cada lado (grados).")]
    public float anguloSaludo = 24f;
    public int ondas = 4;

    [Header("Amor y paz")]
    public float duracionPaz = 6f;
    [Tooltip("Apertura lateral del indice y el medio (grados) para formar la V.")]
    public float aperturaPaz = 15f;

    [Header("General")]
    public float rampa = 0.9f;
    [Tooltip("Piezas que giran con la muneca (se busca por el inicio del nombre).")]
    public string[] prefijosMano = { "Palma", "Indice", "Medio", "Anular", "Menique", "Pulgar", "F3_", "F2_Control", "F2_Riel", "F2_Marca" };

    public Gesto Actual { get; private set; }
    public bool Ocupado { get { return Actual != Gesto.Ninguno; } }
    public float Duracion { get { return dur; } }
    public event Action<Gesto> Cambio;

    ManoAgarre agarre;
    readonly List<Transform> pivotes = new List<Transform>();
    Vector3 eje = Vector3.forward;
    float t, dur, fase, anguloMuneca;
    readonly float[] inicioCierre = new float[5];

    void Awake()
    {
        agarre = GetComponent<ManoAgarre>();
        Vector3 localLod0 = Vector3.zero;
        bool hayLocal = false;
        for (int lod = 0; lod < 3; lod++)
        {
            Transform p = CrearPivote(lod, ref localLod0, ref hayLocal);
            if (p != null) pivotes.Add(p);
        }
    }

    Transform CrearPivote(int lod, ref Vector3 localLod0, ref bool hayLocal)
    {
        Transform raiz = null;
        foreach (Transform h in transform)
            if (h.name.EndsWith("_LOD" + lod)) { raiz = h; break; }
        if (raiz == null) return null;

        string s = "_L" + lod;
        Transform[] todos = raiz.GetComponentsInChildren<Transform>(true);

        // posicion del pivote (local de la raiz del LOD): rotula de la muneca o, si no existe, borde inferior de la palma
        Vector3 local;
        if (hayLocal) local = localLod0;
        else
        {
            Transform rotula = null;
            foreach (Transform t0 in todos)
                if (t0.name.StartsWith("Muneca_Rotula", StringComparison.Ordinal)) { rotula = t0; break; }
            if (rotula != null)
            {
                Renderer rr = rotula.GetComponentInChildren<Renderer>();
                local = raiz.InverseTransformPoint(rr != null ? rr.bounds.center : rotula.position);
            }
            else local = raiz.InverseTransformPoint(EstimarMuneca(raiz, todos));
            localLod0 = local;
            hayLocal = true;
        }

        GameObject go = new GameObject("Muneca_Pivote" + s);
        Transform pv = go.transform;
        pv.SetParent(raiz, false);
        pv.localPosition = local;
        pv.localRotation = Quaternion.identity;
        pv.localScale = Vector3.one;

        // candidatas: las que empiezan con un prefijo de mano y terminan en _Ln
        HashSet<Transform> cand = new HashSet<Transform>();
        foreach (Transform t0 in todos)
        {
            if (t0 == raiz || !t0.name.EndsWith(s, StringComparison.Ordinal)) continue;
            for (int i = 0; i < prefijosMano.Length; i++)
                if (t0.name.StartsWith(prefijosMano[i], StringComparison.Ordinal)) { cand.Add(t0); break; }
        }
        List<Transform> mover = new List<Transform>();
        foreach (Transform c in cand)
        {
            bool tienePadreCand = false;
            for (Transform p = c.parent; p != null && p != raiz; p = p.parent)
                if (cand.Contains(p)) { tienePadreCand = true; break; }
            if (!tienePadreCand) mover.Add(c);
        }
        foreach (Transform c in mover) c.SetParent(pv, true);
        return pv;
    }

    static Vector3 EstimarMuneca(Transform raiz, Transform[] todos)
    {
        Bounds bp = new Bounds(raiz.position, Vector3.zero);
        bool hay = false;
        Vector3 dedos = Vector3.zero; int nd = 0;
        foreach (Transform t0 in todos)
        {
            if (t0.name.StartsWith("Palma", StringComparison.Ordinal))
            {
                Renderer r = t0.GetComponent<Renderer>();
                if (r != null) { if (!hay) { bp = r.bounds; hay = true; } else bp.Encapsulate(r.bounds); }
            }
            if (t0.name.StartsWith("Medio_Proximal", StringComparison.Ordinal)) { dedos += t0.position; nd++; }
        }
        if (!hay) return raiz.position;
        Vector3 dir = nd > 0 ? (dedos / nd - bp.center) : Vector3.up;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
        dir.Normalize();
        float e = Mathf.Abs(dir.x) * bp.extents.x + Mathf.Abs(dir.y) * bp.extents.y + Mathf.Abs(dir.z) * bp.extents.z;
        return bp.center - dir * e;
    }

    // ------------------------------------------------------------------ API
    public void Saludar() { Iniciar(Gesto.Saludar, duracionSaludo); }
    public void AmorYPaz() { Iniciar(Gesto.AmorYPaz, duracionPaz); }

    /// <summary>Termina el gesto con una salida suave (sin saltos).</summary>
    public void Cancelar()
    {
        if (!Ocupado) return;
        if (t < dur - rampa) t = dur - rampa;
    }

    void Iniciar(Gesto g, float duracion)
    {
        if (agarre == null) return;
        Actual = g;
        dur = Mathf.Max(2f * rampa + 0.5f, duracion);
        t = 0f;
        fase = 0f;
        anguloMuneca = 0f;

        // el eje de giro es la normal de la palma, en el espacio de la raiz del LOD
        Transform padre = pivotes.Count > 0 ? pivotes[0].parent : transform;
        eje = padre.InverseTransformDirection(agarre.PalmaNormalMundo).normalized;
        if (eje.sqrMagnitude < 0.5f) eje = Vector3.forward;

        agarre.FijarCierre(0f);
        for (int i = 0; i < 5; i++) inicioCierre[i] = agarre.Cierre;
        agarre.modoManual = true;
        if (Cambio != null) Cambio(g);
    }

    void Update()
    {
        if (!Ocupado) return;
        float dt = Time.deltaTime;
        t += dt;

        float entra = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / rampa));
        float sale = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dur - t) / rampa));
        float w = Mathf.Min(entra, sale);              // peso de la pose del gesto (0 = mano abierta)
        float wInicio = entra;                          // mezcla con la pose con la que se empezo

        float[] c = agarre.cierreManual;
        float[] a = agarre.aperturaManual;
        float angulo = 0f;

        if (Actual == Gesto.Saludar)
        {
            fase += dt * (ondas / Mathf.Max(0.5f, dur - 2f * rampa)) * 2f * Mathf.PI;
            angulo = anguloSaludo * Mathf.Sin(fase) * w;
            for (int i = 0; i < 5; i++)
            {
                float rizo = 0.035f * Mathf.Sin(fase * 1.0f - i * 0.7f) * w;
                float basePose = i == 4 ? 0.10f : (0.02f + 0.02f * i);
                float objetivo = Mathf.Max(0f, basePose * w + rizo);
                c[i] = Mezcla(i, objetivo, wInicio, sale);
            }
            a[0] = 7f * w; a[1] = 4f * w; a[2] = 7f * w; a[3] = 12f * w; a[4] = 0f;
        }
        else // AmorYPaz
        {
            fase += dt * 1.5f * 2f * Mathf.PI / Mathf.Max(0.5f, dur - 2f * rampa);
            angulo = 6f * Mathf.Sin(fase) * w;
            float[] meta = { 0f, 0f, 1f, 1f, 0.9f };
            for (int i = 0; i < 5; i++) c[i] = Mezcla(i, meta[i] * w, wInicio, sale);
            a[0] = aperturaPaz * w; a[1] = aperturaPaz * w; a[2] = 0f; a[3] = 0f; a[4] = 0f;
        }

        anguloMuneca = angulo;
        Quaternion q = Quaternion.AngleAxis(anguloMuneca, eje);
        for (int i = 0; i < pivotes.Count; i++) pivotes[i].localRotation = q;

        if (t >= dur) Terminar();
    }

    // mezcla la pose inicial (por si la mano venia cerrada) con la del gesto
    float Mezcla(int i, float objetivo, float wInicio, float sale)
    {
        float inicio = inicioCierre[i] * (1f - wInicio) * sale;
        return Mathf.Clamp01(objetivo + inicio);
    }

    void Terminar()
    {
        Gesto g = Actual;
        Actual = Gesto.Ninguno;
        for (int i = 0; i < pivotes.Count; i++) pivotes[i].localRotation = Quaternion.identity;
        for (int i = 0; i < 5; i++) { agarre.cierreManual[i] = 0f; agarre.aperturaManual[i] = 0f; }
        agarre.modoManual = false;
        if (Cambio != null) Cambio(Gesto.Ninguno);
    }
}
