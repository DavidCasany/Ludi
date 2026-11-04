#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Plantilla WebGL propia, preparada para móvil y tableta (ver WebPageParts):
//     Tools > Ludi > Web: instal·lar plantilla per a mòbil
// Crea Assets/WebGLTemplates/Hockeymaniacs/index.html y la deja elegida en
// Player Settings > Web > Resolution and Presentation > WebGL Template.
// La plantilla por defecto de Unity deja el canvas con un tamaño fijo, permite hacer zoom/scroll con
// el dedo y no avisa de girar el móvil: con ella el juego no se puede jugar bien en el móvil.
//
// En cada build WebGL con esta plantilla elegida se vuelve a escribir el index.html (por si se ha
// actualizado este script) y, si hay otra plantilla elegida, se avisa en la consola.
public class MobileWebTemplate : IPreprocessBuildWithReport
{
    const string TemplateName = "Hockeymaniacs";
    const string TemplateFolder = "Assets/WebGLTemplates/" + TemplateName;
    const string TemplateSetting = "PROJECT:" + TemplateName;

    public int callbackOrder => 0;

    [MenuItem("Tools/Ludi/Web: instal·lar plantilla per a mòbil")]
    static void InstallFromMenu()
    {
        Write();
        PlayerSettings.WebGL.template = TemplateSetting;
        AssetDatabase.SaveAssets();
        Debug.Log($"[Web mòbil] Plantilla '{TemplateName}' creada en {TemplateFolder} y elegida en Player Settings.");
        EditorUtility.DisplayDialog("Web mòbil",
            "Plantilla '" + TemplateName + "' instal·lada i triada a Player Settings > Web.\n\n" +
            "Fes una build WebGL nova per provar-la al mòbil (el mòbil i l'ordinador a la mateixa xarxa, " +
            "o puja-la al servidor).", "OK");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;

        if (PlayerSettings.WebGL.template == TemplateSetting)
        {
            Write();
            return;
        }
        Debug.LogWarning("[Web mòbil] La build WebGL no usa la plantilla '" + TemplateName + "' (usa '" +
                         PlayerSettings.WebGL.template + "'). En móvil se podrá hacer zoom/scroll y no se adapta " +
                         "a la pantalla. Tools > Ludi > Web: instal·lar plantilla per a mòbil.");
    }

    static void Write()
    {
        Directory.CreateDirectory(TemplateFolder);
        string path = Path.Combine(TemplateFolder, "index.html");
        string html = Html();
        if (File.Exists(path) && File.ReadAllText(path) == html) return;
        File.WriteAllText(path, html, new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(path);
    }

    // Variables de plantilla de Unity: {{{ NOMBRE }}} y las líneas #if/#endif las sustituye al hacer la build
    static string Html() => WebPageParts.Head("{{{ PRODUCT_NAME }}}") + WebPageParts.HelpersScript + @"<script src=""Build/{{{ LOADER_FILENAME }}}""></script>
<script>
(function () {
  var canvas = document.getElementById('unity-canvas');
  var config = {
    dataUrl: ""Build/{{{ DATA_FILENAME }}}"",
    frameworkUrl: ""Build/{{{ FRAMEWORK_FILENAME }}}"",
#if USE_THREADS
    workerUrl: ""Build/{{{ WORKER_FILENAME }}}"",
#endif
#if USE_WASM
    codeUrl: ""Build/{{{ CODE_FILENAME }}}"",
#endif
#if MEMORY_FILENAME
    memoryUrl: ""Build/{{{ MEMORY_FILENAME }}}"",
#endif
#if SYMBOLS_FILENAME
    symbolsUrl: ""Build/{{{ SYMBOLS_FILENAME }}}"",
#endif
    streamingAssetsUrl: ""StreamingAssets"",
    companyName: {{{ JSON.stringify(COMPANY_NAME) }}},
    productName: {{{ JSON.stringify(PRODUCT_NAME) }}},
    productVersion: {{{ JSON.stringify(PRODUCT_VERSION) }}},
    devicePixelRatio: ludiRatio(),
    showBanner: ludiBanner
  };
  createUnityInstance(canvas, config, ludiProgress)
    .then(function (instance) { ludiReady(instance, canvas); })
    .catch(ludiError);
})();
</script>
</body>
</html>
";
}
#endif
