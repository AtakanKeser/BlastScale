using System.Collections;
using BlastScale.Client.Audio;
using BlastScale.Client.Core;
using BlastScale.Client.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace BlastScale.Tests
{
    /// <summary>
    /// Boots the real Main scene in play mode and drives the runtime-built UI. The first test only
    /// checks the login screen appears; the second plays a whole level through the offline demo
    /// (login → home → gameplay → result) so the animated board, the flows and the local
    /// stand-in server are exercised without any network.
    /// </summary>
    public class SceneSmokeTests
    {
        [UnityTest]
        public IEnumerator MainScene_BootsIntoLoginScreen()
        {
            GameBootstrap bootstrap = null;
            yield return TestDriver.Boot(b => bootstrap = b);

            Assert.IsNotNull(Object.FindFirstObjectByType<Camera>(), "the scene needs a camera");
            Assert.IsNotNull(Object.FindFirstObjectByType<EventSystem>(), "the scene needs an EventSystem");
            // Without an AudioListener Unity stays completely silent, and nothing else fails visibly.
            Assert.IsNotNull(Object.FindFirstObjectByType<AudioListener>(), "the scene needs an AudioListener or no sound plays");
            Assert.IsNotNull(Object.FindFirstObjectByType<Canvas>(), "the bootstrap must create a canvas");
            Assert.IsNotNull(GameObject.Find("LoginScreen"), "the first screen must be the login screen");
            Assert.IsNotNull(GameObject.Find("Button Play as guest"), "the login screen must offer guest login");
            Assert.IsNotNull(GameObject.Find("Button Sign in"), "the login screen must offer sign in");
            Assert.IsNotNull(GameObject.Find("Button Register"), "the login screen must offer registration");
            Assert.IsNotNull(GameObject.Find("Button Offline demo"), "the login screen must offer the offline demo");
            Assert.IsNotNull(GameObject.Find("ServerStatus"), "the login screen must show the connection status pill");
            Assert.IsNotNull(GameObject.Find("Button Help"), "the login screen must have the '?' help button");
            Assert.IsInstanceOf<LoginScreen>(bootstrap.App.Screens.Current);

            // The pill probes the server as soon as the screen opens and settles on a verdict
            // (connected or not — both are fine here, the backend may or may not be running).
            var login = (LoginScreen)bootstrap.App.Screens.Current;
            yield return TestDriver.WaitUntil(() => login.ServerState == ServerHealthState.Reachable || login.ServerState == ServerHealthState.Unreachable,
                ServerHealth.TimeoutSeconds + 3f, "the connection probe to finish");
            Debug.Log("[SceneSmokeTests] Server status on the login screen: " + login.ServerState);
            if (login.ServerState == ServerHealthState.Unreachable)
            {
                Assert.IsTrue(GameObject.Find("Button Retry") != null, "the amber state must offer a Retry button");
            }

            // Music is started by the bootstrap, before any screen needs it.
            Assert.IsNotNull(AudioManager.Instance, "the bootstrap must create the audio manager");
            Assert.IsNotNull(AudioManager.Instance.MusicClip, "the music loop must be started at boot");
        }

        [UnityTest]
        public IEnumerator OfflineDemo_PlaysALevelToTheResultScreen()
        {
            GameBootstrap bootstrap = null;
            yield return TestDriver.Boot(b => bootstrap = b);

            TestDriver.Press("Offline demo");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);
            Assert.IsTrue(bootstrap.App.IsOffline, "the offline demo must switch the API client");
            Assert.IsTrue(bootstrap.App.State.IsAuthenticated, "the demo player must be signed in");
            Assert.IsNotNull(bootstrap.App.State.Wallet, "the profile must have been loaded");
            Assert.AreEqual(1, bootstrap.App.State.CurrentLevel);

            // Home builds asynchronously (profile, daily reward, level preview); give it a moment.
            yield return TestDriver.WaitSeconds(0.6f);
            AudioClip musicAtHome = AudioManager.Instance.MusicClip;
            Assert.IsNotNull(musicAtHome, "the music must be playing on the home screen");
            Assert.IsNotNull(GameObject.Find("Button Haptics"), "the home screen must have the haptics toggle");
            TestDriver.Press("Play");
            yield return TestDriver.WaitForScreen<GameplayScreen>(bootstrap);
            var gameplay = (GameplayScreen)bootstrap.App.Screens.Current;
            Assert.IsNotNull(gameplay.Session, "a session must exist");
            Assert.AreEqual(4, bootstrap.App.State.Wallet.lives, "starting a level costs one life");

            int taps = 0;
            yield return TestDriver.PlayGreedy(gameplay, 40, t => taps = t);
            Assert.Greater(taps, 0, "at least one group must have been popped");
            yield return TestDriver.WaitUntil(() => !gameplay.IsBusy || !gameplay.Alive, TestDriver.DefaultTimeout, "the last animation");

            if (gameplay.Alive && gameplay.Session.ObjectiveReached && !gameplay.Session.OutOfMoves)
            {
                Assert.IsTrue(gameplay.Session.Score >= gameplay.Session.TargetScore);
                gameplay.FinishLevel();
            }
            yield return TestDriver.WaitForScreen<ResultScreen>(bootstrap, 30f);
            Debug.Log("[SceneSmokeTests] Result after " + taps + " taps, score " + gameplay.Session.Score + " / target " + gameplay.Session.TargetScore);
            Assert.IsNotNull(GameObject.Find("Button Home"), "the result screen must lead home");
            // The same loop keeps running through gameplay and result: no screen restarts it.
            Assert.AreSame(musicAtHome, AudioManager.Instance.MusicClip, "the music loop must not be replaced between screens");
            Assert.IsTrue(AudioManager.Instance.MusicEnabled, "music stays enabled through the level");
            Debug.Log("[SceneSmokeTests] Music position on the result screen: " + AudioManager.Instance.MusicTime.ToString("0.0") + " s, playing=" + AudioManager.Instance.IsMusicPlaying);
            if (gameplay.Session.ObjectiveReached)
            {
                Assert.AreEqual(2, bootstrap.App.State.CurrentLevel, "a win advances the player to level 2");
                Assert.Greater(bootstrap.App.State.Wallet.coins, 500, "a win pays coins");
            }

            // Let the celebration play out, then go back home through the real button.
            yield return TestDriver.WaitSeconds(1.5f);
            TestDriver.Press("Home");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);
        }
    }
}
