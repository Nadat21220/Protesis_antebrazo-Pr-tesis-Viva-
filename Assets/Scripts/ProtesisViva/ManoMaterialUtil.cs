using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Utilidades de material para Prótesis Viva (URP).
/// - ConfigurarTransparente: deja un material URP/Lit en modo Transparent (igual que el menú Surface Type).
/// - AplicarEmision: pone color + intensidad HDR de emisión sin crear una instancia por renderer.
/// </summary>
public static class ManoMaterialUtil
{
    public static void ConfigurarTransparente(Material m)
    {
        if (m == null) return;
        m.SetFloat("_Surface", 1f);                                   // 0 = Opaque, 1 = Transparent
        m.SetFloat("_Blend", 0f);                                     // Alpha
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);                                    // sin escribir profundidad = menos coste
        m.SetFloat("_AlphaClip", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetShaderPassEnabled("ShadowCaster", false);                // la carcasa de rayos X no proyecta sombra
        m.SetShaderPassEnabled("DepthOnly", false);
    }

    /// <summary>
    /// Si el material expone propiedadIntensidad (p. ej. un Shader Graph con "_Intensidad"), se escribe color e
    /// intensidad por separado. Si no, se escribe color * intensidad en la propiedad de emision (URP/Lit).
    /// </summary>
    public static void AplicarEmision(Material m, string propiedadColor, string propiedadIntensidad, Color color, float intensidad)
    {
        if (m == null) return;

        bool tieneIntensidad = !string.IsNullOrEmpty(propiedadIntensidad) && m.HasProperty(propiedadIntensidad);
        bool tieneColor = !string.IsNullOrEmpty(propiedadColor) && m.HasProperty(propiedadColor);

        if (tieneIntensidad)
        {
            if (tieneColor) m.SetColor(propiedadColor, color);
            m.SetFloat(propiedadIntensidad, intensidad);
        }
        else if (tieneColor)
        {
            // Unity convierte las propiedades Color de gamma a lineal al enviarlas al shader;
            // se multiplica en lineal y se devuelve a gamma para que el valor HDR final sea color * intensidad.
            Color hdr = color.linear * intensidad;
            hdr.a = 1f;
            m.SetColor(propiedadColor, hdr.gamma);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
    }

    public static bool TieneEmision(Material m, string propiedadColor, string propiedadIntensidad)
    {
        if (m == null) return false;
        return (!string.IsNullOrEmpty(propiedadColor) && m.HasProperty(propiedadColor))
            || (!string.IsNullOrEmpty(propiedadIntensidad) && m.HasProperty(propiedadIntensidad));
    }
}
