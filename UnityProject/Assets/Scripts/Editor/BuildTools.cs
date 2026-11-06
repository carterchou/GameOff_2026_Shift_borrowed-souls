using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BorrowedSouls.EditorTools
{
    /// <summary>The WebGL build. Gzip with the fallback, because itch.io doesn't serve the headers a browser needs to unpack it by itself.</summary>
    public static class BuildTools
    {
        [MenuItem("Borrowed Souls/Build WebGL")]
        public static void BuildWebGL()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:BorrowedSouls"; // fills the window, landscape hint on phones, keeps the save
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            BuildReport report = BuildPipeline.BuildPlayer(scenes, "Builds/WebGL", BuildTarget.WebGL, BuildOptions.None);
            BuildSummary summary = report.summary;
            Debug.Log($"[BUILD] Builds/WebGL: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0} s, errors={summary.totalErrors}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
