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

public partial class VisorArquitecturaManager
{
    // =========================================================================
    // 1. GENERACIÓN PROCEDURAL DE TEXTURAS UI
    // =========================================================================

    /// <summary>
    /// Genera proceduralmente un sprite circular/redondeado de 9 fragmentos (9-Sliced Sprite)
    /// para renderizar ventanas y botones con esquinas suaves de alta resolución sin artefactos de compresión.
    /// </summary>
    private Sprite CrearSpriteRedondeado(int res, int radio)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pix = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                int cx = Mathf.Clamp(x, radio, res - 1 - radio);
                int cy = Mathf.Clamp(y, radio, res - 1 - radio);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float alpha = Mathf.Clamp01(radio - dist + 0.5f);
                pix[y * res + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pix);
        tex.Apply();

        Vector4 border = new Vector4(radio, radio, radio, radio);
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    // =========================================================================
    // 2. POSICIONAMIENTO ESPACIAL DEL CANVAS FLOTANTE
    // =========================================================================

    /// <summary>
    /// Aguarda a que los sensores de seguimiento 6DoF del visor estabilicen la orientación inicial
    /// para proyectar la interfaz exactamente frente al campo visual del usuario.
    /// </summary>
    private IEnumerator CentrarMenuCuandoEsteListo()
    {
        yield return new WaitForSeconds(0.4f);
        float tiempo = 0f;
        while (tiempo < 1.0f)
        {
            tiempo += Time.deltaTime;
            if (mainCamera != null && mainCamera.transform.position.sqrMagnitude > 0.01f && mainCamera.transform.forward != Vector3.forward)
                break;
            yield return null;
        }
        yield return null;
        CentrarMenuFrenteAlUsuario();
    }

    /// <summary>
    /// Calcula el vector director horizontal de la mirada del usuario y reubica el Canvas a 1.32m de distancia.
    /// </summary>
    private void CentrarMenuFrenteAlUsuario()
    {
        if (canvasMenuRaiz == null || mainCamera == null) return;
        Vector3 mirada = mainCamera.transform.forward;
        mirada.y = 0;
        if (mirada.sqrMagnitude < 0.01f) mirada = Vector3.forward;
        mirada.Normalize();

        canvasMenuRaiz.transform.position = mainCamera.transform.position + mirada * 1.32f + new Vector3(0, -0.05f, 0);
        canvasMenuRaiz.transform.rotation = Quaternion.LookRotation(mirada);
    }

    /// <summary>
    /// Muestra u oculta la interfaz de usuario en el espacio virtual, sincronizando listas de modelos al abrir.
    /// </summary>
    public void AlternarMenu()
    {
        if (canvasMenuRaiz == null) return;
        bool mostrar = !canvasMenuRaiz.activeSelf;
        canvasMenuRaiz.SetActive(mostrar);
        if (mostrar)
        {
            CentrarMenuFrenteAlUsuario();
            ActualizarEstiloBotonesDock();
            RefrescarListaModelos();
        }
    }

    // =========================================================================
    // 3. CONSTRUCCIÓN PROCEDURAL DE LA INTERFAZ ESPACIAL (Apple VisionOS Dark Graphite)
    // =========================================================================

    /// <summary>
    /// Instancia programáticamente el árbol jerárquico del Canvas en World-Space:
    /// - Ventana Principal con selector de proyectos y acciones rápidas.
    /// - Teclado numérico modal para vinculación por PIN.
    /// - Barra dock inferior flotante con indicadores de modo (Peatón, Dron, Maqueta AR).
    /// </summary>
    private void ConstruirInterfazGrafitoVisionOS()
    {
        canvasMenuRaiz = new GameObject("UI_VisionOS_Graphite");
        Canvas canvas = canvasMenuRaiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = canvasMenuRaiz.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasMenuRaiz.AddComponent<GraphicRaycaster>();
        canvasMenuRaiz.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform rtRaiz = canvasMenuRaiz.GetComponent<RectTransform>();
        rtRaiz.sizeDelta = new Vector2(920f, 600f);
        rtRaiz.localScale = new Vector3(0.0011f, 0.0011f, 0.0011f);

        // --- 1. VENTANA PRINCIPAL (GRAFITO NEUTRO REDONDEADO) ---
        GameObject ventana = CrearPanelRedondeado("Ventana_Principal", rtRaiz, new Vector2(0f, 45f), new Vector2(860f, 440f), colVentanaGrafito);
        RectTransform rtVentana = ventana.GetComponent<RectTransform>();

        // Barra superior de títulos y acciones
        CrearTextoUI("VISOR ARQUITECTÓNICO XR", rtVentana, new Vector2(-275f, 178f), new Vector2(270f, 32f), 16f, colTextoClaro, FontStyles.Bold, TextAlignmentOptions.Left);
        textoEstadoUI = CrearTextoUI("Sincronizando red...", rtVentana, new Vector2(-45f, 178f), new Vector2(200f, 32f), 11.5f, colTextoSecundario, FontStyles.Normal, TextAlignmentOptions.Left);

        GameObject bGiro = CrearBotonRedondeado("BTN_TOGGLE_GIRO", usarGiroConfort ? "Giro: Confort" : "Giro: Suave", rtVentana, new Vector2(100f, 178f), new Vector2(96f, 34f), colBotonInactivo, colTextoClaro, 11.5f);
        txtBtnGiroConfort = bGiro.GetComponentInChildren<TextMeshProUGUI>();

        CrearBotonRedondeado("BTN_TOGGLE_PIN", "PIN", rtVentana, new Vector2(184f, 178f), new Vector2(54f, 34f), colBotonInactivo, colTextoClaro, 12.5f);
        CrearBotonRedondeado("BTN_ACT_LIMPIAR_CACHE", "Limpiar Caché", rtVentana, new Vector2(272f, 178f), new Vector2(106f, 34f), colBotonInactivo, colTextoClaro, 11.5f);
        CrearBotonRedondeado("BTN_ACT_REFRESCAR", "Actualizar", rtVentana, new Vector2(368f, 178f), new Vector2(72f, 34f), colBotonInactivo, colTextoClaro, 11.5f);

        // Divisor visual elegante
        GameObject linea = new GameObject("Linea_Divisoria");
        linea.transform.SetParent(rtVentana, false);
        RectTransform rtLinea = linea.AddComponent<RectTransform>();
        rtLinea.anchoredPosition = new Vector2(0f, 146f);
        rtLinea.sizeDelta = new Vector2(800f, 1.5f);
        linea.AddComponent<Image>().color = new Color(0.20f, 0.20f, 0.23f, 1f);

        // Encabezado de la lista de proyectos y controles de paginación VR
        CrearTextoUI("MODELOS DISPONIBLES (.GLB)", rtVentana, new Vector2(-220f, 118f), new Vector2(360f, 24f), 12f, colTextoSecundario, FontStyles.Bold, TextAlignmentOptions.Left);
        btnPagAnt = CrearBotonRedondeado("BTN_PAG_ANT", "< Ant.", rtVentana, new Vector2(185f, 118f), new Vector2(68f, 28f), colBotonInactivo, colTextoClaro, 11.5f);
        txtPaginacionUI = CrearTextoUI("Pág. 1/1", rtVentana, new Vector2(275f, 118f), new Vector2(95f, 26f), 12f, colTextoSecundario, FontStyles.Bold, TextAlignmentOptions.Center);
        btnPagSig = CrearBotonRedondeado("BTN_PAG_SIG", "Sig. >", rtVentana, new Vector2(365f, 118f), new Vector2(68f, 28f), colBotonInactivo, colTextoClaro, 11.5f);

        // --- 2. MODAL DESPLEGABLE DE PIN ---
        panelModalPin = CrearPanelRedondeado("Modal_PIN", rtVentana, new Vector2(0f, -15f), new Vector2(340f, 320f), colTarjetaGrafito);
        RectTransform rtPin = panelModalPin.GetComponent<RectTransform>();

        CrearTextoUI("CÓDIGO PIN DEL SERVIDOR", rtPin, new Vector2(0f, 128f), new Vector2(280f, 26f), 13f, colTextoSecundario, FontStyles.Bold, TextAlignmentOptions.Center);
        CrearPanelRedondeado("Caja_Digitos", rtPin, new Vector2(0f, 88f), new Vector2(220f, 40f), colVentanaGrafito);
        textoPinUI = CrearTextoUI("_ _ _ _", rtPin, new Vector2(0f, 88f), new Vector2(200f, 36f), 20f, colTextoClaro, FontStyles.Bold, TextAlignmentOptions.Center);

        string[] teclas = new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "OK" };
        for (int i = 0; i < teclas.Length; i++)
        {
            int fila = i / 3;
            int col = i % 3;
            float x = (col - 1) * 76f;
            float y = 28f - fila * 48f;
            bool esOk = teclas[i] == "OK";
            Color cFondo = esOk ? colBotonActivoBlanco : colBotonInactivo;
            Color cTxt = esOk ? colTextoOscuroActivo : colTextoClaro;
            CrearBotonRedondeado("BTN_PIN_" + teclas[i], teclas[i], rtPin, new Vector2(x, y), new Vector2(68f, 40f), cFondo, cTxt, 15f);
        }
        panelModalPin.SetActive(false);

        // --- 3. DOCK INFERIOR FLOTANTE INDEPENDIENTE ---
        GameObject dock = CrearPanelRedondeado("Dock_Inferior_Flotante", rtRaiz, new Vector2(0f, -225f), new Vector2(760f, 68f), colVentanaGrafito);
        RectTransform rtDock = dock.GetComponent<RectTransform>();

        GameObject bPeaton = CrearBotonRedondeado("BTN_MODO_PEATON", "Caminar 1:1", rtDock, new Vector2(-285f, 0f), new Vector2(140f, 46f), colBotonActivoBlanco, colTextoOscuroActivo, 14f);
        imgBtnPeaton = bPeaton.GetComponent<Image>();
        txtBtnPeaton = bPeaton.GetComponentInChildren<TextMeshProUGUI>();

        GameObject bDron = CrearBotonRedondeado("BTN_MODO_DRON", "Vuelo Dron", rtDock, new Vector2(-135f, 0f), new Vector2(140f, 46f), colBotonInactivo, colTextoClaro, 14f);
        imgBtnDron = bDron.GetComponent<Image>();
        txtBtnDron = bDron.GetComponentInChildren<TextMeshProUGUI>();

        GameObject bMaqueta = CrearBotonRedondeado("BTN_MODO_MAQUETA", "Maqueta AR", rtDock, new Vector2(15f, 0f), new Vector2(140f, 46f), colBotonInactivo, colTextoClaro, 14f);
        imgBtnMaqueta = bMaqueta.GetComponent<Image>();
        txtBtnMaqueta = bMaqueta.GetComponentInChildren<TextMeshProUGUI>();

        CrearBotonRedondeado("BTN_MODO_NIVEL", "Cambiar Piso", rtDock, new Vector2(165f, 0f), new Vector2(140f, 46f), colBotonInactivo, colTextoClaro, 14f);
        CrearBotonRedondeado("BTN_ACT_CERRAR", "Ocultar [Y]", rtDock, new Vector2(300f, 0f), new Vector2(110f, 46f), colTarjetaGrafito, colTextoSecundario, 13f);
    }

    /// <summary>
    /// Regenera dinámicamente los botones de selección para cada modelo .glb disponible con soporte de paginación.
    /// </summary>
    private void ReconstruirListaProyectosUI()
    {
        if (canvasMenuRaiz == null) return;
        foreach (var f in filasProyectosDinamicos) Destroy(f);
        filasProyectosDinamicos.Clear();

        Transform trVentana = canvasMenuRaiz.transform.Find("Ventana_Principal");
        if (trVentana == null) return;
        RectTransform rtVentana = trVentana.GetComponent<RectTransform>();

        if (listaNombresModelos.Count == 0)
        {
            if (txtPaginacionUI != null) txtPaginacionUI.text = "0 modelos";
            if (btnPagAnt != null) btnPagAnt.SetActive(false);
            if (btnPagSig != null) btnPagSig.SetActive(false);
            GameObject vacio = CrearTextoUI("No hay modelos disponibles. Abre Portal Arquitectura XR en tu PC o usa 'PIN'.", rtVentana, new Vector2(0f, -10f), new Vector2(680f, 60f), 15f, colTextoSecundario, FontStyles.Normal, TextAlignmentOptions.Center).gameObject;
            filasProyectosDinamicos.Add(vacio);
            return;
        }

        const int porPagina = 4;
        int totalPaginas = Mathf.Max(1, Mathf.CeilToInt(listaNombresModelos.Count / (float)porPagina));
        paginaActualModelos = Mathf.Clamp(paginaActualModelos, 0, totalPaginas - 1);

        if (txtPaginacionUI != null)
            txtPaginacionUI.text = "Pág. " + (paginaActualModelos + 1) + " / " + totalPaginas + " (" + listaNombresModelos.Count + ")";
        if (btnPagAnt != null) btnPagAnt.SetActive(totalPaginas > 1);
        if (btnPagSig != null) btnPagSig.SetActive(totalPaginas > 1);

        int inicio = paginaActualModelos * porPagina;
        int fin = Mathf.Min(inicio + porPagina, listaNombresModelos.Count);

        for (int idxLista = inicio; idxLista < fin; idxLista++)
        {
            int filaLocal = idxLista - inicio;
            string id = listaNombresModelos[idxLista];
            bool esRed = id.StartsWith("PC:");
            string nombreArchivo = id.Replace("PC:", "").Replace("GUARDADO:", "").Replace("LOCAL:", "");
            bool esActivoPC = esRed && !string.IsNullOrEmpty(modeloActivoServidor) && nombreArchivo == modeloActivoServidor;
            string origenTag = esActivoPC ? "RED LOCAL • ACTIVO" : (esRed ? "RED LOCAL" : "MEMORIA");

            float y = 62f - filaLocal * 64f;
            GameObject fila = CrearBotonRedondeado("BTN_MOD_" + idxLista, "", rtVentana, new Vector2(0f, y), new Vector2(800f, 54f), colTarjetaGrafito, colTextoClaro, 15f);
            RectTransform rtFila = fila.GetComponent<RectTransform>();

            CrearTextoUI(nombreArchivo, rtFila, new Vector2(-130f, 0f), new Vector2(480f, 40f), 15.5f, colTextoClaro, FontStyles.Bold, TextAlignmentOptions.Left);
            CrearTextoUI(origenTag, rtFila, new Vector2(195f, 0f), new Vector2(170f, 30f), 11.5f, esActivoPC ? colTextoClaro : colTextoSecundario, esActivoPC ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Right);
            CrearTextoUI("Abrir  ->", rtFila, new Vector2(335f, 0f), new Vector2(90f, 30f), 14f, colTextoClaro, FontStyles.Bold, TextAlignmentOptions.Right);

            if (panelModalPin != null && panelModalPin.activeSelf)
                panelModalPin.transform.SetAsLastSibling();

            filasProyectosDinamicos.Add(fila);
        }
    }

    /// <summary>
    /// Actualiza visualmente el estado resaltado (blanco puro activo / grafito inactivo) del selector de modo en el Dock.
    /// </summary>
    private void ActualizarEstiloBotonesDock()
    {
        if (imgBtnPeaton == null) return;

        bool peatonActivo = !modoDron && !enModoMaqueta;
        imgBtnPeaton.color = peatonActivo ? colBotonActivoBlanco : colBotonInactivo;
        txtBtnPeaton.color = peatonActivo ? colTextoOscuroActivo : colTextoClaro;

        imgBtnDron.color = modoDron ? colBotonActivoBlanco : colBotonInactivo;
        txtBtnDron.color = modoDron ? colTextoOscuroActivo : colTextoClaro;

        imgBtnMaqueta.color = enModoMaqueta ? colBotonActivoBlanco : colBotonInactivo;
        txtBtnMaqueta.color = enModoMaqueta ? colTextoOscuroActivo : colTextoClaro;
    }

    /// <summary>
    /// Despachador central de eventos para acciones de botones táctiles y de puntero VR.
    /// </summary>
    public void EjecutarAccionBoton(string id)
    {
        if (Time.unscaledTime - ultimoTiempoClic < 0.22f) return;
        ultimoTiempoClic = Time.unscaledTime;

        if (id == "BTN_TOGGLE_PIN")
        {
            if (panelModalPin != null)
            {
                panelModalPin.SetActive(!panelModalPin.activeSelf);
                panelModalPin.transform.SetAsLastSibling();
            }
        }
        else if (id.StartsWith("BTN_PIN_"))
        {
            string t = id.Replace("BTN_PIN_", "");
            if (t == "CLR") pinIngresado = "";
            else if (t == "OK") ConectarConPin();
            else if (pinIngresado.Length < 4) pinIngresado += t;

            if (textoPinUI != null) textoPinUI.text = pinIngresado.PadRight(4, '_');
        }
        else if (id.StartsWith("BTN_MOD_"))
        {
            int idx = int.Parse(id.Replace("BTN_MOD_", ""));
            if (idx >= 0 && idx < listaNombresModelos.Count)
                CargarModeloPorIdentificador(listaNombresModelos[idx]);
        }
        else if (id == "BTN_PAG_ANT")
        {
            if (paginaActualModelos > 0)
            {
                paginaActualModelos--;
                ReconstruirListaProyectosUI();
            }
        }
        else if (id == "BTN_PAG_SIG")
        {
            int totalPaginas = Mathf.Max(1, Mathf.CeilToInt(listaNombresModelos.Count / 4.0f));
            if (paginaActualModelos < totalPaginas - 1)
            {
                paginaActualModelos++;
                ReconstruirListaProyectosUI();
            }
        }
        else if (id == "BTN_TOGGLE_GIRO") AlternarModoGiroConfort();
        else if (id == "BTN_ACT_REFRESCAR") RefrescarListaModelos();
        else if (id == "BTN_ACT_LIMPIAR_CACHE") LimpiarCacheDescargasLocal();
        else if (id == "BTN_MODO_PEATON") ActivarModoPeaton1a1();
        else if (id == "BTN_MODO_DRON") ActivarModoDron();
        else if (id == "BTN_MODO_MAQUETA") ActivarModoMaqueta();
        else if (id == "BTN_MODO_NIVEL") SaltarSiguienteNivel();
        else if (id == "BTN_ACT_CERRAR") canvasMenuRaiz.SetActive(false);
    }

    /// <summary>
    /// Actualiza el rótulo de diagnóstico en la ventana y lo emite en la consola de depuración.
    /// </summary>
    private void ActualizarEstado(string msg)
    {
        if (textoEstadoUI != null) textoEstadoUI.text = msg;
        Debug.Log("[VisorArquitectura] " + msg);
    }
}
