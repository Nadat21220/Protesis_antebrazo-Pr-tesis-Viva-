using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Pon este script en el objeto "Protesis_Mano" (el que tiene el LODGroup).
/// Mueve SOLO las articulaciones de Protesis_Mano_LOD0 (dedos, muñeca, etc.) y este script
/// copia esa pose a Protesis_Mano_LOD1 y Protesis_Mano_LOD2, para que los 3 LOD se vean iguales
/// aunque la camara cambie de nivel.
/// </summary>
[DisallowMultipleComponent]
public class ManoLODSync : MonoBehaviour
{
    [Tooltip("Se rellena solo: hijo cuyo nombre termina en _LOD0")]
    public Transform lod0;
    public Transform[] otrosLods;   // LOD1, LOD2 (se rellena solo)

    struct Par { public Transform origen, destino; }
    readonly List<Par> pares = new List<Par>();
    static readonly Regex sufijo = new Regex(@"_(L|LOD)\d$");

    void Awake()
    {
        if (lod0 == null) lod0 = Buscar("_LOD0");
        if (otrosLods == null || otrosLods.Length == 0)
        {
            var l1 = Buscar("_LOD1"); var l2 = Buscar("_LOD2");
            var lista = new List<Transform>();
            if (l1) lista.Add(l1);
            if (l2) lista.Add(l2);
            otrosLods = lista.ToArray();
        }
        if (lod0 == null) { Debug.LogWarning("ManoLODSync: no encontre Protesis_Mano_LOD0"); enabled = false; return; }

        foreach (var otro in otrosLods)
        {
            var mapa = new Dictionary<string, Transform>();
            foreach (var t in otro.GetComponentsInChildren<Transform>(true))
                if (t != otro) mapa[Clave(t, otro)] = t;
            foreach (var t in lod0.GetComponentsInChildren<Transform>(true))
            {
                if (t == lod0) continue;
                if (mapa.TryGetValue(Clave(t, lod0), out var d)) pares.Add(new Par { origen = t, destino = d });
            }
        }
    }

    // Ruta relativa sin los sufijos _L0/_L1/_L2, igual en los tres LOD
    static string Clave(Transform t, Transform raiz)
    {
        var partes = new List<string>();
        for (var x = t; x != null && x != raiz; x = x.parent) partes.Add(sufijo.Replace(x.name, ""));
        partes.Reverse();
        return string.Join("/", partes);
    }

    Transform Buscar(string termina)
    {
        foreach (Transform h in transform) if (h.name.EndsWith(termina)) return h;
        return null;
    }

    void LateUpdate()
    {
        for (int i = 0; i < pares.Count; i++)
        {
            var p = pares[i];
            p.destino.localPosition = p.origen.localPosition;
            p.destino.localRotation = p.origen.localRotation;
            p.destino.localScale = p.origen.localScale;
        }
    }
}
