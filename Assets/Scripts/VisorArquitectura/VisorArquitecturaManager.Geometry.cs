// ================================================================================================
// PROYECTO: Visor Arquitectura MR (Meta Quest / PC VR)
// EMPRESA: roo | Soluciones Digitales
// LEMA: "Tecnología con Propósito — El futuro, simple"
// AUTORES: Diego Joel Cuba Moya & Ronaldo Carlos Robles Romero
// COPYRIGHT: © 2026 roo Soluciones Digitales. Todos los derechos reservados.
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
    // 1. PROCESAMIENTO Y OPTIMIZACIÓN DE GEOMETRÍA EN RUNTIME
    // =========================================================================

    /// <summary>
    /// Procesa la jerarquía importada del archivo glTF/GLB:
    /// - Normaliza la escala métrica si el modelo fue exportado en milímetros o centímetros.
    /// - Centra el modelo respecto al origen de coordenadas conservando las transformaciones locales.
    /// - Agrupa materiales mediante caché para maximizar la compatibilidad con SRP Batcher.
    /// - Genera colisionadores físicos optimizados para pisos y muros estructurales, excluyendo vegetación y vidrios.
    /// </summary>
    private IEnumerator OptimizarEscenaSketchUp(GameObject raiz)
    {
        MeshRenderer[] renderers = raiz.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0) yield break;

        // 1. Medir límites reales de la envolvente arquitectónica
        Bounds limites = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null) limites.Encapsulate(renderers[i].bounds);
        }

        // Corrección de unidades (ej. si el modelo fue modelado en centímetros o milímetros en CAD)
        float maxDim = Mathf.Max(limites.size.x, Mathf.Max(limites.size.y, limites.size.z));
        if (maxDim > 800f)
        {
            raiz.transform.localScale = Vector3.one * 0.01f;
            Physics.SyncTransforms();
            limites = CalcularLimites(raiz);
        }

        // Centrado de la base sobre el origen del mundo virtual
        Vector3 offset = new Vector3(-limites.center.x, -limites.min.y, -limites.center.z);
        raiz.transform.position += offset;
        posCentradaOriginal = raiz.transform.position;
        Physics.SyncTransforms();
        limites = CalcularLimites(raiz);
        limitesModeloOriginal = limites;
        centroLocalModelo = limites.center - posCentradaOriginal;

        // Caché de materiales: cada material original se procesa una única vez,
        // permitiendo que múltiples mallas compartan instancia para aprovechar el SRP Batcher.
        Dictionary<Material, Material> cacheMateriales = new Dictionary<Material, Material>();
        Material materialGrisFallback = null;

        bool desactivarSombrasQuest = !Application.isEditor && !sombrasEnQuest;

        int lote = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer mr = renderers[i];
            if (mr == null) continue;

            MeshFilter mf = mr.GetComponent<MeshFilter>();
            Vector3 tamPieza = mr.bounds.size;
            float espesorMin = Mathf.Min(tamPieza.x, Mathf.Min(tamPieza.y, tamPieza.z));
            bool esDelgada = espesorMin < 0.015f;

            // Diferenciación geométrica:
            // - Delgada en Y: losas, alfombras o pisos planos de una sola cara (requiere colisionador).
            // - Delgada en X o Z: carteles, imágenes murales o láminas verticales (no requiere colisionador).
            bool esPisoDeUnaCara = esDelgada && tamPieza.y <= espesorMin + 0.0001f && Mathf.Min(tamPieza.x, tamPieza.z) > 0.3f;
            bool esLaminaImagen = esDelgada && !esPisoDeUnaCara;

            Material[] mats = mr.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null)
                {
                    if (materialGrisFallback == null) materialGrisFallback = CrearMaterialURP(new Color(0.85f, 0.85f, 0.85f), false);
                    mats[m] = materialGrisFallback;
                    continue;
                }

                if (!cacheMateriales.TryGetValue(mat, out Material procesado))
                {
                    procesado = PrepararMaterial(mat, esLaminaImagen);
                    cacheMateriales[mat] = procesado;
                }
                mats[m] = procesado;
            }
            mr.sharedMaterials = mats;

            // Optimización de iluminación móvil: desactivación de sombras por objeto en Meta Quest
            if (desactivarSombrasQuest || esLaminaImagen)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // Identificación semántica por nomenclatura para evitar colisiones en elementos no estructurales
            string nomObj = mr.gameObject.name.ToLowerInvariant();
            bool esVidrioOVegetacion = nomObj.Contains("plant") || nomObj.Contains("tree") || nomObj.Contains("arbol") ||
                                       nomObj.Contains("hoja") || nomObj.Contains("vidrio") || nomObj.Contains("glass") ||
                                       nomObj.Contains("cristal") || nomObj.Contains("window") || nomObj.Contains("ventana") ||
                                       nomObj.Contains("luz") || nomObj.Contains("light") || nomObj.Contains("enredad") ||
                                       nomObj.Contains("liana") || nomObj.Contains("ivy") || nomObj.Contains("vine");

            // Evaluación de volumen transitable para asignar física estructural
            bool tamanoTransitable = Mathf.Max(tamPieza.x, Mathf.Max(tamPieza.y, tamPieza.z)) >= 0.15f;
            bool debeTenerColision = (renderers.Length <= 10) || (!esLaminaImagen && !esVidrioOVegetacion && tamanoTransitable);

            if (debeTenerColision && !esVidrioOVegetacion && mr.gameObject.GetComponent<Collider>() == null)
            {
                if (mf != null && mf.sharedMesh != null)
                {
                    Mesh mallaCol = GenerarMallaColisionLimpia(mf.sharedMesh);
                    if (mallaCol != null)
                    {
                        try
                        {
                            Mesh mallaOrig = mf.sharedMesh;
                            mf.sharedMesh = mallaCol;
                            MeshCollider mc = mr.gameObject.AddComponent<MeshCollider>();
                            mc.cookingOptions = MeshColliderCookingOptions.None;
                            mf.sharedMesh = mallaOrig;
                            mc.sharedMesh = mallaCol;
                        }
                        catch { }
                    }
                }
            }

            lote++;
            if (lote >= 250)
            {
                lote = 0;
                ActualizarEstado("Optimizando modelo: " + ((i * 100) / renderers.Length) + "%");
                yield return null;
            }
        }

        // Colisionador maestro para agarre y traslación en Modo Maqueta AR
        colliderAgarreMaqueta = raiz.AddComponent<BoxCollider>();
        colliderAgarreMaqueta.size = limites.size;
        colliderAgarreMaqueta.center = limites.center - raiz.transform.position;
        colliderAgarreMaqueta.enabled = false;

        Physics.SyncTransforms();
        DetectarNivelesYPlantaBaja(limites);
        IrAPlantaBaja();

        modeloCargado = true;
        if (canvasMenuRaiz != null) canvasMenuRaiz.SetActive(false);
        ActualizarEstado("Listo. Presiona Botón Y (o TAB) para mostrar/ocultar el menú.");
    }

    /// <summary>
    /// Calcula los límites espaciales agregados (AABB) de todos los MeshRenderers hijos.
    /// </summary>
    private Bounds CalcularLimites(GameObject raiz)
    {
        MeshRenderer[] r = raiz.GetComponentsInChildren<MeshRenderer>(true);
        Bounds b = r[0].bounds;
        for (int i = 1; i < r.Length; i++) b.Encapsulate(r[i].bounds);
        return b;
    }

    /// <summary>
    /// Realiza un barrido de rayos verticales (Raycast Grid) para identificar los niveles habitables,
    /// losas de piso y el punto de acceso ideal (Planta Baja).
    /// </summary>
    private void DetectarNivelesYPlantaBaja(Bounds b)
    {
        nivelesEdificio.Clear();
        indiceNivelActual = 0;

        if (sueloSeguridadInvisible != null)
        {
            sueloSeguridadInvisible.transform.position = new Vector3(b.center.x, b.min.y - 0.05f, b.center.z);
            sueloSeguridadInvisible.SetActive(false);
        }
        if (colliderAgarreMaqueta != null) colliderAgarreMaqueta.enabled = false;
        Physics.SyncTransforms();

        float yCielo = b.max.y + 10f;
        float techoPlantaBajaMax = b.min.y + Mathf.Clamp(b.size.y * 0.40f, 1.5f, 4.2f);

        Vector3 mejorPlantaBaja = new Vector3(b.center.x, b.min.y + 0.85f, b.center.z);
        float mejorPuntaje = float.MaxValue;
        bool halloPB = false;

        float pasoX = Mathf.Max(b.size.x / 10f, 1.2f);
        float pasoZ = Mathf.Max(b.size.z / 10f, 1.2f);

        for (int ix = -4; ix <= 4; ix++)
        {
            for (int iz = -4; iz <= 4; iz++)
            {
                Vector3 origen = new Vector3(b.center.x + ix * pasoX, yCielo, b.center.z + iz * pasoZ);
                RaycastHit[] hits = Physics.RaycastAll(origen, Vector3.down, yCielo + 10f);
                if (hits.Length == 0) continue;

                Array.Sort(hits, (h1, h2) => h1.point.y.CompareTo(h2.point.y));

                for (int k = 0; k < hits.Length; k++)
                {
                    RaycastHit hit = hits[k];
                    if (hit.normal.y < 0.55f) continue;

                    // Descartar salientes pequeñas o escalones menores
                    if (k + 1 < hits.Length && (hits[k + 1].point.y - hit.point.y) < 1.4f)
                        continue;

                    bool esTecho = (k == hits.Length - 1) && (hit.point.y > techoPlantaBajaMax);
                    if (esTecho) continue;

                    Vector3 candidato = hit.point + new Vector3(0, 0.12f, 0);

                    bool repetido = false;
                    for (int n = 0; n < nivelesEdificio.Count; n++)
                    {
                        if (Mathf.Abs(nivelesEdificio[n].y - candidato.y) < 1.6f)
                        {
                            repetido = true;
                            break;
                        }
                    }
                    if (!repetido) nivelesEdificio.Add(candidato);

                    if (hit.point.y <= techoPlantaBajaMax)
                    {
                        float distCentro = new Vector2(candidato.x - b.center.x, candidato.z - b.center.z).magnitude;
                        float puntaje = distCentro + (hit.point.y * 2.0f);
                        if (puntaje < mejorPuntaje)
                        {
                            mejorPuntaje = puntaje;
                            mejorPlantaBaja = candidato;
                            halloPB = true;
                        }
                    }
                }
            }
        }

        if (sueloSeguridadInvisible != null) sueloSeguridadInvisible.SetActive(true);
        Physics.SyncTransforms();

        nivelesEdificio.Sort((a, b2) => a.y.CompareTo(b2.y));

        if (halloPB) puntoSpawnPlantaBaja = mejorPlantaBaja;
        else if (nivelesEdificio.Count > 0) puntoSpawnPlantaBaja = nivelesEdificio[0];
        else
        {
            // Disparar un rayo vertical en el centro para encontrar el piso real del edificio
            if (Physics.Raycast(new Vector3(b.center.x, yCielo, b.center.z), Vector3.down, out RaycastHit hitCentroDirecto, yCielo + 20f))
            {
                puntoSpawnPlantaBaja = hitCentroDirecto.point + new Vector3(0, 0.15f, 0);
            }
            else
            {
                // Elevación de seguridad sobre la base del modelo para que el usuario no aparezca dentro del cimiento
                puntoSpawnPlantaBaja = new Vector3(b.center.x, b.min.y + 0.85f, b.center.z);
            }
            Debug.LogWarning("[VisorArquitectura] Spawn de seguridad en centro del edificio: " + puntoSpawnPlantaBaja);
        }
    }

    /// <summary>
    /// Valida y reconstruye la topología triangular requerida por PhysX para generar un MeshCollider sin excepciones.
    /// </summary>
    private Mesh GenerarMallaColisionLimpia(Mesh mallaOriginal)
    {
        if (mallaOriginal == null || mallaOriginal.vertexCount < 3 || mallaOriginal.subMeshCount == 0)
            return null;

        bool todosSonTriangulos = true;
        for (int s = 0; s < mallaOriginal.subMeshCount; s++)
        {
            if (mallaOriginal.GetTopology(s) != MeshTopology.Triangles)
            {
                todosSonTriangulos = false;
                break;
            }
        }

        if (todosSonTriangulos)
        {
            return mallaOriginal;
        }

        List<int> indicesTriangulos = new List<int>();
        for (int s = 0; s < mallaOriginal.subMeshCount; s++)
        {
            if (mallaOriginal.GetTopology(s) == MeshTopology.Triangles)
            {
                int[] idx = mallaOriginal.GetIndices(s);
                if (idx != null && idx.Length >= 3)
                {
                    indicesTriangulos.AddRange(idx);
                }
            }
        }

        if (indicesTriangulos.Count < 3)
            return null;

        Mesh mallaLimpia = new Mesh();
        mallaLimpia.name = mallaOriginal.name + "_Colision";
        mallaLimpia.indexFormat = mallaOriginal.vertexCount > 65000 
            ? UnityEngine.Rendering.IndexFormat.UInt32 
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mallaLimpia.vertices = mallaOriginal.vertices;
        mallaLimpia.SetTriangles(indicesTriangulos, 0);
        return mallaLimpia;
    }

    // =========================================================================
    // 2. SEGURIDAD FÍSICA Y CONFIGURACIÓN DEL PERSONAJE
    // =========================================================================

    /// <summary>
    /// Genera un plano de colisión gigante debajo del modelo arquitectónico para evitar caídas al vacío.
    /// </summary>
    private void CrearSueloSeguridadInvisible()
    {
        sueloSeguridadInvisible = new GameObject("Suelo_Seguridad_Invisible");
        BoxCollider col = sueloSeguridadInvisible.AddComponent<BoxCollider>();
        col.size = new Vector3(4000f, 1.0f, 4000f);
        col.center = new Vector3(0, -0.5f, 0);
    }

    /// <summary>
    /// Configura las dimensiones anatómicas del CharacterController para evitar atascos en escalones y umbrales.
    /// </summary>
    private void ConfigurarCapsulaAntiatascos()
    {
        if (xrOrigin == null) return;
        cuerpoJugador = xrOrigin.GetComponent<CharacterController>();
        if (cuerpoJugador == null) cuerpoJugador = xrOrigin.gameObject.AddComponent<CharacterController>();

        // La cápsula cubre desde la rodilla (0.50m) hasta la cabeza (1.70m):
        // Colisiona con muros estructurales sin trabarse en escalones o desniveles.
        cuerpoJugador.radius = 0.22f;
        cuerpoJugador.height = 1.20f;
        cuerpoJugador.center = new Vector3(0, 1.10f, 0);
        cuerpoJugador.stepOffset = 0.35f;
        cuerpoJugador.slopeLimit = 60f;
        cuerpoJugador.skinWidth = 0.03f;
    }

    /// <summary>
    /// Ajusta dinámicamente la altura y centro de la cápsula según el movimiento vertical de la cabeza del usuario en VR.
    /// </summary>
    private void SincronizarCapsulaRodillaACabeza()
    {
        if (cuerpoJugador == null || mainCamera == null || xrOrigin == null || enModoMaqueta || modoDron) return;

        Vector3 posCabezaLocal = xrOrigin.InverseTransformPoint(mainCamera.transform.position);
        float alturaCabeza = Mathf.Clamp(posCabezaLocal.y, 1.25f, 2.20f);
        float baseRodilla = 0.48f;
        float altoCapsula = Mathf.Max(alturaCabeza - baseRodilla, 0.75f);

        cuerpoJugador.height = altoCapsula;
        cuerpoJugador.center = new Vector3(posCabezaLocal.x, baseRodilla + altoCapsula * 0.5f, posCabezaLocal.z);
    }

    /// <summary>
    /// Teletransporta al usuario al siguiente nivel arquitectónico detectado en el modelo.
    /// </summary>
    public void SaltarSiguienteNivel()
    {
        if (nivelesEdificio.Count == 0) return;
        indiceNivelActual = (indiceNivelActual + 1) % nivelesEdificio.Count;
        puntoSpawnPlantaBaja = nivelesEdificio[indiceNivelActual];
        IrAPlantaBaja();
        ActualizarEstado("Nivel " + (indiceNivelActual + 1) + " de " + nivelesEdificio.Count);
    }

    /// <summary>
    /// Reubica al usuario en el punto de acceso principal de la Planta Baja.
    /// </summary>
    public void IrAPlantaBaja()
    {
        if (xrOrigin == null) return;
        if (cuerpoJugador != null) cuerpoJugador.enabled = false;
        xrOrigin.position = puntoSpawnPlantaBaja;
        velocidadVerticalY = 0f;
        if (cuerpoJugador != null) cuerpoJugador.enabled = (!modoDron && !enModoMaqueta);
        Physics.SyncTransforms();
    }
}
