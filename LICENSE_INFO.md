# RUKNBIM 5D BOQ Manager — Licensing & Activation Guide

This document provides detailed information on how the licensing, activation, and trial systems are implemented and managed in the RUKNBIM 5D BOQ Manager add-in.

---

## 1. Activation Flow

The add-in uses a hybrid model of **local registry storage** and **online Supabase API validation** to check and enforce license compliance.

### How Activation Works
1. **User Input**: The user enters their **Registered Email** and **Activation Code** in the **Activate License** tab.
2. **Online Validation**: The add-in sends an async RPC request (`rpc/activate_license`) to the Supabase endpoint.
3. **Database Check**: 
   - The database verifies if the activation code exists, is active, is assigned to the entered email, and has not expired.
   - For **trial** activation codes, the system binds the code to the user's specific hardware fingerprint (`device_id`) upon first activation to prevent key sharing.
4. **Local Verification**:
   - If validation succeeds, the API returns the expiration date and signature.
   - The add-in writes the activation details locally to the Windows Registry.

---

## 2. Registry Paths (Local Cache)

Local entitlement status is persisted in the Windows Registry under `HKEY_CURRENT_USER` for security and performance (so it does not query Supabase on every single Revit command run).

### Full Licenses
* **Registry Key Path**: `HKEY_CURRENT_USER\Software\RuknTools\RuknBoqMapper`
* **Values**:
  * `Email` (String): The registered email address.
  * `ActivationCode` (String): The active license code.
  * `ExpiresAt` (String): Expiration date (in UTC ISO 8601 format) or `"Lifetime"`.
  * `VerificationToken` (String): An encrypted signature of the license validation data.

### Local Trials
* **Registry Key Path**: `HKEY_CURRENT_USER\Software\RuknTools\RuknBoqMapper\Trial`
* **Values**:
  * `TrialStartDate` (String): The exact timestamp (in UTC ISO 8601 format) when the local free trial was started.

---

## 3. Trial Rules

* **Duration**: Free trials are active for **7 days** (604,800 seconds) from the `TrialStartDate`.
* **Hardware Locking**: Online trials are locked to a single device hash. If a user attempts to activate a trial code on a second machine, the Supabase RPC function will reject it.
* **Registry Verification**: The remaining trial time is computed locally as:
  $$\text{Remaining Days} = 7.0 - \text{Elapsed Days Since } \textit{TrialStartDate}$$

---

## 4. Resetting & Testing Licenses

For testing or re-activation purposes, developers can reset the license state using the following methods:

### Method A: Using the Add-in UI (Deactivation)
1. Open the **RUKN Tools 5D BOQ Manager** in Revit.
2. Click **License** on the Ribbon to open the License dashboard.
3. Go to the **License Status** tab and click **Sign Out**.
4. This will call the deactivation routine, clear all stored registry keys, and prompt you to activate again.

### Method B: Manual Registry Reset (Developer/Admin)
To completely reset the trial and activation state manually, run these command lines in Command Prompt (`cmd`) or PowerShell:

```powershell
# Remove full license registration
Remove-Item -Path "HKCU:\Software\RuknTools\RuknBoqMapper" -Recurse -ErrorAction SilentlyContinue

# Remove trial registration
Remove-Item -Path "HKCU:\Software\RuknTools\RuknBoqMapper\Trial" -Recurse -ErrorAction SilentlyContinue
```

---

## 5. Supabase Database Schema

The license tables are stored in Supabase with the following schema:

* **Table**: `public.licenses`
  * `id` (UUID, PK)
  * `email` (Text, Unique) — Registered user email.
  * `activation_code` (Text, Unique) — The code sent to the client.
  * `status` (Text) — `"active"`, `"inactive"`, or `"expired"`.
  * `is_trial` (Boolean) — Flag indicating a trial license.
  * `device_id` (Text) — Hardware fingerprint hashed value (locked to first activation for trials).
  * `expires_at` (Timestamp with time zone) — Expiration time.
  * `created_at` (Timestamp with time zone)
