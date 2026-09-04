namespace BlastScale.Client.Net.Dto
{
    // DTOs of /api/v1/auth/* (mirrors security/dto/*.java). Field names are the JSON names.

    /// <summary>POST /api/v1/auth/guest — the device id is the identity of a guest.</summary>
    public sealed class GuestLoginRequest
    {
        public string deviceId;
    }

    /// <summary>POST /api/v1/auth/register — username 3..32 [a-zA-Z0-9_], password 8..72.</summary>
    public sealed class RegisterRequest
    {
        public string username;
        public string password;
    }

    /// <summary>POST /api/v1/auth/login.</summary>
    public sealed class LoginRequest
    {
        public string username;
        public string password;
    }

    /// <summary>Response of all three auth endpoints; <c>token</c> goes into the Authorization header.</summary>
    public sealed class AuthResponse
    {
        public string token;
        public string expiresAt;
        public long playerId;
        public string username;
        public string role;
    }
}
