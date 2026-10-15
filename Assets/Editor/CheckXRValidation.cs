using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.XR.CoreUtils.Editor;

public static class CheckXRValidation
{
    [MenuItem("Tools/XR/1. Validar XR en Consola (Android y PC)")]
    public static void RunValidationInConsole()
    {
        Debug.Log("<b><color=#00bcd4>[XR Validation]</color> Iniciando escaneo sin UI Toolkit...</b>");

        ValidatePlatform(BuildTargetGroup.Android, "Android (Meta Quest)");
        ValidatePlatform(BuildTargetGroup.Standalone, "Standalone (PC VR)");
    }

    [MenuItem("Tools/XR/2. Auto-Corregir Problemas XR (Fix All)")]
    public static void FixAllIssuesInConsole()
    {
        FixPlatformIssues(BuildTargetGroup.Android, "Android (Meta Quest)");
        FixPlatformIssues(BuildTargetGroup.Standalone, "Standalone (PC VR)");
    }

    public static void RunValidationBatch()
    {
        Debug.Log("=== INICIO VALIDACION XR BATCHMODE ===");
        ValidatePlatform(BuildTargetGroup.Android, "Android (Meta Quest)");
        ValidatePlatform(BuildTargetGroup.Standalone, "Standalone (PC VR)");
        Debug.Log("=== FIN VALIDACION XR BATCHMODE ===");
    }

    private static void ValidatePlatform(BuildTargetGroup targetGroup, string platformName)
    {
        var failures = GetFailures(targetGroup);
        if (failures == null) return;

        int totalRules = GetPlatformRuleCount(targetGroup);

        if (failures.Count == 0)
        {
            Debug.Log($"<color=green>✓ [XR Validation - {platformName}]</color> Correcto: 0 fallos detectados (de {totalRules} reglas evaluadas).");
            return;
        }

        int errors = 0;
        int warnings = 0;

        Debug.LogWarning($"<b>[XR Validation - {platformName}] Se encontraron {failures.Count} observacion(es) de {totalRules} reglas:</b>");

        foreach (var rule in failures)
        {
            string category = string.IsNullOrEmpty(rule.Category) ? "General" : rule.Category;
            string fixInfo = !string.IsNullOrEmpty(rule.FixItMessage) ? $" (Fix: {rule.FixItMessage})" : "";

            if (rule.Error)
            {
                errors++;
                Debug.LogError($"[XR Error][{category}] {rule.Message}{fixInfo}");
            }
            else
            {
                warnings++;
                Debug.LogWarning($"[XR Advertencia][{category}] {rule.Message}{fixInfo}");
            }
        }

        Debug.Log($"<b>[{platformName}] Resumen:</b> <color=red>{errors} errores</color> (bloquean el build), <color=orange>{warnings} advertencias</color>.");
    }

    private static void FixPlatformIssues(BuildTargetGroup targetGroup, string platformName)
    {
        var failures = GetFailures(targetGroup);
        if (failures == null || failures.Count == 0)
        {
            Debug.Log($"<color=green>[XR Fix - {platformName}]</color> No hay problemas pendientes para corregir.");
            return;
        }

        int fixedCount = 0;
        foreach (var rule in failures)
        {
            if (rule.FixIt != null && rule.FixItAutomatic)
            {
                try
                {
                    rule.FixIt.Invoke();
                    fixedCount++;
                    Debug.Log($"<color=#4caf50>[XR Corregido]</color> {rule.Message}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[XR Fallo al corregir] {rule.Message}: {ex.Message}");
                }
            }
        }

        Debug.Log($"<b>[XR Fix - {platformName}]</b> Se corrigieron {fixedCount} problema(s) automaticamente.");
        AssetDatabase.SaveAssets();
    }

    private static HashSet<BuildValidationRule> GetFailures(BuildTargetGroup targetGroup)
    {
        var failures = new HashSet<BuildValidationRule>();

        var method = typeof(BuildValidator).GetMethod(
            "GetCurrentValidationIssues",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
        );

        if (method == null)
        {
            Debug.LogError("[XR Validation] No se pudo encontrar el metodo interno GetCurrentValidationIssues en BuildValidator.");
            return null;
        }

        method.Invoke(null, new object[] { failures, targetGroup });
        return failures;
    }

    private static int GetPlatformRuleCount(BuildTargetGroup targetGroup)
    {
        var prop = typeof(BuildValidator).GetProperty("PlatformRules", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (prop != null)
        {
            var dict = prop.GetValue(null) as IDictionary;
            if (dict != null && dict.Contains(targetGroup))
            {
                var list = dict[targetGroup] as ICollection;
                if (list != null) return list.Count;
            }
        }
        return 0;
    }
}
