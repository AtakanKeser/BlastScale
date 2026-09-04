using System.IO;
using BlastScale.Client.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace BlastScale.EditorTools
{
    /// <summary>
    /// Generates <c>Assets/Scenes/Main.unity</c> from code so the scene is reproducible and never
    /// hand-edited YAML. Run it from the menu (BlastScale > Build Main Scene) or headless:
    /// <code>
    ///   Unity -batchmode -nographics -quit -projectPath . -executeMethod BlastScale.EditorTools.SceneBuilder.BuildMainScene
    /// </code>
    /// The scene contains exactly three objects: the camera, the UGUI event system and the
    /// <see cref="GameBootstrap"/> that creates the whole UI at runtime.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("BlastScale/Build Main Scene")]
        public static void BuildMainScene()
        {
            // Text serialization keeps the generated scene diff-friendly in git.
            EditorSettings.serializationMode = SerializationMode.ForceText;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.078f, 0.094f, 0.149f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            // The scene's "microphone": without it Unity plays no audio at all.
            cameraObject.AddComponent<AudioListener>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            // Legacy input module: the project deliberately does not use the Input System package.
            eventSystem.AddComponent<StandaloneInputModule>();

            var bootstrap = new GameObject("GameBootstrap");
            bootstrap.AddComponent<GameBootstrap>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new IOException("Could not save " + ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneBuilder] Wrote " + ScenePath + " and registered it in the build settings.");
        }
    }
}
