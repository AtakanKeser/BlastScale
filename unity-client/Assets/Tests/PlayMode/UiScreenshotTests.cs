using System.Collections;
using BlastScale.Client.Core;
using BlastScale.Client.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BlastScale.Tests
{
    /// <summary>
    /// Visual check: walks through the offline demo and renders the login, home, gameplay (after a
    /// few taps) and result screens into /tmp/blastscale-shots/*.png at 1080x1920. Run Unity in
    /// batch mode WITHOUT -nographics; the PNGs are meant to be looked at by a human.
    /// </summary>
    public class UiScreenshotTests
    {
        private const string Folder = "/tmp/blastscale-shots";

        [UnityTest]
        public IEnumerator CaptureMainScreens()
        {
            GameBootstrap bootstrap = null;
            yield return TestDriver.Boot(b => bootstrap = b);
            yield return TestDriver.BeginCaptureMode();
            yield return TestDriver.WaitSeconds(1.2f); // entrance animations
            yield return TestDriver.Capture(Folder + "/01-login.png");

            TestDriver.Press("Offline demo");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);
            yield return TestDriver.WaitSeconds(1.2f);
            yield return TestDriver.Capture(Folder + "/02-home.png");

            TestDriver.Press("Play");
            yield return TestDriver.WaitForScreen<GameplayScreen>(bootstrap);
            var gameplay = (GameplayScreen)bootstrap.App.Screens.Current;
            yield return TestDriver.WaitSeconds(1.5f); // board pop-in
            yield return TestDriver.Capture(Folder + "/03-gameplay-start.png");

            yield return TestDriver.PlayGreedy(gameplay, 3, _ => { });
            yield return TestDriver.WaitUntil(() => !gameplay.IsBusy || !gameplay.Alive, TestDriver.DefaultTimeout, "animations");
            yield return TestDriver.WaitSeconds(0.8f);
            yield return TestDriver.Capture(Folder + "/04-gameplay-after-taps.png");

            // Capture the middle of a pop for the particle effect, then finish the level.
            if (gameplay.Alive && !gameplay.Session.ObjectiveReached && !gameplay.Session.OutOfMoves)
            {
                yield return TestDriver.PlayGreedy(gameplay, 1, _ => { });
                yield return TestDriver.WaitSeconds(0.12f);
                yield return TestDriver.Capture(Folder + "/05-gameplay-pop.png");
            }
            yield return TestDriver.PlayGreedy(gameplay, 40, _ => { });
            yield return TestDriver.WaitUntil(() => !gameplay.IsBusy || !gameplay.Alive, TestDriver.DefaultTimeout, "the last animation");
            if (gameplay.Alive && gameplay.Session.ObjectiveReached && !gameplay.Session.OutOfMoves)
            {
                yield return TestDriver.WaitSeconds(0.6f);
                yield return TestDriver.Capture(Folder + "/06-gameplay-target-reached.png");
                gameplay.FinishLevel();
            }
            yield return TestDriver.WaitForScreen<ResultScreen>(bootstrap, 30f);
            yield return TestDriver.WaitSeconds(2.6f); // stars, coin count-up, confetti
            yield return TestDriver.Capture(Folder + "/07-result.png");

            TestDriver.Press("Home");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);
            yield return TestDriver.WaitSeconds(1.0f);
            yield return TestDriver.Capture(Folder + "/08-home-after-win.png");

            PressCard("Shop");
            yield return TestDriver.WaitForScreen<ShopScreen>(bootstrap);
            yield return TestDriver.WaitSeconds(0.8f);
            yield return TestDriver.Capture(Folder + "/09-shop.png");

            TestDriver.Press("Back");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);
            yield return TestDriver.WaitSeconds(0.8f);
            PressCard("Leaderboard");
            yield return TestDriver.WaitForScreen<LeaderboardScreen>(bootstrap);
            yield return TestDriver.WaitSeconds(1.0f);
            yield return TestDriver.Capture(Folder + "/10-leaderboard.png");

            TestDriver.Press("Back");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);
            yield return TestDriver.WaitSeconds(0.8f);
            PressCard("Events");
            yield return TestDriver.WaitForScreen<EventsScreen>(bootstrap);
            yield return TestDriver.WaitSeconds(1.0f);
            yield return TestDriver.Capture(Folder + "/11-events.png");

            // Level 2: the give-up dialog and the loss card.
            TestDriver.Press("Back");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);
            yield return TestDriver.WaitSeconds(0.8f);
            TestDriver.Press("Play");
            yield return TestDriver.WaitForScreen<GameplayScreen>(bootstrap);
            gameplay = (GameplayScreen)bootstrap.App.Screens.Current;
            yield return TestDriver.PlayGreedy(gameplay, 2, _ => { });
            yield return TestDriver.WaitUntil(() => !gameplay.IsBusy || !gameplay.Alive, TestDriver.DefaultTimeout, "animations");
            TestDriver.Press("Quit");
            yield return TestDriver.WaitSeconds(0.6f);
            yield return TestDriver.Capture(Folder + "/12-give-up-modal.png");
            TestDriver.Press("Give up");
            yield return TestDriver.WaitForScreen<ResultScreen>(bootstrap, 30f);
            yield return TestDriver.WaitSeconds(1.4f);
            yield return TestDriver.Capture(Folder + "/13-result-lost.png");
            TestDriver.EndCaptureMode();
        }

        /// <summary>The home cards are named "Card &lt;name&gt;" and carry a Button.</summary>
        private static void PressCard(string name)
        {
            GameObject go = GameObject.Find("Card " + name);
            Assert.IsNotNull(go, "card '" + name + "' must exist");
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }
    }
}
