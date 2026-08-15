using System.Text.RegularExpressions;
using BlastScale.Client.Net;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// First screen: one-tap guest login (device id based) or username/password login/register.
    /// It also exposes the server URL so a phone can point at a laptop on the same network without
    /// a rebuild; the value is stored through <see cref="ClientConfig"/>.
    /// </summary>
    public sealed class LoginScreen : UiScreen
    {
        private static readonly Regex UsernamePattern = new Regex("^[a-zA-Z0-9_]+$");

        private InputField _serverUrl;
        private InputField _username;
        private InputField _password;

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 20f, 60);

            UiFactory.CreateSpacer(column);
            UiFactory.CreateLabel(column, "BlastScale", UiTheme.TitleSize, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.CreateLabel(column, "Server-authoritative blast puzzle", UiTheme.BodySize, UiTheme.Muted);
            UiFactory.CreateSpacer(column, 0.4f);

            UiFactory.CreateLabel(column, "Server URL", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleLeft);
            _serverUrl = UiFactory.CreateInputField(column, ClientConfig.DefaultBaseUrl);
            _serverUrl.text = ClientConfig.BaseUrl;

            UiFactory.CreateButton(column, "Play as guest", OnGuest, UiTheme.Accent, UiTheme.HeadingSize, 130f);

            UiFactory.CreateLabel(column, "or sign in with an account", UiTheme.SmallSize, UiTheme.Muted);
            _username = UiFactory.CreateInputField(column, "Username");
            _password = UiFactory.CreateInputField(column, "Password", true);
            RectTransform buttons = UiFactory.CreateRow(column, "AuthButtons", 110f);
            UiFactory.CreateButton(buttons, "Login", OnLogin, UiTheme.Secondary);
            UiFactory.CreateButton(buttons, "Register", OnRegister, UiTheme.Secondary);

            UiFactory.CreateSpacer(column);
            UiFactory.CreateLabel(column, "Scores are computed by the server by replaying your moves.",
                UiTheme.SmallSize, UiTheme.Muted);
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
