#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Tools > Ludi > Borrar ajustes guardados
// Vuelve a los valores por defecto de SettingsData (pantalla completa, flechas de pase...) sin borrar
// el resto de PlayerPrefs. Útil antes de probar o de hacer una build.
public static class SettingsPrefsTools
{
    [MenuItem("Tools/Ludi/Borrar ajustes guardados")]
    static void ClearSettings()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Ajustes", "Sal del modo Play antes de borrar los ajustes.", "OK");
            return;
        }
        GameSettings.DeleteSaved();
        Debug.Log("[Ajustes] Ajustes guardados borrados: la próxima vez se usarán los valores por defecto de SettingsData.");
    }
}
#endif
