using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace QicBoqMapper
{
    public static class LicenseManager
    {
        private const string RegistryPath = @"Software\QicTools\QicBoqMapper";

        // Non-secret: kept in plain text for display only.
        private const string EmailValueName = "Email";

        // Secret values: stored as DPAPI-encrypted Base64. Never the plaintext.
        private const string AccessTokenEncValueName = "AccessTokenEnc";
        private const string RefreshTokenEncValueName = "RefreshTokenEnc";
        private const string AccessTokenExpiresAtUtcValueName = "AccessTokenExpiresAtUtc";

        // Entitlement state (not secret).
        private const string LastVerifiedValueName = "LastVerified";
        private const string ExpiresAtValueName = "ExpiresAt";

        // Legacy plaintext values — present in registries of users who installed
        // older builds. Read once for one-shot migration, then deleted.
        private const string LegacyCodeValueName = "ActivationCode";

        // Supabase configuration
        private const string SupabaseUrl = "https://auvtapbsdewwmzejchgq.supabase.co";
        private const string SupabaseAnonKey = "sb_publishable_LUcQW4gZYVFYGsNY-p-n0A_LSr2N4RH";

        // --------------- Activation state queries ---------------

        public static bool IsActivated()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null) return false;

                    // Must have at least a session marker (new flow) or a legacy LastVerified (legacy flow).
                    string accessEnc = key.GetValue(AccessTokenEncValueName)?.ToString() ?? string.Empty;
                    string lastVerified = key.GetValue(LastVerifiedValueName)?.ToString() ?? string.Empty;
                    string legacyCode = key.GetValue(LegacyCodeValueName)?.ToString() ?? string.Empty;

                    bool hasSomeAuth = !string.IsNullOrWhiteSpace(accessEnc)
                                       || !string.IsNullOrWhiteSpace(lastVerified)
                                       || !string.IsNullOrWhiteSpace(legacyCode);
                    if (!hasSomeAuth) return false;

                    // Enforce local license expiry if set.
                    string expiresAtStr = key.GetValue(ExpiresAtValueName)?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(expiresAtStr)
                        && DateTimeOffset.TryParse(expiresAtStr, out DateTimeOffset expiresAt)
                        && DateTimeOffset.UtcNow > expiresAt)
                    {
                        return false;
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsExpired()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null) return false;
                    string expiresAtStr = key.GetValue(ExpiresAtValueName)?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(expiresAtStr)) return false;
                    return DateTimeOffset.TryParse(expiresAtStr, out DateTimeOffset expiresAt)
                        && DateTimeOffset.UtcNow > expiresAt;
                }
            }
            catch { return false; }
        }

        public static string GetSavedEmail()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                    return key?.GetValue(EmailValueName)?.ToString() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        public static bool ValidateInput(string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code)) return false;
            bool validEmail = Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            bool validCode = code.Trim().Length >= 4;
            return validEmail && validCode;
        }

        // --------------- Session storage (DPAPI) ---------------

        private static void SaveSession(string email, string accessToken, string refreshToken, int expiresInSeconds, string? licenseExpiresAtStr)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null) throw new Exception("Cannot open registry key for write.");

                key.SetValue(EmailValueName, email ?? string.Empty);
                key.SetValue(AccessTokenEncValueName, DpapiHelper.Protect(accessToken));
                key.SetValue(RefreshTokenEncValueName, DpapiHelper.Protect(refreshToken));

                var accessExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, expiresInSeconds - 30));
                key.SetValue(AccessTokenExpiresAtUtcValueName, accessExpiresAt.ToString("O"));

                key.SetValue(LastVerifiedValueName, DateTimeOffset.UtcNow.ToString("O"));

                if (!string.IsNullOrWhiteSpace(licenseExpiresAtStr))
                    key.SetValue(ExpiresAtValueName, licenseExpiresAtStr!);
                else
                    try { key.DeleteValue(ExpiresAtValueName, false); } catch { }

                // Wipe the legacy plaintext code now that we have an encrypted session.
                try { key.DeleteValue(LegacyCodeValueName, false); } catch { }
            }
        }

        public static void SignOut()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null) return;
                    foreach (var name in new[] {
                        AccessTokenEncValueName, RefreshTokenEncValueName, AccessTokenExpiresAtUtcValueName,
                        LastVerifiedValueName, ExpiresAtValueName, LegacyCodeValueName
                    })
                    {
                        try { key.DeleteValue(name, false); } catch { }
                    }
                    // Email is left in place so the form pre-fills next time.
                }
            }
            catch { }
        }

        private static string? ReadAccessTokenIfValid()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null) return null;
                    string enc = key.GetValue(AccessTokenEncValueName)?.ToString() ?? string.Empty;
                    string expStr = key.GetValue(AccessTokenExpiresAtUtcValueName)?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(enc) || string.IsNullOrWhiteSpace(expStr)) return null;
                    if (!DateTimeOffset.TryParse(expStr, out DateTimeOffset exp)) return null;
                    if (DateTimeOffset.UtcNow >= exp) return null;
                    string token = DpapiHelper.Unprotect(enc);
                    return string.IsNullOrWhiteSpace(token) ? null : token;
                }
            }
            catch { return null; }
        }

        private static string? ReadRefreshToken()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    string enc = key?.GetValue(RefreshTokenEncValueName)?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(enc)) return null;
                    string token = DpapiHelper.Unprotect(enc);
                    return string.IsNullOrWhiteSpace(token) ? null : token;
                }
            }
            catch { return null; }
        }

        // --------------- Supabase Auth calls ---------------

        // Attempts password sign-in. On 400 ("Invalid login credentials") tries
        // sign-up then sign-in once (covers legacy customers whose auth.users
        // row was never created). Returns the access token + entitlement.
        // Throws on hard failures (network, server 5xx, missing license row).
        public static async Task<SignInResult> SignInAsync(string email, string password)
        {
            if (!ValidateInput(email, password))
                throw new Exception("Please enter a valid email and activation code.");

            string cleanEmail = email.Trim().ToLowerInvariant();
            string cleanPassword = password.Trim();

            var session = await PasswordSignInAsync(cleanEmail, cleanPassword).ConfigureAwait(false);
            if (session == null)
            {
                // Backward-compat: confirm the licenses table has a row for this
                // email+code first (so we never auto-create auth users for
                // randoms), then try sign-up + sign-in.
                var ent = await LookupEntitlementWithAnonAsync(cleanEmail, cleanPassword).ConfigureAwait(false);
                if (!ent.IsValid)
                    throw new Exception("Invalid, inactive, or expired license.");

                bool signedUp = await SignUpAsync(cleanEmail, cleanPassword).ConfigureAwait(false);
                if (!signedUp)
                    throw new Exception("Could not create auth user. Make sure user sign-ups are enabled in your Supabase project (Authentication → Providers → Email).");

                session = await PasswordSignInAsync(cleanEmail, cleanPassword).ConfigureAwait(false);
                if (session == null)
                    throw new Exception("Sign-in failed after sign-up. If your Supabase project requires email confirmation, disable it in Authentication → Settings.");

                SaveSession(cleanEmail, session.AccessToken, session.RefreshToken, session.ExpiresInSeconds, ent.LicenseExpiresAtStr);
                return new SignInResult(true, ent.LicenseExpiresAtStr);
            }

            // Auth OK — query entitlement using the user's own JWT (works whether
            // RLS is on or off).
            var entitlement = await LookupEntitlementWithBearerAsync(cleanEmail, session.AccessToken).ConfigureAwait(false);
            if (!entitlement.IsValid)
                throw new Exception("Authentication succeeded but no active license was found for this account.");

            SaveSession(cleanEmail, session.AccessToken, session.RefreshToken, session.ExpiresInSeconds, entitlement.LicenseExpiresAtStr);
            return new SignInResult(true, entitlement.LicenseExpiresAtStr);
        }

        private static async Task<TokenBundle?> PasswordSignInAsync(string email, string password)
        {
            string url = $"{SupabaseUrl.TrimEnd('/')}/auth/v1/token?grant_type=password";
            string body = "{\"email\":\"" + JsonEscape(email) + "\",\"password\":\"" + JsonEscape(password) + "\"}";

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                var resp = await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    // 400 = invalid credentials. Caller may try sign-up fallback.
                    if ((int)resp.StatusCode == 400) return null;
                    throw new Exception($"Supabase auth error ({(int)resp.StatusCode}): {text}");
                }
                return ParseTokenBundle(text);
            }
        }

        private static async Task<bool> SignUpAsync(string email, string password)
        {
            string url = $"{SupabaseUrl.TrimEnd('/')}/auth/v1/signup";
            string body = "{\"email\":\"" + JsonEscape(email) + "\",\"password\":\"" + JsonEscape(password) + "\"}";

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                var resp = await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (resp.IsSuccessStatusCode) return true;
                // "User already registered" is fine for our purposes.
                if (text.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return false;
            }
        }

        // Refreshes the access token using the saved refresh token. Returns true
        // on success. Saves the new tokens back to the registry.
        public static async Task<bool> RefreshSessionAsync()
        {
            string? refresh = ReadRefreshToken();
            string email = GetSavedEmail();
            if (string.IsNullOrWhiteSpace(refresh) || string.IsNullOrWhiteSpace(email)) return false;

            string url = $"{SupabaseUrl.TrimEnd('/')}/auth/v1/token?grant_type=refresh_token";
            string body = "{\"refresh_token\":\"" + JsonEscape(refresh!) + "\"}";

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
                {
                    client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                    var resp = await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) return false;
                    string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var bundle = ParseTokenBundle(text);
                    if (bundle == null) return false;

                    string? existingExpiresAt;
                    using (RegistryKey rkey = Registry.CurrentUser.OpenSubKey(RegistryPath))
                        existingExpiresAt = rkey?.GetValue(ExpiresAtValueName)?.ToString();

                    SaveSession(email, bundle.AccessToken, bundle.RefreshToken, bundle.ExpiresInSeconds, existingExpiresAt);
                    return true;
                }
            }
            catch { return false; }
        }

        // Returns a valid access token, refreshing if needed. null if there is
        // no session or refresh failed.
        public static async Task<string?> EnsureAccessTokenAsync()
        {
            string? current = ReadAccessTokenIfValid();
            if (current != null) return current;
            if (await RefreshSessionAsync().ConfigureAwait(false)) return ReadAccessTokenIfValid();
            return null;
        }

        // --------------- Entitlement (licenses table) ---------------

        private static async Task<EntitlementResult> LookupEntitlementWithAnonAsync(string email, string code)
        {
            string encodedEmail = Uri.EscapeDataString(email);
            string encodedCode = Uri.EscapeDataString(code);
            string url = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/licenses?email=ilike.{encodedEmail}&activation_code=eq.{encodedCode}&select=*";

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");
                var resp = await client.GetAsync(url).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return EntitlementResult.Invalid;
                string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ParseEntitlement(text);
            }
        }

        private static async Task<EntitlementResult> LookupEntitlementWithBearerAsync(string email, string bearer)
        {
            string encodedEmail = Uri.EscapeDataString(email);
            string url = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/licenses?email=ilike.{encodedEmail}&select=*";

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearer}");
                var resp = await client.GetAsync(url).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return EntitlementResult.Invalid;
                string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ParseEntitlement(text);
            }
        }

        private static EntitlementResult ParseEntitlement(string json)
        {
            string trimmed = json.Trim();
            if (!trimmed.StartsWith("[") || trimmed == "[]") return EntitlementResult.Invalid;

            // Reject inactive rows
            if (trimmed.Contains("\"status\"")
                && !trimmed.Contains("\"status\":\"active\"")
                && !trimmed.Contains("\"status\": \"active\""))
                return EntitlementResult.Invalid;

            string? expiresAtStr = null;
            var m = Regex.Match(trimmed, "\"expires_at\"\\s*:\\s*\"([^\"]+)\"");
            if (m.Success)
            {
                expiresAtStr = m.Groups[1].Value;
                if (DateTimeOffset.TryParse(expiresAtStr, out DateTimeOffset exp) && DateTimeOffset.UtcNow > exp)
                    return EntitlementResult.Invalid;
            }
            return new EntitlementResult(true, expiresAtStr);
        }

        // --------------- Trial ---------------

        // Server creates a trial row; client then signs up + signs in so the
        // session-based flow takes over. Returns (email, activationCode, expiresAtStr).
        public static async Task<Tuple<string, string, string?>> StartTrialAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                throw new Exception("Please enter a valid email address.");

            string cleanEmail = email.Trim().ToLowerInvariant();
            string url = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/rpc/start_trial";
            string body = "{\"p_email\":\"" + JsonEscape(cleanEmail) + "\"}";

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");
                var resp = await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    if (text.Contains("trial_already_used"))
                        throw new Exception("A trial has already been used for this email. Please request a paid activation code.");
                    if (text.Contains("invalid_email"))
                        throw new Exception("Please enter a valid email address.");
                    throw new Exception($"Trial request failed ({(int)resp.StatusCode}). {text}");
                }

                var codeMatch = Regex.Match(text, "\"activation_code\"\\s*:\\s*\"([^\"]+)\"");
                var expMatch = Regex.Match(text, "\"expires_at\"\\s*:\\s*\"([^\"]+)\"");
                if (!codeMatch.Success)
                    throw new Exception("Unexpected server response. Please try again later.");

                string code = codeMatch.Groups[1].Value;
                string? expiresAtStr = expMatch.Success ? expMatch.Groups[1].Value : null;
                return Tuple.Create<string, string, string?>(cleanEmail, code, expiresAtStr);
            }
        }

        // --------------- One-shot legacy migration ---------------

        // If the registry still has the old plaintext ActivationCode, use it to
        // sign in (creating the auth user if needed), then wipe the plaintext.
        // Safe to call repeatedly — no-op once migration succeeds.
        public static async Task<bool> MigrateLegacyPlaintextAsync()
        {
            try
            {
                string email, legacyCode;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null) return false;
                    email = key.GetValue(EmailValueName)?.ToString() ?? string.Empty;
                    legacyCode = key.GetValue(LegacyCodeValueName)?.ToString() ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(legacyCode)) return false;

                await SignInAsync(email, legacyCode).ConfigureAwait(false); // SaveSession deletes the legacy key on success
                return true;
            }
            catch
            {
                // Migration must never bother the user. They'll be asked to
                // re-enter credentials on next interactive use.
                return false;
            }
        }

        // --------------- Helpers ---------------

        private static string JsonEscape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        private static TokenBundle? ParseTokenBundle(string json)
        {
            var access = Regex.Match(json, "\"access_token\"\\s*:\\s*\"([^\"]+)\"");
            var refresh = Regex.Match(json, "\"refresh_token\"\\s*:\\s*\"([^\"]+)\"");
            var expires = Regex.Match(json, "\"expires_in\"\\s*:\\s*(\\d+)");
            if (!access.Success || !refresh.Success) return null;
            int expiresIn = expires.Success && int.TryParse(expires.Groups[1].Value, out int n) ? n : 3600;
            return new TokenBundle(access.Groups[1].Value, refresh.Groups[1].Value, expiresIn);
        }

        // --------------- Types ---------------

        public sealed class SignInResult
        {
            public bool Success { get; }
            public string? LicenseExpiresAtStr { get; }
            public SignInResult(bool success, string? licenseExpiresAtStr)
            {
                Success = success;
                LicenseExpiresAtStr = licenseExpiresAtStr;
            }
        }

        private sealed class TokenBundle
        {
            public string AccessToken { get; }
            public string RefreshToken { get; }
            public int ExpiresInSeconds { get; }
            public TokenBundle(string a, string r, int s) { AccessToken = a; RefreshToken = r; ExpiresInSeconds = s; }
        }

        private struct EntitlementResult
        {
            public bool IsValid;
            public string? LicenseExpiresAtStr;
            public EntitlementResult(bool valid, string? expiresAtStr) { IsValid = valid; LicenseExpiresAtStr = expiresAtStr; }
            public static EntitlementResult Invalid => new EntitlementResult(false, null);
        }
    }
}
