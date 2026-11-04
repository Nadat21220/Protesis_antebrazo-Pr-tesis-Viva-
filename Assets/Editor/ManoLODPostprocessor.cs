using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Va en Assets/Editor/. Al importar Protesis_Mano_LODs.fbx llena el LODGroup con las mallas
// de cada raiz (Protesis_Mano_LOD0 / LOD1 / LOD2). Unity solo asigna la malla del propio nodo
// _LODn, y aqui cada LOD es un grupo con muchas mallas dentro, por eso salia vacio.
public class ManoLODPostprocessor : AssetPostprocessor
{
    void OnPostprocessModel(GameObject g)
    {
        if (!assetPath.Contains("Protesis_Mano_LODs")) return;
        ManoLODTools.Configurar(g.transform);
    }
}

public static class ManoLODTools
{
    // Transiciones (altura relativa en pantalla): LOD0 > 40%, LOD1 > 15%, LOD2 > 3%, debajo se oculta.
    static readonly float[] umbrales = { 0.40f, 0.15f, 0.03f };

    public static bool Configurar(Transform raiz)
    {
        var lod0 = raiz.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name.StartsWith("Protesis_Mano") && t.name.EndsWith("_LOD0"));
        if (lod0 == null) return false;

        var padre = lod0.parent != null ? lod0.parent : raiz;
        var grupo = padre.GetComponent<LODGroup>();
        if (grupo == null) grupo = padre.gameObject.AddComponent<LODGroup>();

        var lods = new List<LOD>();
        for (int i = 0; i < umbrales.Length; i++)
        {
            Transform nivel = null;
            foreach (Transform h in padre)
                if (h.name.EndsWith("_LOD" + i)) { nivel = h; break; }
            if (nivel == null) continue;
            lods.Add(new LOD(umbrales[i], nivel.GetComponentsInChildren<Renderer>(true)));
        }
        grupo.SetLODs(lods.ToArray());
        grupo.fadeMode = LODFadeMode.None;
        grupo.RecalculateBounds();
        return true;
    }

    // Plan B: si ya tienes la mano en la escena, selecciona el objeto Protesis_Mano_LOD y usa este menu.
    [MenuItem("Tools/Mano/Configurar LOD en el objeto seleccionado")]
    static void Menu()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) { Debug.LogWarning("Selecciona el objeto Protesis_Mano_LOD en la Hierarchy"); return; }
        var existente = sel.GetComponent<LODGroup>();
        if (existente != null) Undo.RecordObject(existente, "Configurar LOD mano");
        Debug.Log(Configurar(sel.transform) ? "LOD de la mano configurado" : "No encontre Protesis_Mano_LOD0 dentro de la seleccion");
    }
}
