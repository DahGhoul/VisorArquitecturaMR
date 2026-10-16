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
    // 1. DESCUBRIMIENTO AUTÓNOMO POR RADAR UDP
    // =========================================================================

    /// <summary>
    /// Inicia un hilo secundario de escucha en puerto 47777 UDP para sincronizar
    /// automáticamente la IP y PIN del servidor de modelos dentro de la red local sin configuración manual.
    /// </summary>
    private void IniciarEscuchaRadarUDP()
    {
        try
        {
            udpClient = new UdpClient(47777);
            udpClient.EnableBroadcast = true;
            hiloRadar = new Thread(() =>
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Any, 47777);
                while (radarCorriendo)
                {
                    try
                    {
                        byte[] bytes = udpClient.Receive(ref ep);
                        RadarPacket pkt = JsonUtility.FromJson<RadarPacket>(Encoding.UTF8.GetString(bytes));
                        if (pkt != null && pkt.tipo == "VISOR_ARQ_SERVER")
                        {
                            urlDetectadaPorRadar = pkt.url;
                            nombrePcRadar = pkt.pc;
                            pinDetectadoRadar = pkt.pin;
                        }
                    }
                    catch { }
                }
            });
            hiloRadar.IsBackground = true;
            hiloRadar.Start();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[VisorArquitectura] No se pudo inicializar el socket UDP: " + ex.Message);
        }
    }

    // =========================================================================
    // 2. CONEXIÓN POR PIN Y RESOLUCIÓN DE SUBRED
    // =========================================================================

    /// <summary>
    /// Resuelve el host y la subred a partir de un código PIN de 4 dígitos (Subred + Host ID),
    /// lanzando peticiones de sondeo HTTP a los candidatos de red más probables.
    /// </summary>
    public void ConectarConPin()
    {
        if (pinIngresado.Length < 4)
        {
            ActualizarEstado("Ingresa los 4 dígitos del PIN.");
            return;
        }

        int subred = int.Parse(pinIngresado.Substring(0, 1));
        int host = int.Parse(pinIngresado.Substring(1, 3));
        string prefijo = "192.168." + subred;

        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    string[] p = ip.ToString().Split('.');
                    if (p.Length == 4 && p[2].EndsWith(subred.ToString()))
                    {
                        prefijo = p[0] + "." + p[1] + "." + p[2];
                        break;
                    }
                }
            }
        }
        catch { }

        StartCoroutine(ProbarCandidatos(new string[] {
            "http://" + prefijo + "." + host + ":8000",
            "http://192.168." + subred + "." + host + ":8000",
            "http://192.168.13" + subred + "." + host + ":8000",
            "http://10.0." + subred + "." + host + ":8000"
        }));
    }

    /// <summary>
    /// Corutina que evalúa endpoints candidatos secuencialmente para establecer la conexión con el servidor.
    /// </summary>
    private IEnumerator ProbarCandidatos(string[] urls)
    {
        ActualizarEstado("Vinculando con PIN " + pinIngresado + "...");
        foreach (string u in urls)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(u + "/lista"))
            {
                req.timeout = 2;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    urlServidor = u;
                    if (panelModalPin != null) panelModalPin.SetActive(false);
                    ProcesarJsonLista(req.downloadHandler.text);
                    yield break;
                }
            }
        }
        ActualizarEstado("Servidor no encontrado en la red local.");
    }

    // =========================================================================
    // 3. SINCRONIZACIÓN DE PROYECTOS Y DESCARGA GLB
    // =========================================================================

    /// <summary>
    /// Consulta los modelos almacenados en el almacenamiento persistente local del dispositivo
    /// y solicita la lista actualizada al portal web en red local.
    /// </summary>
    public void RefrescarListaModelos()
    {
        listaNombresModelos.Clear();
        if (Directory.Exists(Application.persistentDataPath))
        {
            foreach (string f in Directory.GetFiles(Application.persistentDataPath, "*.glb"))
                listaNombresModelos.Add("GUARDADO:" + Path.GetFileName(f));
        }
        StartCoroutine(ConsultarLista());
    }

    /// <summary>
    /// Petición HTTP asíncrona para obtener la lista de proyectos en formato JSON.
    /// </summary>
    private IEnumerator ConsultarLista()
    {
        using (UnityWebRequest req = UnityWebRequest.Get(urlServidor + "/lista"))
        {
            req.timeout = 2;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
                ProcesarJsonLista(req.downloadHandler.text);
            else
                ReconstruirListaProyectosUI();
        }
    }

    /// <summary>
    /// Procesa la respuesta JSON del servidor y actualiza la lista interna de modelos disponibles.
    /// </summary>
    private void ProcesarJsonLista(string json)
    {
        EstadoPortalResponse resp = JsonUtility.FromJson<EstadoPortalResponse>(json);
        if (resp != null && resp.modelos != null)
        {
            foreach (string m in resp.modelos)
            {
                string item = "PC:" + m;
                if (!listaNombresModelos.Contains(item)) listaNombresModelos.Add(item);
            }
            ActualizarEstado("Servidor activo: " + resp.pc + " (" + resp.modelos.Length + " modelos)");
        }
        ReconstruirListaProyectosUI();
    }

    /// <summary>
    /// Inicia la rutina de carga y decodificación para el proyecto seleccionado.
    /// </summary>
    public void CargarModeloPorIdentificador(string id)
    {
        StartCoroutine(FlujoDescargaYCargaGLB(id));
    }

    /// <summary>
    /// Descarga asíncrona por chunks del archivo binario .glb, almacenamiento local en caché,
    /// e instanciación de escena mediante glTFast y optimización de materiales URP.
    /// </summary>
    private IEnumerator FlujoDescargaYCargaGLB(string id)
    {
        modeloCargado = false;
        enModoMaqueta = false;
        modoDron = false;
        ActualizarEstiloBotonesDock();

        string nombreLimpio = id.Replace("PC:", "").Replace("GUARDADO:", "").Replace("LOCAL:", "");
        string rutaLocalCasco = Path.Combine(Application.persistentDataPath, nombreLimpio);

        if (id.StartsWith("LOCAL:"))
        {
            rutaLocalCasco = Path.Combine(Application.streamingAssetsPath, nombreLimpio);
        }
        else if (id.StartsWith("PC:"))
        {
            string urlDescarga = urlServidor + "/modelos/" + Uri.EscapeDataString(nombreLimpio);
            ActualizarEstado("Descargando " + nombreLimpio + "...");

            using (UnityWebRequest req = UnityWebRequest.Get(urlDescarga))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(rutaLocalCasco, req.downloadHandler.data);
                }
                else
                {
                    ActualizarEstado("Error HTTP (" + req.responseCode + ") al descargar");
                    yield break;
                }
            }
        }

        ActualizarEstado("Procesando geometría y materiales...");
        if (contenedorModelo != null) Destroy(contenedorModelo);

        contenedorModelo = new GameObject("Contenedor_Arquitectura");
        var gltf = new GltfImport();
        var tareaCarga = gltf.Load(rutaLocalCasco);
        while (!tareaCarga.IsCompleted) yield return null;

        if (tareaCarga.Result)
        {
            var tareaInst = gltf.InstantiateMainSceneAsync(contenedorModelo.transform);
            while (!tareaInst.IsCompleted) yield return null;

            yield return StartCoroutine(OptimizarEscenaSketchUp(contenedorModelo));
        }
        else
        {
            ActualizarEstado("Error al leer el archivo .glb.");
        }
    }
}
