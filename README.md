<div align="center">

<img src="Docs/Images/roo_logo.png" alt="roo | Soluciones Digitales" width="460" />

# Visor Arquitectura MR
### Realidad Mixta & Virtual de Alta Fidelidad para Arquitectura e Ingeniería (AEC)

[![Unity 6](https://img.shields.io/badge/Unity-6000.3.26f1-blue.svg?logo=unity&logoColor=white)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Pipeline-URP_17.3-red.svg?logo=unity)](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest)
[![Platform](https://img.shields.io/badge/Platform-Meta_Quest_2_|_3_|_3S_|_Pro-purple.svg?logo=oculus&logoColor=white)](https://www.meta.com/quest/)
[![glTF 2.0](https://img.shields.io/badge/Format-glTF%202.0%20%28.glb%29-orange?logo=khronosgroup&logoColor=white)](https://www.khronos.org/gltf/)
[![Ecosystem](https://img.shields.io/badge/Ecosistema-roo_AEC_Spatial_Suite-purple.svg)](#ecosistema-arquitectura-mr--xr)

<br/>

<img src="Docs/Images/roo_badge.png" alt="Tecnología con Propósito — El futuro, simple" width="230" />

<p><em>"Tecnología con Propósito — El futuro, simple"</em></p>

---

### **Desarrollado por:**
**Diego Joel Cuba Moya** & **Ronaldo Carlos Robles Romero**  
**Empresa:** [roo | Soluciones Digitales](https://github.com/roo-soluciones)

</div>

---

## 🏛️ Ecosistema Arquitectura MR / XR

Este proyecto conforma el cliente inmersivo de la suite espacial de **roo | Soluciones Digitales**, diseñada para trabajar de forma sincronizada y sin cables junto a su contraparte en PC:

```
┌────────────────────────────────────────┐       Wi-Fi Local (5 GHz)        ┌────────────────────────────────────────┐
│       VISOR ARQUITECTURA MR (VR)       │  ◄════════════════════════════►  │     PORTAL ARQUITECTURA XR (PC)        │
│                                        │   UDP Radar Broadcast (47777)    │                                        │
│ • Cliente inmersivo Meta Quest (Unity) │   HTTP Range Streaming (8000)    │ • Servidor HTTP multihilo (RFC 7233)   │
│ • Pipeline Universal URP PBR 90 FPS    │                                  │ • Sincronización y emparejamiento PIN  │
│ • Modos: Peatón 1:1, Dron, Maqueta AR  │                                  │ • Optimizador de mallas (gltfpack)     │
│ • Interfaz curva World-Space VisionOS  │                                  │ • Panel Web y Desktop (pywebview)      │
└────────────────────────────────────────┘                                  └────────────────────────────────────────┘
```

1. **[Visor Arquitectura MR](https://github.com/roo-soluciones/VisorArquitecturaMR) (Este repositorio / Meta Quest):**  
   Aplicación cliente desarrollada en Unity 6 (URP) para Meta Quest y PC VR. Detecta en tiempo real el servidor mediante radar UDP, descarga modelos `.glb`, ejecuta el pipeline PBR y ofrece tres modos ergonómicos de recorrido (Peatón 1:1, Vuelo Dron y Maqueta AR en Passthrough).
2. **[Portal Arquitectura XR](https://github.com/roo-soluciones/portal-arquitectura-xr) (Python / PC):**  
   Estación de control y servidor de modelos para la PC del arquitecto. Gestiona la carga de archivos, optimiza mallas con `gltfpack`, emite la señal de radar UDP (puerto `47777`) y sirve los modelos vía HTTP multihilo con RFC 7233 Range Requests.

---

## 🌟 Características Principales

### 1. Pipeline Universal de Materiales URP PBR
Supera las limitaciones clásicas del pipeline móvil de Unity y la exportación CAD/BIM mediante 5 Leyes Físicas Universales en tiempo de ejecución:
- **Vegetación & Follaje Fotorrealista**: Desacoplamiento de swatches de SketchUp, preservando los tonos y saturación natural fotográfica en mallas Cutout (`_ALPHATEST_ON`) sin cuadros blancos ni bordes residuales en Quest.
- **Vidrios y Cristales Arquitectónicos**: Shader URP transparente precompilado anti-stripping con atenuación fotométrica y brillo especular suave.
- **Superficies y Terrenos**: Culling selectivo que erradica solapamientos coplanares (*z-fighting*) entre losas, asfalto y césped exterior.
- **Sólidos Arquitectónicos**: Conformidad estricta con la especificación Khronos glTF 2.0 (respeto a propiedades `doubleSided`, albedo, mapas de normales y rugosidad).
- **Láminas Finas y Cuadros**: Calibración de colas de renderizado (*Render Queue 2005*) para evitar parpadeos visuales contra muros.

### 2. Navegación Ergonómica Multimodal
Permite alternar instantáneamente entre tres perspectivas espaciales:
- 🚶 **Modo Peatón 1:1**: Simulación de caminata a escala humana con física anatómica (`CharacterController` calibrado de rodilla a cabeza) y sensor esférico inferior que escala peldaños y desniveles de hasta 40 cm sin atascos.
- 🛸 **Modo Vuelo Dron 3D**: Vuelo libre tridimensional suave y desacoplado de la gravedad, con orientación referenciada al campo de visión del usuario.
- 🪄 **Modo Maqueta AR (Passthrough)**: Activación del passthrough a color en Meta Quest, reduciendo el edificio a escala de mesa con manipulación 6DoF (rotación orbital, agarre directo y escalado homotético con mandos o manos).

### 3. Sincronización Inalámbrica Autónoma (Radar UDP + Portal Web)
- **Zero Configuration**: El visor integra un socket en segundo plano (`Port 47777`) que detecta automáticamente cualquier PC que ejecute el servidor de proyectos en la misma red Wi-Fi.
- **Vinculación por PIN de 4 Dígitos**: Teclado numérico modal para entornos corporativos con aislamiento de broadcast o múltiples subredes.
- **Caché Persistente**: Los proyectos descargados quedan almacenados en la memoria del dispositivo para demostraciones *offline* sin conexión.

### 4. Interfaz Espacial VisionOS (Dark Graphite)
- Diseñada en World-Space a 1.32 metros con esquinas redondeadas procedurales (9-Sliced Sprites).
- Puntero nativo de alta precisión con debouncing de gatillo y micro-animaciones *Hover* para retroalimentación visual háptica.

---

## 📐 Guía de Buenas Prácticas de Exportación a .GLB (Para Modeladores y Arquitectos)

> [!IMPORTANT]
> **El 90% de los errores visuales en VR (suelos invisibles, texturas desplazadas, parpadeos coplanares y cuadros blancos en hojas) se originan por vicios de modelado en SketchUp o Revit.** Sigue estrictamente estas reglas antes de exportar a `.glb`:

### 1. Cuadros y Láminas: Importar como "Textura" y NUNCA como "Imagen Suelta"
- **El error común:** En SketchUp es frecuente usar `Archivo > Importar > como Imagen` y pegar una foto, plano o cuadro flotando sobre un muro. Al exportar a glTF/GLB, SketchUp genera una entidad desvinculada que no comparte coordenadas UV con la pared, provocando que la imagen se desfase, flote en el aire o parpadee.
- **La solución correcta:** 
  1. Crea un **Material nuevo**, cárgale la imagen como textura de mapa y **píntala directamente sobre la cara de la pared**.
  2. Si necesitas un cuadro físico: modela un marco con volumen real ($1\text{ a }2\text{ cm}$ de espesor) o, si usas una lámina plana, dale clic derecho > **Explotar** y sepárala al menos **$2\text{ mm}$ hacia afuera del muro** para erradicar el parpadeo coplanar (*Z-Fighting*).

### 2. Muros Estructurales: Geometría Sólida 3D vs Planos 2D
- **El error común:** Usar una imagen plana o un plano 2D de grosor $0$ para simular un tabique o división de ambientes.
- **La solución correcta:** Los sistemas de física en Realidad Virtual (`CharacterController` y `PhysX`) necesitan volumen espacial para frenar al jugador de forma sólida. **Todo muro debe modelarse con grosor real mediante Empujar/Tirar (Push/Pull)** de al menos $10\text{ a }15\text{ cm}$.

### 3. Suelos Invisibles y Caídas al Vacío: Caras Invertidas y Losas con Espesor
- **El error común:** Dibujar una losa o vereda como una simple lámina plana de 1 cara donde el lado azul/gris (reverso) queda hacia arriba. En motores de videojuegos y VR, por optimización de rendimiento se aplica *Backface Culling* (descarte de caras traseras): **el reverso es 100% invisible y verás un agujero hacia el vacío**.
- **La solución correcta:**
  1. **Espesor real:** Toda losa, piso o pavimento debe tener volumen tridimensional con *Empujar/Tirar* (mínimo $15\text{ a }20\text{ cm}$ hacia abajo).
  2. **Orientación de normales:** En SketchUp, las caras visibles desde donde camina el usuario deben ser **blancas**. Si ves alguna cara en tono azulado o grisáceo, selecciónala, haz clic derecho y pulsa **Invertir Caras** (*Reverse Faces*).

### 4. Follaje, Lianas y Vegetación: Fondos Transparentes Reales y Sin Tintes
- **El error común:**
  - Descargar texturas de plantas en formato JPG o con fondos de "cuadritos grises falsos" simulando transparencia: esto genera los temidos **cuadros blancos** o bordes sólidos alrededor de las hojas.
  - Aplicar un tinte o color de muestra sobre la textura en el panel de materiales de SketchUp: esto "destiñe" o apaga el verde fotográfico natural dejándolo grisáceo o plomizo en VR.
- **La solución correcta:**
  1. Usar únicamente imágenes **PNG con canal alfa transparente genuino**.
  2. Conservar el material en su color base blanco puro (`#FFFFFF`) para que el visor respete los colores fotográficos originales de las hojas.
  3. Nombrar el material incluyendo alguna de estas palabras clave: `enredad`, `hoja`, `leaf`, `plant`, `arbol`, `tree` o `liana`. El visor activará automáticamente el sombreador **Alpha Cutout optimizado** sin bordes residuales.

### 5. Vidrios y Ventanas
- **El error común:** Pintar vidrio sobre la misma malla del muro o modelar ventanas sin grupo independiente.
- **La solución correcta:**
  1. Agrupar la carpintería y el cristal en grupos separados.
  2. Asignar al material del cristal una opacidad menor al $70\%$ y nombrarlo con `vidrio`, `glass`, `cristal` o `ventana`. El visor le asignará el shader URP transparente con brillo fotométrico.

### 6. Césped y Terrenos Exteriores
- Nombrar el material con `pasto`, `grass`, `cesped` o `terreno`. El visor aplicará culling de una cara superior para evitar que losas de asfalto o cimientos inferiores se traslapen y parpadeen.

### 7. Limpieza y Purga del Archivo (Regla de Oro antes de Exportar)
1. **Purgar no usados:** En SketchUp ve a `Ventana > Información del modelo > Estadísticas > Purgar no usados`. Esto elimina gigabytes de materiales huérfanos, bloques invisibles y geometrías residuales.
2. **Modelar cerca del Origen (0,0,0):** Asegúrate de que el proyecto esté ubicado cerca del eje de coordenadas inicial. Modelar a cientos de metros del origen causa temblores de cámara (*jittering*) en visores VR por pérdida de precisión numérica.
3. **Formato:** Exportar siempre como **`.glb` binario (glTF 2.0)** con unidades en **metros**. Presupuesto sugerido: menor a $2\text{M}$ de polígonos para Meta Quest autónomo (o usar la optimización con `gltfpack` desde Portal Arquitectura XR).

---

## 🏗️ Arquitectura de Software

El núcleo del sistema está estructurado mediante clases parciales modulares para alta mantenibilidad y bajo acoplamiento:

```
Assets/
├── VisorArquitecturaManager.cs              # Orquestador del ciclo de vida, DTOs de red e Inspector
└── Scripts/
    └── VisorArquitectura/
        ├── VisorArquitecturaManager.Geometry.cs    # Análisis espacial AABB, PhysX y detección de pisos
        ├── VisorArquitecturaManager.Locomotion.cs  # Entrada 6DoF, física peatonal y vuelo de dron
        ├── VisorArquitecturaManager.Materials.cs   # Pipeline universal PBR y rescate de shaders URP
        ├── VisorArquitecturaManager.Network.cs     # Socket UDP broadcast y descarga HTTP en chunks
        └── VisorArquitecturaManager.UI.cs          # Canvas espacial World-Space estilo VisionOS
```

---

## 🛠️ Requisitos del Entorno

### Para Visor Arquitectura MR (Visor VR):
- **Motor de Juego:** Unity 6 (`6000.0` o superior) / Unity 6000.3.26f1
- **Pipeline de Render:** Universal Render Pipeline (URP 17.0+)
- **XR Runtime:** OpenXR (`1.16.1+`) con paquete `Meta OpenXR` habilitado
- **glTF Loader:** Unity Cloud glTFast (`6.20.0+`)
- **Plataforma Target:** Android (Meta Quest 2, Quest 3, Quest 3S, Quest Pro) / Windows Standalone (PC VR)
- **Red:** Conexión Wi-Fi local 5 GHz o 6 GHz (mismo router que la PC del portal)

### Para Portal Arquitectura XR (Estación PC):
- **Sistema Operativo:** Windows 10/11 o macOS
- **Servidor:** Python 3.10+ (o ejecutable `.exe` / `.app`)
- **Puertos:** `8000` TCP (HTTP) y `47777` UDP (Broadcast Radar)

---

## 🚀 Guía de Puesta en Marcha

### 1. Clonar el Repositorio
```bash
git clone https://github.com/roo-soluciones/VisorArquitecturaMR.git
```

> [!NOTE]
> El repositorio incluye `.gitignore` y `.gitattributes` oficiales para Unity. Carpetas como `Library/`, `Temp/`, compilaciones intermedias y paquetes temporales se regeneran automáticamente al abrir el proyecto en Unity Hub.

### 2. Abrir en Unity Hub
1. Abre **Unity Hub** y haz clic en **Add > Add project from disk**.
2. Selecciona la carpeta `VisorArquitecturaMR`.
3. Asegúrate de seleccionar el editor **Unity 6 (6000.3.x)** con soporte para **Android Build Support (OpenJDK, Android SDK & NDK)**.
4. Abre la escena principal ubicada en `Assets/_Recovery/0 (1).unity` (o añade la escena a *Scenes in Build* como índice 0).

### 3. Despliegue en Meta Quest (APK)
1. Conecta el visor Meta Quest mediante cable USB-C con el **Modo Desarrollador** activado.
2. En Unity, dirígete a **File > Build Profiles** (o **Build Settings**).
3. Selecciona la plataforma **Android**.
4. En **Run Device**, selecciona tu visor Meta Quest.
5. Haz clic en **Build and Run**.

---

## 🎮 Controles de Entrada (Meta Quest Touch)

| Botón / Control | Modo Peatón 1:1 | Modo Vuelo Dron | Modo Maqueta AR (Passthrough) |
| :--- | :--- | :--- | :--- |
| **Stick Izquierdo** | Caminar (adelante, atrás, lados) | Vuelo horizontal 3D | Trasladar maqueta sobre el plano |
| **Stick Derecho (X)** | Giro suave continuo | Giro suave continuo | Rotar maqueta sobre su centro |
| **Stick Derecho (Y)** | — | Subir / Bajar altura | Escalar maqueta (agrandar/reducir) |
| **Gatillo / Grip** | Apuntar e interactuar con UI | Interactuar con UI | Agarrar y mover maqueta en el espacio |
| **Botón Y (Izq)** | Mostrar / Ocultar Menú | Mostrar / Ocultar Menú | Mostrar / Ocultar Menú |
| **Botón X (Izq)** | Entrar a Modo Maqueta AR | Entrar a Modo Maqueta AR | Volver a Escala 1:1 |
| **Botón A (Der)** | Alternar a Modo Dron | Volver a Modo Peatón | — |
| **Botón B (Der)** | Saltar al siguiente piso | Saltar al siguiente piso | — |

---

## 🔒 Buenas Prácticas para Git + Unity

Este repositorio sigue los estándares de la industria para control de versiones en videojuegos y aplicaciones XR:
- **Archivos Rastreados**: Únicamente el código fuente (`Assets/`), configuración de paquetes (`Packages/manifest.json`), ajustes de motor (`ProjectSettings/`) y documentación (`README.md`, `Docs/`).
- **Archivos `.meta`**: Cada archivo y carpeta en `Assets/` cuenta con su respectivo `.meta` versionado para preservar GUIDs de dependencias e inspectores.
- **Archivos Ignorados**: Caché de importación (`Library/`), ejecutables temporales (`Temp/`), proyectos de Visual Studio (`*.sln`, `*.csproj`), y binarios compilados (`*.apk`).

---

## 📄 Licencia y Créditos

Este software ha sido diseñado y desarrollado como propiedad intelectual de:

**roo | Soluciones Digitales**  
*Lema:* **"Tecnología con Propósito — El futuro, simple"**

**Desarrolladores Principales:**
- **Diego Joel Cuba Moya**
- **Ronaldo Carlos Robles Romero**

*Copyright © 2026 roo Soluciones Digitales. Todos los derechos reservados.*
