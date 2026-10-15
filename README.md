<div align="center">

<img src="Docs/Images/roo_logo.png" alt="roo | Soluciones Digitales" width="460" />

# Visor Arquitectura MR
### Realidad Mixta & Virtual de Alta Fidelidad para Arquitectura e Ingeniería (AEC)

[![Unity 6](https://img.shields.io/badge/Unity-6000.3.26f1-blue.svg?logo=unity&logoColor=white)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Pipeline-URP_17.3-red.svg?logo=unity)](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest)
[![Platform](https://img.shields.io/badge/Platform-Meta_Quest_2_|_3_|_3S_|_Pro-purple.svg?logo=oculus&logoColor=white)](https://www.meta.com/quest/)
[![Spec](https://img.shields.io/badge/glTF-2.0_PBR_Compliant-green.svg)](https://www.khronos.org/gltf/)
[![License](https://img.shields.io/badge/License-Proprietary-darkgray.svg)](#licencia-y-créditos)

<br/>

<img src="Docs/Images/roo_badge.png" alt="Tecnología con Propósito — El futuro, simple" width="230" />

<p><em>"Tecnología con Propósito — El futuro, simple"</em></p>

---

### **Desarrollado por:**
**Diego Joel Cuba Moya** & **Ronaldo Carlos Robles Romero**  
**Empresa:** [roo | Soluciones Digitales](https://github.com/roo-soluciones)

</div>

---

## 🏛️ Descripción General

**Visor Arquitectura MR** es una solución enterprise para visores **Meta Quest (2, 3, 3S, Pro)** y **PC VR**, diseñada para transformar maquetas arquitectónicas y modelos BIM (SketchUp, Revit, Blender, Archicad, Rhino) en experiencias inmersivas 1:1 fluidas a **90 FPS estables**, sin cables y sin tiempos de horneado o preparación manual de escenas.

El sistema incorpora un pipeline universal de materiales PBR, sincronización inalámbrica autónoma mediante difusión UDP en red local, física anatómica adaptativa para escaleras y umbrales, y una interfaz espacial curva inspirada en VisionOS.

---

## 🌟 Características Principales

### 1. Pipeline Universal de Materiales URP PBR
Supera las limitaciones clásicas del pipeline móvil de Unity y la exportación CAD/BIM mediante 5 Leyes Físicas Universales en tiempo de ejecución:
- **Vegetación & Follaje Fotorrealista**: Desacoplamiento de swatches de SketchUp, preservando los tonos y saturación natural fotográfica en mallas Cutout (`_ALPHATEST_ON`) sin cuadros blancos ni bordes residuales en Quest.
- **Vidrios y Cristales Arquitectónicos**: Shader URP transparente precompilado anti-stripping con atenuación fotométrica y brillo especular suave.
- **Superficies y Terrenos**: Culling selectivo que erradica solapamientos coplanares (*z-fighting*) entre losas, asfalto y césped exterior.
- **Sólidos Arquitectónicos**: Conformidad estricta con la especificación Khronos glTF 2.0 (respeto a propiedades `doubleSided`, albedo, mapas de normales y rugosidad).

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

## 📐 Arquitectura de Software

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

| Componente | Requisito Mínimo Recomendado |
| :--- | :--- |
| **Motor de Juego** | Unity 6 (`6000.0` o superior) / Unity 6000.3.26f1 |
| **Pipeline de Render** | Universal Render Pipeline (URP 17.0+) |
| **XR Runtime** | OpenXR (`1.16.1+`) con paquete `Meta OpenXR` habilitado |
| **glTF Loader** | Unity Cloud glTFast (`6.20.0+`) |
| **Plataforma Target** | Android (Meta Quest 2, Quest 3, Quest 3S, Quest Pro) / Windows Standalone (PC VR) |

---

## 🚀 Guía de Puesta en Marcha

### 1. Clonar el Repositorio
```bash
git clone https://github.com/roo-soluciones/VisorArquitecturaMR.git
```

> [!NOTE]
> El repositorio incluye un archivo `.gitignore` y `.gitattributes` optimizados para Unity. Carpetas pesadas como `Library/`, `Temp/`, compilaciones intermedias y paquetes temporales se regeneran automáticamente al abrir el proyecto en Unity Hub.

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
