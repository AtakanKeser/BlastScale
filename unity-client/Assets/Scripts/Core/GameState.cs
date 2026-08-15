using System;
using BlastScale.Client.Net.Dto;
using UnityEngine;

namespace BlastScale.Client.Core
{
    /// <summary>
    /// Everything the client remembers between screens: the bearer token, the profile and wallet,
    /// the remote configuration and the level currently being played. It is a plain in-memory
    /// object — the server is the source of truth, so nothing here is persisted except the base URL
    /// and device id (see <see cref="Net.ClientConfig"/> and <see cref="DeviceIdentity"/>).
    /// </summary>
    public sealed class GameState
    {
        // ----- authentication -----
        public string Token { get; private set; }
        public string TokenExpiresAt { get; private set; }
        public long PlayerId { get; private set; }
        public string Username { get; private set; }
        public string Role { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

        // ----- profile / config -----
        public PlayerProfile Profile { get; private set; }
        public ClientConfigResponse Config { get; private set; }
        public WalletSnapshot Wallet { get; private set; }

        /// <summary>Realtime clock value when <see cref="Wallet"/> was received; drives the life countdown.</summary>
        private float _walletReceivedAt;

        /// <summary>server time minus device UTC time, learned from the config response.</summary>
        private double _serverOffsetSeconds;

        // ----- gameplay -----
        public LevelSession Session { get; set; }

        public int CurrentLevel => Profile != null ? Profile.currentLevel : 1;

        public void SetAuth(AuthResponse auth)
        {
            Token = auth.token;
            TokenExpiresAt = auth.expiresAt;
            PlayerId = auth.playerId;
            Username = auth.username;
            Role = auth.role;
        }

        public void SetProfile(PlayerProfile profile)
        {
            Profile = profile;
            if (profile != null)
            {
                Username = profile.username;
                PlayerId = profile.id;
                if (profile.wallet != null)
                {
                    SetWallet(profile.wallet);
                }
            }
        }

        public void SetConfig(ClientConfigResponse config)
        {
            Config = config;
            DateTime? serverTime = TimeFormat.ParseInstant(config != null ? config.serverTime : null);
            if (serverTime.HasValue)
            {
                _serverOffsetSeconds = (serverTime.Value - DateTime.UtcNow).TotalSeconds;
            }
        }

        /// <summary>Every response that carries a wallet refreshes the local copy (and restarts the countdown).</summary>
        public void SetWallet(WalletSnapshot wallet)
        {
            if (wallet == null)
            {
                return;
            }
            Wallet = wallet;
            _walletReceivedAt = Time.realtimeSinceStartup;
            if (Profile != null)
            {
                Profile.wallet = wallet;
            }
        }

        /// <summary>The server advanced the player; keep the local profile in step until the next refresh.</summary>
        public void AdvanceLevel(int nextLevel)
        {
            if (Profile != null && nextLevel > Profile.currentLevel)
            {
                Profile.currentLevel = nextLevel;
            }
        }

        /// <summary>Seconds until the next life, counted down locally from the last wallet snapshot.</summary>
        public long NextLifeInSecondsNow
        {
            get
            {
                if (Wallet == null || Wallet.lives >= Wallet.maxLives)
                {
                    return 0;
                }
                long elapsed = (long)(Time.realtimeSinceStartup - _walletReceivedAt);
                return Math.Max(0, Wallet.nextLifeInSeconds - elapsed);
            }
        }

        /// <summary>Owned count of a booster type ("HAMMER", ...), 0 when unknown.</summary>
        public int BoosterCount(string boosterType)
        {
            return Wallet != null ? Wallet.BoosterCount(boosterType) : 0;
        }

        /// <summary>Server "now" according to the last config fetch; falls back to the device clock.</summary>
        public DateTime ServerNowUtc => DateTime.UtcNow.AddSeconds(_serverOffsetSeconds);

        /// <summary>Seconds from server-now until an ISO instant, 0 when it is in the past or unparsable.</summary>
        public long SecondsUntil(string isoInstant)
        {
            DateTime? at = TimeFormat.ParseInstant(isoInstant);
            if (!at.HasValue) return 0;
            return Math.Max(0, (long)(at.Value - ServerNowUtc).TotalSeconds);
        }

        /// <summary>Forgets everything tied to the account (called on logout and on an expired token).</summary>
        public void Logout()
        {
            Token = null;
            TokenExpiresAt = null;
            PlayerId = 0;
            Username = null;
            Role = null;
            Profile = null;
            Config = null;
            Wallet = null;
            Session = null;
        }
    }
}
