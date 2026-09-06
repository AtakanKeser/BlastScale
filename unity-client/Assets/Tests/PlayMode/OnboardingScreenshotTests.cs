using System.Collections;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BlastScale.Tests
{
    /// <summary>
    /// Visual check of the first-contact experience: renders the login screen with the connection
    /// pill in both states (the configured server, then a port nobody listens on), the
    /// "could not reach the server" dialog, the inline form validation, the help dialog and the
    /// home screen toggles into /tmp/blastscale-onboarding/*.png at 1080x1920. Run Unity in batch
    /// mode WITHOUT -nographics. The player's own server URL and haptics preference are restored
    /// afterwards because PlayerPrefs are shared with the editor.
    /// </summary>
    public class OnboardingScreenshotTests
    {
        private const string Folder = "/tmp/blastscale-onboarding";

        /// <summary>A port that is closed on every machine (the "discard" port): the probe fails instantly.</summary>
        private const string DeadUrl = "http://localhost:9";

        /// <summary>A reserved, never routed address (RFC 5737 TEST-NET-1): the probe hangs until its timeout.</summary>
        private const string SilentUrl = "http://192.0.2.1:8080";

        private const string BaseUrlPrefKey = "blastscale.baseUrl";

        private bool _hadUrlOverride;
        private string _previousUrl;
        private bool _previousHaptics;

        [SetUp]
        public void RememberPrefs()
        {
            _hadUrlOverride = PlayerPrefs.HasKey(BaseUrlPrefKey);
            _previousUrl = PlayerPrefs.GetString(BaseUrlPrefKey, "");
            _previousHaptics = Haptics.Enabled;
        }

        [TearDown]
        public void RestorePrefs()
        {
            if (_hadUrlOverride) PlayerPrefs.SetString(BaseUrlPrefKey, _previousUrl); else PlayerPrefs.DeleteKey(BaseUrlPrefKey);
            PlayerPrefs.Save();
            Haptics.Enabled = _previousHaptics;
            TestDriver.EndCaptureMode();
        }

        [UnityTest]
        public IEnumerator CaptureLoginStatesAndHelp()
        {
            GameBootstrap bootstrap = null;
            yield return TestDriver.Boot(b => bootstrap = b);
            yield return TestDriver.BeginCaptureMode();
            var login = (LoginScreen)bootstrap.App.Screens.Current;

            // 1. Whatever the configured server says (green when the backend runs on this machine).
            yield return WaitForVerdict(login);
            yield return TestDriver.WaitSeconds(1.0f);
            string firstState = login.ServerState == ServerHealthState.Reachable ? "connected" : "no-server";
            yield return TestDriver.Capture(Folder + "/01-login-" + firstState + ".png");

            // 2. A host that never answers keeps the probe in flight until its 3 s timeout: that is
            //    the window in which the "Checking server…" state can be photographed. With the
            //    project's default "Allow downloads over HTTP = Not allowed", Unity refuses plain
            //    http to anything but localhost up front, which the pill reports as its own state.
            InputField urlField = GameObject.Find("Input Server URL").GetComponent<InputField>();
            urlField.text = SilentUrl;
            urlField.onEndEdit.Invoke(SilentUrl);
            yield return TestDriver.WaitSeconds(0.4f);
            if (login.ServerState == ServerHealthState.Checking)
            {
                yield return TestDriver.Capture(Folder + "/01b-login-checking.png");
            }
            yield return WaitForVerdict(login);
            if (login.ServerState == ServerHealthState.Blocked)
            {
                yield return TestDriver.WaitSeconds(0.3f);
                yield return TestDriver.Capture(Folder + "/01c-login-http-blocked.png");
            }
            Debug.Log("[OnboardingScreenshotTests] " + SilentUrl + " -> " + login.ServerState);

            // 3. Point the field at a dead port: the pill must turn amber and offer Retry.
            urlField.text = DeadUrl;              // onValueChanged -> debounced probe
            urlField.onEndEdit.Invoke(DeadUrl);   // losing focus -> persisted + probed at once
            yield return WaitForVerdict(login);
            Assert.AreEqual(ServerHealthState.Unreachable, login.ServerState, "nothing listens on port 9");
            Assert.AreEqual(DeadUrl, ClientConfig.BaseUrl, "leaving the field persists the URL");
            Assert.IsTrue(GameObject.Find("Button Retry") != null && GameObject.Find("Button Retry").activeInHierarchy, "the amber state shows Retry");
            yield return TestDriver.WaitSeconds(0.5f);
            yield return TestDriver.Capture(Folder + "/02-login-no-server.png");

            // 3. Guest login against the dead port ends in the "could not reach" dialog with
            //    Offline demo / Retry (the API client retries once after a second first).
            TestDriver.Press("Play as guest");
            yield return TestDriver.WaitUntil(() => bootstrap.App.Modal.IsOpen, 15f, "the connection error dialog");
            yield return TestDriver.WaitSeconds(0.6f);
            yield return TestDriver.Capture(Folder + "/03-login-cannot-reach-dialog.png");
            Assert.IsInstanceOf<LoginScreen>(bootstrap.App.Screens.Current, "a failed login stays on the login screen");
            bootstrap.App.Modal.Close();
            yield return TestDriver.WaitSeconds(0.3f);

            // 4. Inline validation: too short username and password, Register disabled...
            InputField username = GameObject.Find("Input Username").GetComponent<InputField>();
            InputField password = GameObject.Find("Input Password").GetComponent<InputField>();
            username.text = "ab";
            password.text = "short";
            yield return null;
            Assert.IsFalse(TestDriver.FindButton("Register").interactable, "Register stays disabled while the form is invalid");
            yield return TestDriver.WaitSeconds(0.3f);
            yield return TestDriver.Capture(Folder + "/04-login-validation.png");
            // ...and enabled once both fields satisfy the rules.
            username.text = "tester_01";
            password.text = "correct horse battery";
            yield return null;
            Assert.IsTrue(TestDriver.FindButton("Register").interactable, "Register is enabled for a valid form");

            // 5. The "?" help dialog.
            TestDriver.Press("Help");
            yield return TestDriver.WaitUntil(() => bootstrap.App.Modal.IsOpen, 5f, "the help dialog");
            yield return TestDriver.WaitSeconds(0.6f);
            yield return TestDriver.Capture(Folder + "/05-login-help.png");
            bootstrap.App.Modal.Close();
            yield return TestDriver.WaitSeconds(0.3f);

            // 6. Home screen: music / sound / haptics toggles, then haptics switched off.
            TestDriver.Press("Offline demo");
            yield return TestDriver.WaitForScreen<HomeScreen>(bootstrap);
            yield return TestDriver.WaitSeconds(1.2f);
            Haptics.Enabled = true;
            yield return TestDriver.Capture(Folder + "/06-home-toggles.png");
            TestDriver.Press("Haptics");
            Assert.IsFalse(Haptics.Enabled, "the toggle flips the haptics preference");
            yield return TestDriver.WaitSeconds(0.3f);
            yield return TestDriver.Capture(Folder + "/07-home-haptics-off.png");
            TestDriver.Press("Haptics");
            Assert.IsTrue(Haptics.Enabled, "the toggle flips it back");
            TestDriver.EndCaptureMode();
        }

        /// <summary>
        /// End-to-end check of the server-side error copy against the real backend at the
        /// configured URL: a wrong password (INVALID_CREDENTIALS) and a username the client rules
        /// would normally block (the server answers VALIDATION_ERROR with per-field messages).
        /// Neither call creates data. Skipped when no server is reachable.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureServerSideErrors()
        {
            GameBootstrap bootstrap = null;
            yield return TestDriver.Boot(b => bootstrap = b);
            yield return TestDriver.BeginCaptureMode();
            var login = (LoginScreen)bootstrap.App.Screens.Current;
            yield return WaitForVerdict(login);
            if (login.ServerState != ServerHealthState.Reachable)
            {
                Assert.Ignore("no BlastScale server at " + ClientConfig.BaseUrl + "; start it with docker compose up to run this test");
            }

            // Wrong credentials (a player that does not exist): the toast and the red hint in the password field.
            InputField username = GameObject.Find("Input Username").GetComponent<InputField>();
            InputField password = GameObject.Find("Input Password").GetComponent<InputField>();
            username.text = "no_such_player_" + Random.Range(1000, 9999);
            password.text = "definitely-wrong-password";
            TestDriver.Press("Sign in");
            yield return TestDriver.WaitUntil(() => HintVisible(password), 15f, "the credentials error");
            Assert.IsInstanceOf<LoginScreen>(bootstrap.App.Screens.Current, "a failed sign-in stays on the login screen");
            yield return TestDriver.WaitSeconds(0.5f);
            yield return TestDriver.Capture(Folder + "/08-login-invalid-credentials.png");

            // Server-side validation. The client rules normally stop "ab" / "short" before any
            // request, so the flow is called directly with the screen's own failure handler: the
            // server's per-field messages must land next to the fields.
            username.text = "ab";
            password.text = "short";
            yield return bootstrap.App.Flow.Register("ab", "short", login.OnAuthFailed);
            yield return TestDriver.WaitUntil(() => HintVisible(username) && HintVisible(password), 15f, "the server's field messages");
            Assert.That(HintText(username), Does.Contain("3").And.Contain("32"), "the server's username message is shown inline");
            Assert.That(HintText(password), Does.Contain("8").And.Contain("72"), "the server's password message is shown inline");
            yield return TestDriver.WaitSeconds(0.5f);
            yield return TestDriver.Capture(Folder + "/09-login-server-validation.png");
            TestDriver.EndCaptureMode();
        }

        /// <summary>The inline hint of an input field is the red label inside it; empty/hidden when the field is fine.</summary>
        private static Text FindHint(InputField field)
        {
            foreach (Text text in field.GetComponentsInChildren<Text>(true))
            {
                if (text.color == BlastScale.Client.UI.UiTheme.Danger) return text;
            }
            return null;
        }

        private static bool HintVisible(InputField field)
        {
            Text hint = FindHint(field);
            return hint != null && hint.gameObject.activeInHierarchy && !string.IsNullOrEmpty(hint.text);
        }

        private static string HintText(InputField field)
        {
            Text hint = FindHint(field);
            return hint != null ? hint.text : "";
        }

        private static IEnumerator WaitForVerdict(LoginScreen login)
        {
            yield return TestDriver.WaitUntil(
                () => login.ServerState != ServerHealthState.Checking && login.ServerState != ServerHealthState.Unknown,
                ServerHealth.TimeoutSeconds + 3f, "the connection probe");
        }
    }
}
