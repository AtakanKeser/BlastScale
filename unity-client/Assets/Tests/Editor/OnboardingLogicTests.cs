using System.Collections.Generic;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.UI.Screens;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BlastScale.Tests
{
    /// <summary>
    /// Pure-logic checks behind the login screen: how typed server URLs are normalised, how the
    /// form validates, and how server error codes turn into readable copy. No scene needed.
    /// </summary>
    public class OnboardingLogicTests
    {
        [TestCase("http://localhost:8080", "http://localhost:8080")]
        [TestCase("  http://localhost:8080/  ", "http://localhost:8080")]
        [TestCase("localhost:8080", "http://localhost:8080")]
        [TestCase("192.168.1.20:8080///", "http://192.168.1.20:8080")]
        [TestCase("myserver.example", "http://myserver.example")]
        [TestCase("https://blast.example.com/", "https://blast.example.com")]
        [TestCase("", "")]
        [TestCase("   ", "")]
        [TestCase(null, "")]
        [TestCase("http://", "")]
        public void NormalizeUrl_AcceptsWhatPeopleType(string typed, string expected)
        {
            Assert.AreEqual(expected, ClientConfig.NormalizeUrl(typed));
        }

        [TestCase("http://localhost:8080/", "localhost:8080")]
        [TestCase("192.168.1.20:8080", "192.168.1.20:8080")]
        [TestCase("", "")]
        public void DisplayHost_DropsTheScheme(string url, string expected)
        {
            Assert.AreEqual(expected, ClientConfig.DisplayHost(url));
        }

        [Test]
        public void UsernameRules_MirrorTheServer()
        {
            Assert.IsNull(LoginScreen.UsernameProblem("abc"));
            Assert.IsNull(LoginScreen.UsernameProblem("  Player_42  "));
            Assert.IsNotNull(LoginScreen.UsernameProblem(""));
            Assert.IsNotNull(LoginScreen.UsernameProblem("ab"));
            Assert.IsNotNull(LoginScreen.UsernameProblem(new string('a', 33)));
            Assert.IsNotNull(LoginScreen.UsernameProblem("no spaces"));
            Assert.IsNotNull(LoginScreen.UsernameProblem("dash-not-allowed"));
        }

        [Test]
        public void PasswordRules_MirrorTheServer()
        {
            Assert.IsNull(LoginScreen.PasswordProblem("12345678"));
            Assert.IsNull(LoginScreen.PasswordProblem(new string('x', 72)));
            Assert.IsNotNull(LoginScreen.PasswordProblem(""));
            Assert.IsNotNull(LoginScreen.PasswordProblem("1234567"));
            Assert.IsNotNull(LoginScreen.PasswordProblem(new string('x', 73)));
        }

        [Test]
        public void ServerUnreachable_CoversTransportAndNonBlastScaleAnswers()
        {
            Assert.IsTrue(GameFlow.IsServerUnreachable(new ApiException(ApiException.NetworkErrorCode, "timeout", 0, "/x", null)));
            Assert.IsTrue(GameFlow.IsServerUnreachable(new ApiException("INTERNAL_ERROR", "boom", 500, "/x", null)));
            Assert.IsTrue(GameFlow.IsServerUnreachable(new ApiException(ApiException.ParseErrorCode, "html", 200, "/x", null)));
            Assert.IsTrue(GameFlow.IsServerUnreachable(new ApiException("HTTP_404", "nginx", 404, "/x", null)));
            Assert.IsFalse(GameFlow.IsServerUnreachable(new ApiException("INVALID_CREDENTIALS", "no", 401, "/x", null)));
            Assert.IsFalse(GameFlow.IsServerUnreachable(new ApiException("USERNAME_TAKEN", "no", 409, "/x", null)));
            Assert.IsFalse(GameFlow.IsServerUnreachable(null));
        }

        [Test]
        public void FriendlyAuthMessage_MapsTheBusinessCodes()
        {
            Assert.That(GameFlow.FriendlyAuthMessage(new ApiException("USERNAME_TAKEN", "Username 'x' is already taken", 409, "/x", null)), Does.Contain("already taken"));
            Assert.That(GameFlow.FriendlyAuthMessage(new ApiException("INVALID_CREDENTIALS", "Invalid username or password", 401, "/x", null)), Does.Contain("Wrong username or password"));
            var limited = new ApiException("RATE_LIMITED", "Too many requests, slow down", 429, "/x",
                new Dictionary<string, JToken> { { "limitPerMinute", new JValue(30) } });
            Assert.That(GameFlow.FriendlyAuthMessage(limited), Does.Contain("30 per minute"));
            Assert.IsNull(GameFlow.FriendlyAuthMessage(new ApiException("NO_LIVES_LEFT", "none", 409, "/x", null)), "other codes keep the server's message");
        }

        [Test]
        public void ValidationError_ExposesPerFieldMessages()
        {
            var error = new ApiException("VALIDATION_ERROR", "Request validation failed", 400, "/api/v1/auth/register",
                new Dictionary<string, JToken>
                {
                    { "username", new JValue("size must be between 3 and 32") },
                    { "password", new JValue("must not be blank") }
                });
            Dictionary<string, string> fields = GameFlow.FieldMessages(error);
            Assert.AreEqual("size must be between 3 and 32", fields["username"]);
            Assert.AreEqual("must not be blank", fields["password"]);
            string friendly = GameFlow.FriendlyAuthMessage(error);
            Assert.That(friendly, Does.Contain("Username: size must be between 3 and 32"));
            Assert.That(friendly, Does.Contain("Password: must not be blank"));
            Assert.AreEqual(0, GameFlow.FieldMessages(new ApiException("USERNAME_TAKEN", "x", 409, "/x", null)).Count);
        }
    }
}
