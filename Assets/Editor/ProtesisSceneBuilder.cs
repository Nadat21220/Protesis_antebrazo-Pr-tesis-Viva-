using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Un clic: Tools > Mano > Construir Escena Completa y UI
///  1. Localiza ProtMano / Protesis_Mano_LODs (LODGroup), Directional Light, Camera y Global Volume.
///  2. Extrae los materiales del FBX a Assets/Materials/Mano y los reasigna a todos los renderers (66 x 3 LOD).
///  3. Agrega ManoLODSync, ManoAgarre, ManoSensoresFeedback y ManoRayosX y conecta sus referencias.
///  4. Crea ProtesisViva_Sistema (ModoIluminacion, CamaraSelector, ProtesisVivaControles).
///  5. Construye el Canvas (TextMeshPro, 1080x1920), el EventSystem y cablea todos los botones / el slider
///     con listeners persistentes (se ven en el Inspector).
/// Es repetible: si ya existe el Canvas lo reconstruye.
/// </summary>
public static class ProtesisSceneBuilder
{
    const string CarpetaMat = "Assets/Materials/Mano";
    const string RutaRayosX = "Assets/Materials/Carcasa_RayosX.mat";
    const string RutaLuzRoja = "Assets/Materials/Luz_Roja_Advertencia.mat";
    const string NombreCanvas = "Canvas_ProtesisViva";

    static readonly Color Fondo = new Color(0.07f, 0.01f, 0.02f, 1f);
    static readonly Color Panel = new Color(0.14f, 0.03f, 0.04f, 0.94f);
    static readonly Color PanelClaro = new Color(0.24f, 0.06f, 0.07f, 1f);
    static readonly Color Acento = new Color(0.93f, 0.14f, 0.17f, 1f);
    static readonly Color AcentoSuave = new Color(0.93f, 0.14f, 0.17f, 0.30f);
    static readonly Color Borde = new Color(1f, 0.42f, 0.42f, 0.55f);
    static readonly Color Blanco = new Color(0.98f, 0.96f, 0.96f, 1f);
    static readonly Color Gris = new Color(0.88f, 0.72f, 0.72f, 1f);

    // ------------------------------------------------------------------ menus

    [MenuItem("Tools/Mano/Construir Escena Completa y UI")]
    public static void Construir()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("Prótesis Viva",
                "Faltan los recursos de TextMeshPro.\nWindow > TextMeshPro > Import TMP Essential Resources, y vuelve a ejecutar.", "OK");
            return;
        }

        LODGroup lod = Object.FindFirstObjectByType<LODGroup>();
        if (lod == null)
        {
            EditorUtility.DisplayDialog("Prótesis Viva",
                "No encontré el LODGroup de Protesis_Mano_LODs. Abre la escena Protecis_Mano.unity.", "OK");
            return;
        }

        GameObject mano = lod.gameObject;
        Light sol = RenderSettings.sun != null ? RenderSettings.sun : BuscarLuzDireccional();
        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        Volume vol = BuscarVolumenGlobal();

        string avisos = "";
        if (sol == null) avisos += "\n- No encontré Directional Light.";
        if (cam == null) avisos += "\n- No encontré Camera.";
        if (vol == null) avisos += "\n- No encontré Global Volume (con Bloom).";

        // 1. materiales
        string resMat = ExtraerYReasignarMateriales(mano);

        // 2. componentes de la mano
        Agregar<ManoLODSync>(mano);
        ManoAgarre agarre = Agregar<ManoAgarre>(mano);
        string resCables = ActivarLecturaMallas() + " " + (Agregar<ManoCablesF2>(mano) != null ? "ManoCablesF2 listo." : "");
        ManoSensoresFeedback sens = Agregar<ManoSensoresFeedback>(mano);
        ManoRayosX rayos = Agregar<ManoRayosX>(mano);
        ManoGestos gestos = Agregar<ManoGestos>(mano);
        sens.agarre = agarre;
        rayos.materialTransparente = ObtenerMaterialRayosX();
        Marcar(agarre); Marcar(sens); Marcar(rayos); Marcar(gestos);

        // 3. sistema
        GameObject sis = GameObject.Find("ProtesisViva_Sistema");
        if (sis == null)
        {
            sis = new GameObject("ProtesisViva_Sistema");
            Undo.RegisterCreatedObjectUndo(sis, "Prótesis Viva");
        }
        ProtesisVivaUI viejo = sis.GetComponent<ProtesisVivaUI>();
        if (viejo != null) Undo.DestroyObjectImmediate(viejo);   // esta version construia la UI en Play; ya no hace falta

        ModoIluminacion luz = Agregar<ModoIluminacion>(sis);
        luz.sol = sol; luz.volumen = vol; luz.camara = cam;
        CamaraSelector camaras = Agregar<CamaraSelector>(sis);
        camaras.camara = cam; camaras.mano = agarre; camaras.rayos = rayos;
        ProtesisVivaControles ctl = Agregar<ProtesisVivaControles>(sis);
        ctl.agarre = agarre; ctl.camaras = camaras; ctl.luz = luz;
        Marcar(luz); Marcar(camaras); Marcar(ctl);

        ManoAnotaciones anot = Agregar<ManoAnotaciones>(sis);
        anot.mano = agarre; anot.camara = cam;
        anot.CargarPorDefecto();
        ctl.gestos = gestos; ctl.anotaciones = anot;
        Marcar(anot);

        string resTienda = EscenarioTiendaBuilder.Construir(mano, sol, cam, luz, sis);

        // horizontal
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // 4. event system + canvas
        AsegurarEventSystem();
        ConstruirCanvas(ctl, agarre, camaras, luz, anot);

        Marcar(ctl);
        EditorUtility.SetDirty(mano);
        EditorUtility.SetDirty(sis);
        EditorSceneManager.MarkSceneDirty(mano.scene);
        Selection.activeGameObject = sis;

        EditorUtility.DisplayDialog("Prótesis Viva",
            "Escena construida.\n\n" + resMat + "\n" + resCables + "\n" + resTienda + (avisos.Length > 0 ? "\n\nAvisos:" + avisos : "") +
            "\n\nGuarda la escena (Ctrl+S), pon el Game view en 1920x1080 (horizontal) y pulsa Play.", "OK");
    }

    [MenuItem("Tools/Mano/Solo materiales (extraer y reasignar)")]
    public static void SoloMateriales()
    {
        LODGroup lod = Object.FindFirstObjectByType<LODGroup>();
        if (lod == null) { EditorUtility.DisplayDialog("Prótesis Viva", "Abre Protecis_Mano.unity.", "OK"); return; }
        string r = ExtraerYReasignarMateriales(lod.gameObject);
        EditorSceneManager.MarkSceneDirty(lod.gameObject.scene);
        EditorUtility.DisplayDialog("Prótesis Viva", r, "OK");
    }

    [MenuItem("Tools/Mano/Agregar cables F2 sincronizados")]
    public static void SoloCables()
    {
        LODGroup lod = Object.FindFirstObjectByType<LODGroup>();
        if (lod == null) { EditorUtility.DisplayDialog("Prótesis Viva", "Abre Protecis_Mano.unity.", "OK"); return; }
        Agregar<ManoAgarre>(lod.gameObject);
        string r = ActivarLecturaMallas();
        Agregar<ManoCablesF2>(lod.gameObject);
        EditorSceneManager.MarkSceneDirty(lod.gameObject.scene);
        EditorUtility.DisplayDialog("Prótesis Viva", r + "\nManoCablesF2 agregado. Guarda y pulsa Play.", "OK");
    }

    // Los cables se deforman por CPU: la malla del FBX debe ser legible (Read/Write).
    static string ActivarLecturaMallas()
    {
        foreach (string g in AssetDatabase.FindAssets("Protesis_Mano_LODs t:Model"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.EndsWith("Protesis_Mano_LODs.fbx")) continue;
            ModelImporter mi = AssetImporter.GetAtPath(p) as ModelImporter;
            if (mi == null) return "No pude abrir el importador del FBX.";
            if (mi.isReadable) return "FBX ya legible.";
            mi.isReadable = true;
            mi.SaveAndReimport();
            return "Read/Write activado en el FBX.";
        }
        return "No encontré Protesis_Mano_LODs.fbx.";
    }

    // ------------------------------------------------------------------ materiales

    static string ExtraerYReasignarMateriales(GameObject mano)
    {
        string fbx = null;
        foreach (string g in AssetDatabase.FindAssets("Protesis_Mano_LODs t:Model"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (p.EndsWith("Protesis_Mano_LODs.fbx")) { fbx = p; break; }
        }
        if (fbx == null) return "No encontré Protesis_Mano_LODs.fbx; no toqué los materiales.";

        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(CarpetaMat)) AssetDatabase.CreateFolder("Assets/Materials", "Mano");

        int extraidos = 0;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbx))
        {
            Material m = o as Material;
            if (m == null) continue;
            string destino = CarpetaMat + "/" + m.name + ".mat";
            // Luz_Roja_Advertencia ya está remapeado a Assets/Materials/Luz_Roja_Advertencia.mat (Shader Graph propio).
            if (m.name == "Luz_Roja_Advertencia" || AssetDatabase.LoadAssetAtPath<Material>(destino) != null) continue;
            string error = AssetDatabase.ExtractAsset(m, destino);
            if (string.IsNullOrEmpty(error)) extraidos++;
            else Debug.LogWarning("No se pudo extraer " + m.name + ": " + error);
        }
        if (extraidos > 0)
        {
            AssetDatabase.WriteImportSettingsIfDirty(fbx);
            AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
        }

        // catálogo por nombre
        Dictionary<string, Material> porNombre = new Dictionary<string, Material>();
        foreach (string g in AssetDatabase.FindAssets("t:Material", new[] { CarpetaMat }))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            if (m != null) porNombre[m.name] = m;
        }
        Material roja = AssetDatabase.LoadAssetAtPath<Material>(RutaLuzRoja);
        if (roja != null) porNombre["Luz_Roja_Advertencia"] = roja;

        // reasignación explícita a todos los renderers (66 x 3 LOD = 198)
        int renderers = 0, cambiados = 0;
        HashSet<Material> distintos = new HashSet<Material>();
        foreach (Renderer r in mano.GetComponentsInChildren<Renderer>(true))
        {
            renderers++;
            Material[] actual = r.sharedMaterials;
            bool cambio = false;
            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i] == null) continue;
                Material rep;
                if (porNombre.TryGetValue(actual[i].name, out rep) && rep != actual[i]) { actual[i] = rep; cambio = true; }
            }
            if (cambio)
            {
                Undo.RecordObject(r, "Reasignar materiales");
                r.sharedMaterials = actual;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                cambiados++;
            }
            foreach (Material m in r.sharedMaterials) if (m != null) distintos.Add(m);
        }

        string incompatibles = "";
        foreach (Material m in distintos)
            if (m.shader == null || m.shader.name.StartsWith("Hidden/") || m.shader.name.StartsWith("Standard"))
                incompatibles += " " + m.name;

        return "Materiales: " + extraidos + " extraídos, " + cambiados + " de " + renderers + " renderers reasignados, " +
               distintos.Count + " materiales distintos en uso." +
               (incompatibles.Length > 0 ? "\nOjo, shader no URP en:" + incompatibles : "");
    }

    static Material ObtenerMaterialRayosX()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(RutaRayosX);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) { Debug.LogError("No encontré Universal Render Pipeline/Lit."); return null; }
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        m = new Material(sh) { name = "Carcasa_RayosX" };
        m.SetColor("_BaseColor", new Color(0.75f, 0.02f, 0.03f, 0.22f));   // Rojo_Chasis
        m.SetFloat("_Smoothness", 0.35f);
        ManoMaterialUtil.ConfigurarTransparente(m);
        AssetDatabase.CreateAsset(m, RutaRayosX);
        AssetDatabase.SaveAssets();
        return m;
    }

    // ------------------------------------------------------------------ búsqueda

    static Light BuscarLuzDireccional()
    {
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) return l;
        return null;
    }

    static Volume BuscarVolumenGlobal()
    {
        Volume cualquiera = null;
        foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (!v.isGlobal) continue;
            if (cualquiera == null) cualquiera = v;
            UnityEngine.Rendering.Universal.Bloom b;
            if (v.sharedProfile != null && v.sharedProfile.TryGet(out b)) return v;
        }
        return cualquiera;
    }

    static T Agregar<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static void Marcar(Object o) { EditorUtility.SetDirty(o); }

    static void AsegurarEventSystem()
    {
        EventSystem es = Object.FindFirstObjectByType<EventSystem>();
        GameObject go;
        if (es == null)
        {
            go = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
        }
        else go = es.gameObject;

        System.Type nuevo = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (nuevo != null)
        {
            StandaloneInputModule viejo = go.GetComponent<StandaloneInputModule>();
            if (viejo != null) Undo.DestroyObjectImmediate(viejo);
            if (go.GetComponent(nuevo) == null) Undo.AddComponent(go, nuevo);
        }
        else if (go.GetComponent<BaseInputModule>() == null)
            Undo.AddComponent<StandaloneInputModule>(go);
    }

    // ------------------------------------------------------------------ sprites

    const string CarpetaUI = "Assets/UI/ProtesisViva";
    enum TipoSprite { Rect, Aro, Circulo, Degradado }
    static Sprite sRect, sAro, sCirculo, sDegradado;

    static void PrepararSprites()
    {
        sRect = CrearSprite("UI_Rect", TipoSprite.Rect);
        sAro = CrearSprite("UI_Aro", TipoSprite.Aro);
        sCirculo = CrearSprite("UI_Circulo", TipoSprite.Circulo);
        sDegradado = CrearSprite("UI_Degradado", TipoSprite.Degradado);
    }

    // Sprites con bordes suavizados (antialias por distancia): se ven nitidos a cualquier tamano gracias al 9-slice.
    static Sprite CrearSprite(string nombre, TipoSprite tipo)
    {
        string ruta = CarpetaUI + "/" + nombre + ".png";
        Sprite existente = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        if (existente != null) return existente;

        if (!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder(CarpetaUI)) AssetDatabase.CreateFolder("Assets/UI", "ProtesisViva");

        int w = tipo == TipoSprite.Degradado ? 8 : 128, h = 128;
        float radio = 40f, grosor = 4f;
        float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
        Color[] pix = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a;
                if (tipo == TipoSprite.Degradado) a = y / (float)(h - 1);
                else
                {
                    float px = x - cx, py = y - cy, d;
                    if (tipo == TipoSprite.Circulo) d = Mathf.Sqrt(px * px + py * py) - (w * 0.5f - 1f);
                    else
                    {
                        float qx = Mathf.Abs(px) - (cx - radio), qy = Mathf.Abs(py) - (cy - radio);
                        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                        d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radio;
                    }
                    a = tipo == TipoSprite.Aro
                        ? Mathf.Clamp01(0.5f - (Mathf.Abs(d + grosor * 0.5f) - grosor * 0.5f))
                        : Mathf.Clamp01(0.5f - d);
                }
                pix[y * w + x] = new Color(1f, 1f, 1f, a);
            }

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(pix);
        tex.Apply();
        File.WriteAllBytes(Path.GetFullPath(ruta), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ruta);

        TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.spritePixelsPerUnit = 100f;
            if (tipo == TipoSprite.Rect || tipo == TipoSprite.Aro) ti.spriteBorder = new Vector4(44f, 44f, 44f, 44f);
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    // ------------------------------------------------------------------ canvas

    static void ConstruirCanvas(ProtesisVivaControles ctl, ManoAgarre agarre, CamaraSelector camaras, ModoIluminacion luz, ManoAnotaciones anot)
    {
        PrepararSprites();

        GameObject viejo = GameObject.Find(NombreCanvas);
        if (viejo != null) Undo.DestroyObjectImmediate(viejo);

        GameObject cg = new GameObject(NombreCanvas, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(cg, "Canvas Prótesis Viva");
        Canvas canvas = cg.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler sc = cg.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        GameObject zona = Nuevo("ZonaSegura", cg.transform);
        Estirar(zona);
        ctl.zonaSegura = zona.GetComponent<RectTransform>();

        GameObject colHud = Nuevo("Contenedor_HUD", zona.transform);
        Estirar(colHud);

        // =============================== HUD ===============================
        GameObject hud = Nuevo("Pantalla_HUD", colHud.transform);
        Estirar(hud);
        ctl.hud = hud;

        // ---- barra superior: vista + botones
        ctl.vista = Txt(hud.transform, "Vista", "Vista 1 · General", 34, Acento, TextAlignmentOptions.Left, true,
                        new Vector2(0.018f, 0.905f), new Vector2(0.40f, 0.985f));

        string[] nomTop = { ProtesisVivaTextos.BotonControles, ProtesisVivaTextos.BotonInfo, ProtesisVivaTextos.BotonVista,
                            ProtesisVivaTextos.BotonLuzDia, ProtesisVivaTextos.BotonInicio };
        Button[] top = new Button[5];
        TextMeshProUGUI[] topTxt = new TextMeshProUGUI[5];
        Image[] topImg = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            float x0 = 0.428f + i * 0.1135f;
            Color fondo = (i == 0 || i == 1 || i == 2) ? Acento : AcentoSuave;
            top[i] = Boton(hud.transform, "Top" + i, nomTop[i], 30, fondo, Blanco,
                           new Vector2(x0, 0.905f), new Vector2(x0 + 0.108f, 0.985f), out topTxt[i], out topImg[i]);
        }
        ctl.textoBotonControles = topTxt[0]; ctl.botonControlesFondo = topImg[0];
        ctl.textoBotonLuz = topTxt[3];

        // ---- mensaje guia (pequeño)
        Image banner = Caja(hud.transform, "Banner", Panel, new Vector2(0.018f, 0.79f), new Vector2(0.40f, 0.89f));
        Aro(banner.gameObject, Borde);
        Txt(banner.transform, "Texto", ProtesisVivaTextos.Banner, 25, Blanco, TextAlignmentOptions.Left, false,
            Vector2.zero, Vector2.one, new Vector2(22f, 8f), new Vector2(-22f, -8f));

        // ---- panel de cinematicas (izquierda)
        Image panelCine = Caja(hud.transform, "PanelCinematicas", Panel, new Vector2(0.018f, 0.34f), new Vector2(0.20f, 0.775f));
        Aro(panelCine.gameObject, Borde);
        Sombra(panelCine.gameObject);
        Txt(panelCine.transform, "Titulo", ProtesisVivaTextos.TituloCine, 28, Acento, TextAlignmentOptions.Center, true,
            new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.97f));
        string[] nomCine = { ProtesisVivaTextos.BotonSaludar, ProtesisVivaTextos.BotonAmorPaz, ProtesisVivaTextos.BotonExplicar };
        Button[] bCine = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            float y1 = 0.78f - i * 0.255f;
            TextMeshProUGUI tc; Image ic;
            bCine[i] = Boton(panelCine.transform, "Cine" + i, nomCine[i], 30, i == 2 ? Acento : AcentoSuave, Blanco,
                             new Vector2(0.07f, y1 - 0.22f), new Vector2(0.93f, y1), out tc, out ic);
        }

        // ---- estado (siempre visible, abajo al centro)
        Image pill = Caja(hud.transform, "Estado", Panel, new Vector2(0.25f, 0.03f), new Vector2(0.71f, 0.115f));
        Aro(pill.gameObject, Borde);
        Sombra(pill.gameObject);
        ctl.estado = Txt(pill.transform, "Texto", ProtesisVivaTextos.EstadoReposo, 38, Gris, TextAlignmentOptions.Center, true,
                         Vector2.zero, Vector2.one, new Vector2(24f, 6f), new Vector2(-24f, -6f));

        // ---- panel de controles de la mano (se muestra / oculta con el boton "Controles")
        Image panelC = Caja(hud.transform, "PanelControles", new Color(0.12f, 0.02f, 0.03f, 0.96f),
                            new Vector2(0.735f, 0.07f), new Vector2(0.985f, 0.885f));
        Aro(panelC.gameObject, Borde);
        Sombra(panelC.gameObject);
        ctl.panelControles = panelC.gameObject;
        Transform P = panelC.transform;

        Txt(P, "EtiquetaCierre", ProtesisVivaTextos.EtiquetaCierre, 26, Gris, TextAlignmentOptions.Left, false,
            new Vector2(0.06f, 0.915f), new Vector2(0.66f, 0.985f));
        ctl.textoCierre = Txt(P, "ValorCierre", "0 %", 28, Blanco, TextAlignmentOptions.Right, true,
                              new Vector2(0.66f, 0.915f), new Vector2(0.94f, 0.985f));

        Slider slider = CrearSlider(P);
        Anclar(slider.gameObject, new Vector2(0.06f, 0.805f), new Vector2(0.94f, 0.91f));
        ctl.slider = slider;

        Txt(P, "EtiquetaNivel", ProtesisVivaTextos.EtiquetaNivel, 26, Gris, TextAlignmentOptions.Left, false,
            new Vector2(0.06f, 0.735f), new Vector2(0.94f, 0.80f));

        Button[] bNivel = new Button[3];
        ctl.nivelFondo = new Image[3];
        ctl.nivelTexto = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            float x0 = 0.05f + i * 0.31f;
            TextMeshProUGUI t; Image im;
            bNivel[i] = Boton(P, "Nivel" + i, ProtesisVivaTextos.Niveles[i], 30, AcentoSuave, Blanco,
                              new Vector2(x0, 0.605f), new Vector2(x0 + 0.29f, 0.725f), out t, out im);
            ctl.nivelFondo[i] = im;
            ctl.nivelTexto[i] = t;
        }

        // chips 2 x 2 (F1 presion, F3 temperatura, vibracion, alerta)
        Image[] chipFondo = new Image[4];
        TextMeshProUGUI[] chipTxt = new TextMeshProUGUI[4];
        string[] chipNom = { ProtesisVivaTextos.ChipPresion, ProtesisVivaTextos.ChipTemp, ProtesisVivaTextos.ChipVibracion, ProtesisVivaTextos.ChipAlerta };
        for (int i = 0; i < 4; i++)
        {
            float x0 = 0.05f + (i % 2) * 0.47f;
            float y1 = i < 2 ? 0.585f : 0.45f;
            chipFondo[i] = Caja(P, "Chip" + i, PanelClaro, new Vector2(x0, y1 - 0.125f), new Vector2(x0 + 0.44f, y1));
            Aro(chipFondo[i].gameObject, Borde);
            chipTxt[i] = Txt(chipFondo[i].transform, "Texto", chipNom[i], 30, Blanco, TextAlignmentOptions.Center, false,
                             Vector2.zero, Vector2.one, new Vector2(6f, 4f), new Vector2(-6f, -4f));
        }
        ctl.chipPresion = chipTxt[0]; ctl.fondoChipPresion = chipFondo[0];
        ctl.chipTemp = chipTxt[1]; ctl.fondoChipTemp = chipFondo[1];
        ctl.chipVibracion = chipTxt[2]; ctl.fondoChipVibracion = chipFondo[2];
        ctl.chipAlerta = chipTxt[3]; ctl.fondoChipAlerta = chipFondo[3];

        TextMeshProUGUI tCal, tAbrir; Image iCal, iAbrir;
        Button bCal = Boton(P, "BtnObjeto", ProtesisVivaTextos.BotonObjetoFrio, 32, AcentoSuave, Blanco,
                            new Vector2(0.05f, 0.195f), new Vector2(0.95f, 0.315f), out tCal, out iCal);
        Button bAbrir = Boton(P, "BtnAbrirMano", ProtesisVivaTextos.BotonAbrirMano, 32, Acento, Blanco,
                              new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.17f), out tAbrir, out iAbrir);
        ctl.textoBotonCaliente = tCal; ctl.botonCalienteFondo = iCal;

        // =============================== INFO (panel independiente) ===============================
        GameObject info = Nuevo("Pantalla_Info", colHud.transform);
        Estirar(info);
        ctl.info = info;

        Image caja = Caja(info.transform, "Caja", new Color(0.09f, 0.015f, 0.025f, 0.98f), new Vector2(0.14f, 0.07f), new Vector2(0.86f, 0.88f));
        Aro(caja.gameObject, Borde);
        Sombra(caja.gameObject);

        Button[] tabs = new Button[3];
        ctl.tabsFondo = new Image[3];
        ctl.tabsTexto = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            TextMeshProUGUI t; Image im;
            float x0 = 0.03f + 0.32f * i;
            tabs[i] = Boton(caja.transform, "Tab" + i, ProtesisVivaTextos.TabsInfo[i], 30, AcentoSuave, Blanco,
                            new Vector2(x0, 0.895f), new Vector2(x0 + 0.30f, 0.975f), out t, out im);
            ctl.tabsFondo[i] = im;
            ctl.tabsTexto[i] = t;
        }

        ctl.infoTitulo = Txt(caja.transform, "Titulo", ProtesisVivaTextos.TitulosFicha[0], 38, Acento, TextAlignmentOptions.Left, true,
                             new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.885f));

        GameObject vp = Nuevo("Viewport", caja.transform);
        vp.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        vp.AddComponent<RectMask2D>();
        Anclar(vp, new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.79f));

        GameObject cont = Nuevo("Contenido", vp.transform);
        RectTransform crt = cont.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        cont.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TextMeshProUGUI cuerpo = cont.AddComponent<TextMeshProUGUI>();
        cuerpo.text = ProtesisVivaTextos.CuerposFicha[0];
        cuerpo.fontSize = 32;
        cuerpo.color = Blanco;
        cuerpo.alignment = TextAlignmentOptions.TopLeft;
        cuerpo.richText = true;
        cuerpo.lineSpacing = 8f;
        cuerpo.raycastTarget = false;
        ctl.infoCuerpo = cuerpo;

        ScrollRect scroll = vp.AddComponent<ScrollRect>();
        scroll.viewport = vp.GetComponent<RectTransform>();
        scroll.content = crt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;
        ctl.scroll = scroll;

        TextMeshProUGUI tCerrar; Image iCerrar;
        Button bCerrar = Boton(caja.transform, "Cerrar", ProtesisVivaTextos.BotonCerrar, 34, Acento, Blanco,
                               new Vector2(0.36f, 0.025f), new Vector2(0.64f, 0.11f), out tCerrar, out iCerrar);

        // =============================== ANOTACIONES (modo explicacion) ===============================
        GameObject anotGo = Nuevo("Capa_Anotaciones", colHud.transform);
        Estirar(anotGo);
        CanvasGroup anotGrupo = anotGo.AddComponent<CanvasGroup>();
        anotGrupo.blocksRaycasts = false;
        anotGrupo.interactable = false;
        int nA = 6;
        RectTransform[] aBanners = new RectTransform[nA];
        TMP_Text[] aTit = new TMP_Text[nA];
        TMP_Text[] aCue = new TMP_Text[nA];
        Image[] aLin = new Image[nA];
        RectTransform[] aPun = new RectTransform[nA];
        for (int i = 0; i < nA; i++)
        {
            GameObject lg = Nuevo("Linea" + i, anotGo.transform);
            Image li = lg.AddComponent<Image>();
            li.color = new Color(1f, 0.40f, 0.40f, 0.95f);
            li.raycastTarget = false;
            RectTransform lr = lg.GetComponent<RectTransform>();
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0f, 0.5f);
            lr.sizeDelta = new Vector2(0f, 3f);
            aLin[i] = li;

            GameObject dg = Nuevo("Punto" + i, anotGo.transform);
            Image halo = dg.AddComponent<Image>();
            halo.sprite = sCirculo;
            halo.color = new Color(0.93f, 0.14f, 0.17f, 0.55f);
            halo.raycastTarget = false;
            RectTransform dr = dg.GetComponent<RectTransform>();
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(36f, 36f);
            GameObject cg2 = Nuevo("Centro", dg.transform);
            Image ci = cg2.AddComponent<Image>();
            ci.sprite = sCirculo;
            ci.color = Blanco;
            ci.raycastTarget = false;
            RectTransform cr = cg2.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(16f, 16f);
            aPun[i] = dr;
        }
        for (int i = 0; i < nA; i++)
        {
            bool izq = i < 3;
            int fila = i % 3;
            float yc = fila == 0 ? 0.78f : (fila == 1 ? 0.50f : 0.22f);
            Image bx = Caja(anotGo.transform, "Banner" + i, new Color(0.12f, 0.02f, 0.03f, 0.95f),
                            new Vector2(izq ? 0.02f : 0.73f, yc - 0.105f), new Vector2(izq ? 0.27f : 0.98f, yc + 0.105f));
            Aro(bx.gameObject, Borde);
            Sombra(bx.gameObject);
            bx.raycastTarget = false;
            bx.gameObject.AddComponent<CanvasGroup>();
            aTit[i] = Txt(bx.transform, "Titulo", ProtesisVivaTextos.AnotTitulos[i], 30, Acento, TextAlignmentOptions.Left, true,
                          new Vector2(0f, 0.60f), new Vector2(1f, 1f), new Vector2(20f, 0f), new Vector2(-20f, -10f));
            aCue[i] = Txt(bx.transform, "Texto", ProtesisVivaTextos.AnotCuerpos[i], 24, Blanco, TextAlignmentOptions.TopLeft, false,
                          new Vector2(0f, 0f), new Vector2(1f, 0.62f), new Vector2(20f, 10f), new Vector2(-20f, 0f));
            aBanners[i] = bx.rectTransform;
        }
        anot.contenedor = colHud.GetComponent<RectTransform>();
        anot.grupo = anotGrupo;
        anot.banners = aBanners; anot.titulos = aTit; anot.cuerpos = aCue; anot.lineas = aLin; anot.puntos = aPun;
        EditorUtility.SetDirty(anot);
        anotGo.SetActive(false);

        // =============================== CAPA DE CINEMATICA (solo rotulo y boton X) ===============================
        GameObject capaCine = Nuevo("Capa_Cine", colHud.transform);
        Estirar(capaCine);
        ctl.capaCine = capaCine;
        Image chipCine = Caja(capaCine.transform, "Rotulo", Panel, new Vector2(0.018f, 0.905f), new Vector2(0.34f, 0.985f));
        Aro(chipCine.gameObject, Borde);
        ctl.textoCine = Txt(chipCine.transform, "Texto", ProtesisVivaTextos.CineExplicando, 32, Acento, TextAlignmentOptions.Center, true,
                            Vector2.zero, Vector2.one, new Vector2(18f, 6f), new Vector2(-18f, -6f));
        TextMeshProUGUI tX; Image iX;
        Button bX = Boton(capaCine.transform, "BotonX", ProtesisVivaTextos.CineCerrar, 46, Acento, Blanco,
                          new Vector2(0.918f, 0.88f), new Vector2(0.982f, 0.985f), out tX, out iX);
        capaCine.SetActive(false);

        // =============================== MENÚ ===============================
        GameObject menu = Nuevo("Pantalla_Menu", cg.transform);
        Estirar(menu);
        menu.AddComponent<Image>().color = Fondo;
        ctl.menu = menu;

        GameObject brillo = Nuevo("Resplandor", menu.transform);
        Anclar(brillo, new Vector2(0f, 0.3f), new Vector2(1f, 1f));
        Image ibr = brillo.AddComponent<Image>();
        ibr.sprite = sDegradado;
        ibr.color = new Color(0.93f, 0.14f, 0.17f, 0.22f);
        ibr.raycastTarget = false;

        GameObject colMenu = Nuevo("Contenedor_Menu", menu.transform);
        Estirar(colMenu);

        Txt(colMenu.transform, "Titulo", ProtesisVivaTextos.Titulo.Replace("Viva", "<color=#F0242B>Viva</color>"), 120, Blanco,
            TextAlignmentOptions.Center, true, new Vector2(0.15f, 0.69f), new Vector2(0.85f, 0.90f));
        Txt(colMenu.transform, "Subtitulo", ProtesisVivaTextos.Subtitulo, 40, Gris, TextAlignmentOptions.Center, false,
            new Vector2(0.15f, 0.60f), new Vector2(0.85f, 0.68f));
        GameObject linea = Nuevo("Linea", colMenu.transform);
        Image il = linea.AddComponent<Image>();
        il.sprite = sRect; il.type = Image.Type.Sliced; il.color = Acento; il.raycastTarget = false;
        Anclar(linea, new Vector2(0.43f, 0.585f), new Vector2(0.57f, 0.593f));

        Image tarjeta = Caja(colMenu.transform, "TarjetaFrase", new Color(0.18f, 0.04f, 0.05f, 0.92f), new Vector2(0.14f, 0.30f), new Vector2(0.86f, 0.55f));
        Aro(tarjeta.gameObject, Borde);
        Sombra(tarjeta.gameObject);
        ctl.fraseMenu = Txt(tarjeta.transform, "Frase", ProtesisVivaTextos.FrasesMenu[0], 44, Blanco, TextAlignmentOptions.Center, false,
                            Vector2.zero, Vector2.one, new Vector2(50f, 24f), new Vector2(-50f, -24f));
        ctl.fraseGrupo = ctl.fraseMenu.gameObject.AddComponent<CanvasGroup>();

        TextMeshProUGUI tCom; Image iCom;
        Button bCom = Boton(colMenu.transform, "Comenzar", ProtesisVivaTextos.BotonComenzar, 48, Acento, Blanco,
                            new Vector2(0.38f, 0.12f), new Vector2(0.62f, 0.245f), out tCom, out iCom);
        Txt(colMenu.transform, "Pie", ProtesisVivaTextos.PieMenu, 26, Gris, TextAlignmentOptions.Center, false,
            new Vector2(0.15f, 0.02f), new Vector2(0.85f, 0.09f));

        ctl.columnas = new[] { colHud.GetComponent<RectTransform>(), colMenu.GetComponent<RectTransform>() };

        // estado inicial guardado en la escena
        hud.SetActive(false);
        info.SetActive(false);
        menu.SetActive(true);

        // =============================== cableado (listeners persistentes, visibles en el Inspector) ===============================
        UnityEventTools.AddPersistentListener(slider.onValueChanged, agarre.FijarCierre);
        for (int i = 0; i < 3; i++)
            UnityEventTools.AddIntPersistentListener(bNivel[i].onClick, agarre.FijarNivelFuerza, i);
        UnityEventTools.AddPersistentListener(bCal.onClick, agarre.AlternarObjetoCaliente);
        UnityEventTools.AddPersistentListener(bAbrir.onClick, ctl.AbrirMano);
        UnityEventTools.AddPersistentListener(top[0].onClick, ctl.AlternarControles);
        UnityEventTools.AddPersistentListener(top[1].onClick, ctl.AlternarInfo);
        UnityEventTools.AddPersistentListener(top[2].onClick, camaras.Siguiente);
        UnityEventTools.AddPersistentListener(top[3].onClick, luz.Alternar);
        UnityEventTools.AddPersistentListener(top[4].onClick, ctl.MostrarMenu);
        UnityEventTools.AddPersistentListener(bCerrar.onClick, ctl.AlternarInfo);
        UnityEventTools.AddPersistentListener(bCom.onClick, ctl.Comenzar);
        for (int i = 0; i < 3; i++)
            UnityEventTools.AddIntPersistentListener(tabs[i].onClick, ctl.MostrarTab, i);
        UnityEventTools.AddPersistentListener(bCine[0].onClick, ctl.Saludar);
        UnityEventTools.AddPersistentListener(bCine[1].onClick, ctl.AmorYPaz);
        UnityEventTools.AddPersistentListener(bCine[2].onClick, ctl.Explicar);
        UnityEventTools.AddPersistentListener(bX.onClick, ctl.CerrarCine);
    }

    // ------------------------------------------------------------------ ayudas de UI

    static GameObject Nuevo(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return go;
    }

    static void Estirar(GameObject go) { Anclar(go, Vector2.zero, Vector2.one); }

    static void Anclar(GameObject go, Vector2 min, Vector2 max)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Image Caja(Transform padre, string nombre, Color color, Vector2 aMin, Vector2 aMax)
    {
        GameObject go = Nuevo(nombre, padre);
        Anclar(go, aMin, aMax);
        Image im = go.AddComponent<Image>();
        im.sprite = sRect;
        im.type = Image.Type.Sliced;
        im.color = color;
        return im;
    }

    static void Aro(GameObject go, Color color)
    {
        GameObject r = Nuevo("Aro", go.transform);
        Estirar(r);
        Image im = r.AddComponent<Image>();
        im.sprite = sAro;
        im.type = Image.Type.Sliced;
        im.fillCenter = false;
        im.color = color;
        im.raycastTarget = false;
    }

    static void Sombra(GameObject go)
    {
        Shadow s = go.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.55f);
        s.effectDistance = new Vector2(0f, -7f);
    }

    static TextMeshProUGUI Txt(Transform padre, string nombre, string contenido, float tam, Color color,
                               TextAlignmentOptions alin, bool negrita, Vector2 aMin, Vector2 aMax)
    {
        return Txt(padre, nombre, contenido, tam, color, alin, negrita, aMin, aMax, Vector2.zero, Vector2.zero);
    }

    static TextMeshProUGUI Txt(Transform padre, string nombre, string contenido, float tam, Color color,
                               TextAlignmentOptions alin, bool negrita, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        GameObject go = Nuevo(nombre, padre);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenido;
        t.fontSize = tam;
        t.color = color;
        t.alignment = alin;
        t.fontStyle = negrita ? FontStyles.Bold : FontStyles.Normal;
        t.enableAutoSizing = true;
        t.fontSizeMax = tam;
        t.fontSizeMin = Mathf.Max(18f, tam * 0.55f);
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    static Button Boton(Transform padre, string nombre, string texto, float tam, Color fondo, Color colorTexto,
                        Vector2 aMin, Vector2 aMax, out TextMeshProUGUI etiqueta, out Image imagen)
    {
        Image im = Caja(padre, nombre, fondo, aMin, aMax);
        Button b = im.gameObject.AddComponent<Button>();
        b.targetGraphic = im;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1f, 0.92f, 0.92f, 1f);
        cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        Aro(im.gameObject, Borde);
        Sombra(im.gameObject);
        etiqueta = Txt(im.transform, "Etiqueta", texto, tam, colorTexto, TextAlignmentOptions.Center, true,
                       Vector2.zero, Vector2.one, new Vector2(14f, 6f), new Vector2(-14f, -6f));
        imagen = im;
        return b;
    }

    static Slider CrearSlider(Transform padre)
    {
        GameObject go = Nuevo("SliderCierre", padre);
        Slider s = go.AddComponent<Slider>();

        Image fondo = BandaSlider(go.transform, "Fondo", new Color(1f, 1f, 1f, 0.16f));
        GameObject area = Nuevo("AreaRelleno", go.transform);
        RectTransform ra = area.GetComponent<RectTransform>();
        ra.anchorMin = new Vector2(0f, 0.5f); ra.anchorMax = new Vector2(1f, 0.5f);
        ra.offsetMin = new Vector2(0f, -11f); ra.offsetMax = new Vector2(0f, 11f);
        Image relleno = Caja(area.transform, "Relleno", Acento, Vector2.zero, Vector2.one);

        GameObject areaMango = Nuevo("AreaMango", go.transform);
        RectTransform ram = areaMango.GetComponent<RectTransform>();
        ram.anchorMin = Vector2.zero; ram.anchorMax = Vector2.one;
        ram.offsetMin = new Vector2(34f, 0f); ram.offsetMax = new Vector2(-34f, 0f);

        GameObject mango = Nuevo("Mango", areaMango.transform);
        Image im = mango.AddComponent<Image>();
        im.sprite = sCirculo;
        im.color = Blanco;
        mango.GetComponent<RectTransform>().sizeDelta = new Vector2(68f, 68f);
        Sombra(mango);

        s.fillRect = relleno.rectTransform;
        s.handleRect = mango.GetComponent<RectTransform>();
        s.targetGraphic = im;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f; s.value = 0f;
        return s;
    }

    static Image BandaSlider(Transform padre, string nombre, Color color)
    {
        Image im = Caja(padre, nombre, color, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
        im.rectTransform.offsetMin = new Vector2(0f, -11f);
        im.rectTransform.offsetMax = new Vector2(0f, 11f);
        return im;
    }
}
