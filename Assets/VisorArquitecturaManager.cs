// ================================================================================================
// PROYECTO: Visor Arquitectura MR (Meta Quest / PC VR)
// ORIGEN: Universidad Nacional de Trujillo (UNT) — Ing. de Sistemas & Arquitectura
// COLABORACIÓN: roo | Soluciones Digitales ("Tecnología con Propósito — El futuro, simple")
// AUTORES: Diego Joel Cuba Moya & Ronaldo Carlos Robles Romero
// NATURALEZA: Desarrollo académico e innovación tecnológica interdisciplinaria (Sin fines de lucro)
// ================================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
using GLTFast;

/// <summary>
/// Estructura de respuesta del estado del portal web local de modelos 3D.
/// </summary>
[Serializable]
public class EstadoPortalResponse
{
    public string ip;
    public string pin;
    public string url;
    public string pc;
    public string modelo_activo;
    public string[] modelos;
}

/// <summary>
/// Paquete de telemetría y descubrimiento por difusión (broadcast) UDP.
/// </summary>
[Serializable]
public class RadarPacket
{
    public string tipo;
    public string url;
    public string ip;
    public string pin;
    public string pc;
    public string modelo_activo;
}

/// <summary>
/// Gestor principal del Visor de Arquitectura para Realidad Mixta (MR) y Realidad Virtual (VR).
/// Orquesta la descarga de modelos glTF/GLB, optimización PBR URP en runtime,
/// navegación inmersiva (Peatón, Dron, Maqueta AR) e interfaces de usuario espaciales.
/// </summary>
public partial class VisorArquitecturaManager : MonoBehaviour
{
    // =========================================================================
    // 1. CONFIGURACIÓN EN INSPECTOR
    // =========================================================================

    [Header("1. Referencias XR (Detección Automática)")]
    [Tooltip("Transform raíz del XR Origin / XR Rig.")]
    public Transform xrOrigin;
    [Tooltip("Transform de compensación de cámara (Camera Offset).")]
    public Transform cameraOffset;
    [Tooltip("Cámara principal del visor HMD.")]
    public Camera mainCamera;

    [Header("2. Red Local y Servidor")]
    [Tooltip("Dirección HTTP del servidor portal de modelos.")]
    public string urlServidor = "http://127.0.0.1:8000";
    [Tooltip("Código PIN de 4 dígitos para sincronización local.")]
    public string pinIngresado = "";

    [Header("3. Parámetros de Recorrido y Confort VR (Antináusea)")]
    [Tooltip("Velocidad de desplazamiento en modo Peatón (m/s) calibrada a paso humano natural.")]
    public float velocidadCaminar = 2.2f;
    [Tooltip("Velocidad de desplazamiento en modo Dron / Vuelo (m/s) estabilizada.")]
    public float velocidadDron = 3.8f;
    [Tooltip("Activa el Giro Confort por pasos instantáneos (Snap Turn) para evitar mareos al rotar cámara.")]
    public bool usarGiroConfort = true;
    [Tooltip("Ángulo de rotación por cada pulso en modo Giro Confort (grados).")]
    public float anguloGiroConfort = 35.0f;
    [Tooltip("Velocidad de giro continuo (grados/s) cuando se desactiva Giro Confort.")]
    public float velocidadGiroSuave = 55.0f;
    [Tooltip("Activa la viñeta periférica antináusea automática al desplazarse.")]
    public bool usarVinetaConfort = true;
    [Tooltip("Factor de reducción métrica para el modo Maqueta AR.")]
    public float escalaMaquetaActual = 0.03f;

    [Header("4. Rendimiento y Materiales URP")]
    [Tooltip("Material base de referencia para el shader Universal Render Pipeline Lit.")]
    public Material materialBaseInspector;
    [Tooltip("Material de vidrio/cristal transparente para evitar shader stripping en builds de Quest.")]
    public Material materialVidrioInspector;
    [Tooltip("Material de follaje/cutout con Alpha Clipping para vegetación optimizada.")]
    public Material materialFollajeInspector;
    [Tooltip("Activa sombras dinámicas en tiempo real (desactivado por defecto en Meta Quest para garantizar 90 FPS).")]
    public bool sombrasEnQuest = false;
    [Tooltip("Fuerza renderizado de doble cara (Two-Sided) para compatibilidad universal con mallas de SketchUp.")]
    public bool forzarDobleCara = true;

    // =========================================================================
    // 2. ESTADO INTERNO Y COMPONENTES
    // =========================================================================

    private CharacterController cuerpoJugador;
    private Transform manoDerechaTransform;
    private Transform manoIzquierdaTransform;
    private Transform punteroNativoDerecho;
    private GameObject contenedorModelo;
    private BoxCollider colliderAgarreMaqueta;
    private GameObject sueloSeguridadInvisible;
    private Material materialBaseURP;
    private Sprite spriteRedondeadoUI;
    private Bounds limitesModeloOriginal;

    private bool modeloCargado = false;
    private bool modoDron = false;
    private bool enModoMaqueta = false;
    private bool passthroughActivo = false;
    private bool arrastrandoMaqueta = false;
    private float distanciaAgarreMaqueta = 1.0f;

    private Vector3 velocidadSuavizada = Vector3.zero;
    private float velocidadVerticalY = 0f;
    private float yawPC = 0f;
    private float pitchPC = 0f;
    private bool snapTurnListo = true;
    private float tiempoUltimoSnapTurn = 0f;
    private Image imgVinetaConfort;
    private float alphaVinetaActual = 0f;
    private TextMeshProUGUI txtBtnGiroConfort;

    private Vector3 posCentradaOriginal;
    private Vector3 centroLocalModelo = Vector3.zero;
    private Vector3 puntoSpawnPlantaBaja = new Vector3(0, 0.1f, 0);
    private List<Vector3> nivelesEdificio = new List<Vector3>();
    private int indiceNivelActual = 0;

    // =========================================================================
    // 3. COMUNICACIÓN DE RED Y RADAR UDP
    // =========================================================================

    private UdpClient udpClient;
    private Thread hiloRadar;
    private volatile bool radarCorriendo = true;
    private string urlDetectadaPorRadar = "";
    private string nombrePcRadar = "";
    private string pinDetectadoRadar = "";

    // =========================================================================
    // 4. INTERFAZ GRÁFICA ESPACIAL (Apple VisionOS Dark Graphite Style)
    // =========================================================================

    private GameObject canvasMenuRaiz;
    private GameObject panelModalPin;
    private TextMeshProUGUI textoEstadoUI;
    private TextMeshProUGUI textoPinUI;
    private Image imgBtnPeaton, imgBtnDron, imgBtnMaqueta;
    private TextMeshProUGUI txtBtnPeaton, txtBtnDron, txtBtnMaqueta;
    private List<GameObject> filasProyectosDinamicos = new List<GameObject>();
    private List<string> listaNombresModelos = new List<string>();

    // Paleta cromática corporativa: Grafito Neutro & Blanco Puro
    private readonly Color colVentanaGrafito = new Color(0.08f, 0.08f, 0.09f, 0.94f);
    private readonly Color colTarjetaGrafito = new Color(0.12f, 0.12f, 0.14f, 0.98f);
    private readonly Color colBotonInactivo = new Color(0.17f, 0.17f, 0.19f, 1.0f);
    private readonly Color colBotonHover = new Color(0.25f, 0.25f, 0.28f, 1.0f);
    private readonly Color colBotonActivoBlanco = new Color(0.98f, 0.98f, 0.99f, 1.0f);
    private readonly Color colTextoClaro = new Color(0.95f, 0.95f, 0.97f, 1.0f);
    private readonly Color colTextoSecundario = new Color(0.62f, 0.62f, 0.66f, 1.0f);
    private readonly Color colTextoOscuroActivo = new Color(0.07f, 0.07f, 0.08f, 1.0f);

    private Image ultimoBotonHover;
    private Color colorBaseHover;
    private float ultimoTiempoClic = 0f;

    // Registro de estados de botones para debounce y detección de flancos
    private bool gatilloPrevio = false;
    private bool btnAPrevio = false;
    private bool btnBPrevio = false;
    private bool btnXPrevio = false;
    private bool btnYPrevio = false;

    // =========================================================================
    // 5. MÉTODOS AUXILIARES DE CONSTRUCCIÓN DE INTERFAZ
    // =========================================================================

    /// <summary>
    /// Instancia un panel rectangular con esquinas suavizadas estilo Sliced Sprite.
    /// </summary>
    private GameObject CrearPanelRedondeado(string nombre, RectTransform padre, Vector2 pos, Vector2 tamano, Color color)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamano;

        Image img = go.AddComponent<Image>();
        img.sprite = spriteRedondeadoUI;
        img.type = Image.Type.Sliced;
        img.color = color;
        return go;
    }

    /// <summary>
    /// Crea y configura un elemento de texto TextMeshPro con los estilos y fuentes dados.
    /// </summary>
    private TextMeshProUGUI CrearTextoUI(string texto, RectTransform padre, Vector2 pos, Vector2 tamano, float fontSize, Color color, FontStyles estilo, TextAlignmentOptions alineacion)
    {
        GameObject go = new GameObject("Txt");
        go.transform.SetParent(padre, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamano;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = estilo;
        tmp.alignment = alineacion;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>
    /// Construye un botón interactivo compatible con raycast VR y colisiones hápticas.
    /// </summary>
    private GameObject CrearBotonRedondeado(string idUnico, string etiqueta, RectTransform padre, Vector2 pos, Vector2 tamano, Color colorFondo, Color colorTexto, float fontSize)
    {
        GameObject btn = CrearPanelRedondeado(idUnico, padre, pos, tamano, colorFondo);
        BoxCollider col = btn.AddComponent<BoxCollider>();
        col.size = new Vector3(tamano.x, tamano.y, 5f);

        Button bComp = btn.AddComponent<Button>();
        bComp.transition = Selectable.Transition.None;
        bComp.onClick.AddListener(() => EjecutarAccionBoton(idUnico));

        if (!string.IsNullOrEmpty(etiqueta))
        {
            CrearTextoUI(etiqueta, btn.GetComponent<RectTransform>(), Vector2.zero, tamano - new Vector2(14f, 4f), fontSize, colorTexto, FontStyles.Bold, TextAlignmentOptions.Center);
        }
        return btn;
    }

    // =========================================================================
    // 6. CICLO DE VIDA MONOBEHAVIOUR
    // =========================================================================

    void Awake()
    {
        spriteRedondeadoUI = CrearSpriteRedondeado(64, 14);
        ExtraerMaterialURPNativo();
        ConfigurarEscenaYDesactivarLocomocionVieja();
    }

    void Start()
    {
        ConfigurarEscenaYDesactivarLocomocionVieja();
        Physics.queriesHitBackfaces = true;
        ConfigurarOrigenPisoQuest();
        ConfigurarIluminacionArquitectonica();

        CrearSueloSeguridadInvisible();
        ConfigurarCapsulaAntiatascos();
        IrAPlantaBaja();

        ConstruirInterfazGrafitoVisionOS();
        ActualizarEstiloBotonesDock();

        // Modo inmersivo VR por defecto al iniciar; Passthrough se reserva para Modo Maqueta AR
        ConfigurarPassthrough(false);

        StartCoroutine(CentrarMenuCuandoEsteListo());
        IniciarEscuchaRadarUDP();
        RefrescarListaModelos();
    }

    void OnDestroy()
    {
        radarCorriendo = false;
        if (udpClient != null)
        {
            try { udpClient.Close(); } catch { }
        }
    }

    void Update()
    {
        // Detección automática por difusión UDP del servidor en red local
        if (!string.IsNullOrEmpty(urlDetectadaPorRadar) && urlServidor == "http://127.0.0.1:8000")
        {
            urlServidor = urlDetectadaPorRadar;
            pinIngresado = pinDetectadoRadar;
            if (textoPinUI != null) textoPinUI.text = pinIngresado;
            ActualizarEstado("Conectado a " + nombrePcRadar + " (PIN " + pinDetectadoRadar + ")");
            urlDetectadaPorRadar = "";
            if (panelModalPin != null) panelModalPin.SetActive(false);
            RefrescarListaModelos();
        }

        SincronizarCapsulaRodillaACabeza();
        ProcesarInteraccionYPuntero();
        ProcesarLocomocionConSensorEsferico();
    }

    // =========================================================================
    // 7. GESTIÓN DE ALMACENAMIENTO Y CACHÉ LOCAL
    // =========================================================================

    /// <summary>
    /// Limpia todos los modelos GLB guardados localmente en caché para liberar espacio
    /// o forzar la recarga de activos actualizados desde el portal.
    /// </summary>
    public void LimpiarCacheDescargasLocal()
    {
        try
        {
            string ruta = Application.persistentDataPath;
            if (Directory.Exists(ruta))
            {
                string[] archivos = Directory.GetFiles(ruta, "*.glb");
                foreach (string f in archivos)
                {
                    try { File.Delete(f); } catch { }
                }
            }
            ActualizarEstado("Caché local de modelos limpiada.");
            RefrescarListaModelos();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[VisorArquitectura] Error al limpiar caché: " + ex.Message);
        }
    }
}
