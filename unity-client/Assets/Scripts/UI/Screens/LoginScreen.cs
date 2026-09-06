using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// First screen. It makes the three ways of trying the game obvious:
    /// <list type="bullet">
    ///   <item><b>Play as guest</b> — one tap, device-id based account, needs the server;</item>
    ///   <item><b>Sign in / Register</b> — username and password, needs the server;</item>
    ///   <item><b>Offline demo</b> — plays against the local engine, no server, progress on this device.</item>
    /// </list>
    /// The server URL field is editable so a phone can point at a laptop on the same Wi-Fi without
    /// a rebuild (stored through <see cref="ClientConfig"/>). Under it a status pill probes the
    /// public liveness endpoint (<see cref="ServerHealth"/>) whenever the screen opens or the URL
    /// changes, so a tester immediately sees whether "docker compose up" is running. Form mistakes
    /// are shown inline next to the fields, and the server's own error codes are mapped to plain
    /// language by <see cref="GameFlow"/>.
    /// </summary>
    public sealed class LoginScreen : UiScreen
    {
        /// <summary>Same rules as the server's RegisterRequest bean validation.</summary>
        private static readonly Regex UsernamePattern = new Regex("^[a-zA-Z0-9_]+$");
        private const int UsernameMin = 3;
        private const int UsernameMax = 32;
        private const int PasswordMin = 8;
        private const int PasswordMax = 72;

        /// <summary>Typing pauses this long before the URL is probed again.</summary>
        private const float ProbeDebounceSeconds = 0.6f;

        private InputField _serverUrl;
        private InputField _username;
        private InputField _password;
        private Text _usernameHint;
        private Text _passwordHint;
        private Image _usernameBorder;
        private Image _passwordBorder;
        private Button _registerButton;
        private RectTransform _card;
        private RectTransform _logoRow;
        private Text _title;

        // ----- connection status -----
        private RectTransform _statusPill;
        private Image _statusBody;
        private Image _statusDot;
        private Text _statusLabel;
        private Button _retryButton;
        private Text _noServerHint;
        private Coroutine _scheduledProbe;
        private int _probeSerial;

        private bool _usernameTouched;
        private bool _passwordTouched;

        /// <summary>What the last probe of the current URL found (tests read this).</summary>
        public ServerHealthState ServerState { get; private set; } = ServerHealthState.Unknown;

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 14f, 56, 40, 40);

            UiFactory.CreateSpacer(column, 0.6f);
            _title = UiFactory.CreateTitle(column, "BlastScale", UiTheme.TitleSize + 8, UiTheme.Text);
            UiFactory.AddOutline(_title, 3f, new Color(0.2f, 0.05f, 0.35f, 0.6f));
            UiFactory.CreateLabel(column, "Server-authoritative blast puzzle", UiTheme.BodySize - 2, UiTheme.TextSoft, TextAnchor.MiddleCenter, UiFont.BodyBold);
            BuildLogoBlocks(column);
            UiFactory.CreateSpacer(column, 0.4f);

            _card = UiFactory.CreateCard(column, "LoginCard", UiTheme.CardRadius, 32, 12);
            BuildServerSection(_card);
            UiFactory.CreateGap(_card, 2f);
            UiFactory.CreateButton(_card, "Play as guest", OnGuest, ButtonStyle.Primary, UiTheme.HeadingSize - 6, UiTheme.ButtonHeight, -1f, IconFactory.Play());

            RectTransform orRow = UiFactory.CreateRow(_card, "Or", 40f, 16f);
            UiFactory.SetLayout(UiFactory.CreateDivider(orRow).gameObject, flexibleWidth: 1f, preferredHeight: 2f);
            UiFactory.CreateLabel(orRow, "or sign in / register", UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.BodyBold);
            UiFactory.SetLayout(UiFactory.CreateDivider(orRow).gameObject, flexibleWidth: 1f, preferredHeight: 2f);

            _username = UiFactory.CreateInputField(_card, "Username", false, 92f);
            _username.characterLimit = UsernameMax;
            _usernameHint = CreateInlineHint(_username, out _usernameBorder);
            _password = UiFactory.CreateInputField(_card, "Password", true, 92f);
            _password.characterLimit = PasswordMax;
            _passwordHint = CreateInlineHint(_password, out _passwordBorder);
            RectTransform buttons = UiFactory.CreateRow(_card, "AuthButtons", UiTheme.ButtonHeight - 10f, 14f);
            UiFactory.CreateButton(buttons, "Sign in", OnLogin, ButtonStyle.Blue, UiTheme.BodySize, UiTheme.ButtonHeight - 10f);
            _registerButton = UiFactory.CreateButton(buttons, "Register", OnRegister, ButtonStyle.Secondary, UiTheme.BodySize, UiTheme.ButtonHeight - 10f);

            UiFactory.CreateGap(column, 4f);
            UiFactory.CreateButton(column, "Offline demo", OnOfflineDemo, ButtonStyle.Ghost, UiTheme.BodySize, UiTheme.ButtonHeight - 10f, -1f, IconFactory.Bolt(), UiTheme.Gold);
            UiFactory.CreateLabel(column, "No server needed · plays on this device, progress stays here",
                UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.Body);

            UiFactory.CreateSpacer(column);
            UiFactory.CreateLabel(column, "Scores are computed by the server by replaying your moves.",
                UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.Body);

            _username.text = PlayerPrefs.GetString(GameFlow.LastUsernameKey, "");
            _username.onValueChanged.AddListener(_ => { _usernameTouched = true; RefreshFormHints(); });
            _password.onValueChanged.AddListener(_ => { _passwordTouched = true; RefreshFormHints(); });
            RefreshFormHints();
        }

        /// <summary>
        /// "Play online" header with the help button, the URL field, the connection status pill
        /// (+ Retry) and the two hints that tell a tester which URL to use and what to do when
        /// nothing answers.
        /// </summary>
        private void BuildServerSection(RectTransform card)
        {
            RectTransform header = UiFactory.CreateRow(card, "ServerHeader", 56f, 12f, TextAnchor.MiddleLeft);
            Text heading = UiFactory.CreateLabel(header, "Play online · needs the server", UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.BodyBold);
            UiFactory.SetLayout(heading.gameObject, flexibleWidth: 1f);
            UiFactory.CreateIconButton(header, "Help", IconFactory.Question(), OnHelp, ButtonStyle.Ghost, 56f, null, 0.64f);

            _serverUrl = UiFactory.CreateInputField(card, "Server URL", false, 96f);
            _serverUrl.text = ClientConfig.BaseUrl;
            _serverUrl.onValueChanged.AddListener(_ => ScheduleProbe());
            _serverUrl.onEndEdit.AddListener(_ => { ApplyServerUrl(); ProbeNow(); });

            RectTransform statusRow = UiFactory.CreateRow(card, "StatusRow", 48f, 12f, TextAnchor.MiddleLeft);
            _statusPill = UiFactory.CreateRect(statusRow, "ServerStatus");
            UiFactory.AddHorizontalLayout(_statusPill, 10f, new RectOffset(18, 24, 0, 0), TextAnchor.MiddleLeft);
            UiFactory.SetLayout(_statusPill.gameObject, preferredHeight: 48f, minHeight: 48f, flexibleWidth: 0f, flexibleHeight: 0f);
            _statusBody = UiFactory.CreateImage(_statusPill, "Body", SpriteFactory.RoundedRect(24f), UiTheme.CardFill);
            UiFactory.Stretch(_statusBody.rectTransform);
            UiFactory.IgnoreLayout(_statusBody.rectTransform);
            _statusDot = UiFactory.CreateIcon(_statusPill, SpriteFactory.Circle(64), UiTheme.Muted, 18f);
            _statusLabel = UiFactory.CreateLabel(_statusPill, "", UiTheme.TinySize, UiTheme.TextSoft, TextAnchor.MiddleLeft, UiFont.BodyBold);
            _statusLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _retryButton = UiFactory.CreateButton(statusRow, "Retry", ProbeNow, ButtonStyle.Ghost, UiTheme.TinySize, 48f, 140f);
            UiFactory.CreateSpacer(statusRow);

            Text hint = UiFactory.CreateLabel(card, "Same computer: http://localhost:8080  ·  Phone on the same Wi-Fi: http://<computer IP>:8080",
                UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.Body);
            hint.supportRichText = false; // "<computer IP>" must stay literal text
            _noServerHint = UiFactory.CreateLabel(card, "Start it with <b>docker compose up</b> in the repository, or pick Offline demo below.",
                UiTheme.TinySize, UiTheme.Amber, TextAnchor.MiddleLeft, UiFont.Body);
            _noServerHint.gameObject.SetActive(false);
            SetStatus(ServerHealthState.Unknown, null);
        }

        /// <summary>
        /// A small red message drawn inside the right edge of an input field (no layout shift) and
        /// the field's border, which turns red with it. Hidden while the field is fine.
        /// </summary>
        private static Text CreateInlineHint(InputField field, out Image border)
        {
            border = field.transform.Find("Border").GetComponent<Image>();
            Text hint = UiFactory.CreateLabel(field.transform, "", UiTheme.TinySize, UiTheme.Danger, TextAnchor.MiddleRight, UiFont.BodyBold);
            UiFactory.Stretch(hint.rectTransform, 30f, 26f, 8f, 8f);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.gameObject.SetActive(false);
            return hint;
        }

        /// <summary>A row of the six block sprites under the title: instantly says "this is a block game".</summary>
        private void BuildLogoBlocks(RectTransform column)
        {
            _logoRow = UiFactory.CreateRow(column, "LogoBlocks", 100f, 4f);
            UiFactory.CreateSpacer(_logoRow);
            for (int i = 0; i < UiTheme.BlockColorCount; i++)
            {
                Image block = UiFactory.CreateImage(_logoRow, "Block " + i, BlockSprites.Get(i), Color.white);
                UiFactory.SetLayout(block.gameObject, preferredWidth: 100f, preferredHeight: 100f, minWidth: 100f);
            }
            UiFactory.CreateSpacer(_logoRow);
        }

        protected override void OnShown()
        {
            // The blocks bounce in one after another; the title breathes gently.
            int index = 0;
            foreach (Transform child in _logoRow)
            {
                if (child.GetComponent<Image>() == null) continue;
                Tween.ScaleFrom(child, 0f, 0.5f, Ease.OutBack, 0.15f + index * 0.07f);
                index++;
            }
            Tween.Pulse(_title.transform, 0.025f, 2.2f);
            ProbeNow();
        }

        protected override void OnDismissed()
        {
            Tween.Kill(_title.transform);
        }

        // ------------------------------------------------------------------ connection status

        /// <summary>The URL as it will be used: normalised, but not yet persisted.</summary>
        private string TypedUrl
        {
            get
            {
                string normalized = ClientConfig.NormalizeUrl(_serverUrl.text);
                return normalized.Length == 0 ? ClientConfig.DefaultBaseUrl : normalized;
            }
        }

        /// <summary>Waits for a pause in typing, then probes (every keystroke restarts the wait).</summary>
        private void ScheduleProbe()
        {
            if (!IsAlive) return;
            if (_scheduledProbe != null)
            {
                App.Runner.StopCoroutine(_scheduledProbe);
            }
            _scheduledProbe = Run(ProbeAfterPause());
        }

        private IEnumerator ProbeAfterPause()
        {
            yield return new WaitForSecondsRealtime(ProbeDebounceSeconds);
            _scheduledProbe = null;
            ProbeNow();
        }

        /// <summary>Probes the typed URL right away; an older probe still in flight is ignored when it answers.</summary>
        private void ProbeNow()
        {
            if (!IsAlive) return;
            int serial = ++_probeSerial;
            string url = TypedUrl;
            SetStatus(ServerHealthState.Checking, url);
            Run(ServerHealth.Probe(url, (state, detail) =>
            {
                if (!IsAlive || serial != _probeSerial) return;
                SetStatus(state, url);
            }));
        }

        /// <summary>Repaints the pill, the Retry button and the amber hint for a state.</summary>
        private void SetStatus(ServerHealthState state, string url)
        {
            ServerState = state;
            string host = ClientConfig.DisplayHost(url ?? TypedUrl);
            Color accent;
            string text;
            switch (state)
            {
                case ServerHealthState.Reachable:
                    accent = UiTheme.Primary;
                    text = "Connected · " + host;
                    break;
                case ServerHealthState.Unreachable:
                    accent = UiTheme.Amber;
                    text = "No server at " + host;
                    break;
                case ServerHealthState.Blocked:
                    accent = UiTheme.Amber;
                    text = "http to " + host + " is blocked by Unity";
                    break;
                default:
                    accent = UiTheme.Muted;
                    text = "Checking server...";
                    break;
            }
            bool unreachable = state == ServerHealthState.Unreachable || state == ServerHealthState.Blocked;
            _statusDot.color = accent;
            _statusBody.color = unreachable || state == ServerHealthState.Reachable ? UiTheme.WithAlpha(accent, 0.18f) : UiTheme.CardFill;
            _statusLabel.text = text;
            _statusLabel.color = unreachable ? UiTheme.Amber : UiTheme.TextSoft;
            _retryButton.gameObject.SetActive(unreachable);
            _noServerHint.text = state == ServerHealthState.Blocked
                ? "Player Settings > Other Settings > <b>Allow downloads over HTTP</b> must be \"Always allowed\" for a plain-http server (or use https)."
                : "Start it with <b>docker compose up</b> in the repository, or pick Offline demo below.";
            _noServerHint.gameObject.SetActive(unreachable);
            if (state == ServerHealthState.Reachable)
            {
                Tween.Punch(_statusDot.transform, 0.35f, 0.35f);
            }
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Persists whatever is in the URL field so every call (including this login) uses it.</summary>
        private void ApplyServerUrl()
        {
            ClientConfig.BaseUrl = _serverUrl.text;
            string applied = ClientConfig.BaseUrl;
            if (_serverUrl.text != applied)
            {
                _serverUrl.SetTextWithoutNotify(applied);
            }
        }

        private void OnGuest()
        {
            ApplyServerUrl();
            Run(App.Flow.LoginAsGuest(OnAuthFailed));
        }

        private void OnLogin()
        {
            ApplyServerUrl();
            _usernameTouched = _passwordTouched = true;
            if (Validate(false))
            {
                Run(App.Flow.Login(_username.text.Trim(), _password.text, OnAuthFailed));
            }
        }

        private void OnRegister()
        {
            ApplyServerUrl();
            _usernameTouched = _passwordTouched = true;
            if (Validate(true))
            {
                Run(App.Flow.Register(_username.text.Trim(), _password.text, OnAuthFailed));
            }
        }

        private void OnOfflineDemo()
        {
            Run(App.Flow.StartOfflineDemo());
        }

        /// <summary>The "?" button: the three ways to try the game, in one dialog.</summary>
        private void OnHelp()
        {
            App.Modal.Show("How to test BlastScale",
                "<b>Offline demo</b> — nothing needed. Tap Offline demo and play; progress stays on this device.\n\n" +
                "<b>Same machine</b> — run <b>docker compose up</b> in the repository, keep http://localhost:8080 " +
                "and tap Play as guest (or register an account).\n\n" +
                "<b>Phone</b> — same Wi-Fi as the computer: enter the computer's IP, e.g. http://192.168.1.20:8080, " +
                "and see \"Playing on an iPhone\" in the README.",
                TextAnchor.MiddleLeft,
                ModalButton.Primary("Got it", null));
        }

        /// <summary>
        /// Called by the flow when a sign-in attempt failed (the flow already showed the dialog or
        /// toast). Here the failure is reflected where the tester looks: the status pill is
        /// re-probed for an unreachable server, and field problems appear next to the fields.
        /// Public so tests can feed real server answers through the same path.
        /// </summary>
        public void OnAuthFailed(ApiException error)
        {
            if (!IsAlive || error == null) return;
            if (GameFlow.IsServerUnreachable(error))
            {
                ProbeNow();
                return;
            }
            switch (error.Code)
            {
                case "USERNAME_TAKEN":
                    ShowHint(_usernameHint, _usernameBorder, "Already taken");
                    break;
                case "INVALID_CREDENTIALS":
                    ShowHint(_passwordHint, _passwordBorder, "Wrong username or password");
                    break;
                case "VALIDATION_ERROR":
                {
                    Dictionary<string, string> fields = GameFlow.FieldMessages(error);
                    if (fields.TryGetValue("username", out string usernameMessage)) ShowHint(_usernameHint, _usernameBorder, usernameMessage);
                    if (fields.TryGetValue("password", out string passwordMessage)) ShowHint(_passwordHint, _passwordBorder, passwordMessage);
                    break;
                }
            }
            Tween.Shake(_card, 8f, 0.3f);
        }

        // ------------------------------------------------------------------ validation

        /// <summary>Client-side rule for the username, mirroring the server; null when fine.</summary>
        public static string UsernameProblem(string username)
        {
            string trimmed = (username ?? "").Trim();
            if (trimmed.Length == 0) return "Enter a username";
            if (trimmed.Length < UsernameMin || trimmed.Length > UsernameMax) return UsernameMin + "–" + UsernameMax + " characters";
            if (!UsernamePattern.IsMatch(trimmed)) return "Letters, digits and _ only";
            return null;
        }

        /// <summary>Client-side rule for the password, mirroring the server; null when fine.</summary>
        public static string PasswordProblem(string password)
        {
            string value = password ?? "";
            if (value.Length == 0) return "Enter a password";
            if (value.Length < PasswordMin || value.Length > PasswordMax) return PasswordMin + "–" + PasswordMax + " characters";
            return null;
        }

        /// <summary>
        /// Repaints the inline hints and the Register button. Hints only appear for fields the
        /// player has touched, so an untouched form is not covered in red; Register stays
        /// disabled until both fields satisfy the rules.
        /// </summary>
        private void RefreshFormHints()
        {
            string usernameProblem = UsernameProblem(_username.text);
            string passwordProblem = PasswordProblem(_password.text);
            ShowHint(_usernameHint, _usernameBorder, _usernameTouched ? usernameProblem : null);
            ShowHint(_passwordHint, _passwordBorder, _passwordTouched ? passwordProblem : null);
            UiFactory.SetButtonEnabled(_registerButton, usernameProblem == null && passwordProblem == null);
        }

        private static void ShowHint(Text hint, Image border, string message)
        {
            bool visible = !string.IsNullOrEmpty(message);
            hint.text = visible ? message : "";
            hint.gameObject.SetActive(visible);
            border.color = visible ? UiTheme.WithAlpha(UiTheme.Danger, 0.8f) : UiTheme.CardBorder;
        }

        /// <summary>Obvious mistakes never leave the device; signing in only needs both fields filled.</summary>
        private bool Validate(bool forRegister)
        {
            string username = _username.text.Trim();
            string password = _password.text;
            if (forRegister)
            {
                RefreshFormHints();
                bool ok = UsernameProblem(username) == null && PasswordProblem(password) == null;
                if (!ok) App.Toast.Show("Please fix the highlighted fields", true);
                return ok;
            }
            bool filled = username.Length > 0 && password.Length > 0;
            ShowHint(_usernameHint, _usernameBorder, username.Length == 0 ? "Enter your username" : null);
            ShowHint(_passwordHint, _passwordBorder, password.Length == 0 ? "Enter your password" : null);
            if (!filled) App.Toast.Show("Enter your username and password", true);
            return filled;
        }
    }
}
