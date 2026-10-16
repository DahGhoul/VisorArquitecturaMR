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
    // 1. INICIALIZACIÓN Y CALIBRACIÓN DE DISPOSITIVOS XR
    // =========================================================================

    /// <summary>
    /// Establece el modo de rastreo del suelo (Floor Origin) en el subsistema XR de Meta Quest.
    /// </summary>
    private void ConfigurarOrigenPisoQuest()
    {
        List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var sub in subsystems)
        {
            sub.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        }
    }

    /// <summary>
    /// Auto-detecta la jerarquía de controladores y cámara, desactivando componentes de locomoción
    /// o teletransporte heredados (incluyendo ControllerInputActionManager y Teleport Interactor)
    /// para evitar la aparición de la línea roja de teletransporte al avanzar en Modo Dron o Peatón.
    /// </summary>
    private void ConfigurarEscenaYDesactivarLocomocionVieja()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();
        }

        if (mainCamera != null)
        {
            mainCamera.nearClipPlane = 0.04f;
            mainCamera.farClipPlane = 2000f;
        }

        if (xrOrigin == null)
        {
            GameObject objOrigin = GameObject.Find("XR Origin (XR Rig)");
            if (objOrigin == null) objOrigin = GameObject.Find("XR Origin");
            if (objOrigin != null) xrOrigin = objOrigin.transform;
            else if (mainCamera != null && mainCamera.transform.root != null) xrOrigin = mainCamera.transform.root;
        }

        // 1. Desactivar componentes MonoBehaviour de interacción y locomoción conflictivos PRIMERO
        // (Especialmente ControllerInputActionManager, que reactivaba el Teleport Interactor rojo al mover el stick)
        MonoBehaviour[] todosLosScripts = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour mb in todosLosScripts)
        {
            if (mb == null || mb == this) continue;
            string n = mb.GetType().Name.ToLowerInvariant();
            if (n.Contains("turnprovider") || n.Contains("moveprovider") || n.Contains("teleportation") ||
                n.Contains("gravityprovider") || n.Contains("charactercontrollerdriver") ||
                n.Contains("snapturn") || n.Contains("locomotionmediator") || n.Contains("locomotioncontroller") ||
                n.Contains("controllerinputactionmanager"))
            {
                mb.enabled = false;
            }
        }

        // 2. Desactivar GameObjects de locomoción obsoleta y neutralizar permanentemente cualquier línea de Teleport
        GameObject[] todosLosObjetos = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in todosLosObjetos)
        {
            if (go == null || go == this.gameObject) continue;
            string nom = go.name.ToLowerInvariant();
            if (nom == "locomotion" || nom == "turn" || nom == "snap turn" || nom == "move" ||
                nom.Contains("teleport") || nom.Contains("snapturn") || nom.Contains("continuousmove") ||
                nom.Contains("tunneling") || nom.Contains("climb") || nom == "gravity" || nom == "jump")
            {
                foreach (LineRenderer lr in go.GetComponentsInChildren<LineRenderer>(true))
                {
                    lr.enabled = false;
                }
                foreach (MonoBehaviour comp in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (comp != null && comp != this) comp.enabled = false;
                }
                go.SetActive(false);
            }
        }

        if (xrOrigin != null)
        {
            foreach (Transform t in xrOrigin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Camera Offset")) cameraOffset = t;
                if (t.name == "Right Controller") manoDerechaTransform = t;
                if (t.name == "Left Controller") manoIzquierdaTransform = t;
                if (t.name == "Near-Far Interactor" || t.name == "Ray Interactor")
                {
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                    if (manoDerechaTransform != null && t.IsChildOf(manoDerechaTransform))
                        punteroNativoDerecho = t;
                }
            }
        }
    }

    // =========================================================================
    // 2. SISTEMA DE LOCOMOCIÓN ESPACIAL (Peatón, Dron, Giro Suave)
    // =========================================================================

    /// <summary>
    /// Procesa la entrada continua de mandos VR (o teclado/mouse en Editor), gestionando:
    /// - Giro continuo suave centrado en la cabeza del usuario.
    /// - Cinemática de vuelo libre 3D en Modo Dron.
    /// - Física de apoyo y sensor esférico de pasos/escaleras en Modo Peatón 1:1.
    /// </summary>
    private void ProcesarLocomocionConSensorEsferico()
    {
        if (xrOrigin == null || mainCamera == null) return;

        UnityEngine.XR.InputDevice der = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        UnityEngine.XR.InputDevice izq = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        // Botón Y (Mano Izq) o TAB: Mostrar/Ocultar Menú Flotante
        bool toggleMenu = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
        if (izq.isValid && izq.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool bY))
        {
            if (bY && !btnYPrevio) toggleMenu = true;
            btnYPrevio = bY;
        }
        if (toggleMenu) AlternarMenu();

        // Botón X (Mano Izq) o M: Alternar Modo Maqueta AR
        bool toggleMaqueta = Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
        if (izq.isValid && izq.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool bX))
        {
            if (bX && !btnXPrevio) toggleMaqueta = true;
            btnXPrevio = bX;
        }
        if (toggleMaqueta) ActivarModoMaqueta();

        // Botón A (Mano Der) o V: Alternar entre Peatón 1:1 y Vuelo Dron
        bool toggleDron = Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
        if (der.isValid && der.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool bA))
        {
            if (bA && !btnAPrevio) toggleDron = true;
            btnAPrevio = bA;
        }
        if (toggleDron)
        {
            if (modoDron) ActivarModoPeaton1a1();
            else ActivarModoDron();
        }

        // Botón B (Mano Der) o N: Ciclar al Siguiente Nivel / Piso
        bool toggleNivel = Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame;
        if (der.isValid && der.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool bB))
        {
            if (bB && !btnBPrevio) toggleNivel = true;
            btnBPrevio = bB;
        }
        if (toggleNivel) SaltarSiguienteNivel();

        Vector2 stickIzq = Vector2.zero;
        Vector2 stickDer = Vector2.zero;
        if (izq.isValid) izq.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out stickIzq);
        if (der.isValid) der.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out stickDer);

        // Controles de cámara en Unity Editor (Clic derecho + Mouse)
        if (Application.isEditor && Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yawPC += delta.x * 0.18f;
            pitchPC = Mathf.Clamp(pitchPC - delta.y * 0.18f, -80f, 80f);
            xrOrigin.localRotation = Quaternion.Euler(0f, yawPC, 0f);
            mainCamera.transform.localRotation = Quaternion.Euler(pitchPC, 0f, 0f);
        }

        if (enModoMaqueta)
        {
            if (cuerpoJugador != null && cuerpoJugador.enabled) cuerpoJugador.enabled = false;
            velocidadVerticalY = 0f;
            return;
        }

        // Giro Suave Continuo pivotado sobre la posición de la cabeza
        if (Mathf.Abs(stickDer.x) > 0.2f)
        {
            xrOrigin.RotateAround(mainCamera.transform.position, Vector3.up, stickDer.x * velocidadGiroSuave * Time.deltaTime);
        }

        Vector3 frente = mainCamera.transform.forward;
        frente.y = 0;
        if (frente.sqrMagnitude < 0.001f) frente = Vector3.forward;
        frente.Normalize();

        Vector3 derecha = mainCamera.transform.right;
        derecha.y = 0;
        if (derecha.sqrMagnitude < 0.001f) derecha = Vector3.right;
        derecha.Normalize();

        Vector3 inputDir = frente * stickIzq.y + derecha * stickIzq.x;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) inputDir += frente;
            if (Keyboard.current.sKey.isPressed) inputDir -= frente;
            if (Keyboard.current.aKey.isPressed) inputDir -= derecha;
            if (Keyboard.current.dKey.isPressed) inputDir += derecha;
        }
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        // --- MODO DRON FLUIDO (VUELO LIBRE 3D) ---
        if (modoDron)
        {
            if (cuerpoJugador != null && cuerpoJugador.enabled) cuerpoJugador.enabled = false;

            // Desplazamiento 3D referenciado a la orientación de la cabeza
            Vector3 frente3D = mainCamera.transform.forward;
            Vector3 derecha3D = mainCamera.transform.right;
            Vector3 deseoDron = (frente3D * stickIzq.y + derecha3D * stickIzq.x) * velocidadDron;

            if (Mathf.Abs(stickDer.y) > 0.15f)
            {
                deseoDron.y += stickDer.y * (velocidadDron * 0.85f);
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) deseoDron += frente3D * velocidadDron;
                if (Keyboard.current.sKey.isPressed) deseoDron -= frente3D * velocidadDron;
                if (Keyboard.current.aKey.isPressed) deseoDron -= derecha3D * velocidadDron;
                if (Keyboard.current.dKey.isPressed) deseoDron += derecha3D * velocidadDron;
                if (Keyboard.current.eKey.isPressed || Keyboard.current.spaceKey.isPressed) deseoDron.y += velocidadDron * 0.85f;
                if (Keyboard.current.qKey.isPressed || Keyboard.current.cKey.isPressed) deseoDron.y -= velocidadDron * 0.85f;
            }

            velocidadSuavizada = Vector3.Lerp(velocidadSuavizada, deseoDron, 10f * Time.deltaTime);
            xrOrigin.position += velocidadSuavizada * Time.deltaTime;
            Physics.SyncTransforms();
            return;
        }

        // --- MODO PEATÓN 1:1 CON SENSOR ESFÉRICO Y APOYO CONTINUO ---
        if (!modeloCargado) return;
        if (cuerpoJugador != null && !cuerpoJugador.enabled) cuerpoJugador.enabled = true;

        velocidadSuavizada = Vector3.Lerp(velocidadSuavizada, inputDir * velocidadCaminar, 12f * Time.deltaTime);

        // Movimiento horizontal amortiguado por muros estructurales
        if (cuerpoJugador != null && cuerpoJugador.enabled)
        {
            cuerpoJugador.Move(new Vector3(velocidadSuavizada.x, 0f, velocidadSuavizada.z) * Time.deltaTime);
        }

        // Sensor esférico de apoyo vertical (detecta escalones y losas sin atascos)
        Vector3 posCabeza = mainCamera.transform.position;
        Vector3 posPies = new Vector3(posCabeza.x, xrOrigin.position.y, posCabeza.z);
        Vector3 origenSonda = posPies + new Vector3(0, 0.90f, 0);

        bool halloPiso = false;
        float yPisoObjetivo = xrOrigin.position.y;

        // 1. Sonda adelantada: anticipa el siguiente peldaño de escaleras
        if (inputDir.sqrMagnitude > 0.04f)
        {
            Vector3 origenAdelante = origenSonda + inputDir.normalized * 0.28f;
            if (Physics.SphereCast(origenAdelante, 0.15f, Vector3.down, out RaycastHit hitAdelante, 2.5f))
            {
                if (Mathf.Abs(hitAdelante.normal.y) > 0.55f && hitAdelante.collider != colliderAgarreMaqueta)
                {
                    float dif = hitAdelante.point.y - xrOrigin.position.y;
                    if (dif >= -0.40f && dif <= 0.45f)
                    {
                        yPisoObjetivo = hitAdelante.point.y;
                        halloPiso = true;
                    }
                }
            }
        }

        // 2. Sonda directa centrada bajo los pies
        if (Physics.SphereCast(origenSonda, 0.15f, Vector3.down, out RaycastHit hitCentro, 3.5f))
        {
            if (Mathf.Abs(hitCentro.normal.y) > 0.55f && hitCentro.collider != colliderAgarreMaqueta)
            {
                float dif = hitCentro.point.y - xrOrigin.position.y;
                if (!halloPiso || dif > -0.35f)
                {
                    yPisoObjetivo = hitCentro.point.y;
                    halloPiso = true;
                }
            }
        }

        if (halloPiso)
        {
            float distanciaAlSuelo = xrOrigin.position.y - yPisoObjetivo;

            if (distanciaAlSuelo <= 0.35f && distanciaAlSuelo >= -0.45f)
            {
                velocidadVerticalY = 0f;
                float nuevaY = Mathf.MoveTowards(xrOrigin.position.y, yPisoObjetivo + 0.01f, 4.5f * Time.deltaTime);
                xrOrigin.position = new Vector3(xrOrigin.position.x, nuevaY, xrOrigin.position.z);
            }
            else if (distanciaAlSuelo > 0.35f)
            {
                velocidadVerticalY = Mathf.Max(velocidadVerticalY - 9.81f * Time.deltaTime, -12.0f);
                float caida = velocidadVerticalY * Time.deltaTime;
                if (xrOrigin.position.y + caida <= yPisoObjetivo + 0.01f)
                {
                    xrOrigin.position = new Vector3(xrOrigin.position.x, yPisoObjetivo + 0.01f, xrOrigin.position.z);
                    velocidadVerticalY = 0f;
                }
                else
                {
                    xrOrigin.position += new Vector3(0, caida, 0);
                }
            }
        }
        else
        {
            velocidadVerticalY = Mathf.Max(velocidadVerticalY - 9.81f * Time.deltaTime, -12.0f);
            xrOrigin.position += new Vector3(0, velocidadVerticalY * Time.deltaTime, 0);
        }

        // Límite de seguridad contra caídas accidentales al vacío
        float yMinSeguro = (sueloSeguridadInvisible != null) ? sueloSeguridadInvisible.transform.position.y + 0.05f : -0.1f;
        if (xrOrigin.position.y < yMinSeguro)
        {
            xrOrigin.position = new Vector3(xrOrigin.position.x, yMinSeguro + 0.02f, xrOrigin.position.z);
            velocidadVerticalY = 0f;
        }
    }

    /// <summary>
    /// Activa el modo de inmersión peatonal a escala real (1:1) con física de colisiones activada.
    /// </summary>
    public void ActivarModoPeaton1a1()
    {
        if (enModoMaqueta) SalirDeModoMaquetaInterno();
        modoDron = false;
        ConfigurarPassthrough(false);
        velocidadSuavizada = Vector3.zero;
        if (cuerpoJugador != null) cuerpoJugador.enabled = true;
        ActualizarEstiloBotonesDock();
        ActualizarEstado("Modo Peatón 1:1 activo.");
    }

    /// <summary>
    /// Activa el modo de navegación aérea libre estilo dron sin gravedad ni colisión de piso.
    /// </summary>
    public void ActivarModoDron()
    {
        if (enModoMaqueta) SalirDeModoMaquetaInterno();
        modoDron = true;
        ConfigurarPassthrough(false);
        velocidadSuavizada = Vector3.zero;
        if (cuerpoJugador != null) cuerpoJugador.enabled = false;
        ActualizarEstiloBotonesDock();
        ActualizarEstado("Modo Vuelo Dron activo (Stick Izq vuela en 3D hacia donde miras).");
    }

    /// <summary>
    /// Transforma el modelo en una maqueta interactiva flotante en Realidad Mixta (Passthrough).
    /// </summary>
    public void ActivarModoMaqueta()
    {
        if (contenedorModelo == null || mainCamera == null) return;

        if (enModoMaqueta)
        {
            ActivarModoPeaton1a1();
            return;
        }

        enModoMaqueta = true;
        modoDron = false;
        if (cuerpoJugador != null) cuerpoJugador.enabled = false;
        velocidadVerticalY = 0f;

        if (colliderAgarreMaqueta != null) colliderAgarreMaqueta.enabled = true;
        foreach (Collider c in contenedorModelo.GetComponentsInChildren<Collider>(true))
        {
            if (c != colliderAgarreMaqueta) c.enabled = false;
        }

        Vector3 dirFrente = mainCamera.transform.forward;
        dirFrente.y = 0;
        if (dirFrente.sqrMagnitude < 0.01f) dirFrente = Vector3.forward;
        dirFrente.Normalize();

        Vector3 centroObjetivo = mainCamera.transform.position + dirFrente * 0.85f;
        centroObjetivo.y = mainCamera.transform.position.y - 0.25f;

        contenedorModelo.transform.localScale = Vector3.one * escalaMaquetaActual;
        contenedorModelo.transform.rotation = Quaternion.identity;
        contenedorModelo.transform.position = centroObjetivo - (centroLocalModelo * escalaMaquetaActual);

        ConfigurarPassthrough(true);
        ActualizarEstiloBotonesDock();
        ActualizarEstado("Maqueta AR: Stick Der gira/escala | Gatillo mueve.");
    }

    /// <summary>
    /// Restaura la escala original 1:1 y reposiciona al usuario en la Planta Baja.
    /// </summary>
    private void SalirDeModoMaquetaInterno()
    {
        enModoMaqueta = false;
        if (contenedorModelo != null)
        {
            if (colliderAgarreMaqueta != null) colliderAgarreMaqueta.enabled = false;
            foreach (Collider c in contenedorModelo.GetComponentsInChildren<Collider>(true))
            {
                if (c != colliderAgarreMaqueta) c.enabled = true;
            }
            contenedorModelo.transform.localScale = Vector3.one;
            contenedorModelo.transform.position = posCentradaOriginal;
            contenedorModelo.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
        }
        ConfigurarPassthrough(false);
        IrAPlantaBaja();
    }

    // =========================================================================
    // 3. INTERACCIÓN POR RAYCASTING Y MANIPULACIÓN HÁPTICA
    // =========================================================================

    /// <summary>
    /// Obtiene el rayo director nativo de interacción a partir del controlador XR derecho o del cursor en Editor.
    /// </summary>
    private Ray ObtenerRayoNativo()
    {
        if (punteroNativoDerecho != null)
        {
            return new Ray(punteroNativoDerecho.position, punteroNativoDerecho.forward);
        }
        if (manoDerechaTransform != null)
        {
            Quaternion rotAim = manoDerechaTransform.rotation * Quaternion.Euler(35f, 0f, 0f);
            return new Ray(manoDerechaTransform.position, rotAim * Vector3.forward);
        }
        if (mainCamera != null && Mouse.current != null && Application.isEditor)
        {
            return mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        }
        return mainCamera != null ? new Ray(mainCamera.transform.position, mainCamera.transform.forward) : new Ray(Vector3.zero, Vector3.forward);
    }

    /// <summary>
    /// Restaura el color original del botón sobre el que se encontraba el cursor al salir del estado Hover.
    /// </summary>
    private void RestaurarHover()
    {
        if (ultimoBotonHover != null)
        {
            ultimoBotonHover.color = colorBaseHover;
            ultimoBotonHover = null;
        }
    }

    /// <summary>
    /// Evalúa la colisión de rayos de interacción con elementos de la interfaz espacial
    /// y gestiona el arrastre 3D, rotación y escalado de la maqueta en Modo AR.
    /// </summary>
    private void ProcesarInteraccionYPuntero()
    {
        Ray rayo = ObtenerRayoNativo();
        UnityEngine.XR.InputDevice der = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        UnityEngine.XR.InputDevice izq = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool gatillo = false;
        bool grip = false;
        Vector2 stickDer = Vector2.zero;
        Vector2 stickIzq = Vector2.zero;

        if (der.isValid)
        {
            der.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out gatillo);
            der.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out grip);
            der.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out stickDer);
        }
        if (izq.isValid)
        {
            izq.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out stickIzq);
        }
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.isPressed) gatillo = true;
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) stickDer.y = Mathf.Sign(scroll);
        }

        bool agarrando = enModoMaqueta ? (gatillo || grip) : gatillo;
        bool clicDown = agarrando && !gatilloPrevio;
        gatilloPrevio = agarrando;

        // --- MANIPULACIÓN DE MAQUETA EN MODO REALIDAD MIXTA ---
        if (enModoMaqueta && contenedorModelo != null)
        {
            Vector3 centroMaqueta = contenedorModelo.transform.TransformPoint(centroLocalModelo);

            // 1. Rotación orbital continua alrededor del centro del modelo
            if (Mathf.Abs(stickDer.x) > 0.15f)
            {
                contenedorModelo.transform.RotateAround(centroMaqueta, Vector3.up, -stickDer.x * 90f * Time.deltaTime);
            }

            // 2. Escalado proporcional homotético respecto a su centroide
            if (Mathf.Abs(stickDer.y) > 0.15f)
            {
                centroMaqueta = contenedorModelo.transform.TransformPoint(centroLocalModelo);
                float nuevaEscala = Mathf.Clamp(escalaMaquetaActual * (1f + stickDer.y * 0.7f * Time.deltaTime), 0.004f, 0.30f);
                float factor = nuevaEscala / escalaMaquetaActual;
                escalaMaquetaActual = nuevaEscala;
                contenedorModelo.transform.localScale = Vector3.one * escalaMaquetaActual;
                contenedorModelo.transform.position = centroMaqueta + (contenedorModelo.transform.position - centroMaqueta) * factor;
            }

            // 3. Traslación planar suave con stick izquierdo
            if (Mathf.Abs(stickIzq.y) > 0.15f || Mathf.Abs(stickIzq.x) > 0.15f)
            {
                Vector3 fwdCam = mainCamera.transform.forward;
                fwdCam.y = 0f;
                if (fwdCam.sqrMagnitude < 0.01f) fwdCam = Vector3.forward;
                fwdCam.Normalize();
                Vector3 rightCam = mainCamera.transform.right;
                rightCam.y = 0f;
                rightCam.Normalize();

                Vector3 deltaMover = (fwdCam * stickIzq.y + rightCam * stickIzq.x) * (1.2f * Time.deltaTime);
                contenedorModelo.transform.position += deltaMover;
            }

            // 4. Agarre háptico directo mediante Gatillo o Grip
            if (arrastrandoMaqueta)
            {
                if (!agarrando)
                {
                    arrastrandoMaqueta = false;
                }
                else
                {
                    Vector3 destino = rayo.origin + rayo.direction * distanciaAgarreMaqueta;
                    Vector3 centroActual = contenedorModelo.transform.TransformPoint(centroLocalModelo);
                    Vector3 offsetCentro = contenedorModelo.transform.position - centroActual;
                    contenedorModelo.transform.position = Vector3.Lerp(contenedorModelo.transform.position, destino + offsetCentro, 15f * Time.deltaTime);
                    return;
                }
            }
        }

        RestaurarHover();

        if (Physics.Raycast(rayo, out RaycastHit hit, 45f))
        {
            if (hit.collider.gameObject.name.StartsWith("BTN_"))
            {
                Image img = hit.collider.GetComponent<Image>();
                if (img != null && img.color != colBotonActivoBlanco)
                {
                    ultimoBotonHover = img;
                    colorBaseHover = img.color;
                    img.color = colBotonHover;
                }
                if (clicDown)
                {
                    RestaurarHover();
                    EjecutarAccionBoton(hit.collider.gameObject.name);
                }
                return;
            }

            if (enModoMaqueta && contenedorModelo != null && (hit.collider == colliderAgarreMaqueta || hit.collider.transform.IsChildOf(contenedorModelo.transform)))
            {
                if (clicDown)
                {
                    arrastrandoMaqueta = true;
                    distanciaAgarreMaqueta = Mathf.Clamp(hit.distance, 0.35f, 3.5f);
                }
            }
        }
    }
}
