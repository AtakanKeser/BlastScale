using System.Text.RegularExpressions;
using BlastScale.Client.Net;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// First screen: one-tap guest login (device id based), username/password login/register, and
    /// the "Offline demo" that plays against the local engine without any server. It also exposes
    /// the server URL so a phone can point at a laptop on the same network without a rebuild; the
    /// value is stored through <see cref="ClientConfig"/>.
    /// </summary>
    public sealed class LoginScreen : UiScreen
    {
        private static readonly Regex UsernamePattern = new Regex("^[a-zA-Z0-9_]+$");

        private InputField _serverUrl;
        private InputField _username;
        private InputField _password;
        private RectTransform _logoRow;
        private Text _title;

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 14f, 56, 40, 40);

            UiFactory.CreateSpacer(column, 0.8f);
            _title = UiFactory.CreateTitle(column, "BlastScale", UiTheme.TitleSize + 8, UiTheme.Text);
            UiFactory.AddOutline(_title, 3f, new Color(0.2f, 0.05f, 0.35f, 0.6f));
            UiFactory.CreateLabel(column, "Server-authoritative blast puzzle", UiTheme.BodySize - 2, UiTheme.TextSoft, TextAnchor.MiddleCenter, UiFont.BodyBold);
            BuildLogoBlocks(column);
            UiFactory.CreateSpacer(column, 0.5f);

            RectTransform card = UiFactory.CreateCard(column, "LoginCard", UiTheme.CardRadius, 32, 12);
            UiFactory.CreateLabel(card, "Server URL", UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.BodyBold);
            _serverUrl = UiFactory.CreateInputField(card, ClientConfig.DefaultBaseUrl, false, 96f);
            _serverUrl.text = ClientConfig.BaseUrl;
            UiFactory.CreateGap(card, 2f);
            UiFactory.CreateButton(card, "Play as guest", OnGuest, ButtonStyle.Primary, UiTheme.HeadingSize - 6, UiTheme.ButtonHeight, -1f, IconFactory.Play());

            RectTransform orRow = UiFactory.CreateRow(card, "Or", 40f, 16f);
            UiFactory.SetLayout(UiFactory.CreateDivider(orRow).gameObject, flexibleWidth: 1f, preferredHeight: 2f);
            UiFactory.CreateLabel(orRow, "or sign in", UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.BodyBold);
            UiFactory.SetLayout(UiFactory.CreateDivider(orRow).gameObject, flexibleWidth: 1f, preferredHeight: 2f);

            _username = UiFactory.CreateInputField(card, "Username", false, 96f);
            _password = UiFactory.CreateInputField(card, "Password", true, 96f);
            RectTransform buttons = UiFactory.CreateRow(card, "AuthButtons", UiTheme.ButtonHeight - 10f, 14f);
            UiFactory.CreateButton(buttons, "Login", OnLogin, ButtonStyle.Blue, UiTheme.BodySize, UiTheme.ButtonHeight - 10f);
            UiFactory.CreateButton(buttons, "Register", OnRegister, ButtonStyle.Secondary, UiTheme.BodySize, UiTheme.ButtonHeight - 10f);

            UiFactory.CreateGap(column, 4f);
            UiFactory.CreateButton(column, "Offline demo", OnOfflineDemo, ButtonStyle.Ghost, UiTheme.BodySize, UiTheme.ButtonHeight - 10f, -1f, IconFactory.Bolt(), UiTheme.Gold);
            UiFactory.CreateLabel(column, "Plays levels on this device without a server. Progress is local only.",
                UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.Body);

            UiFactory.CreateSpacer(column);
            UiFactory.CreateLabel(column, "Scores are computed by the server by replaying your moves.",
                UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.Body);
        }

        /// <summary>A row of the six block sprites under the title: instantly says "this is a block game".</summary>
        private void BuildLogoBlocks(RectTransform column)
        {
            _logoRow = UiFactory.CreateRow(column, "LogoBlocks", 110f, 4f);
            UiFactory.CreateSpacer(_logoRow);
            for (int i = 0; i < UiTheme.BlockColorCount; i++)
            {
                Image block = UiFactory.CreateImage(_logoRow, "Block " + i, BlockSprites.Get(i), Color.white);
                UiFactory.SetLayout(block.gameObject, preferredWidth: 110f, preferredHeight: 110f, minWidth: 110f);
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
        }

        protected override void OnDismissed()
        {
            Tween.Kill(_title.transform);
        }

        /// <summary>Persists whatever is in the URL field so every call (including this login) uses it.</summary>
        private void ApplyServerUrl()
        {
            ClientConfig.BaseUrl = _serverUrl.text;
            _serverUrl.text = ClientConfig.BaseUrl;
        }

        private void OnGuest()
        {
            ApplyServerUrl();
            Run(App.Flow.LoginAsGuest());
        }

        private void OnLogin()
        {
            ApplyServerUrl();
            if (Validate(false))
            {
                Run(App.Flow.Login(_username.text.Trim(), _password.text));
            }
        }

        private void OnRegister()
        {
            ApplyServerUrl();
            if (Validate(true))
            {
                Run(App.Flow.Register(_username.text.Trim(), _password.text));
            }
        }

        private void OnOfflineDemo()
        {
            Run(App.Flow.StartOfflineDemo());
        }

        /// <summary>Mirrors the server's bean validation so obvious mistakes never leave the device.</summary>
        private bool Validate(bool forRegister)
        {
            string username = _username.text.Trim();
            string password = _password.text;
            if (username.Length == 0 || password.Length == 0)
            {
                App.Toast.Show("Enter a username and a password", true);
                return false;
            }
            if (forRegister)
            {
                if (username.Length < 3 || username.Length > 32 || !UsernamePattern.IsMatch(username))
                {
                    App.Toast.Show("Username: 3-32 letters, digits or underscores", true);
                    return false;
                }
                if (password.Length < 8 || password.Length > 72)
                {
                    App.Toast.Show("Password: 8-72 characters", true);
                    return false;
                }
            }
            return true;
        }
    }
}
