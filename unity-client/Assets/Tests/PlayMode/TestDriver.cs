using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BlastScale.Client.Core;
using BlastScale.Client.Net.Offline;
using BlastScale.Client.UI;
using BlastScale.Client.UI.Screens;
using BlastScale.Engine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlastScale.Tests
{
    /// <summary>
    /// Helpers shared by the play mode tests: boot the real scene, press buttons by name, wait for
    /// a screen, play a level greedily through the public seams of <see cref="GameplayScreen"/>
    /// and render the UI camera into a portrait PNG. Everything runs against the offline demo so
    /// no server is needed.
    /// </summary>
    public static class TestDriver
    {
        public const float DefaultTimeout = 20f;

        /// <summary>Loads Main.unity from a clean offline state and returns the bootstrap once it ran Awake/Start.</summary>
        public static IEnumerator Boot(Action<GameBootstrap> onReady)
        {
            OfflineApiClient.ResetSave();
            SceneManager.LoadScene("Main");
            yield return null; // the scene becomes active at the end of this frame
            yield return null; // Awake/Start of GameBootstrap have run
            GameBootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            Assert.IsNotNull(bootstrap, "the scene needs the GameBootstrap object");
            Assert.IsNotNull(bootstrap.App, "the bootstrap must have created the app context");
            onReady(bootstrap);
        }

        /// <summary>Finds a button created by UiFactory (objects are named "Button &lt;label&gt;").</summary>
        public static Button FindButton(string label)
        {
            GameObject go = GameObject.Find("Button " + label);
            return go != null ? go.GetComponent<Button>() : null;
        }

        /// <summary>Presses a button as the player would (through its onClick).</summary>
        public static void Press(string label)
        {
            Button button = FindButton(label);
            Assert.IsNotNull(button, "button '" + label + "' must exist");
            Assert.IsTrue(button.interactable, "button '" + label + "' must be enabled");
            button.onClick.Invoke();
        }

        /// <summary>Waits until the current screen is a live instance of <typeparamref name="T"/>.</summary>
        public static IEnumerator WaitForScreen<T>(GameBootstrap bootstrap, float timeout = DefaultTimeout) where T : UiScreen
        {
            yield return WaitUntil(() => bootstrap.App.Screens.Current is T screen && screen.Alive, timeout, "screen " + typeof(T).Name);
        }

        public static IEnumerator WaitUntil(Func<bool> condition, float timeout, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > timeout)
                {
                    Assert.Fail("timed out waiting for " + what);
                }
                yield return null;
            }
        }

        public static IEnumerator WaitSeconds(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Plays greedily (largest group first) until the target is reached or the moves run out,
        /// waiting for the board animation to settle after every tap. Returns the number of taps.
        /// </summary>
        public static IEnumerator PlayGreedy(GameplayScreen screen, int maxTaps, Action<int> onDone)
        {
            int taps = 0;
            while (taps < maxTaps && screen.Alive)
            {
                yield return WaitUntil(() => !screen.IsBusy || !screen.Alive, DefaultTimeout, "board animation to settle");
                if (!screen.Alive) break;
                LevelSession session = screen.Session;
                if (session.ObjectiveReached || session.OutOfMoves)
                {
                    break;
                }
                List<List<CellPos>> groups = session.Board.Groups();
                Assert.IsTrue(groups.Count > 0, "the engine guarantees at least one group");
                List<CellPos> best = groups[0];
                foreach (List<CellPos> group in groups)
                {
                    if (group.Count > best.Count) best = group;
                }
                screen.TapCell(best[0].Row, best[0].Col);
                taps++;
                yield return null;
            }
            onDone(taps);
        }

        private static RenderTexture _captureTexture;
        private static Camera _captureCamera;
        private static CanvasScaler _captureScaler;
        private static CanvasScaler.ScaleMode _previousMode;
        private static float _previousFactor;

        /// <summary>
        /// Points the UI camera at a 1080x1920 render texture and pins the canvas to that size, so
        /// every later <see cref="Capture"/> (and the layout the game runs with) is independent of
        /// the tiny batch mode game view. Call <see cref="EndCaptureMode"/> when done.
        /// </summary>
        public static IEnumerator BeginCaptureMode(int width = 1080, int height = 1920)
        {
            _captureCamera = Camera.main;
            if (_captureCamera == null) _captureCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(_captureCamera, "a camera is needed to capture the UI");
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "the canvas must exist");
            _captureScaler = canvas.GetComponent<CanvasScaler>();
            _captureTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            _captureTexture.Create();
            _previousMode = _captureScaler.uiScaleMode;
            _previousFactor = _captureScaler.scaleFactor;
            _captureCamera.targetTexture = _captureTexture;
            _captureScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _captureScaler.scaleFactor = 1f;
            // Let the layout groups settle at the new canvas size.
            for (int i = 0; i < 4; i++)
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
            }
        }

        /// <summary>Restores the camera and the canvas scaler.</summary>
        public static void EndCaptureMode()
        {
            if (_captureCamera != null) _captureCamera.targetTexture = null;
            if (_captureScaler != null)
            {
                _captureScaler.uiScaleMode = _previousMode;
                _captureScaler.scaleFactor = _previousFactor;
            }
            if (_captureTexture != null)
            {
                _captureTexture.Release();
                UnityEngine.Object.Destroy(_captureTexture);
                _captureTexture = null;
            }
        }

        /// <summary>
        /// Renders one frame into the capture texture and writes it as JPG, without advancing the
        /// game loop. Used by the video recorder, which needs exactly one file per rendered frame
        /// (PNG would be several times slower and the footage is re-encoded anyway).
        /// </summary>
        public static void RenderFrameToFile(string path, int quality = 92)
        {
            Assert.IsNotNull(_captureTexture, "BeginCaptureMode must run before RenderFrameToFile");
            Canvas.ForceUpdateCanvases();
            _captureCamera.Render();
            int width = _captureTexture.width;
            int height = _captureTexture.height;
            RenderTexture.active = _captureTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToJPG(quality));
            UnityEngine.Object.Destroy(texture);
        }

        /// <summary>Renders the UI camera into the capture texture and writes it as PNG (needs <see cref="BeginCaptureMode"/>).</summary>
        public static IEnumerator Capture(string path)
        {
            Assert.IsNotNull(_captureTexture, "BeginCaptureMode must run before Capture");
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            _captureCamera.Render();

            int width = _captureTexture.width;
            int height = _captureTexture.height;
            RenderTexture.active = _captureTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            Debug.Log("[TestDriver] Captured " + path);
        }
    }
}
