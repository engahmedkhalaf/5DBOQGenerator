using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace RuknBoqMapper
{
    public static class LicenseManager
    {
        private const string RegistryPath = @"Software\RuknTools\RuknBoqMapper";

        // Non-secret: stored as-is for display.
        private const string EmailValueName = "Email";

        // Secret: stored as DPAPI-encrypted Base64. Never plaintext.
        private const string CodeEncValueName = "ActivationCodeEnc";

        // Entitlement state (non-secret).
        private const string LastVerifiedValueName = "LastVerified";
        private const string ExpiresAtValueName = "ExpiresAt";

        // Legacy plaintext value left behind by older builds. Read once for
        // one-shot re-encryption migration, then deleted.
        private const string LegacyCodeValueName = "ActivationCode";

        // Supabase configuration
        private const string SupabaseUrl = "https://auvtapbsdewwmzejchgq.supabase.co";
        private const string SupabaseAnonKey = "sb_publishable_LUcQW4gZYVFYGsNY-p-n0A_LSr2N4RH";

        // --------------- State queries ---------------

        public static bool IsActivated()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null) return false;

                    bool hasEncrypted = !string.IsNullOrWhiteSpace(key.GetValue(CodeEncValueName)?.ToString());
                    bool hasLegacy = !string.IsNullOrWhiteSpace(key.GetValue(LegacyCodeValueName)?.ToString());
                    bool hasVerified = !string.IsNullOrWhiteSpace(key.GetValue(LastVerifiedValueName)?.ToString());
                    if (!(hasEncrypted || hasLegacy) || !hasVerified) return false;

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
            catch { return false; }
        }

        public static bool IsExpired()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    string expiresAtStr = key?.GetValue(ExpiresAtValueName)?.ToString() ?? string.Empty;
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

        // --------------- Persistence ---------------

        private static void SaveActivated(string email, string code, string? licenseExpiresAtStr)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null) throw new Exception("Cannot open registry key for write.");

                key.SetValue(EmailValueName, email ?? string.Empty);
                key.SetValue(CodeEncValueName, DpapiHelper.Protect(code ?? string.Empty));
                key.SetValue(LastVerifiedValueName, DateTimeOffset.UtcNow.ToString("O"));

                if (!string.IsNullOrWhiteSpace(licenseExpiresAtStr))
                    key.SetValue(ExpiresAtValueName, licenseExpiresAtStr!);
                else
                    try { key.DeleteValue(ExpiresAtValueName, false); } catch { }

                // Once we've written the encrypted blob, wipe any legacy plaintext.
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
                    foreach (var name in new[] { CodeEncValueName, LegacyCodeValueName, LastVerifiedValueName, ExpiresAtValueName })
                        try { key.DeleteValue(name, false); } catch { }
                    // Email left in place so the form pre-fills next time.
                }
            }
            catch { }
        }

        // --------------- License lookup ---------------

        // Hits the licenses table directly. Returns (isValid, expiresAtStr).
        public static async Task<Tuple<bool, string?>> ValidateLicenseWithSupabaseAsync(string email, string code)
        {
            if (!ValidateInput(email, code))
                return Tuple.Create<bool, string?>(false, null);

            string cleanEmail = email.Trim().ToLowerInvariant();
            string cleanCode = code.Trim();
            string encodedEmail = Uri.EscapeDataString(cleanEmail);
            string encodedCode = Uri.EscapeDataString(cleanCode);
            string url = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/licenses?email=ilike.{encodedEmail}&activation_code=eq.{encodedCode}&select=*";

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");

                HttpResponseMessage resp;
                try { resp = await client.GetAsync(url).ConfigureAwait(false); }
                catch (Exception ex) { throw new Exception($"Could not reach Supabase: {ex.Message}"); }

                if (!resp.IsSuccessStatusCode)
                {
                    string err = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception($"Supabase returned {(int)resp.StatusCode}: {err}");
                }

                string text = (await resp.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
                if (!text.StartsWith("[") || text == "[]")
                    return Tuple.Create<bool, string?>(false, null);

                if (text.Contains("\"status\"")
                    && !text.Contains("\"status\":\"active\"")
                    && !text.Contains("\"status\": \"active\""))
                    return Tuple.Create<bool, string?>(false, null);

                string? expiresAtStr = null;
                var m = Regex.Match(text, "\"expires_at\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success)
                {
                    expiresAtStr = m.Groups[1].Value;
                    if (DateTimeOffset.TryParse(expiresAtStr, out DateTimeOffset exp) && DateTimeOffset.UtcNow > exp)
                        return Tuple.Create<bool, string?>(false, null);
                }
                return Tuple.Create<bool, string?>(true, expiresAtStr);
            }
        }

        // High-level activate: validate against the table, claim the device
        // (trials get device-locked on first use), then persist on success.
        public static async Task<Tuple<bool, string?>> ActivateAsync(string email, string code)
        {
            var result = await ValidateLicenseWithSupabaseAsync(email, code).ConfigureAwait(false);
            if (!result.Item1) return result;

            string status = await ClaimDeviceAsync(email, code).ConfigureAwait(false);
            if (status == "device_mismatch")
                throw new Exception("This trial license is locked to a different device. Please request a paid license to use it on multiple machines.");
            if (status != "ok")
                throw new Exception($"License check failed: {status}.");

            SaveActivated(email.Trim().ToLowerInvariant(), code.Trim(), result.Item2);
            return result;
        }

        private static async Task<string> ClaimDeviceAsync(string email, string code)
        {
            string url = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/rpc/claim_device";
            string body = "{\"p_email\":\"" + JsonEscape(email.Trim().ToLowerInvariant())
                        + "\",\"p_code\":\"" + JsonEscape(code.Trim())
                        + "\",\"p_device_id\":\"" + JsonEscape(DeviceFingerprint.Get()) + "\"}";

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");
                var resp = await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"Device check failed ({(int)resp.StatusCode}). {text}");
                // RPC returns a bare quoted string, e.g. "ok" or "device_mismatch".
                return text.Trim().Trim('"');
            }
        }

        // --------------- Trial ---------------

        public static async Task<Tuple<string, string, string?>> StartTrialAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                throw new Exception("Please enter a valid email address.");

            string cleanEmail = email.Trim().ToLowerInvariant();
            string url = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/rpc/start_trial";
            string body = "{\"p_email\":\"" + JsonEscape(cleanEmail)
                        + "\",\"p_device_id\":\"" + JsonEscape(DeviceFingerprint.Get()) + "\"}";

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");
                var resp = await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    if (text.Contains("device_mismatch"))
                        throw new Exception("A trial is already active for this email on a different device. Each trial is locked to one machine.");
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

                // Persist immediately (encrypted) so the caller doesn't have to.
                SaveActivated(cleanEmail, code, expiresAtStr);

                return Tuple.Create<string, string, string?>(cleanEmail, code, expiresAtStr);
            }
        }

        // --------------- Legacy plaintext migration ---------------

        // Re-encrypts any plaintext ActivationCode left over from older builds.
        // Silent and idempotent.
        public static Task<bool> MigrateLegacyPlaintextAsync()
        {
            try
            {
                string email, legacyCode, expiresAtStr;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null) return Task.FromResult(false);
                    email = key.GetValue(EmailValueName)?.ToString() ?? string.Empty;
                    legacyCode = key.GetValue(LegacyCodeValueName)?.ToString() ?? string.Empty;
                    expiresAtStr = key.GetValue(ExpiresAtValueName)?.ToString() ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(legacyCode)) return Task.FromResult(false);

                SaveActivated(email, legacyCode, string.IsNullOrWhiteSpace(expiresAtStr) ? null : expiresAtStr);
                return Task.FromResult(true);
            }
            catch { return Task.FromResult(false); }
        }

        // --------------- Helpers ---------------

        private static string JsonEscape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
