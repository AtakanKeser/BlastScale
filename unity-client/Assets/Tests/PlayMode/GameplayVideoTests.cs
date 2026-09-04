using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BlastScale.Client.Core;
using BlastScale.Client.UI.Screens;
using BlastScale.Engine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BlastScale.Tests
{
    /// <summary>
    /// Recording tool rather than an assertion: walks through the offline demo (login, home, a full
    /// level, the result screen, leaderboard, events, shop) and writes one JPG per rendered frame to
    /// <c>BLASTSCALE_VIDEO_DIR</c> (default <c>/tmp/blastscale-video/frames</c>). ffmpeg turns those
    /// frames into the demo video shown in the README.
    ///
    /// <para>The recording is deterministic: <see cref="Time.captureFramerate"/> pins the game clock
    /// to <see cref="Fps"/> steps, so every rendered frame advances exactly 1/30 s of animation no
    /// matter how long the JPG encoding of the previous frame took. Waits are therefore counted in
    /// frames, never in wall-clock seconds.</para>
    ///
    /// <para>Marked Explicit so a normal test run skips it; record with
    /// <c>-testPlatform PlayMode -testFilter BlastScale.Tests.GameplayVideoTests</c> and Unity in
    /// batch mode <b>without</b> <c>-nographics</c>.</para>
    /// </summary>
    public class GameplayVideoTests
    {
        private const int Fps = 30;
        private const int MaxFrames = 30 * 60; // hard stop after a minute of footage

        private string _folder;
        private int _frame;

        [UnityTest, Explicit("Recording tool: writes a JPG per frame, run it with -testFilter")]
        public IEnumerator RecordDemoVideo()
        {
            _folder = System.Environment.GetEnvironmentVariable("BLASTSCALE_VIDEO_DIR");
            if (string.IsNullOrEmpty(_folder))
            {
                _folder = "/tmp/blastscale-video/frames";
            }
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
            Directory.CreateDirectory(_folder);

            int previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = Fps; // deterministic 1/30 s per rendered frame
            GameBootstrap bootstrap = null;
            yield return TestDriver.Boot(b => bootstrap = b);
            yield return TestDriver.BeginCaptureMode();

            // ----- 1. Login: the title, the colour palette and the offline entry point -----
            yield return Record(45);
            TestDriver.Press("Offline demo");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);

            // ----- 2. Home: currencies, daily reward, the menu cards -----
            yield return Record(75);
            TestDriver.Press("Play");
            yield return TestDriver.WaitForScreen<GameplayScreen>(bootstrap);
            var gameplay = (GameplayScreen)bootstrap.App.Screens.Current;

            // ----- 3. Gameplay: the board drops in, then deliberate taps so every pop is visible -----
            yield return Record(50);
            yield return PlayForVideo(gameplay, 30);

            // ----- 4. Result: confetti, stars, the reward breakdown -----
            if (gameplay.Alive && gameplay.Session.ObjectiveReached)
            {
                yield return Record(25);
                gameplay.FinishLevel();
            }
            yield return TestDriver.WaitForScreen<ResultScreen>(bootstrap, 30f);
            yield return Record(110);

            // ----- 5. The live-ops surfaces the backend drives -----
            TestDriver.Press("Home");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);
            yield return Record(40);

            yield return Visit(bootstrap, "Leaderboard", 65);
            yield return Visit(bootstrap, "Events", 65);
            yield return Visit(bootstrap, "Shop", 65);

            TestDriver.EndCaptureMode();
            Time.captureFramerate = previousCaptureFramerate;
            Debug.Log("[video] recorded " + _frame + " frames (" + (_frame / (float)Fps).ToString("0.0", CultureInfo.InvariantCulture) + " s) to " + _folder);
            Assert.Greater(_frame, Fps * 10, "the recording should be at least ten seconds long");
        }

        /// <summary>Opens one of the home menu cards, holds on it, and comes back.</summary>
        private IEnumerator Visit(GameBootstrap bootstrap, string card, int frames)
        {
            GameObject go = GameObject.Find("Card " + card) ?? GameObject.Find("Button " + card);
            if (go == null)
            {
                yield break;
            }
            var button = go.GetComponent<UnityEngine.UI.Button>();
            if (button == null)
            {
                yield break;
            }
            button.onClick.Invoke();
            yield return Record(frames);
            TestDriver.Press("Back");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);
            yield return Record(20);
        }

        /// <summary>
        /// Plays greedily like <see cref="TestDriver.PlayGreedy"/>, but paced for the camera: a short
        /// pause before each tap and enough frames afterwards to show the pop, the fall and the
        /// score count-up.
        /// </summary>
        private IEnumerator PlayForVideo(GameplayScreen screen, int maxTaps)
        {
            for (int tap = 0; tap < maxTaps && screen.Alive; tap++)
            {
                yield return RecordUntil(() => !screen.IsBusy || !screen.Alive, 90);
                if (!screen.Alive || screen.Session.ObjectiveReached || screen.Session.OutOfMoves)
                {
                    yield break;
                }
                List<List<CellPos>> groups = screen.Session.Board.Groups();
                if (groups.Count == 0)
                {
                    yield break;
                }
                List<CellPos> best = groups[0];
                foreach (List<CellPos> group in groups)
                {
                    if (group.Count > best.Count) best = group;
                }
                yield return Record(6); // a beat before the tap, so it reads as a decision
                screen.TapCell(best[0].Row, best[0].Col);
                yield return Record(16); // pop, particles, falling blocks
            }
        }

        /// <summary>Renders and stores <paramref name="frames"/> frames.</summary>
        private IEnumerator Record(int frames)
        {
            for (int i = 0; i < frames && _frame < MaxFrames; i++)
            {
                TestDriver.RenderFrameToFile(Path.Combine(_folder, "frame_" + _frame.ToString("D5") + ".jpg"));
                _frame++;
                yield return null;
            }
        }

        /// <summary>Records until the condition holds (bounded, so a stuck animation cannot hang the run).</summary>
        private IEnumerator RecordUntil(System.Func<bool> condition, int maxFrames)
        {
            for (int i = 0; i < maxFrames && !condition() && _frame < MaxFrames; i++)
            {
                TestDriver.RenderFrameToFile(Path.Combine(_folder, "frame_" + _frame.ToString("D5") + ".jpg"));
                _frame++;
                yield return null;
            }
        }
    }
}
