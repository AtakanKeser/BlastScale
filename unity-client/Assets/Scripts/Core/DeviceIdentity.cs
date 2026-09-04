using System;
using UnityEngine;

namespace BlastScale.Client.Core
{
    /// <summary>
    /// The stable identifier sent to <c>POST /auth/guest</c>. Unity's device id is used when the
    /// platform provides one; otherwise a GUID is generated once and kept in PlayerPrefs so the
    /// guest account survives restarts. The server requires 8..128 characters.
    /// </summary>
    public static class DeviceIdentity
    {
        private const string PrefKey = "blastscale.deviceId";
        private const int MinLength = 8;
        private const int MaxLength = 128;

        public static string Get()
        {
            string id = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier || id.Length < MinLength)
            {
                id = PlayerPrefs.GetString(PrefKey, "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(PrefKey, id);
                    PlayerPrefs.Save();
                }
            }
            return id.Length > MaxLength ? id.Substring(0, MaxLength) : id;
        }
    }
}
