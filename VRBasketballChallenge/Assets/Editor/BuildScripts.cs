using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VRBasketball.EditorTools
{
    /// <summary>
    /// Compilaciones en modo batch:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod VRBasketball.EditorTools.BuildScripts.BatchBuildWebGL
    /// </summary>
    public static class BuildScripts
    {
        private static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        public static void BatchBuildWebGL()
        {
            ProjectSetup.ConfigureAll();
            Build(BuildTarget.WebGL, Path.Combine(ProjectRoot, "Builds/WebGL"), "VRBasketballChallenge_WebGL");
        }

        public static void BatchBuildWindows()
        {
            ProjectSetup.ConfigureAll();
            Build(BuildTarget.StandaloneWindows64,
                Path.Combine(ProjectRoot, "Builds/Windows/VRBasketballChallenge.exe"),
                "VRBasketballChallenge_Windows");
        }

        public static void BatchBuildAndroid()
        {
            ProjectSetup.ConfigureAll();
            Build(BuildTarget.Android,
                Path.Combine(ProjectRoot, "Builds/Android/VRBasketballChallenge.apk"),
                "VRBasketballChallenge_Android");
        }

        private static void Build(BuildTarget target, string location, string label)
        {
            string folder = target == BuildTarget.WebGL
                ? location
                : Path.GetDirectoryName(location);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = location,
                target = target,
                options = BuildOptions.None
            };

            Debug.Log("VR Basketball: compilando " + label + " -> " + location);

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(string.Format("VR Basketball: {0} OK · {1:0.0} MB · {2}",
                    label,
                    summary.totalSize / 1048576f,
                    summary.totalTime));
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError(string.Format("VR Basketball: {0} FALLÓ · resultado {1} · {2} errores",
                    label, summary.result, summary.totalErrors));
                EditorApplication.Exit(1);
            }
        }
    }
}
