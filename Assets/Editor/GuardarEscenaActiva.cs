using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

[InitializeOnLoad]
public static class GuardarEscenaActiva
{
    [MenuItem("ROO/Guardar Escena 0 (1)")]
    public static void SincronizarEscenaActiva()
    {
        if (EditorApplication.isPlaying) return;

        Scene escena = SceneManager.GetActiveScene();
        if (!escena.IsValid()) return;

        Directory.CreateDirectory("Assets/_Recovery");

        string rutaRecovery = "Assets/_Recovery/0 (1).unity";

        bool guardado = EditorSceneManager.SaveScene(escena, rutaRecovery, false);
        Debug.Log("[ROO] Resultado de guardar escena " + escena.name + " en " + rutaRecovery + ": " + guardado);

        EditorBuildSettingsScene[] escenasBuild = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(rutaRecovery, true)
        };
        EditorBuildSettings.scenes = escenasBuild;
        AssetDatabase.Refresh();
    }
}
