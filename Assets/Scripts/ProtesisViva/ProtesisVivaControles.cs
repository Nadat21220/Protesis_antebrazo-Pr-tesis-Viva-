using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Logica de la interfaz HORIZONTAL (menu, HUD, panel de controles, panel de informacion).
/// NO crea elementos: el Canvas lo construye Assets/Editor/ProtesisSceneBuilder.cs, que conecta aqui las referencias.
///
/// Diseno adaptable: la interfaz vive en un contenedor 16:9. En un celular horizontal ocupa toda la pantalla;
/// en una ventana mas cuadrada o vertical queda centrada con barras y NUNCA se sale de la pantalla.
/// El panel de controles de la mano y el panel de informacion son independientes y se muestran/ocultan con botones.
/// </summary>
public class ProtesisVivaControles : MonoBehaviour
{
    [Header("Sistemas")]
    public ManoAgarre agarre;
    public CamaraSelector camaras;
    public ModoIluminacion luz;
    public ManoGestos gestos;
    public ManoAnotaciones anotaciones;

    [Header("Pantallas")]
    public GameObject menu;
    public GameObject hud;
    public GameObject panelControles;
    public GameObject info;
    [Tooltip("Capa que queda visible durante una cinematica: solo el rotulo y el boton X.")]
    public GameObject capaCine;
    public TMP_Text textoCine;
    public RectTransform zonaSegura;
    public RectTransform[] columnas;

    [Header("Textos")]
    public TMP_Text fraseMenu;
    public TMP_Text estado;
    public TMP_Text vista;
    public TMP_Text textoCierre;
    public TMP_Text textoBotonLuz;
    public TMP_Text textoBotonCaliente;
    public TMP_Text textoBotonControles;
    public TMP_Text chipPresion, chipTemp, chipVibracion, chipAlerta;
    public TMP_Text infoTitulo;
    public TMP_Text infoCuerpo;

    [Header("Elementos")]
    public Slider slider;
    public CanvasGroup fraseGrupo;
    public ScrollRect scroll;
    public Image[] tabsFondo = new Image[3];
    public TMP_Text[] tabsTexto = new TMP_Text[3];
    public Image[] nivelFondo = new Image[3];
    public TMP_Text[] nivelTexto = new TMP_Text[3];
    public Image botonCalienteFondo, botonControlesFondo;
    public Image fondoChipPresion, fondoChipTemp, fondoChipVibracion, fondoChipAlerta;

    [Header("Ajustes")]
    public bool bloquearHorizontal = true;
    public int fpsObjetivo = 60;
    public float segundosPorFrase = 4.5f;

    static readonly Color Fondo = new Color(0.07f, 0.01f, 0.02f, 1f);
    static readonly Color PanelClaro = new Color(0.24f, 0.06f, 0.07f, 1f);
    static readonly Color Acento = new Color(0.93f, 0.14f, 0.17f, 1f);
    static readonly Color AcentoSuave = new Color(0.93f, 0.14f, 0.17f, 0.30f);
    static readonly Color Alerta = new Color(1f, 0.30f, 0.22f, 1f);
    static readonly Color Naranja = new Color(1f, 0.55f, 0.12f, 1f);
    static readonly Color Blanco = new Color(0.98f, 0.96f, 0.96f, 1f);
    static readonly Color Gris = new Color(0.88f, 0.72f, 0.72f, 1f);

    enum ModoCine { Ninguno, Gesto, Explicacion }
    ModoCine modoCine;
    bool controlesEstaban = true;

    int fraseIdx;
    float fraseT;
    int anchoPrev, altoPrev;

    void Start()
    {
        if (agarre == null) agarre = FindFirstObjectByType<ManoAgarre>();
        if (camaras == null) camaras = FindFirstObjectByType<CamaraSelector>();
        if (luz == null) luz = FindFirstObjectByType<ModoIluminacion>();
        if (gestos == null) gestos = FindFirstObjectByType<ManoGestos>();
        if (anotaciones == null) anotaciones = FindFirstObjectByType<ManoAnotaciones>();

        if (bloquearHorizontal)
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
        Application.targetFrameRate = fpsObjetivo;

        if (agarre != null) agarre.Cambio += AlCambiarAgarre;
        if (camaras != null) camaras.VistaCambio += AlCambiarVista;
        if (luz != null) luz.Cambio += AlCambiarLuz;
        if (gestos != null) gestos.Cambio += AlCambiarGesto;
        if (capaCine != null) capaCine.SetActive(false);

        AjustarLayout();
        MostrarMenu();
        AlCambiarAgarre(agarre);
        AlCambiarVista(0);
        AlCambiarLuz(luz != null && luz.Noche);
        MostrarTab(0);
        ActualizarBotonControles();
    }

    void OnDestroy()
    {
        if (agarre != null) agarre.Cambio -= AlCambiarAgarre;
        if (camaras != null) camaras.VistaCambio -= AlCambiarVista;
        if (luz != null) luz.Cambio -= AlCambiarLuz;
        if (gestos != null) gestos.Cambio -= AlCambiarGesto;
    }

    void Update()
    {
        if (Screen.width != anchoPrev || Screen.height != altoPrev) AjustarLayout();

        if (menu == null || !menu.activeSelf || fraseMenu == null) return;
        fraseT += Time.deltaTime;
        float ciclo = Mathf.Max(2f, segundosPorFrase);
        if (fraseT >= ciclo)
        {
            fraseT = 0f;
            fraseIdx = (fraseIdx + 1) % ProtesisVivaTextos.FrasesMenu.Length;
            fraseMenu.text = ProtesisVivaTextos.FrasesMenu[fraseIdx];
        }
        if (fraseGrupo != null)
            fraseGrupo.alpha = Mathf.Clamp01(Mathf.Min(fraseT / 0.6f, (ciclo - fraseT) / 0.6f));
    }

    // ---------------------------------------------------------------- layout adaptable (16:9)
    void AjustarLayout()
    {
        anchoPrev = Screen.width;
        altoPrev = Screen.height;
        if (anchoPrev <= 0 || altoPrev <= 0) return;

        Rect s = Screen.safeArea;
        if (zonaSegura != null)
        {
            zonaSegura.anchorMin = new Vector2(s.xMin / anchoPrev, s.yMin / altoPrev);
            zonaSegura.anchorMax = new Vector2(s.xMax / anchoPrev, s.yMax / altoPrev);
            zonaSegura.offsetMin = zonaSegura.offsetMax = Vector2.zero;
        }

        // Si la pantalla es mas ancha que 16:9 el contenedor ocupa todo; si no, se ajusta al ancho y se centra.
        float altoCol = Mathf.Min(s.height, s.width * (9f / 16f));
        float frac = altoCol / Mathf.Max(1f, s.height);
        if (columnas != null)
            foreach (RectTransform c in columnas)
            {
                if (c == null) continue;
                c.anchorMin = new Vector2(0f, 0.5f - frac * 0.5f);
                c.anchorMax = new Vector2(1f, 0.5f + frac * 0.5f);
                c.offsetMin = c.offsetMax = Vector2.zero;
            }
    }

    // ---------------------------------------------------------------- pantallas
    public void MostrarMenu()
    {
        SalirCine();
        menu.SetActive(true); hud.SetActive(false); info.SetActive(false);
    }

    public void Comenzar()
    {
        menu.SetActive(false); hud.SetActive(true); info.SetActive(false);
        if (panelControles != null) panelControles.SetActive(true);
        ActualizarBotonControles();
    }

    public void AlternarInfo() { info.SetActive(!info.activeSelf); }

    public void AlternarControles()
    {
        if (panelControles != null) panelControles.SetActive(!panelControles.activeSelf);
        ActualizarBotonControles();
    }

    public void AbrirMano()
    {
        if (slider != null) slider.value = 0f;
        else if (agarre != null) agarre.FijarCierre(0f);
    }

    // ---------------------------------------------------------------- cinematicas
    public void Saludar()
    {
        if (gestos == null || camaras == null) return;
        EntrarCine(ModoCine.Gesto, ProtesisVivaTextos.CineSaludando);
        AbrirMano();
        camaras.IrACine(CamaraSelector.PoseSaludo, gestos.duracionSaludo);
        gestos.Saludar();
    }

    public void AmorYPaz()
    {
        if (gestos == null || camaras == null) return;
        EntrarCine(ModoCine.Gesto, ProtesisVivaTextos.CineAmorPaz);
        AbrirMano();
        camaras.IrACine(CamaraSelector.PoseSaludo, gestos.duracionPaz);
        gestos.AmorYPaz();
    }

    public void Explicar()
    {
        if (camaras == null) return;
        EntrarCine(ModoCine.Explicacion, ProtesisVivaTextos.CineExplicando);
        AbrirMano();
        camaras.IrACine(CamaraSelector.PosePerfil, 0f);
        if (camaras.rayos != null) camaras.rayos.Activar(true);
        if (anotaciones != null) anotaciones.Mostrar();
    }

    /// <summary>Boton X: sale de cualquier cinematica y devuelve la interfaz normal.</summary>
    public void CerrarCine() { SalirCine(); }

    void EntrarCine(ModoCine modo, string rotulo)
    {
        if (modoCine == ModoCine.Ninguno)
        {
            controlesEstaban = panelControles != null && panelControles.activeSelf;
            hud.SetActive(false);
        }
        info.SetActive(false);
        modoCine = modo;
        if (textoCine != null) textoCine.text = rotulo;
        if (capaCine != null) capaCine.SetActive(true);
    }

    void SalirCine()
    {
        if (modoCine == ModoCine.Ninguno) return;
        ModoCine m = modoCine;
        modoCine = ModoCine.Ninguno;

        if (m == ModoCine.Gesto && gestos != null && gestos.Ocupado) gestos.Cancelar();
        if (m == ModoCine.Explicacion && anotaciones != null) anotaciones.Ocultar();
        if (camaras != null)
        {
            camaras.SalirCine();
            if (camaras.rayos != null) camaras.rayos.Activar(camaras.Vista == 2);
        }
        if (capaCine != null) capaCine.SetActive(false);
        if (hud != null && !menu.activeSelf)
        {
            hud.SetActive(true);
            if (panelControles != null) panelControles.SetActive(controlesEstaban);
            ActualizarBotonControles();
        }
    }

    void AlCambiarGesto(Gesto g)
    {
        if (g == Gesto.Ninguno && modoCine == ModoCine.Gesto) SalirCine();
    }

    void ActualizarBotonControles()
    {
        bool visible = panelControles != null && panelControles.activeSelf;
        if (botonControlesFondo != null) botonControlesFondo.color = visible ? Acento : AcentoSuave;
    }

    public void MostrarTab(int i)
    {
        i = Mathf.Clamp(i, 0, 2);
        if (infoTitulo != null) infoTitulo.text = ProtesisVivaTextos.TitulosFicha[i];
        if (infoCuerpo != null) infoCuerpo.text = ProtesisVivaTextos.CuerposFicha[i];
        for (int k = 0; k < tabsFondo.Length; k++)
        {
            if (tabsFondo[k] != null) tabsFondo[k].color = k == i ? Acento : AcentoSuave;
            if (tabsTexto[k] != null) tabsTexto[k].color = Blanco;
        }
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    // ---------------------------------------------------------------- reacciones
    void AlCambiarAgarre(ManoAgarre a)
    {
        if (a == null) return;

        if (estado != null)
        {
            if (a.AlertaCalor) { estado.text = ProtesisVivaTextos.EstadoAlertaCalor; estado.color = Naranja; }
            else
                switch (a.Estado)
                {
                    case EstadoAgarre.Reposo: estado.text = ProtesisVivaTextos.EstadoReposo; estado.color = Gris; break;
                    case EstadoAgarre.Cerrando: estado.text = ProtesisVivaTextos.EstadoCerrando; estado.color = Blanco; break;
                    default: estado.text = ProtesisVivaTextos.EstadoAgarre + " · " + a.NivelFuerza; estado.color = Alerta; break;
                }
        }

        if (textoCierre != null)
            textoCierre.text = Mathf.RoundToInt(a.ordenCierre * 100f) + " %";

        for (int i = 0; i < nivelFondo.Length; i++)
            if (nivelFondo[i] != null) nivelFondo[i].color = i == a.nivelFuerza ? Acento : AcentoSuave;

        if (textoBotonCaliente != null)
            textoBotonCaliente.text = a.objetoCaliente ? ProtesisVivaTextos.BotonObjetoCaliente : ProtesisVivaTextos.BotonObjetoFrio;
        if (botonCalienteFondo != null)
            botonCalienteFondo.color = a.objetoCaliente ? Naranja : AcentoSuave;

        Chip(chipPresion, fondoChipPresion, ProtesisVivaTextos.ChipPresion, Mathf.RoundToInt(a.Presion * 100f) + " %",
             a.AgarreFirme ? Acento : PanelClaro);
        Chip(chipTemp, fondoChipTemp, ProtesisVivaTextos.ChipTemp, Mathf.RoundToInt(a.Temperatura) + " °C",
             a.AlertaCalor ? Naranja : PanelClaro);
        bool vib = a.Vibracion > 0.01f;
        Chip(chipVibracion, fondoChipVibracion, ProtesisVivaTextos.ChipVibracion,
             vib ? ProtesisVivaTextos.Si : ProtesisVivaTextos.No, vib ? Acento : PanelClaro);
        Chip(chipAlerta, fondoChipAlerta, ProtesisVivaTextos.ChipAlerta,
             a.AlertaCalor ? ProtesisVivaTextos.Si : ProtesisVivaTextos.No, a.AlertaCalor ? Alerta : PanelClaro);
    }

    static void Chip(TMP_Text txt, Image fondo, string titulo, string valor, Color color)
    {
        if (txt != null) txt.text = "<size=70%><color=#E8B8B8>" + titulo + "</color></size>\n<b>" + valor + "</b>";
        if (fondo != null) fondo.color = color;
    }

    void AlCambiarVista(int i)
    {
        if (vista != null && camaras != null) vista.text = camaras.NombreVista;
    }

    void AlCambiarLuz(bool noche)
    {
        if (textoBotonLuz != null)
            textoBotonLuz.text = noche ? ProtesisVivaTextos.BotonLuzNoche : ProtesisVivaTextos.BotonLuzDia;
    }
}
