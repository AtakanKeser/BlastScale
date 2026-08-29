using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlastScale.EditorTools
{
    /// <summary>
    /// Headless iOS export used by <c>unity-client/build-ios.sh</c>:
    /// <c>Unity -batchmode -buildTarget iOS -executeMethod BlastScale.EditorTools.IosBuild.Build</c>.
    ///
    /// It applies every player setting the phone build needs (bundle id, automatic signing with the
    /// developer's team, portrait only, IL2CPP, plain-HTTP allowed so the app can talk to the backend
    /// on the local network), bakes the server URL into Resources/server-config.json and exports the
    /// Xcode project to <c>build/ios</c>. Signing and the actual compile happen in xcodebuild.
    /// </summary>
    public static class IosBuild
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string OutputPath = "build/ios";
        private const string ServerConfigPath = "Assets/Resources/server-config.json";
        private const string IconPath = "Assets/Icon/app-icon.png";

        public static void Build()
        {
            try
            {
                WriteServerConfig();
                ApplyPlayerSettings();
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.iOS,
                    options = BuildOptions.None,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError($"iOS export failed: {report.summary.result}, {report.summary.totalErrors} error(s)");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log($"iOS export succeeded: {report.summary.outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("iOS export threw: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// The phone cannot reach "localhost"; the build script passes the Mac's LAN address in
        /// BLASTSCALE_SERVER_URL and it becomes the client's default base URL (still editable in-game).
        /// </summary>
        private static void WriteServerConfig()
        {
            string serverUrl = Environment.GetEnvironmentVariable("BLASTSCALE_SERVER_URL");
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                Debug.Log("BLASTSCALE_SERVER_URL not set; the client keeps its built-in default server URL");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ServerConfigPath));
            File.WriteAllText(ServerConfigPath, "{\"baseUrl\": \"" + serverUrl.Trim().TrimEnd('/') + "\"}\n");
            AssetDatabase.ImportAsset(ServerConfigPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Baked server URL " + serverUrl + " into " + ServerConfigPath);
        }

        private static void ApplyPlayerSettings()
        {
            string teamId = Environment.GetEnvironmentVariable("BLASTSCALE_IOS_TEAM_ID") ?? string.Empty;
            string bundleId = Environment.GetEnvironmentVariable("BLASTSCALE_IOS_BUNDLE_ID");
            if (string.IsNullOrWhiteSpace(bundleId))
            {
                bundleId = "com.atakankeser.blastscale";
            }

            PlayerSettings.productName = "BlastScale";
            PlayerSettings.companyName = "Atakan Keser";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleId);
            PlayerSettings.iOS.buildNumber = DateTime.UtcNow.ToString("yyyyMMddHHmm"); // unique per install
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.appleDeveloperTeamID = teamId;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

            // The backend runs over plain HTTP on the LAN during development; iOS blocks that unless
            // the app opts in (NSAllowsArbitraryLoads).
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            // A portrait puzzle game: no rotation, no status bar.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.statusBarHidden = true;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                // The default icon set is what iOS app icon sizes are generated from.
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            }
            else
            {
                Debug.LogWarning("App icon not found at " + IconPath + "; Unity's default icon will be used");
            }
        }
    }
}
