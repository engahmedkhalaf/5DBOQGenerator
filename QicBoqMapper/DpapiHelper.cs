using System;
using System.Security.Cryptography;
using System.Text;

namespace QicBoqMapper
{
    // Wraps Windows Data Protection API (DPAPI). Encrypts strings using the
    // CurrentUser scope so a value written by one Windows user cannot be read
    // by another user on the same machine. Encrypted blobs are returned as
    // Base64 so they round-trip safely through registry REG_SZ values.
    internal static class DpapiHelper
    {
        // Optional per-app entropy. Not a secret, just guards against another
        // process on the same user account accidentally reading our values.
        private static readonly byte[] Entropy =
            Encoding.UTF8.GetBytes("QicBoqMapper.v1");

        public static string Protect(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return string.Empty;
            byte[] data = Encoding.UTF8.GetBytes(plaintext);
            byte[] encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public static string Unprotect(string base64Cipher)
        {
            if (string.IsNullOrEmpty(base64Cipher)) return string.Empty;
            try
            {
                byte[] encrypted = Convert.FromBase64String(base64Cipher);
                byte[] data = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                // Corrupted blob, wrong user, or registry tampering.
                return string.Empty;
            }
        }
    }
}
