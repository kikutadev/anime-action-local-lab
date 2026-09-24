using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AnimeActionWebBuild
{
    [MenuItem("Anime Action/Build WebGL")]
    public static void BuildWebGl()
    {
        AnimeActionAcceptance.ValidateMotions();
        ModelQualityAcceptance.ValidateAssets();
        ModelQualityShowcaseSetup.Generate();
        ModelQualityAcceptance.ValidateScene();

        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string output = Path.GetFullPath(Path.Combine(projectRoot, "..", "publish"));
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }
        Directory.CreateDirectory(output);

        PlayerSettings.productName = "Anime Character Action Test";
        PlayerSettings.companyName = "kikutadev";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "dev.kikuta.animecharacteractiontest");
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.memorySize = 768;
        PlayerSettings.runInBackground = true;

        QualitySettings.antiAliasing = 4;
        QualitySettings.shadowDistance = 20f;

        BuildPlayerOptions options = new()
        {
            scenes = new[] { ModelQualityShowcaseSetup.ScenePath },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"WebGL build failed: {report.summary.result}, errors={report.summary.totalErrors}");
        }

        File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
        WriteResponsiveIndex(output);
        Debug.Log($"WebGL build succeeded: {output} ({report.summary.totalSize} bytes)");
    }

    private static void WriteResponsiveIndex(string output)
    {
        const string html = @"<!doctype html>
<html lang=""ja"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width,initial-scale=1,viewport-fit=cover,user-scalable=no"">
  <meta name=""theme-color"" content=""#e0e6ee"">
  <title>Anime Character Action Test</title>
  <style>
    * { box-sizing: border-box; }
    html, body { width: 100%; height: 100%; margin: 0; overflow: hidden; background: #e0e6ee; }
    body { position: fixed; inset: 0; overscroll-behavior: none; font-family: -apple-system,BlinkMacSystemFont,""Segoe UI"",sans-serif; }
    #unity-container { position: fixed; inset: 0; width: 100vw; height: 100vh; height: 100dvh; }
    #unity-canvas { display: block; width: 100%; height: 100%; background: #e0e6ee; touch-action: none; }
    #loading { position: fixed; inset: 0; display: grid; place-items: center; pointer-events: none; color: #202735; background: #e0e6ee; transition: opacity .18s ease; }
    #loading.hidden { opacity: 0; }
    #loading-card { width: min(280px,72vw); text-align: center; }
    #loading-title { font-size: 15px; font-weight: 700; margin-bottom: 12px; }
    #loading-track { height: 5px; border-radius: 999px; overflow: hidden; background: rgba(24,33,48,.14); }
    #loading-fill { width: 0; height: 100%; background: #526b98; transition: width .08s linear; }
    #warning { position: fixed; top: max(12px,env(safe-area-inset-top)); left: 50%; transform: translateX(-50%); z-index: 10; max-width: 92vw; }
  </style>
</head>
<body>
  <div id=""unity-container""><canvas id=""unity-canvas"" tabindex=""-1""></canvas></div>
  <div id=""loading""><div id=""loading-card""><div id=""loading-title"">3Dモデルを読み込み中</div><div id=""loading-track""><div id=""loading-fill""></div></div></div></div>
  <div id=""warning""></div>
  <script>
    const canvas = document.getElementById('unity-canvas');
    canvas.addEventListener('contextmenu', event => event.preventDefault());
    const loading = document.getElementById('loading');
    const fill = document.getElementById('loading-fill');
    const warning = document.getElementById('warning');
    const config = {
      dataUrl: 'Build/publish.data',
      frameworkUrl: 'Build/publish.framework.js',
      codeUrl: 'Build/publish.wasm',
      streamingAssetsUrl: 'StreamingAssets',
      companyName: 'kikutadev',
      productName: 'Anime Character Action Test',
      productVersion: '1.0',
      devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2),
      showBanner: (msg, type) => {
        const item = document.createElement('div');
        item.textContent = msg;
        item.style.cssText = 'margin:6px;padding:8px 12px;border-radius:10px;background:' + (type === 'error' ? '#a93636' : '#e6c45c') + ';color:#fff;font-size:12px;';
        warning.appendChild(item);
        if (type !== 'error') setTimeout(() => item.remove(), 5000);
      }
    };
    const script = document.createElement('script');
    script.src = 'Build/publish.loader.js';
    script.onload = () => createUnityInstance(canvas, config, p => {
      fill.style.width = (p * 100).toFixed(1) + '%';
    }).then(instance => {
      window.unityInstance = instance;
      loading.classList.add('hidden');
      setTimeout(() => loading.remove(), 220);
    }).catch(err => {
      document.getElementById('loading-title').textContent = '読み込みに失敗しました';
      console.error(err);
    });
    document.body.appendChild(script);
  </script>
</body>
</html>";
        File.WriteAllText(Path.Combine(output, "index.html"), html);
    }

}
