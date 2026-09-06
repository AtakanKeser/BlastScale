using System.IO;
using BlastScale.Client.Net.Offline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlastScale.EditorTools
{
    /// <summary>
    /// First-run help for someone who just cloned the repository: a fresh Unity project opens on
    /// an empty "Untitled" scene where pressing Play shows nothing. This script opens
    /// <c>Assets/Scenes/Main.unity</c> instead (only when no real scene is open and nothing would
    /// be lost), makes sure the scene is registered in the build settings, and adds two menu items:
    /// <list type="bullet">
    ///   <item><b>BlastScale &gt; Open Main Scene</b> — the same, on demand;</item>
    ///   <item><b>BlastScale &gt; Reset local save</b> — wipes the offline demo progress and every
    ///         PlayerPrefs value (server URL override, audio/haptics toggles, remembered username),
    ///         handy before showing the game to someone.</item>
    /// </list>
    /// </summary>
    [InitializeOnLoad]
    public static class EditorOnboarding
    {
        static EditorOnboarding()
        {
            // The static constructor runs during domain reload, too early to touch scenes;
            // delayCall waits for the editor to finish loading.
            EditorApplication.delayCall += OpenMainSceneIfUntitled;
        }

        /// <summary>Opens Main.unity when the editor sits on an untitled, unmodified scene (a fresh clone).</summary>
        private static void OpenMainSceneIfUntitled()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }
            Scene active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty)
            {
                return; // a real scene is open, or the untitled scene holds unsaved work
            }
            if (!File.Exists(SceneBuilder.ScenePath))
            {
                Debug.LogWarning("[BlastScale] " + SceneBuilder.ScenePath + " is missing; run BlastScale > Build Main Scene to generate it.");
                return;
            }
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath, OpenSceneMode.Single);
            EnsureSceneInBuildSettings();
            Debug.Log("[BlastScale] Opened " + SceneBuilder.ScenePath + " — press Play, then pick Offline demo to try the game without a server.");
        }

        [MenuItem("BlastScale/Open Main Scene")]
        public static void OpenMainScene()
        {
            if (!File.Exists(SceneBuilder.ScenePath))
            {
                if (EditorUtility.DisplayDialog("Main scene missing", SceneBuilder.ScenePath + " does not exist yet. Generate it now?", "Build it", "Cancel"))
                {
                    SceneBuilder.BuildMainScene();
                }
                return;
            }
            // Asks about unsaved changes in the current scene first (returns false on Cancel).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath, OpenSceneMode.Single);
            EnsureSceneInBuildSettings();
        }

        [MenuItem("BlastScale/Reset local save")]
        public static void ResetLocalSave()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Reset local save", "Stop play mode first, then reset.", "OK");
                return;
            }
            OfflineSave.Reset();
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[BlastScale] Cleared the offline demo save and every PlayerPrefs value (server URL override, toggles, remembered username).");
        }

        /// <summary>Adds Main.unity to the build settings when it is missing (a build without it would be empty).</summary>
        public static bool EnsureSceneInBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (scene.path == SceneBuilder.ScenePath)
                {
                    return true;
                }
            }
            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(SceneBuilder.ScenePath, true);
            EditorBuildSettings.scenes = updated;
            Debug.Log("[BlastScale] Registered " + SceneBuilder.ScenePath + " in the build settings.");
            return false;
        }
    }
}
