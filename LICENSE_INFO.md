# RUKNBIM 5D BOQ Manager — License & Trial Guide

This guide explains how to start a free trial, activate a full license, and reset the license status of the **RUKNBIM 5D BOQ Manager** add-in.

---

## 1. Start a Free Trial
1. Open Revit and click **License** on the Ribbon.
2. In the **Get License** tab, click **Start Free 7-Day Trial**.
3. Your trial starts immediately. You can view the remaining days in the **License Status** tab.
   * *Note: Trials are locked to one machine to prevent abuse.*

---

## 2. Request an Activation Code
If you want to purchase a full license or request a code:
1. Click the **Request Activation Code (Email)** button in the **Get License** tab.
2. This automatically prepares an email draft with your computer name.
3. Send this email to **engkhalaf7@gmail.com** to request your code.

---

## 3. Activate a Full License
Once you receive your activation code:
1. Go to the **Activate License** tab.
2. Enter your **Email Address** and the **Activation Code**.
3. Click **Activate**.
4. The status will update to "Active / Licensed" in the **License Status** tab.

---

## 4. How to Reset (For Testing)
If you need to clear the license or trial state to test activation again:

* **Option A (Via UI)**: Go to the **License Status** tab and click **Sign Out**.
* **Option B (Via registry)**: Open PowerShell and run this command:
  ```powershell
  Remove-Item -Path "HKCU:\Software\RuknTools\RuknBoqMapper" -Recurse -ErrorAction SilentlyContinue
  ```
