using System;
using System.Collections.Generic;
using UnityEngine;

public enum EstadoAgarre { Reposo, Cerrando, AgarreFirme }

/// <summary>
/// Simulador de la protesis segun el "Documento de funcionalidades del prototipo".
/// Va en el MISMO objeto que ManoLODSync y el LODGroup (el prefab "Protesis_Mano_LODs").
///
///  F1 · Vibracion al agarrar bien: el sensor de presion de las yemas mide el apretón; si llega a un agarre
///       firme, el motor de vibracion de la muñeca vibra. Al soltar, se detiene.
///  F2 · Fuerza ajustable: la perilla (Suave / Medio / Fuerte) fija el limite. El servomotor jala los cables
///       solo hasta ese limite, nunca mas.
///  F3 · Alerta de objeto caliente: el sensor de temperatura de la yema compara contra un limite de seguridad;
///       si lo pasa se enciende la luz roja y vibra el motor. Al soltar y enfriarse, la alerta se apaga sola.
///
/// Ademas tiene un "modo manual" que usa ManoGestos: cierre y apertura (abduccion) de cada dedo por separado,
/// para gestos como saludar o amor y paz.
/// Solo mueve las piezas del LOD0; ManoLODSync copia esa pose a LOD1 y LOD2 en LateUpdate.
/// El eje de flexion de cada dedo se calcula en Start con la geometria real (direccion del dedo y lado del sensor).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(ManoLODSync))]
public class ManoAgarre : MonoBehaviour
{
    [Header("Controles (los mueve la UI)")]
    [Range(0f, 1f)] public float ordenCierre;
    [Tooltip("0 = Suave, 1 = Medio, 2 = Fuerte")]
    [Range(0, 2)] public int nivelFuerza = 1;
    [Tooltip("Simula que el objeto agarrado está caliente.")]
    public bool objetoCaliente;
    [Tooltip("Qué tan rápido sigue la mano a la orden.")]
    public float suavizado = 10f;

    [Header("Flexión de dedos (grados al 100 % de cierre)")]
    public float proximal = 70f;
    public float media = 85f;
    public float distal = 55f;
    public float pulgarProximal = 30f;
    public float pulgarDistal = 60f;
    [Tooltip("Márcalo solo si los dedos se cerraran hacia el dorso.")]
    public bool invertirCierre = false;

    [Header("Modo manual (gestos). Orden: Indice, Medio, Anular, Menique, Pulgar")]
    public bool modoManual;
    public float[] cierreManual = new float[5];
    [Tooltip("Apertura lateral en grados (positivo = separa el dedo hacia afuera).")]
    public float[] aperturaManual = new float[5];

    [Header("F2 · Fuerza ajustable")]
    [Tooltip("Límite de presión por nivel: Suave, Medio, Fuerte.")]
    public float[] limites = { 0.35f, 0.65f, 1f };
    [Tooltip("La perilla se desliza por el riel entre las marcas Suave y Fuerte. Giro extra opcional (0 = sin giro).")]
    public float perillaGrados = 0f;
    public Vector3 perillaEjeLocal = Vector3.forward;
    [Tooltip("Giro de la polea del servomotor al jalar los cables.")]
    public float poleaGrados = 220f;
    public Vector3 poleaEjeLocal = Vector3.forward;

    [Header("F1 · Sensor de presión y vibración")]
    [Tooltip("Cierre al que las yemas tocan el objeto.")]
    [Range(0f, 1f)] public float contactoEn = 0.45f;
    [Tooltip("Presión (0-1) desde la cual el agarre cuenta como firme.")]
    [Range(0f, 1f)] public float umbralFirme = 0.25f;

    [Header("F3 · Temperatura")]
    public float tempAmbiente = 25f;
    public float tempObjeto = 70f;
    public float limiteTemp = 50f;
    public float histeresisTemp = 3f;
    public float velCalentar = 45f;
    public float velEnfriar = 10f;

    public event Action<ManoAgarre> Cambio;

    // ---------------- lecturas para otros scripts ----------------
    public float FuerzaActual { get { return cierreAct; } }
    public float Cierre { get { return cierreAct; } }
    public float LimiteActual { get { return limiteAct; } }
    public float Presion { get { return modoManual ? 0f : Mathf.Clamp01(Mathf.InverseLerp(contactoEn, 1f, cierreAct)); } }
    public bool Contacto { get { return !modoManual && cierreAct >= contactoEn + 0.01f; } }
    public bool AgarreFirme { get { return Presion >= umbralFirme; } }
    public float Temperatura { get { return temp; } }
    public bool AlertaCalor { get { return alerta; } }
    /// <summary>Se incrementa cada vez que cambia la pose de los dedos (lo usan los cables para deformarse).</summary>
    public int VersionPose { get; private set; }

    public float Vibracion
    {
        get
        {
            float v = AgarreFirme ? Mathf.Lerp(0.4f, 1f, Presion) : 0f;
            if (alerta) v = Mathf.Max(v, 0.85f);
            return v;
        }
    }

    public EstadoAgarre Estado
    {
        get
        {
            if (cierreAct < 0.04f) return EstadoAgarre.Reposo;
            return AgarreFirme ? EstadoAgarre.AgarreFirme : EstadoAgarre.Cerrando;
        }
    }

    public string NivelFuerza
    {
        get { return nivelFuerza <= 0 ? "Suave" : (nivelFuerza == 1 ? "Medio" : "Fuerte"); }
    }

    /// <summary>Dirección horizontal (mundo) hacia donde mira la palma. La usa el selector de cámara.</summary>
    public Vector3 LadoPalmaMundo
    {
        get
        {
            Vector3 h = transform.TransformDirection(palmaLocal);
            h.y = 0f;
            return h.sqrMagnitude < 1e-4f ? Vector3.forward : h.normalized;
        }
    }

    /// <summary>Normal de la palma en 3D (mundo). Eje del movimiento de saludo de la muñeca.</summary>
    public Vector3 PalmaNormalMundo { get { return transform.TransformDirection(palmaLocal).normalized; } }

    // ---------------- API de la UI ----------------
    public void FijarCierre(float valor) { ordenCierre = Mathf.Clamp01(valor); }
    public void FijarNivelFuerza(int nivel) { nivelFuerza = Mathf.Clamp(nivel, 0, 2); }
    public void FijarObjetoCaliente(bool valor) { objetoCaliente = valor; }
    public void AlternarObjetoCaliente() { objetoCaliente = !objetoCaliente; }

    // ---------------- interno ----------------
    class Junta { public Transform t; public Quaternion reposo; public Vector3 eje; public Vector3 ejeAbd; public float grados; }
    class DedoRig
    {
        public string nombre; public int idx; public float retraso; public float signoAbd = 1f;
        public Transform prox; public Vector3 largo, palmar;
        public readonly List<Junta> juntas = new List<Junta>();
    }

    readonly List<DedoRig> dedos = new List<DedoRig>();
    readonly Dictionary<string, Transform> piezas = new Dictionary<string, Transform>();
    Transform perilla, polea;
    Quaternion perillaRotReposo, poleaRotReposo;
    Vector3 perillaPosReposo, railS, railF;
    float railT0;
    bool hayRiel;
    Vector3 palmaLocal = Vector3.forward;
    Vector3 palmaAcumulada;
    int palmaConteo;

    float cierreAct, limiteAct, temp;
    bool alerta;
    bool listo;
    bool manualPrev;
    float uCierre = -1f, uLimite = -1f, uTemp = -1f;
    int uNivel = -1;
    bool uCaliente, uAlerta;

    void Start()
    {
        Transform lod0 = null;
        foreach (Transform h in transform)
            if (h.name.EndsWith("_LOD0")) { lod0 = h; break; }

        if (lod0 == null)
        {
            Debug.LogWarning("ManoAgarre: no encontre un hijo que termine en _LOD0.");
            enabled = false;
            return;
        }

        foreach (Transform t in lod0.GetComponentsInChildren<Transform>(true))
            piezas[t.name] = t;

        ConstruirDedo("Indice", 0f, 0);
        ConstruirDedo("Medio", 0.05f, 1);
        ConstruirDedo("Anular", 0.10f, 2);
        ConstruirDedo("Menique", 0.15f, 3);
        ConstruirPulgar(0.0f, 4);
        CalcularAbduccion();

        perilla = Hallar("F2_Control_Fuerza_Perilla_L0", true);
        if (perilla != null) { perillaRotReposo = perilla.localRotation; perillaPosReposo = perilla.localPosition; }
        polea = Hallar("F2_Polea_L0", false);
        if (polea != null) poleaRotReposo = polea.localRotation;
        PrepararRiel();
        AnclarGuias();

        if (palmaConteo > 0)
            palmaLocal = transform.InverseTransformDirection(palmaAcumulada / palmaConteo);

        limites = (limites != null && limites.Length >= 3) ? limites : new[] { 0.35f, 0.65f, 1f };
        if (cierreManual == null || cierreManual.Length < 5) cierreManual = new float[5];
        if (aperturaManual == null || aperturaManual.Length < 5) aperturaManual = new float[5];
        limiteAct = limites[Mathf.Clamp(nivelFuerza, 0, 2)];
        temp = tempAmbiente;
        listo = true;
        Aplicar();
    }

    Transform Hallar(string nombre, bool avisar)
    {
        Transform t;
        if (piezas.TryGetValue(nombre, out t)) return t;
        if (avisar) Debug.LogWarning("ManoAgarre: no encontre la pieza '" + nombre + "'.");
        return null;
    }

    // Las guias de los cables (F2_Guia_<Dedo>_1 / _2) vienen pegadas a la raiz y se quedaban estaticas.
    // Se cuelgan de la falange que les corresponde (1 = proximal, 2 = media), en los 3 LOD, sin mover su posicion.
    void AnclarGuias()
    {
        string[] nombres = { "Indice", "Medio", "Anular", "Menique" };
        for (int lod = 0; lod < 3; lod++)
        {
            Transform raiz = null;
            foreach (Transform h in transform)
                if (h.name.EndsWith("_LOD" + lod)) { raiz = h; break; }
            if (raiz == null) continue;

            Dictionary<string, Transform> d = new Dictionary<string, Transform>();
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) d[t.name] = t;

            string s = "_L" + lod;
            foreach (string dedo in nombres)
                for (int i = 1; i <= 2; i++)
                {
                    Transform guia, hueso;
                    if (!d.TryGetValue("F2_Guia_" + dedo + "_" + i + s, out guia)) continue;
                    if (!d.TryGetValue(dedo + (i == 1 ? "_Proximal" : "_Media") + s, out hueso)) continue;
                    guia.SetParent(hueso, true);
                }
        }
    }

    void PrepararRiel()
    {
        Transform s = Hallar("F2_Marca_Suave_L0", false);
        Transform f = Hallar("F2_Marca_Fuerte_L0", false);
        if (perilla == null || perilla.parent == null || s == null || f == null) return;

        Transform p = perilla.parent;
        railS = p.InverseTransformPoint(s.position);
        railF = p.InverseTransformPoint(f.position);
        Vector3 riel = railF - railS;
        float l2 = riel.sqrMagnitude;
        railT0 = l2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(perillaPosReposo - railS, riel) / l2) : 0f;
        hayRiel = l2 > 1e-8f;
    }

    void ConstruirDedo(string nombre, float retraso, int idx)
    {
        Transform prox = Hallar(nombre + "_Proximal_L0", true);
        Transform med = Hallar(nombre + "_Media_L0", true);
        Transform dis = Hallar(nombre + "_Distal_L0", true);
        Transform sen = Hallar(nombre + "_Sensor_Presion_L0", false);
        if (prox == null || med == null || dis == null) return;

        Vector3 largo, palmar;
        Vector3 r = EjeFlexionMundo(prox, dis, sen, out largo, out palmar);
        DedoRig d = new DedoRig { nombre = nombre, idx = idx, retraso = retraso, prox = prox, largo = largo, palmar = palmar };
        d.juntas.Add(NuevaJunta(prox, r, palmar, proximal));
        d.juntas.Add(NuevaJunta(med, r, palmar, media));
        d.juntas.Add(NuevaJunta(dis, r, palmar, distal));
        dedos.Add(d);
    }

    void ConstruirPulgar(float retraso, int idx)
    {
        Transform prox = Hallar("Pulgar_Proximal_L0", true);
        Transform dis = Hallar("Pulgar_Distal_L0", true);
        Transform sen = Hallar("Pulgar_Sensor_Presion_L0", false);
        if (prox == null || dis == null) return;

        Vector3 largo, palmar;
        Vector3 r = EjeFlexionMundo(prox, dis, sen, out largo, out palmar);
        DedoRig d = new DedoRig { nombre = "Pulgar", idx = idx, retraso = retraso, prox = prox, largo = largo, palmar = palmar, signoAbd = 0f };
        d.juntas.Add(NuevaJunta(prox, r, palmar, pulgarProximal));
        d.juntas.Add(NuevaJunta(dis, r, palmar, pulgarDistal));
        dedos.Add(d);
    }

    // Eje de flexion (mundo) = largo del dedo x lado del sensor de presion (la yema, lado palmar).
    // Con la convencion de Unity, girar un angulo positivo alrededor de este eje lleva el dedo hacia el sensor.
    Vector3 EjeFlexionMundo(Transform prox, Transform distalT, Transform sensor, out Vector3 largoOut, out Vector3 palmarOut)
    {
        Vector3 largo = distalT.position - prox.position;
        if (largo.sqrMagnitude < 1e-8f) largo = distalT.rotation * Vector3.forward;
        largo.Normalize();

        Vector3 haciaSensor = sensor != null ? sensor.position - distalT.position : distalT.rotation * Vector3.down;
        Vector3 palmar = haciaSensor - Vector3.Dot(haciaSensor, largo) * largo;
        if (palmar.sqrMagnitude < 1e-8f) palmar = distalT.rotation * Vector3.down;
        palmar.Normalize();

        palmaAcumulada += palmar;
        palmaConteo++;
        largoOut = largo;
        palmarOut = palmar;
        return Vector3.Cross(largo, palmar).normalized;
    }

    Junta NuevaJunta(Transform t, Vector3 ejeMundo, Vector3 palmarMundo, float grados)
    {
        return new Junta
        {
            t = t,
            reposo = t.localRotation,
            eje = Quaternion.Inverse(t.rotation) * ejeMundo,
            ejeAbd = Quaternion.Inverse(t.rotation) * palmarMundo,
            grados = grados
        };
    }

    // Signo de la apertura lateral: positivo = el dedo se separa hacia afuera.
    // Indice y Medio se separan entre si (V de "amor y paz"); Anular y Menique hacia afuera de la mano.
    void CalcularAbduccion()
    {
        Vector3 centroIM = Vector3.zero, centroTodos = Vector3.zero;
        int nIM = 0, nT = 0;
        foreach (DedoRig d in dedos)
        {
            if (d.idx > 3) continue;
            centroTodos += d.prox.position; nT++;
            if (d.idx <= 1) { centroIM += d.prox.position; nIM++; }
        }
        if (nT == 0) return;
        centroTodos /= nT;
        if (nIM > 0) centroIM /= nIM;

        foreach (DedoRig d in dedos)
        {
            if (d.idx > 3) continue;
            Vector3 referencia = d.idx <= 1 && nIM > 1 ? centroIM : centroTodos;
            Vector3 afuera = d.prox.position - referencia;
            Vector3 desplaza = Vector3.Cross(d.palmar, d.largo);   // hacia donde se mueve la punta con angulo positivo
            d.signoAbd = Vector3.Dot(desplaza, afuera) >= 0f ? 1f : -1f;
        }
    }

    void Update()
    {
        if (!listo) return;
        float dt = Time.deltaTime;

        // F2: el limite sigue al nivel elegido; el servomotor nunca cierra mas alla de ese limite.
        float limObjetivo = limites[Mathf.Clamp(nivelFuerza, 0, 2)];
        limiteAct = Mathf.MoveTowards(limiteAct, limObjetivo, dt * 1.5f);
        float tope = Mathf.Lerp(contactoEn, 1f, limiteAct);
        float objetivo = Mathf.Min(ordenCierre, tope);

        float k = 1f - Mathf.Exp(-suavizado * dt);
        float prevCierre = cierreAct;
        cierreAct = Mathf.Lerp(cierreAct, objetivo, k);
        if (Mathf.Abs(cierreAct - objetivo) < 0.0005f) cierreAct = objetivo;

        // F3: el sensor mide solo cuando la yema toca el objeto.
        float objTemp = (Contacto && objetoCaliente) ? tempObjeto : tempAmbiente;
        float vel = objTemp > temp ? velCalentar : velEnfriar;
        temp = Mathf.MoveTowards(temp, objTemp, vel * dt);
        if (temp > limiteTemp) alerta = true;
        else if (temp < limiteTemp - histeresisTemp) alerta = false;

        if (modoManual || modoManual != manualPrev ||
            !Mathf.Approximately(cierreAct, prevCierre) || !Mathf.Approximately(limiteAct, uLimite))
        {
            manualPrev = modoManual;
            Aplicar();
        }

        if (Mathf.Abs(cierreAct - uCierre) > 0.0005f || Mathf.Abs(limiteAct - uLimite) > 0.0005f ||
            Mathf.Abs(temp - uTemp) > 0.05f || nivelFuerza != uNivel ||
            objetoCaliente != uCaliente || alerta != uAlerta)
        {
            uCierre = cierreAct; uLimite = limiteAct; uTemp = temp;
            uNivel = nivelFuerza; uCaliente = objetoCaliente; uAlerta = alerta;
            if (Cambio != null) Cambio(this);
        }
    }

    float PosicionPerilla()
    {
        float l0 = limites[0], l1 = limites[1], l2 = limites[2];
        if (limiteAct <= l1) return 0.5f * Mathf.InverseLerp(l0, l1, limiteAct);
        return 0.5f + 0.5f * Mathf.InverseLerp(l1, l2, limiteAct);
    }

    void Aplicar()
    {
        float signo = invertirCierre ? -1f : 1f;

        for (int i = 0; i < dedos.Count; i++)
        {
            DedoRig d = dedos[i];
            float c, ab = 0f;
            if (modoManual)
            {
                c = Mathf.Clamp01(cierreManual[d.idx]);
                ab = aperturaManual[d.idx] * d.signoAbd;
            }
            else
            {
                c = Mathf.Clamp01((cierreAct - d.retraso) / Mathf.Max(0.0001f, 1f - d.retraso));
                c = Mathf.SmoothStep(0f, 1f, c);
            }

            for (int j = 0; j < d.juntas.Count; j++)
            {
                Junta jt = d.juntas[j];
                Quaternion q = jt.reposo;
                if (j == 0 && Mathf.Abs(ab) > 0.001f) q *= Quaternion.AngleAxis(ab, jt.ejeAbd);
                q *= Quaternion.AngleAxis(signo * jt.grados * c, jt.eje);
                jt.t.localRotation = q;
            }
        }

        if (perilla != null)
        {
            float n = PosicionPerilla();
            if (hayRiel) perilla.localPosition = perillaPosReposo + (railF - railS) * (n - railT0);
            perilla.localRotation = perillaRotReposo * Quaternion.AngleAxis(perillaGrados * n, perillaEjeLocal);
        }

        if (polea != null)
            polea.localRotation = poleaRotReposo * Quaternion.AngleAxis(poleaGrados * cierreAct, poleaEjeLocal);

        VersionPose++;
    }
}
