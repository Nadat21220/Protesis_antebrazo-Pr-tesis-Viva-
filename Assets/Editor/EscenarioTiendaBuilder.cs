using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Construye la tienda alrededor de la mano: una vitrina de cristal sobre un mueble, dentro de un local con paredes,
/// letreros de neon, repisas y focos. Todo se coloca en funcion del tamano real de la mano (H = altura del LOD0),
/// asi que no importa a que escala este el modelo. La orientacion se toma de la palma: el frente de la tienda
/// mira hacia donde mira la palma (el "cliente" ve la palma de frente al saludar).
/// Lo llama ProtesisSceneBuilder.Construir(). Es repetible: borra y vuelve a crear "Escenario_Tienda".
/// </summary>
public static class EscenarioTiendaBuilder
{
    const string Carpeta = "Assets/Materials/Tienda";
    const string NombreRaiz = "Escenario_Tienda";

    static Transform raiz;
    static float H = 7.3f;
    static readonly List<Renderer> neones = new List<Renderer>();
    static readonly List<Light> focosVitrina = new List<Light>();
    static readonly List<Light> lucesAmbiente = new List<Light>();

    public static string Construir(GameObject mano, Light sol, Camera cam, ModoIluminacion luz, GameObject sistema)
    {
        Transform lod0 = null;
        foreach (Transform h in mano.transform)
            if (h.name.EndsWith("_LOD0")) { lod0 = h; break; }
        if (lod0 == null) return "Tienda: no encontre el hijo _LOD0 de la mano; no la construi.";

        Bounds b = new Bounds(lod0.position, Vector3.zero);
        bool hay = false;
        foreach (Renderer r in lod0.GetComponentsInChildren<Renderer>(true))
        {
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        if (!hay) return "Tienda: el LOD0 no tiene renderers.";

        H = b.size.y;
        float m = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (H < 0.6f * m) H = m;

        GameObject viejo = GameObject.Find(NombreRaiz);
        if (viejo != null) Undo.DestroyObjectImmediate(viejo);

        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Materials", "Tienda");

        neones.Clear(); focosVitrina.Clear(); lucesAmbiente.Clear();

        Vector3 frente = FrentePalma(lod0);
        GameObject go = new GameObject(NombreRaiz);
        Undo.RegisterCreatedObjectUndo(go, "Escenario tienda");
        raiz = go.transform;
        raiz.position = new Vector3(b.center.x, b.min.y, b.center.z);
        raiz.rotation = Quaternion.LookRotation(frente, Vector3.up);

        // ------------------------------------------------ materiales
        Material mPodio = Mat("Podio", new Color(0.035f, 0.035f, 0.045f), 0.35f, 0.75f);
        Material mMueble = Mat("Mueble", new Color(0.07f, 0.05f, 0.055f), 0.1f, 0.45f);
        Material mEncimera = Mat("Encimera", new Color(0.02f, 0.02f, 0.025f), 0.2f, 0.9f);
        Material mMarco = Mat("Marco", new Color(0.04f, 0.04f, 0.048f), 0.85f, 0.55f);
        Material mCristal = Mat("Cristal", new Color(0.85f, 0.95f, 1f, 0.06f), 0f, 0.95f, 0f, true);
        Material mPiso = Mat("Piso", new Color(0.05f, 0.04f, 0.045f), 0.1f, 0.6f);
        Material mPared = Mat("Pared", new Color(0.15f, 0.05f, 0.06f), 0f, 0.25f);
        Material mPanel = Mat("PanelPared", new Color(0.24f, 0.075f, 0.09f), 0.1f, 0.45f);
        Material mRepisa = Mat("Repisa", new Color(0.09f, 0.075f, 0.08f), 0.2f, 0.5f);
        Material mCajaRoja = Mat("CajaRoja", new Color(0.75f, 0.08f, 0.1f), 0f, 0.4f);
        Material mCajaBlanca = Mat("CajaBlanca", new Color(0.85f, 0.83f, 0.82f), 0f, 0.35f);
        Material mCajaNegra = Mat("CajaNegra", new Color(0.05f, 0.05f, 0.055f), 0.2f, 0.5f);
        Material mNeon = Mat("Neon_Rojo", new Color(1f, 0.10f, 0.12f), 0f, 0.3f, 1.4f);

        // ------------------------------------------------ medidas (en unidades de H)
        const float ancho = 1.7f, fondo = 1.3f;
        const float yBase = -0.16f, yTecho = 1.40f, yPiso = -0.86f, yCielo = 2.1f, sala = 2.6f;
        float altoCaja = yTecho - yBase;
        float yMedCaja = (yBase + yTecho) * 0.5f;
        const float g = 0.012f;

        Transform grpVitrina = Grupo("Vitrina");
        Transform grpLocal = Grupo("Local");
        Transform grpLuces = Grupo("Luces");

        // ------------------------------------------------ podio y mueble
        Cilindro("Podio", grpVitrina, new Vector3(0f, -0.08f, 0f), 0.62f, 0.16f, mPodio);
        Cilindro("Aro_Podio", grpVitrina, new Vector3(0f, -0.150f, 0f), 0.645f, 0.012f, mNeon, true);

        Caja("Mueble", grpVitrina, new Vector3(0f, (yBase + yPiso) * 0.5f, 0f), new Vector3(ancho + 0.08f, yBase - yPiso, fondo + 0.08f), mMueble);
        Caja("Encimera", grpVitrina, new Vector3(0f, yBase - 0.012f, 0f), new Vector3(ancho + 0.14f, 0.024f, fondo + 0.14f), mEncimera);
        float zFrente = fondo * 0.5f + 0.042f;
        Caja("Led_Frente", grpVitrina, new Vector3(0f, yBase - 0.08f, zFrente), new Vector3(ancho * 0.9f, 0.012f, 0.006f), mNeon, true);
        Caja("Led_Atras", grpVitrina, new Vector3(0f, yBase - 0.08f, -zFrente), new Vector3(ancho * 0.9f, 0.012f, 0.006f), mNeon, true);

        // ------------------------------------------------ cristal y marco
        Caja("Cristal_Frente", grpVitrina, new Vector3(0f, yMedCaja, fondo * 0.5f), new Vector3(ancho, altoCaja, g), mCristal, false, true);
        Caja("Cristal_Atras", grpVitrina, new Vector3(0f, yMedCaja, -fondo * 0.5f), new Vector3(ancho, altoCaja, g), mCristal, false, true);
        Caja("Cristal_Izq", grpVitrina, new Vector3(-ancho * 0.5f, yMedCaja, 0f), new Vector3(g, altoCaja, fondo), mCristal, false, true);
        Caja("Cristal_Der", grpVitrina, new Vector3(ancho * 0.5f, yMedCaja, 0f), new Vector3(g, altoCaja, fondo), mCristal, false, true);
        Caja("Tapa", grpVitrina, new Vector3(0f, yTecho + 0.02f, 0f), new Vector3(ancho + 0.06f, 0.04f, fondo + 0.06f), mMarco);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Caja("Poste", grpVitrina, new Vector3(sx * ancho * 0.5f, yMedCaja, sz * fondo * 0.5f), new Vector3(0.022f, altoCaja, 0.022f), mMarco);
        Caja("Marco_Frente", grpVitrina, new Vector3(0f, yBase + 0.012f, fondo * 0.5f), new Vector3(ancho, 0.024f, 0.026f), mMarco);
        Caja("Marco_Atras", grpVitrina, new Vector3(0f, yBase + 0.012f, -fondo * 0.5f), new Vector3(ancho, 0.024f, 0.026f), mMarco);
        Caja("Marco_Izq", grpVitrina, new Vector3(-ancho * 0.5f, yBase + 0.012f, 0f), new Vector3(0.026f, 0.024f, fondo), mMarco);
        Caja("Marco_Der", grpVitrina, new Vector3(ancho * 0.5f, yBase + 0.012f, 0f), new Vector3(0.026f, 0.024f, fondo), mMarco);

        // ------------------------------------------------ local
        float altoSala = yCielo - yPiso;
        float yMedSala = (yCielo + yPiso) * 0.5f;
        Caja("Piso", grpLocal, new Vector3(0f, yPiso - 0.01f, 0f), new Vector3(sala * 2f, 0.02f, sala * 2f), mPiso);
        Caja("Pared_Fondo", grpLocal, new Vector3(0f, yMedSala, -sala), new Vector3(sala * 2f, altoSala, 0.05f), mPared);
        Caja("Pared_Frente", grpLocal, new Vector3(0f, yMedSala, sala), new Vector3(sala * 2f, altoSala, 0.05f), mPared);
        Caja("Pared_Izq", grpLocal, new Vector3(-sala, yMedSala, 0f), new Vector3(0.05f, altoSala, sala * 2f), mPared);
        Caja("Pared_Der", grpLocal, new Vector3(sala, yMedSala, 0f), new Vector3(0.05f, altoSala, sala * 2f), mPared);
        GameObject techo = Caja("Techo", grpLocal, new Vector3(0f, yCielo + 0.025f, 0f), new Vector3(sala * 2f, 0.05f, sala * 2f), mPared);
        techo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;   // no deja pasar el sol, pero no se ve

        // listones decorativos en las paredes de fondo y de frente
        for (int i = -3; i <= 3; i++)
        {
            Caja("Liston_F" + i, grpLocal, new Vector3(i * 0.62f, yMedSala + 0.1f, -sala + 0.04f), new Vector3(0.13f, altoSala - 0.5f, 0.03f), mPanel);
            Caja("Liston_T" + i, grpLocal, new Vector3(i * 0.62f, yMedSala + 0.1f, sala - 0.04f), new Vector3(0.13f, altoSala - 0.5f, 0.03f), mPanel);
        }

        // repisas con cajas en las paredes laterales
        Material[] cajas = { mCajaRoja, mCajaBlanca, mCajaNegra, mCajaRoja, mCajaBlanca };
        for (int lado = -1; lado <= 1; lado += 2)
            for (int nivel = 0; nivel < 3; nivel++)
            {
                float y = -0.30f + nivel * 0.62f;
                float x = lado * (sala - 0.19f);
                Caja("Repisa", grpLocal, new Vector3(x, y, 0f), new Vector3(0.34f, 0.025f, 3.4f), mRepisa);
                Caja("Led_Repisa", grpLocal, new Vector3(x - lado * 0.14f, y - 0.02f, 0f), new Vector3(0.012f, 0.008f, 3.3f), mNeon, true);
                for (int k = 0; k < 7; k++)
                {
                    float z = -1.5f + k * 0.5f + ((k + nivel + lado) % 3) * 0.05f;
                    float hh = 0.14f + ((k * 7 + nivel * 3) % 4) * 0.04f;
                    Caja("Caja", grpLocal, new Vector3(x, y + 0.0125f + hh * 0.5f, z), new Vector3(0.24f, hh, 0.26f),
                         cajas[(k + nivel + (lado > 0 ? 1 : 0)) % cajas.Length]);
                }
            }

        // letreros de neon (texto 3D + marco). El del fondo se lee desde el lado de la palma; el del frente, desde el dorso.
        Letrero("PRÓTESIS VIVA", grpLocal, new Vector3(0f, 1.32f, -sala + 0.075f), true, mNeon);
        Letrero("MANO ACCESIBLE", grpLocal, new Vector3(0f, 1.32f, sala - 0.075f), false, mNeon);

        // ------------------------------------------------ luces
        Light key = Foco("Foco_Principal", grpLuces, LightType.Spot, new Vector3(0.15f, 1.28f, 0.50f), new Vector3(0f, 0.5f, 0f),
                         new Color(1f, 0.95f, 0.88f), 1.6f, 3f, 44f, LightShadows.Soft, focosVitrina);
        Foco("Foco_Trasero", grpLuces, LightType.Spot, new Vector3(-0.15f, 1.28f, -0.50f), new Vector3(0f, 0.5f, 0f),
             new Color(0.85f, 0.9f, 1f), 1.2f, 3f, 46f, LightShadows.None, focosVitrina);
        Foco("Contraluz", grpLuces, LightType.Spot, new Vector3(-0.50f, 1.25f, -0.15f), new Vector3(0.05f, 0.7f, 0f),
             new Color(0.75f, 0.85f, 1f), 1.0f, 3f, 40f, LightShadows.None, focosVitrina);
        Foco("Acento_Rojo", grpLuces, LightType.Point, new Vector3(0.62f, 0.15f, 0.30f), new Vector3(0f, 0.5f, 0f),
             new Color(1f, 0.12f, 0.12f), 0.9f, 1.6f, 0f, LightShadows.None, focosVitrina);
        Foco("Neon_Fondo", grpLuces, LightType.Point, new Vector3(0f, 1.2f, -sala + 0.6f), new Vector3(0f, 1.2f, 0f),
             new Color(1f, 0.15f, 0.15f), 0.9f, 3.2f, 0f, LightShadows.None, focosVitrina);
        Foco("Neon_Frente", grpLuces, LightType.Point, new Vector3(0f, 1.2f, sala - 0.6f), new Vector3(0f, 1.2f, 0f),
             new Color(1f, 0.15f, 0.15f), 0.9f, 3.2f, 0f, LightShadows.None, focosVitrina);

        GameObject foco = new GameObject("Foco_Mano");
        foco.transform.SetParent(raiz, false);
        foco.transform.position = b.center;

        Luz("Techo_Calido_A", grpLuces, new Vector3(0f, 1.9f, 1.4f), new Color(1f, 0.86f, 0.72f), 1.3f * H * H, 5.5f * H);
        Luz("Techo_Calido_B", grpLuces, new Vector3(0f, 1.9f, -1.4f), new Color(1f, 0.86f, 0.72f), 1.3f * H * H, 5.5f * H);

        // ------------------------------------------------ componente de ejecucion
        EscenarioTienda et = go.AddComponent<EscenarioTienda>();
        et.luz = luz;
        et.camara = cam;
        et.foco = foco.transform;
        et.alturaMano = H;
        et.focosVitrina = focosVitrina.ToArray();
        et.lucesAmbiente = lucesAmbiente.ToArray();
        et.neones = neones.ToArray();
        EditorUtility.SetDirty(et);

        // ------------------------------------------------ ambiente
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.30f, 0.26f, 0.27f);
        RenderSettings.reflectionIntensity = 0.4f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.05f, 0.02f, 0.03f);
        RenderSettings.fogDensity = 0.05f / H;
        if (sol != null)
        {
            Undo.RecordObject(sol, "Sol tienda");
            sol.intensity = 0.6f;
            sol.shadows = LightShadows.Soft;
            EditorUtility.SetDirty(sol);
        }

        string urp = AjustarURP();
        return "Tienda: vitrina y local creados (H = " + H.ToString("0.0") + " u). " + urp +
               "\nAjusta la intensidad en Escenario_Tienda/Luces si la ves muy oscura o quemada.";
    }

    // ------------------------------------------------------------------ orientacion
    // Direccion horizontal hacia donde mira la palma (lado de los sensores de presion). Misma geometria que ManoAgarre.
    static Vector3 FrentePalma(Transform lod0)
    {
        Dictionary<string, Transform> d = new Dictionary<string, Transform>();
        foreach (Transform t in lod0.GetComponentsInChildren<Transform>(true)) d[t.name] = t;
        Vector3 suma = Vector3.zero;
        string[] dedos = { "Indice", "Medio", "Anular", "Menique" };
        foreach (string f in dedos)
        {
            Transform p, ds, s;
            if (!d.TryGetValue(f + "_Proximal_L0", out p) || !d.TryGetValue(f + "_Distal_L0", out ds) ||
                !d.TryGetValue(f + "_Sensor_Presion_L0", out s)) continue;
            Vector3 largo = (ds.position - p.position).normalized;
            Vector3 hs = s.position - ds.position;
            Vector3 pal = hs - Vector3.Dot(hs, largo) * largo;
            if (pal.sqrMagnitude > 1e-8f) suma += pal.normalized;
        }
        suma.y = 0f;
        if (suma.sqrMagnitude < 1e-4f) return Vector3.forward;
        return suma.normalized;
    }

    // ------------------------------------------------------------------ primitivas
    static Transform Grupo(string nombre)
    {
        GameObject g = new GameObject(nombre);
        g.transform.SetParent(raiz, false);
        return g.transform;
    }

    static GameObject Caja(string nombre, Transform padre, Vector3 posH, Vector3 tamH, Material mat, bool neon = false, bool sinSombra = false)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = nombre;
        Object.DestroyImmediate(c.GetComponent<Collider>());
        c.transform.SetParent(padre, false);
        c.transform.localPosition = posH * H;
        c.transform.localScale = tamH * H;
        Renderer r = c.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        if (neon) { r.shadowCastingMode = ShadowCastingMode.Off; neones.Add(r); }
        if (sinSombra) r.shadowCastingMode = ShadowCastingMode.Off;
        if (!neon) GameObjectUtility.SetStaticEditorFlags(c, StaticEditorFlags.BatchingStatic);
        return c;
    }

    static GameObject Cilindro(string nombre, Transform padre, Vector3 posH, float diametroH, float alturaH, Material mat, bool neon = false)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = nombre;
        Object.DestroyImmediate(c.GetComponent<Collider>());
        c.transform.SetParent(padre, false);
        c.transform.localPosition = posH * H;
        c.transform.localScale = new Vector3(diametroH * H, alturaH * H * 0.5f, diametroH * H);
        Renderer r = c.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        if (neon) { r.shadowCastingMode = ShadowCastingMode.Off; neones.Add(r); }
        return c;
    }

    static void Letrero(string texto, Transform padre, Vector3 posH, bool miraAdelante, Material neon)
    {
        GameObject go = new GameObject("Letrero");
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posH * H;
        // el texto 3D se lee desde su lado -Z: el del fondo (mira a +Z del local) se gira 180 grados
        go.transform.localRotation = miraAdelante ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = texto;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.color = new Color(1f, 0.30f, 0.30f);
        t.enableAutoSizing = true;
        t.fontSizeMin = 1f;
        t.fontSizeMax = 1000f;
        t.rectTransform.sizeDelta = new Vector2(2.5f * H, 0.40f * H);
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.shadowCastingMode = ShadowCastingMode.Off;

        float z = posH.z;
        float y = posH.y;
        float dz = miraAdelante ? 0.012f : -0.012f;
        Caja("Neon_Sup", padre, new Vector3(0f, y + 0.26f, z + dz), new Vector3(2.7f, 0.016f, 0.016f), neon, true);
        Caja("Neon_Inf", padre, new Vector3(0f, y - 0.26f, z + dz), new Vector3(2.7f, 0.016f, 0.016f), neon, true);
        Caja("Neon_Izq", padre, new Vector3(-1.35f, y, z + dz), new Vector3(0.016f, 0.536f, 0.016f), neon, true);
        Caja("Neon_Der", padre, new Vector3(1.35f, y, z + dz), new Vector3(0.016f, 0.536f, 0.016f), neon, true);
    }

    // ------------------------------------------------------------------ luces
    // intensidad = k * distancia^2 : la iluminacion sobre la mano no depende de la escala del modelo.
    static Light Foco(string nombre, Transform padre, LightType tipo, Vector3 posH, Vector3 miraH, Color color,
                      float k, float rangoH, float angulo, LightShadows sombra, List<Light> lista)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posH * H;
        if (tipo == LightType.Spot)
        {
            Vector3 destino = raiz.TransformPoint(miraH * H);
            go.transform.rotation = Quaternion.LookRotation(destino - go.transform.position, Vector3.up);
        }
        Light l = go.AddComponent<Light>();
        l.type = tipo;
        l.color = color;
        float d = Mathf.Max(0.5f, (posH - miraH).magnitude) * H;
        l.intensity = k * d * d;
        l.range = rangoH * H;
        if (tipo == LightType.Spot)
        {
            l.spotAngle = angulo;
            l.innerSpotAngle = angulo * 0.55f;
        }
        l.shadows = sombra;
        if (sombra != LightShadows.None) { l.shadowStrength = 0.85f; l.shadowBias = 0.03f; l.shadowNormalBias = 0.4f; }
        lista.Add(l);
        return l;
    }

    static void Luz(string nombre, Transform padre, Vector3 posH, Color color, float intensidad, float rango)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posH * H;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensidad;
        l.range = rango;
        l.shadows = LightShadows.None;
        lucesAmbiente.Add(l);
    }

    // ------------------------------------------------------------------ materiales
    static Material Mat(string nombre, Color color, float metal, float suave, float emision = 0f, bool transparente = false)
    {
        string ruta = Carpeta + "/" + nombre + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { name = nombre };
            AssetDatabase.CreateAsset(m, ruta);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Smoothness", suave);
        if (transparente) ManoMaterialUtil.ConfigurarTransparente(m);
        if (emision > 0f) ManoMaterialUtil.AplicarEmision(m, "_EmissionColor", null, color, emision);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ URP
    // Luces adicionales por pixel (el proyecto las tenia por vertice: los focos se verian mal sobre superficies grandes),
    // sombras para el foco principal y mas distancia de sombras (la mano mide ~7 unidades; por defecto son 3 m).
    static string AjustarURP()
    {
        HashSet<RenderPipelineAsset> assets = new HashSet<RenderPipelineAsset>();
        if (GraphicsSettings.defaultRenderPipeline != null) assets.Add(GraphicsSettings.defaultRenderPipeline);
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            RenderPipelineAsset a = QualitySettings.GetRenderPipelineAssetAt(i);
            if (a != null) assets.Add(a);
        }
        int n = 0;
        foreach (RenderPipelineAsset a in assets)
        {
            SerializedObject so = new SerializedObject(a);
            SerializedProperty p = so.FindProperty("m_AdditionalLightsRenderingMode");
            if (p != null) p.intValue = 1;   // 1 = PerPixel
            p = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (p != null && p.intValue < 6) p.intValue = 6;
            p = so.FindProperty("m_AdditionalLightShadowsSupported");
            if (p != null) p.boolValue = true;
            p = so.FindProperty("m_ShadowDistance");
            if (p != null && p.floatValue < 40f) p.floatValue = 40f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(a);
            n++;
        }
        if (n > 0) AssetDatabase.SaveAssets();
        return "URP: luces por pixel y sombras ajustadas en " + n + " asset(s).";
    }
}
