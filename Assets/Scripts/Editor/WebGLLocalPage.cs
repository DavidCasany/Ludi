#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

// Genera una versión de la build WebGL que funciona ABRIENDO index.html con doble clic (file://),
// sin servidor ni conexión: los navegadores (sobre todo Chrome) bloquean que una página local cargue
// los .data / .wasm de la carpeta Build, así que aquí se meten DENTRO del propio index.html (base64)
// y se le pasan al cargador de Unity como blob: URLs, que sí están permitidas.
//
// Se ejecuta solo al terminar cada build WebGL y crea, al lado de la carpeta de la build:
//     <build>_Local/index.html      ← un único archivo, el que se entrega para jugar en local
// La build normal (index.html + Build/ + TemplateData/) se queda igual: es la que va a la web.
// También: Tools > Ludi > Generar index.html local (WebGL), para una build ya hecha.
//
// Requisito: Player Settings > Web > Publishing Settings > Compression Format = Disabled.
public static class WebGLLocalPage
{
    const string Title = "Hockeymaniacs";

    [PostProcessBuild(100)]
    static void OnPostprocessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.WebGL) return;
        Generate(buildPath, false);
    }

    [MenuItem("Tools/Ludi/Generar index.html local (WebGL)")]
    static void FromMenu()
    {
        string path = EditorUtility.OpenFolderPanel("Carpeta de la build WebGL (la que tiene index.html y Build/)", "", "");
        if (!string.IsNullOrEmpty(path)) Generate(path, true);
    }

    static void Generate(string buildPath, bool showDialog)
    {
        string buildDir = Path.Combine(buildPath, "Build");
        if (!Directory.Exists(buildDir)) { Fail("No hay carpeta Build en " + buildPath, showDialog); return; }

        string[] files = Directory.GetFiles(buildDir);
        string compressed = files.FirstOrDefault(f => f.EndsWith(".gz") || f.EndsWith(".br") || f.EndsWith(".unityweb"));
        if (compressed != null)
        {
            Fail("La build está comprimida (" + Path.GetFileName(compressed) + "). Pon Player Settings > Web > " +
                 "Publishing Settings > Compression Format = Disabled y vuelve a hacer la build.", showDialog);
            return;
        }

        string loader = files.FirstOrDefault(f => f.EndsWith(".loader.js"));
        string framework = files.FirstOrDefault(f => f.EndsWith(".framework.js"));
        string data = files.FirstOrDefault(f => f.EndsWith(".data"));
        string wasm = files.FirstOrDefault(f => f.EndsWith(".wasm"));
        if (loader == null || framework == null || data == null || wasm == null)
        {
            Fail("Faltan archivos en " + buildDir + " (loader.js, framework.js, .data o .wasm).", showDialog);
            return;
        }

        string outDir = buildPath.TrimEnd('/', '\\') + "_Local";
        Directory.CreateDirectory(outDir);
        string outFile = Path.Combine(outDir, "index.html");

        using (StreamWriter w = new StreamWriter(outFile, false, new System.Text.UTF8Encoding(false)))
        {
            w.Write(Head());
            // Los datos primero (no se ejecutan: type no es JavaScript); el script del final los lee
            WriteBlob(w, "b-framework", framework);
            WriteBlob(w, "b-data", data);
            WriteBlob(w, "b-code", wasm);
            w.Write("<script>\n");
            w.Write(File.ReadAllText(loader).Replace("</script", "<\\/script"));
            w.Write("\n</script>\n");
            w.Write(Boot());
        }

        long mb = new FileInfo(outFile).Length / (1024 * 1024);
        string msg = $"Creado {outFile} ({mb} MB). Ábrelo con doble clic para probarlo sin servidor.";
        Debug.Log("[WebGL local] " + msg);
        if (showDialog) EditorUtility.DisplayDialog("WebGL local", msg, "OK");
    }

    static void WriteBlob(StreamWriter w, string id, string path)
    {
        w.Write($"<script type=\"application/octet-stream\" id=\"{id}\">");
        w.Write(Convert.ToBase64String(File.ReadAllBytes(path)));
        w.Write("</script>\n");
    }

    static void Fail(string msg, bool showDialog)
    {
        Debug.LogError("[WebGL local] " + msg);
        if (showDialog) EditorUtility.DisplayDialog("WebGL local", msg, "OK");
    }

    // ---------- Página ----------

    static string Head() => @"<!DOCTYPE html>
<html lang=""ca"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>" + Title + @"</title>
<style>
  html, body { margin: 0; height: 100%; background: #12151c; overflow: hidden; font-family: Arial, sans-serif; }
  /* 16:9 centrado, ocupando todo lo posible de la ventana */
  #unity-canvas { position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%);
                  width: min(100vw, 177.78vh); height: min(56.25vw, 100vh); background: #12151c; outline: none; }
  #loading { position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center;
             justify-content: center; color: #e8ecf3; gap: 18px; }
  #loading h1 { margin: 0; font-size: 48px; letter-spacing: 6px; }
  #loading h1 span { color: #f2782b; }
  #bar { width: 320px; height: 10px; border-radius: 5px; background: #2a3242; overflow: hidden; }
  #fill { width: 0; height: 100%; background: #f2782b; transition: width .15s; }
  #fs { position: absolute; right: 12px; bottom: 12px; padding: 8px 14px; border: 0; border-radius: 8px;
        background: #2a3242; color: #e8ecf3; cursor: pointer; font-size: 14px; display: none; }
  #fs:hover { background: #f2782b; }
</style>
</head>
<body>
<canvas id=""unity-canvas"" tabindex=""-1""></canvas>
<div id=""loading""><h1>HOCKEY<span>MANIACS</span></h1><div id=""bar""><div id=""fill""></div></div><p id=""msg"">Carregant…</p></div>
<button id=""fs"">Pantalla completa</button>
";

    static string Boot() => @"<script>
(function () {
  function blobUrl(id, type) {
    var el = document.getElementById(id);
    var bin = atob(el.textContent);
    el.textContent = '';                       // libera la memoria del texto
    var bytes = new Uint8Array(bin.length);
    for (var i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    return URL.createObjectURL(new Blob([bytes], { type: type }));
  }

  var canvas = document.getElementById('unity-canvas');
  var fill = document.getElementById('fill');
  var msg = document.getElementById('msg');

  // Deja pintar la pantalla de carga antes de decodificar los datos (tarda un momento)
  setTimeout(function () {
    var config = {
      frameworkUrl: blobUrl('b-framework', 'application/javascript'),
      dataUrl: blobUrl('b-data', 'application/octet-stream'),
      codeUrl: blobUrl('b-code', 'application/wasm'),
      streamingAssetsUrl: 'StreamingAssets',
      companyName: 'Hockeymaniacs',
      productName: '" + Title + @"',
      productVersion: '1.0',
      showBanner: function (text, type) { (type === 'error' ? console.error : console.warn)(text); }
    };
    createUnityInstance(canvas, config, function (p) { fill.style.width = (100 * p) + '%'; })
      .then(function (instance) {
        document.getElementById('loading').remove();
        var fs = document.getElementById('fs');
        fs.style.display = 'block';
        fs.onclick = function () { instance.SetFullscreen(1); };
        canvas.focus();
      })
      .catch(function (e) { msg.textContent = 'No s\'ha pogut carregar el joc: ' + e; });
  }, 50);
})();
</script>
</body>
</html>
";
}
#endif
