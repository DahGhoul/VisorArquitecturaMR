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
        paginaActualModelos = 0;
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
    /// Procesa la respuesta JSON del servidor y coloca los modelos de RED LOCAL (PC:)
    /// en primer lugar (encabezados por el modelo activo), seguidos de los modelos en MEMORIA (GUARDADO:).
    /// </summary>
    private void ProcesarJsonLista(string json)
    {
        EstadoPortalResponse resp = JsonUtility.FromJson<EstadoPortalResponse>(json);
        if (resp != null && resp.modelos != null)
        {
            modeloActivoServidor = resp.modelo_activo ?? "";
            List<string> modelosRed = new List<string>();

            if (!string.IsNullOrEmpty(modeloActivoServidor))
            {
                foreach (string m in resp.modelos)
                {
                    if (m == modeloActivoServidor)
                    {
                        modelosRed.Add("PC:" + m);
                        break;
                    }
                }
            }

            foreach (string m in resp.modelos)
            {
                string item = "PC:" + m;
                if (!modelosRed.Contains(item))
                    modelosRed.Add(item);
            }

            // Insertar todos los modelos de RED LOCAL al inicio de la lista (antes que los GUARDADO:)
            for (int i = modelosRed.Count - 1; i >= 0; i--)
            {
                listaNombresModelos.Remove(modelosRed[i]);
                listaNombresModelos.Insert(0, modelosRed[i]);
            }

            paginaActualModelos = 0;
            ActualizarEstado("Servidor activo: " + resp.pc + " (" + resp.modelos.Length + " en red)");
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
        SanitizarGLBEnDisco(rutaLocalCasco);

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

    /// <summary>
    /// Neutraliza in-place (en &lt; 1 ms y sin tocar el chunk binario de geometría) la extensión obsoleta
    /// KHR_materials_pbrSpecularGlossiness exportada por plugins como SimLab GLTF en SketchUp,
    /// asegurando que glTFast utilice siempre el bloque estándar pbrMetallicRoughness.
    /// </summary>
    private void SanitizarGLBEnDisco(string rutaArchivo)
    {
        try
        {
            if (string.IsNullOrEmpty(rutaArchivo) || !File.Exists(rutaArchivo)) return;

            using (FileStream fs = new FileStream(rutaArchivo, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                if (fs.Length < 28) return;
                byte[] header = new byte[20];
                if (fs.Read(header, 0, 20) != 20) return;

                uint magic = BitConverter.ToUInt32(header, 0);
                uint jsonLen = BitConverter.ToUInt32(header, 12);
                uint jsonType = BitConverter.ToUInt32(header, 16);

                // 0x46546C67 = "glTF", 0x4E4F534A = "JSON"
                if (magic != 0x46546C67 || jsonType != 0x4E4F534A) return;
                if (jsonLen == 0 || jsonLen > 50 * 1024 * 1024 || 20 + jsonLen > fs.Length) return;

                byte[] jsonBytes = new byte[jsonLen];
                int leidos = 0;
                while (leidos < (int)jsonLen)
                {
                    int n = fs.Read(jsonBytes, leidos, (int)jsonLen - leidos);
                    if (n <= 0) break;
                    leidos += n;
                }
                if (leidos != (int)jsonLen) return;

                string jsonStr = Encoding.UTF8.GetString(jsonBytes);
                if (!jsonStr.Contains("KHR_materials_pbrSpecularGlossiness")) return;

                // Reemplazo exacto byte a byte (35 caracteres ASCII = 35 bytes UTF-8)
                string limpio = jsonStr.Replace("KHR_materials_pbrSpecularGlossiness", "_HR_materials_pbrSpecularGlossiness");
                byte[] nuevosBytes = Encoding.UTF8.GetBytes(limpio);

                if (nuevosBytes.Length == jsonBytes.Length)
                {
                    fs.Seek(20, SeekOrigin.Begin);
                    fs.Write(nuevosBytes, 0, nuevosBytes.Length);
                    fs.Flush();
                }
            }
        }
        catch { }
    }
}
