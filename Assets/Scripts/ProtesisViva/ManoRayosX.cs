using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Vista de rayos X: hace semitransparente la carcasa (Antebrazo y Palma, material Rojo_Chasis)
/// para ver cables, poleas, servomotor y riel. Va en el mismo objeto que ManoAgarre.
///
/// Para evitar overdraw en Android:
///  - solo 2 piezas por LOD pasan a transparente (las demás siguen opacas),
///  - ZWrite desactivado y sin sombra en el material transparente,
///  - se usa UNA instancia de material compartida por todas las piezas,
///  - en cuanto el fundido llega a opaco se vuelven a poner los materiales originales (cero coste fuera de la Vista 3).
/// </summary>
[DisallowMultipleComponent]
public class ManoRayosX : MonoBehaviour
{
    [Tooltip("Material URP/Lit en modo Transparent (menú Tools > Protesis Viva > 2). Si se deja vacío se crea en tiempo de ejecución.")]
    public Material materialTransparente;
    public string[] piezas = { "Antebrazo", "Palma" };
    [Range(0.05f, 0.6f)] public float alpha = 0.22f;
    public float duracion = 0.45f;

    static readonly Regex Sufijo = new Regex(@"_(L|LOD)\d$");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    class Item
    {
        public Renderer r;
        public Material[] originales;
        public ShadowCastingMode sombra;
    }

    readonly List<Item> items = new List<Item>();
    Material instancia;
    Color colorBase = Color.white;
    bool activo;
    bool aplicado;
    bool fallo;
    float t01;

    public bool Activo { get { return activo; } }
    public void Activar(bool valor) { activo = valor; }

    void Start()
    {
        HashSet<string> nombres = new HashSet<string>(piezas);
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!nombres.Contains(Sufijo.Replace(r.name, ""))) continue;
            items.Add(new Item { r = r, originales = r.sharedMaterials, sombra = r.shadowCastingMode });
        }

        if (items.Count > 0 && items[0].originales.Length > 0)
        {
            Material m0 = items[0].originales[0];
            if (m0 != null && m0.HasProperty(BaseColorId)) colorBase = m0.GetColor(BaseColorId);
        }
        else
        {
            Debug.LogWarning("ManoRayosX: no encontre renderers para " + string.Join(", ", piezas));
        }
    }

    void CrearInstancia()
    {
        if (instancia != null || fallo) return;

        if (materialTransparente != null)
        {
            instancia = new Material(materialTransparente);
        }
        else
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) { fallo = true; Debug.LogWarning("ManoRayosX: no hay material transparente ni shader URP/Lit."); return; }
            instancia = new Material(sh);
            ManoMaterialUtil.ConfigurarTransparente(instancia);
        }
        instancia.name = "Carcasa_RayosX (runtime)";
        instancia.SetColor(BaseColorId, new Color(colorBase.r, colorBase.g, colorBase.b, 1f));
    }

    void Update()
    {
        float objetivo = activo ? 1f : 0f;
        if (!Mathf.Approximately(t01, objetivo))
            t01 = Mathf.MoveTowards(t01, objetivo, Time.deltaTime / Mathf.Max(0.01f, duracion));

        if (t01 > 0f && !aplicado && !fallo)
        {
            CrearInstancia();
            if (instancia != null) CambiarMateriales(true);
        }

        if (aplicado)
        {
            Color c = colorBase;
            c.a = Mathf.Lerp(1f, alpha, Mathf.SmoothStep(0f, 1f, t01));
            instancia.SetColor(BaseColorId, c);

            if (t01 <= 0f) CambiarMateriales(false);
        }
    }

    void CambiarMateriales(bool rayosX)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            if (rayosX)
            {
                Material[] arr = new Material[it.originales.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = instancia;
                it.r.sharedMaterials = arr;
                it.r.shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                it.r.sharedMaterials = it.originales;
                it.r.shadowCastingMode = it.sombra;
            }
        }
        aplicado = rayosX;
    }
}
