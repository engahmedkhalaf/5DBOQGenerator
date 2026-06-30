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
        private const string EmailValueName = "Email";
        private const string CodeEncValueName = "ActivationCodeEnc";
        private const string LastVerifiedValueName = "LastVerified";
        private const string ExpiresAtValueName = "ExpiresAt";
        private const string LegacyCodeValueName = "ActivationCode";

        private const string TrialRegistryPath = @"Software\RuknTools\RuknBoqMapper\Trial";
        private const string TrialStartDateValueName = "TrialStartDate";

        private const string SupabaseUrl = "https://dfkcnyzuiquvozvncwph.supabase.co";
        private const string SupabaseAnonKey = "sb_publishable_zhW-Ox8_ssRAZKkGkBbsog_1juWTr1X";

        // --------------- State queries ---------------

        public static bool IsFullyActivated()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null) return false;

                    bool hasCode = !string.IsNullOrWhiteSpace(key.GetValue(CodeEncValueName)?.ToString())
                                || !string.IsNullOrWhiteSpace(key.GetValue(LegacyCodeValueName)?.ToString());
                    bool hasVerified = !string.IsNullOrWhiteSpace(key.GetValue(LastVerifiedValueName)?.ToString());
                    if (!hasCode || !hasVerified) return false;

                    string expiresAtStr = key.GetValue(ExpiresAtValueName)?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(expiresAtStr)
                        && DateTimeOffset.TryParse(expiresAtStr, out DateTimeOffset expiresAt)
                        && DateTimeOffset.UtcNow > expiresAt)
                        return false;

                    return true;
                }
            }
            catch { return false; }
        }

        public static bool IsActivated() => IsFullyActivated();

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
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$") && code.Trim().Length >= 4;
        }

        // --------------- Trial (local only) ---------------

        public static double GetTrialRemainingDays()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(TrialRegistryPath))
                {
                    if (key == null) return 0;
                    string? startStr = key.GetValue(TrialStartDateValueName)?.ToString();
                    if (string.IsNullOrEmpty(startStr) || !DateTime.TryParse(startStr, out DateTime startDate))
                        return 0;
                    double remaining = 7.0 - (DateTime.UtcNow - startDate.ToUniversalTime()).TotalDays;
                    return remaining > 0 ? remaining : 0;
                }
            }
            catch { return 0; }
        }

        public static bool IsLocalTrialActive()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(TrialRegistryPath))
                    return key != null && !string.IsNullOrEmpty(key.GetValue(TrialStartDateValueName)?.ToString());
            }
            catch { return false; }
        }

        public static void StartLocalTrial()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(TrialRegistryPath))
                    key?.SetValue(TrialStartDateValueName, DateTime.UtcNow.ToString("o"));
            }
            catch { }
        }

        // --------------- Persistence ---------------

        private static void SaveActivated(string email, string code, string? expiresAtStr)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null) throw new Exception("Cannot open registry key for write.");
                key.SetValue(EmailValueName, email ?? string.Empty);
                key.SetValue(CodeEncValueName, DpapiHelper.Protect(code ?? string.Empty));
                key.SetValue(LastVerifiedValueName, DateTimeOffset.UtcNow.ToString("O"));
                if (!string.IsNullOrWhiteSpace(expiresAtStr))
                    key.SetValue(ExpiresAtValueName, expiresAtStr!);
                else
                    try { key.DeleteValue(ExpiresAtValueName, false); } catch { }
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
                }
                try { Registry.CurrentUser.DeleteSubKeyTree(TrialRegistryPath, false); } catch { }
            }
            catch { }
        }

        // --------------- Connection test ---------------

        // Returns true if Supabase is reachable. Fast, no auth side-effects.
        public static async Task<bool> PingAsync()
        {
            string url = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/licenses?limit=0";
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
            {
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");
                try
                {
                    var resp = await client.GetAsync(url).ConfigureAwait(false);
                    return resp.IsSuccessStatusCode;
                }
                catch { return false; }
            }
        }

        // --------------- Activate (single RPC call) ---------------

        // Calls verify_license RPC which handles lookup + machine claiming in one shot.
        // Returns (isValid, expireDate as ISO string or null).
        public static async Task<Tuple<bool, string?>> ActivateAsync(string email, string code)
        {
            if (!ValidateInput(email, code))
                return Tuple.Create<bool, string?>(false, null);

            string cleanEmail = email.Trim().ToLowerInvariant();
            string cleanCode = code.Trim();
            string machineId = DeviceFingerprint.Get();

            string url = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/rpc/verify_license";
            string body = "{\"p_email\":\"" + JsonEscape(cleanEmail)
                        + "\",\"p_code\":\"" + JsonEscape(cleanCode)
                        + "\",\"p_machine_id\":\"" + JsonEscape(machineId) + "\"}";

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");

                HttpResponseMessage resp;
                try { resp = await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")).ConfigureAwait(false); }
                catch (Exception ex) { throw new Exception($"Could not reach Supabase: {ex.Message}"); }

                string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"Supabase returned {(int)resp.StatusCode}: {text}");

                if (text.Contains("\"not_found\""))
                    return Tuple.Create<bool, string?>(false, null);

                if (text.Contains("\"machine_mismatch\""))
                    throw new Exception("This license is already activated on a different device.");

                // Parse expire_date (date type: "YYYY-MM-DD")
                string? expiresAtStr = null;
                var m = Regex.Match(text, "\"expire_date\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success)
                {
                    expiresAtStr = m.Groups[1].Value;
                    if (DateTimeOffset.TryParse(expiresAtStr, out DateTimeOffset exp) && DateTimeOffset.UtcNow > exp)
                        return Tuple.Create<bool, string?>(false, null);
                }

                SaveActivated(cleanEmail, cleanCode, expiresAtStr);
                return Tuple.Create<bool, string?>(true, expiresAtStr);
            }
        }

        // --------------- Legacy plaintext migration ---------------

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
