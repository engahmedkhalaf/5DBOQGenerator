# License System — Implementation Plan
**Project:** RUKN 5D BOQ Manager (Revit Add-in)  
**Backend:** Supabase (WPH ADDIN project — `dfkcnyzuiquvozvncwph`)  
**Last updated:** 2026-06-30

---

## 1. Architecture Overview

```
User installs add-in
        │
        ▼
Revit loads RuknBoqMapper.dll
        │
        ▼
LicenseManager.IsActivated()  ──── YES ──▶ Run normally
        │
       NO
        │
        ▼
LicenseWindow opens
        │
  User enters Email + Code
        │
        ▼
POST /rest/v1/rpc/verify_license   ◀── Supabase (WPH ADDIN)
        │
   ┌────┴────┐
  OK     not_found / machine_mismatch
   │
   ▼
SaveActivated() → Windows Registry (DPAPI encrypted)
        │
        ▼
Add-in unlocked
```

---

## 2. Supabase Database

**Project URL:** `https://dfkcnyzuiquvozvncwph.supabase.co`  
**Auth Key:** `sb_publishable_zhW-Ox8_ssRAZKkGkBbsog_1juWTr1X`

### Table: `public.licenses`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `email` | text | NO | Primary key — lowercase |
| `activation_code` | text | NO | The code given to user |
| `product` | text | YES | e.g. `rukn_5d_boq` — for multi-product use |
| `machine_id` | text | YES | Filled on first activation (device lock) |
| `expire_date` | date | YES | NULL = lifetime license |
| `created_at` | date | NO | Auto = CURRENT_DATE |

> RLS is **enabled** on the table. Direct anon reads are blocked.  
> All access goes through the `verify_license` RPC (SECURITY DEFINER).

### Function: `public.verify_license(p_email, p_code, p_machine_id)`

**Flow:**
1. Look up row by `email ILIKE p_email AND activation_code = p_code`
2. If not found → return `{ status: "not_found" }`
3. If `machine_id` is NULL → claim device (write `p_machine_id`) → return `{ status: "ok", ... }`
4. If `machine_id` ≠ `p_machine_id` → return `{ status: "machine_mismatch" }`
5. If match → return full license record

**Returns:**
```json
{ "status": "ok", "email": "...", "product": "...", "expire_date": "YYYY-MM-DD", "created_at": "...", "machine_id": "..." }
```

> ⚠️ `verify_license` still returns `trial_days` in the JSON body — harmless since the column was dropped and it will return NULL. Can be cleaned up later.

---

## 3. C# Implementation (`LicenseManager.cs`)

### Registry Keys
Path: `HKCU\Software\RuknTools\RuknBoqMapper`

| Value | Content |
|---|---|
| `Email` | Plaintext email |
| `ActivationCodeEnc` | DPAPI-encrypted activation code |
| `LastVerified` | ISO timestamp of last successful activation |
| `ExpiresAt` | ISO date string from `expire_date` (absent = lifetime) |

Trial path: `HKCU\Software\RuknTools\RuknBoqMapper\Trial`  
| `TrialStartDate` | ISO timestamp of local trial start |

### Key Methods

| Method | What it does |
|---|---|
| `IsActivated()` | Checks registry — has code + verified + not expired |
| `ActivateAsync(email, code)` | Calls `verify_license` RPC → saves to registry |
| `PingAsync()` | Quick GET to check Supabase is reachable |
| `SignOut()` | Clears all registry values |
| `GetTrialRemainingDays()` | Local 7-day countdown from `TrialStartDate` |
| `MigrateLegacyPlaintextAsync()` | One-time migration for old unencrypted installs |

### Device Fingerprint
`DeviceFingerprint.Get()` — generates a stable machine ID (used as `p_machine_id`).  
First activation claims the device. License is locked to that machine.

---

## 4. UI (`LicenseWindow.xaml`)

| State | What user sees |
|---|---|
| Not activated | Email + Code fields, Activate button, Request Code link |
| Activated | Status = Active/Licensed, Sign Out button |
| Expired | Status = Expired, form re-appears |

**Extra controls:**
- **Test Connection** button → calls `PingAsync()` → shows `✔ Connected` / `✘ Unreachable`
- **RUKNBIM** logo badge (top-right) → opens `https://www.ruknbim.com/`
- **Request Activation Code** → opens mailto to `support@ruknbim.com`

---

## 5. Admin Workflow — How to Issue a License

1. Open Supabase dashboard → Table Editor → `licenses`
2. Insert a new row:

```sql
INSERT INTO public.licenses (email, activation_code, product, expire_date)
VALUES (
  'client@example.com',
  'RUKN-XXXX-XXXX-XXXX',   -- generate a unique code
  'rukn_5d_boq',
  NULL                      -- NULL = lifetime, or '2027-01-01' for expiry
);
```

3. Send the client their `email` + `activation_code`
4. On first use, `machine_id` is auto-filled and device is locked

**To transfer license to a new machine:**
```sql
UPDATE public.licenses SET machine_id = NULL WHERE email = 'client@example.com';
```

---

## 6. What Is Done ✅

- [x] Supabase `licenses` table with RLS
- [x] `verify_license` RPC (lookup + device claim in one call)
- [x] `LicenseManager` — activate, sign out, expiry check, registry persistence
- [x] DPAPI encryption of activation code in registry
- [x] Device fingerprinting + machine lock
- [x] `PingAsync` connection test
- [x] License window UI (activate / sign-out / status)
- [x] Installer (Inno Setup 7 → `RUKN_5D_BOQ_Manager_Setup.exe`)

---

## 7. What Is Pending ⬜

- [ ] **Online re-verification** — currently only verifies once (on activation). Add periodic re-check (e.g. on Revit startup) to catch revoked licenses
- [ ] **License revocation** — admin sets `activation_code = 'REVOKED'` or adds a `revoked` boolean column
- [ ] **Multi-seat / floating licenses** — currently 1 license = 1 machine
- [ ] **Offline grace period** — if Supabase unreachable, allow N days before blocking
- [ ] **Update `verify_license`** to remove `trial_days` from return JSON (cosmetic cleanup)
- [ ] **Installer for Revit 2024/2025** — current ISS targets 2023 only

---

## 8. Quick Reference

| Item | Value |
|---|---|
| Supabase project | WPH ADDIN (`dfkcnyzuiquvozvncwph`) |
| RPC endpoint | `POST /rest/v1/rpc/verify_license` |
| Registry root | `HKCU\Software\RuknTools\RuknBoqMapper` |
| Installer output | `Output\RUKN_5D_BOQ_Manager_Setup.exe` |
| Support email | support@ruknbim.com |
| Website | https://www.ruknbim.com |
