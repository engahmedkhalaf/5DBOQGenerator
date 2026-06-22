using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace QicBoqMapper
{
    // Produces a stable, hashed identifier for the current Windows machine.
    // Used to lock trial licenses to a single device.
    //
    // Source: HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid (stable across
    // Windows updates, regenerated only on OS reinstall). Hashed with SHA-256
    // and salted so the raw GUID is never sent over the wire.
    internal static class DeviceFingerprint
    {
        private const string Salt = "QicBoqMapper.DeviceFingerprint.v1";
        private static string? _cached;

        public static string Get()
        {
            if (_cached != null) return _cached;
            string raw = ReadMachineGuid();
            if (string.IsNullOrEmpty(raw)) raw = $"{Environment.MachineName}|{Environment.UserName}";
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + "|" + raw));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                _cached = sb.ToString();
                return _cached;
            }
        }

        private static string ReadMachineGuid()
        {
            // 64-bit view first; fall back to default view.
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    string? v = subKey?.GetValue("MachineGuid")?.ToString();
                    if (!string.IsNullOrEmpty(v)) return v!;
                }
            }
            catch { }

            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                    return key?.GetValue("MachineGuid")?.ToString() ?? string.Empty;
            }
            catch { return string.Empty; }
        }
    }
}
