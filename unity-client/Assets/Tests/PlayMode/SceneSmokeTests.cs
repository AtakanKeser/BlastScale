using System.Collections;
using BlastScale.Client.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BlastScale.Tests
{
    /// <summary>
    /// Boots the real Main scene in play mode and checks that the runtime-built UI appears.
    /// No network is involved: the login screen only calls the server when a button is pressed.
    /// </summary>
    public class SceneSmokeTests
    {
        [UnityTest]
        public IEnumerator MainScene_BootsIntoLoginScreen()
        {
            SceneManager.LoadScene("Main");
            yield return null; // the scene becomes active at the end of this frame
            yield return null; // Awake/Start of GameBootstrap have run

            Assert.IsNotNull(Object.FindFirstObjectByType<Camera>(), "the scene needs a camera");
            Assert.IsNotNull(Object.FindFirstObjectByType<EventSystem>(), "the scene needs an EventSystem");
            GameBootstrap bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.IsNotNull(bootstrap, "the scene needs the GameBootstrap object");
            Assert.IsNotNull(bootstrap.App, "the bootstrap must have created the app context");
            Assert.IsNotNull(Object.FindFirstObjectByType<Canvas>(), "the bootstrap must create a canvas");
            Assert.IsNotNull(GameObject.Find("LoginScreen"), "the first screen must be the login screen");
            Assert.IsNotNull(GameObject.Find("Button Play as guest"), "the login screen must offer guest login");
        }
    }
}
