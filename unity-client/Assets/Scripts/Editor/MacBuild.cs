using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlastScale.EditorTools
{
    /// <summary>
    /// Headless macOS build used by <c>unity-client/build-mac.sh</c>:
    /// <c>Unity -batchmode -quit -buildTarget StandaloneOSX -executeMethod BlastScale.EditorTools.MacBuild.Build</c>.
    ///
    /// The game is designed for a portrait phone screen, while Unity's desktop defaults are a
    /// 1920x1080 exclusive full-screen window — which would stretch the layout sideways. The build
    /// therefore opens a resizable 9:16 window that fits on a laptop display and produces a
    /// double-clickable <c>build/mac/BlastScale.app</c>. It talks to the backend at
    /// http://localhost:8080 by default (the login screen shows whether the server is reachable),
    /// and the offline demo works without any server.
    /// </summary>
    public static class MacBuild
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string OutputPath = "build/mac/BlastScale.app";
        private const string IconPath = "Assets/Icon/app-icon.png";

        // 9:16 like the phone. These are backing pixels: with Retina support on, Unity sizes the
        // window in pixels, so 900x1600 opens as 450x800 points on a laptop display — tall enough to
        // play comfortably and still below the menu bar of a 13-14" screen. (The first build used
        // 450x800 and opened a 225x400 point window.) macOS clamps it on smaller external displays,
        // and the window stays resizable.
        private const int WindowWidth = 900;
        private const int WindowHeight = 1600;

        /// <summary>Applies the desktop player settings and builds the app; exits Unity with 0 on success.</summary>
        public static void Build()
        {
            try
            {
                ApplyPlayerSettings();
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.None,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError($"macOS build failed: {report.summary.result}, {report.summary.totalErrors} error(s)");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log($"macOS build succeeded: {report.summary.outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("macOS build threw: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Desktop window, name and icon; everything else is shared with the phone build.</summary>
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "BlastScale";
            PlayerSettings.companyName = "Atakan Keser";
            PlayerSettings.bundleVersion = "0.1.0";

            // A portrait window instead of Unity's landscape exclusive full screen.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = WindowWidth;
            PlayerSettings.defaultScreenHeight = WindowHeight;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.macRetinaSupport = true; // crisp text on Retina displays

            // Mono builds quickly and needs no extra module; the game has no hot path that needs IL2CPP.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            }
        }
    }
}
