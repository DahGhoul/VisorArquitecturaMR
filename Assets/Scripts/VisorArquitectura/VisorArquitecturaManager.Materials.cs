// ================================================================================================
// PROYECTO: Visor Arquitectura MR (Meta Quest / PC VR)
// ORIGEN: Universidad Nacional de Trujillo (UNT) — Ing. de Sistemas & Arquitectura
// COLABORACIÓN: roo | Soluciones Digitales ("Tecnología con Propósito — El futuro, simple")
// AUTORES: Diego Joel Cuba Moya & Ronaldo Carlos Robles Romero
// NATURALEZA: Desarrollo académico e innovación tecnológica interdisciplinaria (Sin fines de lucro)
// ================================================================================================

using UnityEngine;
using System.Collections.Generic;

public partial class VisorArquitecturaManager
{
    private Shader shaderURPLitNativo;

    /// <summary>
    /// Clasificación de modo de renderizado físico para materiales URP en runtime.
    /// </summary>
    public enum ModoMaterialURP { Opaco, Cutout, Transparente }

    private Material materialBaseCutout;
    private Material materialBaseTransparent;

    // =========================================================================
    // 1. CONFIGURACIÓN VISUAL, PASSTHROUGH Y RESCATE DE SHADER URP NATIVO
    // =========================================================================

    /// <summary>
    /// Rescata las referencias de shaders y materiales URP precompilados desde Resources o Inspector,
    /// garantizando que las variantes de shader no sean descartadas por el stripping en builds para Quest.
    /// </summary>
    private void ExtraerMaterialURPNativo()
    {
        if (materialBaseInspector != null && materialBaseInspector.shader != null && !materialBaseInspector.shader.name.Contains("Error"))
        {
            materialBaseURP = new Material(materialBaseInspector);
            shaderURPLitNativo = materialBaseInspector.shader;
        }

        if (materialFollajeInspector != null && materialFollajeInspector.shader != null && !materialFollajeInspector.shader.name.Contains("Error"))
        {
            materialBaseCutout = new Material(materialFollajeInspector);
        }

        if (materialVidrioInspector != null && materialVidrioInspector.shader != null && !materialVidrioInspector.shader.name.Contains("Error"))
        {
            materialBaseTransparent = new Material(materialVidrioInspector);
        }

        Material matRes = Resources.Load<Material>("DefaultURPLit");
        if (matRes != null && matRes.shader != null && !matRes.shader.name.Contains("Error"))
        {
            if (materialBaseURP == null) materialBaseURP = new Material(matRes);
            if (shaderURPLitNativo == null) shaderURPLitNativo = matRes.shader;
        }

        Material matCutoutRes = Resources.Load<Material>("DefaultURPLit_Cutout");
        if (matCutoutRes != null && matCutoutRes.shader != null)
        {
            if (materialBaseCutout == null) materialBaseCutout = new Material(matCutoutRes);
        }

        Material matTranspRes = Resources.Load<Material>("DefaultURPLit_Transparent");
        if (matTranspRes != null && matTranspRes.shader != null)
        {
            if (materialBaseTransparent == null) materialBaseTransparent = new Material(matTranspRes);
        }

        if (shaderURPLitNativo == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh != null)
            {
                shaderURPLitNativo = sh;
                if (materialBaseURP == null) materialBaseURP = new Material(sh);
            }
        }
    }

    /// <summary>
    /// Establece la iluminación arquitectónica tricolor (cielo, horizonte, rebote vegetal en piso)
    /// y calibra la fuente solar direccional para realismo arquitectónico.
    /// </summary>
    private void ConfigurarIluminacionArquitectonica()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.74f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.60f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.42f, 0.32f);

        Light sol = FindFirstObjectByType<Light>();
        if (sol == null)
        {
            GameObject objSol = new GameObject("Sol_Arquitectonico");
            sol = objSol.AddComponent<Light>();
            sol.type = LightType.Directional;
        }
        sol.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        sol.intensity = 1.18f;
        sol.color = new Color(1.0f, 0.98f, 0.95f);
        sol.shadows = sombrasEnQuest ? LightShadows.Soft : LightShadows.None;
    }

    /// <summary>
    /// Alterna el modo Passthrough de Realidad Mixta (fondo transparente en cámara para visión del entorno real)
    /// o Realidad Virtual inmersiva (Skybox).
    /// </summary>
    public void ConfigurarPassthrough(bool activar)
    {
        passthroughActivo = activar;
        if (mainCamera == null) return;

        if (passthroughActivo && !Application.isEditor)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0, 0, 0, 0);
        }
        else
        {
            mainCamera.clearFlags = CameraClearFlags.Skybox;
        }
    }

    /// <summary>
    /// Sobrecarga para instanciación rápida de material URP Lit.
    /// </summary>
    private Material CrearMaterialURP(Color c, bool transparente)
    {
        return CrearMaterialURP(c, transparente ? ModoMaterialURP.Transparente : ModoMaterialURP.Opaco);
    }

    /// <summary>
    /// Instancia un material URP Lit configurado con las propiedades de blend y render queue correspondientes.
    /// </summary>
    private Material CrearMaterialURP(Color c, ModoMaterialURP modo)
    {
        if (materialBaseURP == null || shaderURPLitNativo == null || shaderURPLitNativo.name.Contains("Error") || shaderURPLitNativo.name == "Standard")
        {
            ExtraerMaterialURPNativo();
        }

        Material baseMat = null;
        if (modo == ModoMaterialURP.Cutout && materialBaseCutout != null) baseMat = materialBaseCutout;
        else if (modo == ModoMaterialURP.Transparente && materialBaseTransparent != null) baseMat = materialBaseTransparent;
        else baseMat = materialBaseURP != null ? materialBaseURP : (materialBaseInspector != null ? materialBaseInspector : null);

        Shader sh = shaderURPLitNativo != null ? shaderURPLitNativo : Shader.Find("Universal Render Pipeline/Lit");
        Material m = (baseMat != null) ? new Material(baseMat) : new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Simple Lit"));

        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);

        if (forzarDobleCara)
        {
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        }

        if (modo == ModoMaterialURP.Transparente)
        {
            ForzarModoTransparente(m);
        }
        else if (modo == ModoMaterialURP.Cutout)
        {
            ForzarModoCutout(m);
        }
        else
        {
            ForzarModoOpaco(m);
        }
        return m;
    }

    /// <summary>
    /// Configura las palabras clave y parámetros de alpha clipping para vegetación y recortes transparentes.
    /// </summary>
    private void ForzarModoCutout(Material m)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
        if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.35f);
        if (m.HasProperty("alphaCutoff")) m.SetFloat("alphaCutoff", 0.35f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.0f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.0f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0.0f);
        if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0.0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.renderQueue = 2450;
    }

    /// <summary>
    /// Configura el material para renderizado opaco estándar sin alpha testing.
    /// </summary>
    private void ForzarModoOpaco(Material m)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetOverrideTag("RenderType", "Opaque");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.renderQueue = 2000;
    }

    /// <summary>
    /// Configura el material para transparencia alfa con escritura de profundidad desactivada (ZWrite 0).
    /// </summary>
    private void ForzarModoTransparente(Material m)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 3000;
    }

    // =========================================================================
    // 2. PROCESADOR UNIVERSAL DE MATERIALES (PBR NATIVO + FÍSICA DE FORMATOS)
    // =========================================================================

    /// <summary>
    /// Comprueba si el formato interno de una textura incluye canal alfa nativo.
    /// </summary>
    private bool TexturaTieneCanalAlfa(Texture tex)
    {
        if (tex is Texture2D t2d)
        {
            string fmt = t2d.format.ToString();
            return fmt.Contains("RGBA") || fmt.Contains("ARGB") || fmt.Contains("ASTC") || 
                   fmt.Contains("Alpha") || fmt.Contains("BC7") || fmt.Contains("DXT5") || fmt.Contains("ETC2_RGBA");
        }
        return false;
    }

    /// <summary>
    /// Pipeline universal de conversión de materiales PBR (Leyes Universales de Materiales Arquitectónicos):
    /// 1. Cristales y Espejos: Transparencia físicamente calibrada con brillo especular elevado.
    /// 2. Vegetación y Enredaderas: Alpha Cutout con preservación de tonos vegetales fotográficos.
    /// 3. Terrenos y Césped: Culling de una cara para evitar colisiones coplanares subterráneas.
    /// 4. Sólidos arquitectónicos: Muros, losas y maderas opacas respetando las especificaciones glTF 2.0.
    /// 5. Láminas finas y cuadros: Offset de render queue para eliminar z-fighting.
    /// </summary>
    private Material PrepararMaterial(Material matOrig, bool esLaminaImagen)
    {
        if (matOrig == null) return CrearMaterialURP(new Color(0.85f, 0.85f, 0.85f), false);

        string nombreMat = matOrig.name.ToLowerInvariant();
        Material m = new Material(matOrig);

        if (m.shader == null || !m.shader.isSupported || m.shader.name.Contains("Error") || m.shader.name.Contains("SpecularGlossiness"))
        {
            m = ConvertirAURPLitConservandoMapas(matOrig);
        }

        Color colBase = Color.white;
        if (m.HasProperty("baseColorFactor")) colBase = m.GetColor("baseColorFactor");
        else if (m.HasProperty("_BaseColor")) colBase = m.GetColor("_BaseColor");
        else if (m.HasProperty("_Color")) colBase = m.GetColor("_Color");
        else if (m.HasProperty("diffuseFactor")) colBase = m.GetColor("diffuseFactor");
        else if (m.HasProperty("_DiffuseFactor")) colBase = m.GetColor("_DiffuseFactor");

        Texture texBase = null;
        if (m.HasProperty("baseColorTexture")) texBase = m.GetTexture("baseColorTexture");
        if (texBase == null && m.HasProperty("_BaseMap")) texBase = m.GetTexture("_BaseMap");
        if (texBase == null && m.HasProperty("_MainTex")) texBase = m.GetTexture("_MainTex");
        if (texBase == null && m.HasProperty("diffuseTexture")) texBase = m.GetTexture("diffuseTexture");
        if (texBase == null && m.HasProperty("_DiffuseMap")) texBase = m.GetTexture("_DiffuseMap");
        if (texBase == null) texBase = m.mainTexture;
        if (texBase == null && matOrig != null)
        {
            if (matOrig.HasProperty("baseColorTexture")) texBase = matOrig.GetTexture("baseColorTexture");
            if (texBase == null && matOrig.HasProperty("_BaseMap")) texBase = matOrig.GetTexture("_BaseMap");
            if (texBase == null && matOrig.HasProperty("_MainTex")) texBase = matOrig.GetTexture("_MainTex");
            if (texBase == null && matOrig.HasProperty("diffuseTexture")) texBase = matOrig.GetTexture("diffuseTexture");
            if (texBase == null && matOrig.HasProperty("_DiffuseMap")) texBase = matOrig.GetTexture("_DiffuseMap");
            if (texBase == null) texBase = matOrig.mainTexture;
        }

        bool tieneTextura = (texBase != null);
        bool tieneAlfaEnTextura = tieneTextura && TexturaTieneCanalAlfa(texBase);

        bool vieneComoCutout = m.IsKeywordEnabled("_ALPHATEST_ON") || (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f) || (m.renderQueue >= 2400 && m.renderQueue < 2550);
        bool vieneComoTransparente = (m.renderQueue >= 2550) || m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || (colBase.a < 0.99f);

        // 1. Detección de Terreno / Césped / Pasto exterior
        bool esColorVerdeVegetal = !tieneTextura && (colBase.g > colBase.r * 1.15f) && (colBase.g > colBase.b * 1.15f) && (colBase.g > 0.25f);
        bool esSuperficieTerreno = esColorVerdeVegetal || nombreMat.Contains("pasto") || nombreMat.Contains("grass") ||
                                   nombreMat.Contains("cesped") || nombreMat.Contains("lawn") || nombreMat.Contains("terreno") ||
                                   nombreMat.Contains("zacate") || nombreMat.Contains("grama") || nombreMat.Contains("turf");

        // 2. Detección de Vegetación / Recorte Alfa (Hojas, Enredaderas, Árboles, Plantas, Rejas)
        bool esCutoutFisico = tieneAlfaEnTextura && (vieneComoCutout || vieneComoTransparente);
        bool esNombreVegetacion = nombreMat.Contains("enredad") || nombreMat.Contains("liana") || nombreMat.Contains("leaf") ||
                                  nombreMat.Contains("hoja") || nombreMat.Contains("plant") || nombreMat.Contains("veget") ||
                                  nombreMat.Contains("arbol") || nombreMat.Contains("tree") || nombreMat.Contains("shrub") ||
                                  nombreMat.Contains("ivy") || nombreMat.Contains("vine") || nombreMat.Contains("foliage") ||
                                  nombreMat.Contains("flora") || nombreMat.Contains("rama") || nombreMat.Contains("branch") ||
                                  nombreMat.StartsWith("c1 ") || nombreMat.StartsWith("d1 ");

        bool esVegetacionOCutout = !esSuperficieTerreno && (vieneComoCutout || esCutoutFisico || esNombreVegetacion);

        // 3. Detección de Cristales, Ventanas, Barandas y Agua
        bool esOpacidadCristalFisico = !tieneAlfaEnTextura && (colBase.a >= 0.02f && colBase.a <= 0.72f);
        bool esNombreCristal = nombreMat.Contains("vidrio") || nombreMat.Contains("glass") || nombreMat.Contains("cristal") ||
                               nombreMat.Contains("ventana") || nombreMat.Contains("window") || nombreMat.Contains("glazing") ||
                               nombreMat.Contains("translucent") || nombreMat.Contains("water") || nombreMat.Contains("agua");

        // ---------------------------------------------------------------------
        // LEY UNIVERSAL 1: CRISTALES, VENTANAS, BARANDAS Y AGUA
        // ---------------------------------------------------------------------
        if (esNombreCristal || esOpacidadCristalFisico)
        {
            Material matVidrio = ConvertirAURPLitConservandoMapas(matOrig, ModoMaterialURP.Transparente);
            ForzarModoTransparente(matVidrio);

            float alfaFinal = tieneTextura ? Mathf.Clamp(colBase.a, 0.35f, 0.65f) : Mathf.Clamp(colBase.a, 0.22f, 0.50f);
            Color colVidrio = new Color(colBase.r, colBase.g, colBase.b, alfaFinal);
            AsignarColorUniversal(matVidrio, colVidrio);

            matVidrio.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            if (matVidrio.HasProperty("_Cull")) matVidrio.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            if (matVidrio.HasProperty("_Smoothness")) matVidrio.SetFloat("_Smoothness", 0.90f);
            return matVidrio;
        }

        // ---------------------------------------------------------------------
        // LEY UNIVERSAL 2: VEGETACIÓN Y RECORTES ALFA (Hojas, Enredaderas, Árboles)
        // ---------------------------------------------------------------------
        if (esVegetacionOCutout)
        {
            Material matCutout = ConvertirAURPLitConservandoMapas(matOrig, ModoMaterialURP.Cutout);
            ForzarModoCutout(matCutout);

            matCutout.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            if (matCutout.HasProperty("_Cull")) matCutout.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            // COLOR NATURAL PURO: Las texturas fotográficas ya contienen su color natural.
            // Se asigna Color.white para evitar tintes indeseados derivados de la exportación CAD.
            if (tieneTextura)
            {
                AsignarColorUniversal(matCutout, Color.white);
            }
            return matCutout;
        }

        // ---------------------------------------------------------------------
        // LEY UNIVERSAL 3: TERRENO EXTERIOR Y CÉSPED
        // ---------------------------------------------------------------------
        if (esSuperficieTerreno)
        {
            ForzarModoOpaco(m);
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);

            if (tieneTextura)
            {
                AsignarColorUniversal(m, Color.white);
            }
            else
            {
                Color verdePasto = (colBase.g > colBase.r && colBase.g > 0.25f)
                    ? new Color(colBase.r, colBase.g, colBase.b, 1.0f)
                    : new Color(0.38f, 0.58f, 0.15f, 1.0f);
                AsignarColorUniversal(m, verdePasto);
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            m.renderQueue = 2000;
            return m;
        }

        // ---------------------------------------------------------------------
        // LEY UNIVERSAL 4: MATERIALES SÓLIDOS (Muros, Maderas, Losas, Techos)
        // ---------------------------------------------------------------------
        if (vieneComoTransparente && colBase.a < 0.99f && colBase.a > 0.72f)
        {
            m = ConvertirAURPLitConservandoMapas(matOrig);
        }

        ForzarModoOpaco(m);

        if (!tieneTextura)
        {
            Color colSolido = new Color(colBase.r, colBase.g, colBase.b, 1.0f);
            AsignarColorUniversal(m, colSolido);
        }
        else
        {
            if (colBase.r < 0.08f && colBase.g < 0.08f && colBase.b < 0.08f)
            {
                AsignarColorUniversal(m, Color.white);
            }
        }

        // DOBLE CARA UNIVERSAL EN SÓLIDOS ARQUITECTÓNICOS:
        // Garantiza que techos, losas y muros de SketchUp sean siempre visibles desde adentro/abajo y desde arriba/afuera.
        m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", (float)UnityEngine.Rendering.CullMode.Off);

        // ---------------------------------------------------------------------
        // LEY UNIVERSAL 5: LÁMINAS DELGADAS Y MURALES
        // ---------------------------------------------------------------------
        if (esLaminaImagen && tieneTextura)
        {
            m.renderQueue = 2005;
        }

        return m;
    }

    /// <summary>
    /// Asigna de manera segura el color base en todas las propiedades cromáticas comunes de URP.
    /// </summary>
    private void AsignarColorUniversal(Material m, Color c)
    {
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    /// <summary>
    /// Convierte un material arbitrario importado a una instancia limpia del shader URP Lit,
    /// conservando texturas albedo, mapas de normales, propiedades metálicas y rugosidad.
    /// </summary>
    private Material ConvertirAURPLitConservandoMapas(Material matOrig, ModoMaterialURP modo = ModoMaterialURP.Opaco)
    {
        Material nuevo = CrearMaterialURP(Color.white, modo);
        if (matOrig == null) return nuevo;

        nuevo.name = matOrig.name;

        Color c = Color.white;
        if (matOrig.HasProperty("baseColorFactor")) c = matOrig.GetColor("baseColorFactor");
        else if (matOrig.HasProperty("_BaseColor")) c = matOrig.GetColor("_BaseColor");
        else if (matOrig.HasProperty("_Color")) c = matOrig.GetColor("_Color");
        else if (matOrig.HasProperty("diffuseFactor")) c = matOrig.GetColor("diffuseFactor");
        else if (matOrig.HasProperty("_DiffuseFactor")) c = matOrig.GetColor("_DiffuseFactor");

        // Textura principal y transformación UV (Escala y Desplazamiento)
        Texture tex = null;
        if (matOrig.HasProperty("baseColorTexture")) tex = matOrig.GetTexture("baseColorTexture");
        if (tex == null && matOrig.HasProperty("_BaseMap")) tex = matOrig.GetTexture("_BaseMap");
        if (tex == null && matOrig.HasProperty("_MainTex")) tex = matOrig.GetTexture("_MainTex");
        if (tex == null && matOrig.HasProperty("diffuseTexture")) tex = matOrig.GetTexture("diffuseTexture");
        if (tex == null && matOrig.HasProperty("_DiffuseMap")) tex = matOrig.GetTexture("_DiffuseMap");
        if (tex == null) tex = matOrig.mainTexture;

        if (tex != null && c.r < 0.08f && c.g < 0.08f && c.b < 0.08f)
        {
            c = Color.white;
        }
        AsignarColorUniversal(nuevo, c);

        if (tex != null)
        {
            Vector2 escala = Vector2.one;
            Vector2 offset = Vector2.zero;
            if (matOrig.HasProperty("baseColorTexture_ST"))
            {
                Vector4 st = matOrig.GetVector("baseColorTexture_ST");
                escala = new Vector2(st.x, st.y);
                offset = new Vector2(st.z, st.w);
            }
            else if (matOrig.HasProperty("_BaseMap_ST"))
            {
                Vector4 st = matOrig.GetVector("_BaseMap_ST");
                escala = new Vector2(st.x, st.y);
                offset = new Vector2(st.z, st.w);
            }
            else if (matOrig.HasProperty("diffuseTexture_ST"))
            {
                Vector4 st = matOrig.GetVector("diffuseTexture_ST");
                escala = new Vector2(st.x, st.y);
                offset = new Vector2(st.z, st.w);
            }

            nuevo.mainTexture = tex;
            nuevo.mainTextureScale = escala;
            nuevo.mainTextureOffset = offset;
            if (nuevo.HasProperty("_BaseMap"))
            {
                nuevo.SetTexture("_BaseMap", tex);
                nuevo.SetTextureScale("_BaseMap", escala);
                nuevo.SetTextureOffset("_BaseMap", offset);
            }
            if (nuevo.HasProperty("_MainTex"))
            {
                nuevo.SetTexture("_MainTex", tex);
                nuevo.SetTextureScale("_MainTex", escala);
                nuevo.SetTextureOffset("_MainTex", offset);
            }
        }

        // Mapa de Normales (Relieve PBR)
        Texture normalTex = null;
        if (matOrig.HasProperty("normalTexture")) normalTex = matOrig.GetTexture("normalTexture");
        else if (matOrig.HasProperty("_BumpMap")) normalTex = matOrig.GetTexture("_BumpMap");
        if (normalTex != null && nuevo.HasProperty("_BumpMap"))
        {
            nuevo.SetTexture("_BumpMap", normalTex);
            nuevo.EnableKeyword("_NORMALMAP");
        }

        if (matOrig.HasProperty("roughnessFactor"))
        {
            nuevo.SetFloat("_Smoothness", Mathf.Clamp01(1.0f - matOrig.GetFloat("roughnessFactor")));
        }
        else if (matOrig.HasProperty("_Smoothness"))
        {
            nuevo.SetFloat("_Smoothness", matOrig.GetFloat("_Smoothness"));
        }
        else
        {
            nuevo.SetFloat("_Smoothness", 0.15f);
        }

        if (matOrig.HasProperty("metallicFactor"))
        {
            nuevo.SetFloat("_Metallic", matOrig.GetFloat("metallicFactor"));
        }
        else if (matOrig.HasProperty("_Metallic"))
        {
            nuevo.SetFloat("_Metallic", matOrig.GetFloat("_Metallic"));
        }

        return nuevo;
    }
}