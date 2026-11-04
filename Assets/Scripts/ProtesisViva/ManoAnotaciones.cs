using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modo explicacion: banners con lineas guia que senalan las partes de la mano.
/// La capa de UI la construye ProtesisSceneBuilder; este script solo la mueve:
///  - ubica el punto de cada parte en pantalla (sigue a la camara y a la pieza),
///  - reparte los banners en dos columnas (izquierda / derecha) segun donde cae cada punto, sin cruzar lineas,
///  - los muestra uno por uno con una linea que crece hasta la pieza.
/// </summary>
public class ManoAnotaciones : MonoBehaviour
{
    [Serializable]
    public class Item
    {
        public string titulo;
        [TextArea] public string texto;
        [Tooltip("Se usa la primera pieza del LOD0 cuyo nombre contenga alguna de estas palabras.")]
        public string[] piezas;
        [Tooltip("Alternativa: nombre (inicio) de un material, p. ej. Luz_Roja_Advertencia.")]
        public string material;
        [NonSerialized] public Transform ancla;
        [NonSerialized] public Vector3 offsetLocal;
        [NonSerialized] public bool valido;
    }

    public ManoAgarre mano;
    public Camera camara;
    public RectTransform contenedor;
    public CanvasGroup grupo;

    [Header("Un elemento de UI por item (el builder los crea)")]
    public RectTransform[] banners;
    public TMP_Text[] titulos;
    public TMP_Text[] cuerpos;
    public Image[] lineas;
    public RectTransform[] puntos;

    [Header("Animacion")]
    public float retraso = 1.2f;
    public float escalonado = 0.22f;
    public float grosorLinea = 3f;

    public List<Item> items = new List<Item>();

    public bool Visible { get; private set; }

    float t;
    bool asignado;
    int[] slotDe;                 // item -> slot (0..2 izquierda, 3..5 derecha)
    bool resuelto;

    // geometria de los banners (fracciones del contenedor)
    const float XIzqMin = 0.02f, XIzqMax = 0.27f, XDerMin = 0.73f, XDerMax = 0.98f;
    static readonly float[] YCentro = { 0.78f, 0.50f, 0.22f };
    const float Alto = 0.21f;

    void Reset() { CargarPorDefecto(); }

    public void CargarPorDefecto()
    {
        items = new List<Item>
        {
            new Item { piezas = new[] { "Indice_Sensor_Presion", "Sensor_Presion" } },
            new Item { piezas = new[] { "Sensor_Temp", "Temperatura" }, material = "Naranja_Sensor_Temp" },
            new Item { piezas = new[] { "Luz_Roja", "F3_Luz" }, material = "Luz_Roja_Advertencia" },
            new Item { piezas = new[] { "F1_Motor_Vibracion" }, material = "Azul_Motor_Vibracion" },
            new Item { piezas = new[] { "F2_Control_Fuerza_Perilla", "Perilla" } },
            new Item { piezas = new[] { "F2_Polea", "F2_Servo", "Servo", "F2_Cable_Medio" } }
        };
        int n = Mathf.Min(items.Count, ProtesisVivaTextos.AnotTitulos.Length);
        for (int i = 0; i < n; i++)
        {
            items[i].titulo = ProtesisVivaTextos.AnotTitulos[i];
            items[i].texto = ProtesisVivaTextos.AnotCuerpos[i];
        }
    }

    void Start()
    {
        if (mano == null) mano = FindFirstObjectByType<ManoAgarre>();
        if (camara == null) camara = Camera.main;
        if (items == null || items.Count == 0) CargarPorDefecto();
        PonerTextos();
        Apagar();
    }

    void PonerTextos()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (titulos != null && i < titulos.Length && titulos[i] != null) titulos[i].text = items[i].titulo;
            if (cuerpos != null && i < cuerpos.Length && cuerpos[i] != null) cuerpos[i].text = items[i].texto;
        }
    }

    public void Mostrar()
    {
        if (!resuelto) Resolver();
        Visible = true;
        t = 0f;
        asignado = false;
        if (grupo != null) { grupo.alpha = 0f; grupo.gameObject.SetActive(true); }
    }

    public void Ocultar() { Visible = false; }

    void Apagar()
    {
        if (grupo != null) { grupo.alpha = 0f; grupo.gameObject.SetActive(false); }
    }

    // Busca en el LOD0 la pieza de cada item y guarda un punto en su espacio local (sigue a la pieza si se mueve).
    void Resolver()
    {
        resuelto = true;
        if (mano == null) return;
        Transform lod0 = null;
        foreach (Transform h in mano.transform)
            if (h.name.EndsWith("_LOD0")) { lod0 = h; break; }
        if (lod0 == null) return;

        Transform[] todos = lod0.GetComponentsInChildren<Transform>(true);
        Renderer[] rends = lod0.GetComponentsInChildren<Renderer>(true);

        foreach (Item it in items)
        {
            Renderer r = null;
            if (it.piezas != null)
                foreach (string clave in it.piezas)
                {
                    foreach (Transform tr in todos)
                    {
                        if (tr.name.IndexOf(clave, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        r = tr.GetComponent<Renderer>();
                        if (r == null) r = tr.GetComponentInChildren<Renderer>();
                        if (r != null) break;
                    }
                    if (r != null) break;
                }
            if (r == null && !string.IsNullOrEmpty(it.material))
                foreach (Renderer rr in rends)
                {
                    foreach (Material m in rr.sharedMaterials)
                        if (m != null && m.name.StartsWith(it.material, StringComparison.Ordinal)) { r = rr; break; }
                    if (r != null) break;
                }

            if (r == null) { Debug.LogWarning("ManoAnotaciones: no encontre la pieza de '" + it.titulo + "'."); continue; }
            it.ancla = r.transform;
            it.offsetLocal = r.transform.InverseTransformPoint(r.bounds.center);
            it.valido = true;
        }
    }

    void LateUpdate()
    {
        if (grupo == null || contenedor == null || camara == null) return;

        if (!Visible)
        {
            if (grupo.alpha > 0f)
            {
                grupo.alpha = Mathf.MoveTowards(grupo.alpha, 0f, Time.deltaTime * 4f);
                if (grupo.alpha <= 0f) Apagar();
                else Dibujar(1f);
            }
            return;
        }

        t += Time.deltaTime;
        float espera = retraso;
        if (t < espera) { grupo.alpha = 0f; return; }
        float k = t - espera;
        grupo.alpha = Mathf.Clamp01(k / 0.35f);

        if (!asignado) Asignar();
        Dibujar(k);
    }

    Vector2 PuntoPantalla(Item it, out bool visible)
    {
        Vector3 mundo = it.ancla.TransformPoint(it.offsetLocal);
        Vector3 sp = camara.WorldToScreenPoint(mundo);
        visible = sp.z > 0.01f;
        Vector2 lp;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(contenedor, sp, null, out lp);
        return lp;
    }

    // Reparte los items: los de la izquierda del dibujo van a la columna izquierda, ordenados de arriba a abajo.
    void Asignar()
    {
        int n = Mathf.Min(items.Count, banners.Length);
        List<int> v = new List<int>();
        Vector2[] p = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            if (!items[i].valido) continue;
            bool vis;
            p[i] = PuntoPantalla(items[i], out vis);
            v.Add(i);
        }

        v.Sort((a, b) => p[a].x.CompareTo(p[b].x));
        int mitad = (v.Count + 1) / 2;
        List<int> izq = v.GetRange(0, Mathf.Min(mitad, 3));
        List<int> der = v.GetRange(Mathf.Min(mitad, 3), Mathf.Min(v.Count - Mathf.Min(mitad, 3), 3));
        izq.Sort((a, b) => p[b].y.CompareTo(p[a].y));
        der.Sort((a, b) => p[b].y.CompareTo(p[a].y));

        slotDe = new int[n];
        for (int i = 0; i < n; i++) slotDe[i] = -1;
        for (int s = 0; s < izq.Count; s++) slotDe[izq[s]] = s;
        for (int s = 0; s < der.Count; s++) slotDe[der[s]] = 3 + s;

        for (int i = 0; i < n; i++)
        {
            if (slotDe[i] < 0) continue;
            bool izqLado = slotDe[i] < 3;
            int fila = slotDe[i] % 3;
            RectTransform bn = banners[i];
            bn.anchorMin = new Vector2(izqLado ? XIzqMin : XDerMin, YCentro[fila] - Alto * 0.5f);
            bn.anchorMax = new Vector2(izqLado ? XIzqMax : XDerMax, YCentro[fila] + Alto * 0.5f);
            bn.offsetMin = bn.offsetMax = Vector2.zero;
        }
        asignado = true;
    }

    void Dibujar(float k)
    {
        if (!asignado || slotDe == null) return;
        Rect r = contenedor.rect;
        int n = slotDe.Length;
        for (int i = 0; i < n; i++)
        {
            bool activo = slotDe[i] >= 0 && items[i].valido;
            if (banners[i] != null) banners[i].gameObject.SetActive(activo);
            if (lineas[i] != null) lineas[i].gameObject.SetActive(activo);
            if (puntos[i] != null) puntos[i].gameObject.SetActive(activo);
            if (!activo) continue;

            float p = Mathf.Clamp01((k - i * escalonado) / 0.45f);
            float e = Mathf.SmoothStep(0f, 1f, p);

            bool izqLado = slotDe[i] < 3;
            int fila = slotDe[i] % 3;
            banners[i].localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, e);
            CanvasGroup cg = banners[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = e;

            bool vis;
            Vector2 fin = PuntoPantalla(items[i], out vis);
            float ex = izqLado ? XIzqMax : XDerMin;
            Vector2 ini = new Vector2(r.xMin + r.width * ex, r.yMin + r.height * YCentro[fila]);

            Vector2 d = fin - ini;
            float largo = d.magnitude;
            RectTransform lr = lineas[i].rectTransform;
            lr.anchoredPosition = ini;
            lr.sizeDelta = new Vector2(largo * e, grosorLinea);
            lr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            lineas[i].enabled = vis;

            float pulso = 1f + 0.18f * Mathf.Sin(Time.time * 4f + i);
            puntos[i].anchoredPosition = fin;
            puntos[i].localScale = Vector3.one * (e * pulso);
            puntos[i].gameObject.SetActive(vis);
        }
    }
}
