using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hace que los cables amarillos F2_Cable_* (Indice, Medio, Anular, Menique, Pulgar) se doblen junto con los dedos.
/// Los cables son mallas rigidas; aqui se deforman por CPU (skinning lineal) siguiendo la cadena
/// Proximal -> Media -> Distal -> Sensor de cada dedo. Se hace en los 3 LOD, cada uno con sus propios huesos.
/// Requiere que la malla del FBX tenga Read/Write activado (Tools > Mano > Agregar cables F2 lo activa solo).
/// Va en el mismo objeto que ManoAgarre.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]   // despues de ManoAgarre (Update) y de ManoLODSync (LateUpdate)
[RequireComponent(typeof(ManoAgarre))]
public class ManoCablesF2 : MonoBehaviour
{
    [Tooltip("Semiancho de la zona que se dobla alrededor de cada articulacion (en unidades del modelo).")]
    [Range(0.05f, 0.6f)] public float zonaDeFlexion = 0.25f;

    class Cable
    {
        public Transform t;
        public Mesh malla;
        public Vector3[] v0, n0, v, n;
        public float[] pesos;            // (huesos + 1) pesos por vertice
        public Transform[] huesos;
        public Matrix4x4[] reposoInv;    // inversa de la pose de reposo del hueso, en espacio del cable
    }

    static readonly string[] Dedos = { "Indice", "Medio", "Anular", "Menique", "Pulgar" };

    readonly List<Cable> cables = new List<Cable>();
    ManoAgarre agarre;
    int ultima = -1;

    void Start()
    {
        agarre = GetComponent<ManoAgarre>();
        for (int lod = 0; lod < 3; lod++) ConstruirLod(lod);
        if (cables.Count == 0) { enabled = false; return; }
        Deformar();
    }

    void ConstruirLod(int lod)
    {
        Transform raiz = null;
        foreach (Transform h in transform)
            if (h.name.EndsWith("_LOD" + lod)) { raiz = h; break; }
        if (raiz == null) return;

        Dictionary<string, Transform> piezas = new Dictionary<string, Transform>();
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) piezas[t.name] = t;

        string s = "_L" + lod;
        foreach (string dedo in Dedos)
        {
            Transform cable, sensor;
            if (!piezas.TryGetValue("F2_Cable_" + dedo + s, out cable)) continue;
            piezas.TryGetValue(dedo + "_Sensor_Presion" + s, out sensor);

            List<Transform> huesos = new List<Transform>();
            string[] nombres = dedo == "Pulgar"
                ? new[] { "Pulgar_Proximal", "Pulgar_Distal" }
                : new[] { dedo + "_Proximal", dedo + "_Media", dedo + "_Distal" };
            bool ok = true;
            foreach (string n in nombres)
            {
                Transform h;
                if (piezas.TryGetValue(n + s, out h)) huesos.Add(h); else ok = false;
            }
            if (!ok || sensor == null) { Debug.LogWarning("ManoCablesF2: faltan piezas para " + dedo + s); continue; }

            Cable c = Preparar(cable, huesos.ToArray(), sensor);
            if (c != null) cables.Add(c);
        }
    }

    Cable Preparar(Transform cable, Transform[] huesos, Transform sensor)
    {
        MeshFilter mf = cable.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return null;
        if (!mf.sharedMesh.isReadable)
        {
            Debug.LogWarning("ManoCablesF2: la malla '" + mf.sharedMesh.name + "' no es legible. Activa Read/Write en el FBX (Tools > Mano > Agregar cables F2).");
            return null;
        }

        Cable c = new Cable();
        c.t = cable;
        c.malla = Instantiate(mf.sharedMesh);
        c.malla.name = mf.sharedMesh.name + "_deformable";
        c.malla.MarkDynamic();
        mf.sharedMesh = c.malla;
        c.v0 = c.malla.vertices;
        c.n0 = c.malla.normals;
        c.v = new Vector3[c.v0.Length];
        c.n = new Vector3[c.n0.Length];
        c.huesos = huesos;

        Matrix4x4 w2c = cable.worldToLocalMatrix;
        int nb = huesos.Length;
        c.reposoInv = new Matrix4x4[nb];
        for (int k = 0; k < nb; k++) c.reposoInv[k] = (w2c * huesos[k].localToWorldMatrix).inverse;

        // polilinea de reposo (espacio del cable): inicio del cable -> articulaciones -> sensor de la yema
        Vector3 inicio = InicioCable(c.v0);
        List<Vector3> K = new List<Vector3> { inicio };
        for (int k = 0; k < nb; k++) K.Add(w2c.MultiplyPoint3x4(huesos[k].position));
        K.Add(w2c.MultiplyPoint3x4(sensor.position));

        float[] acum = new float[K.Count];
        for (int i = 1; i < K.Count; i++) acum[i] = acum[i - 1] + Vector3.Distance(K[i - 1], K[i]);

        c.pesos = new float[c.v0.Length * (nb + 1)];
        float w = Mathf.Max(0.01f, zonaDeFlexion);
        float[] b = new float[nb + 2];
        for (int vi = 0; vi < c.v0.Length; vi++)
        {
            float u = Proyectar(c.v0[vi], K, acum);
            b[0] = 1f;
            for (int k = 1; k <= nb; k++) b[k] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(acum[k] - w, acum[k] + w, u));
            b[nb + 1] = 0f;
            for (int k = 0; k <= nb; k++) c.pesos[vi * (nb + 1) + k] = b[k] - b[k + 1];
        }
        return c;
    }

    static Vector3 InicioCable(Vector3[] v)
    {
        // promedio de los vertices mas cercanos al extremo de la muneca (menor z local)
        float zMin = float.MaxValue;
        for (int i = 0; i < v.Length; i++) if (v[i].z < zMin) zMin = v[i].z;
        Vector3 suma = Vector3.zero; int n = 0;
        for (int i = 0; i < v.Length; i++)
            if (v[i].z < zMin + 0.05f) { suma += v[i]; n++; }
        return n > 0 ? suma / n : v[0];
    }

    static float Proyectar(Vector3 p, List<Vector3> K, float[] acum)
    {
        float mejorD = float.MaxValue, mejorU = 0f;
        for (int i = 0; i < K.Count - 1; i++)
        {
            Vector3 d = K[i + 1] - K[i];
            float l2 = Mathf.Max(1e-8f, d.sqrMagnitude);
            float t = Mathf.Clamp01(Vector3.Dot(p - K[i], d) / l2);
            float dist = (p - (K[i] + d * t)).sqrMagnitude;
            if (dist < mejorD) { mejorD = dist; mejorU = acum[i] + t * Mathf.Sqrt(l2); }
        }
        return mejorU;
    }

    void LateUpdate()
    {
        int v = agarre.VersionPose;
        if (v == ultima) return;
        ultima = v;
        Deformar();
    }

    void Deformar()
    {
        for (int ci = 0; ci < cables.Count; ci++)
        {
            Cable c = cables[ci];
            int nb = c.huesos.Length;
            Matrix4x4 w2c = c.t.worldToLocalMatrix;
            Matrix4x4[] D = new Matrix4x4[nb + 1];
            D[0] = Matrix4x4.identity;
            for (int k = 0; k < nb; k++) D[k + 1] = w2c * c.huesos[k].localToWorldMatrix * c.reposoInv[k];

            for (int vi = 0; vi < c.v0.Length; vi++)
            {
                Vector3 p = Vector3.zero, n = Vector3.zero;
                for (int k = 0; k <= nb; k++)
                {
                    float w = c.pesos[vi * (nb + 1) + k];
                    if (w < 1e-4f) continue;
                    p += D[k].MultiplyPoint3x4(c.v0[vi]) * w;
                    if (vi < c.n0.Length) n += D[k].MultiplyVector(c.n0[vi]) * w;
                }
                c.v[vi] = p;
                if (vi < c.n.Length) c.n[vi] = n.normalized;
            }
            c.malla.vertices = c.v;
            if (c.n.Length == c.v.Length) c.malla.normals = c.n;
            c.malla.RecalculateBounds();
        }
    }
}
