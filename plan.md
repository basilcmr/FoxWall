# FoxWall - Future Roadmap & Architecture Plan

This document outlines the detailed technical design, architectural changes, and security considerations for the five major future enhancements of our modified FoxWall firewall, ordered from easiest to most complex to implement.

---

## 1. Import & Export of Application Exceptions List with Settings
**Goal:** Allow users to export their custom whitelisted/blocked application exceptions list alongside general settings to a JSON backup file, and import them with the choice to either **Replace** the existing configuration or **Merge (Add)** new entries without creating duplicates.

### Architecture & Design Details

#### 1. Data Serialization Schema
- Define a serialization layout class `FirewallBackup` containing:
  - `Version`: Version metadata to ensure forward/backward compatibility.
  - `Settings`: General configuration properties.
  - `Exceptions`: A list of application whitelisting and block rules (`List<FirewallException>`).
- Leverage TinyWall's existing JSON helpers (`PolymorphicJsonConverter.cs`, `SerializationHelper.cs`) to serialize/deserialize this payload securely.

#### 2. User Interface Enhancements
- **Settings Dialog Integration:** Add "Export Config..." and "Import Config..." buttons inside the Settings Panel.
- **Import Mode Selector Dialog:** When the user imports a backup file, launch a premium, minimalist dialog asking the user to choose their import method:
  - **Option A: Replace (Overwrite):** Completely clears the existing local database of exceptions and applies the imported exceptions list.
  - **Option B: Merge (Additive):** Merges the imported exceptions into the current list.
- **Merge Deduplication Logic:** During Merge mode, traverse both sets and evaluate the identity of each rule based on:
  - Executable path / name matching.
  - Service names (if service exceptions).
  - Port/Protocol parameters.
  - *Result:* If a match is found, preserve the user's current settings or prompt a resolution; if no match exists, cleanly insert the imported rule.

#### 3. Core Service WCF & DB Integration
- **IPC Messages:** Create WCF request payloads `ExportSettingsRequest` and `ImportSettingsRequest` in [Message.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/Message.cs).
- **Service Transaction Safety:** Ensure the import operation on the service side runs inside a database transaction. If the import fails at any step, rollback to the previous active ruleset, avoiding an unstable or corrupted state.

---

## 2. Advanced Path-Based & Wildcard Whitelisting
**Goal:** Solve whitelisting issues for fast-updating applications (like Discord, Slack, or MS Teams) that constantly change their executables or output directories during updates, without prompting the user repeatedly.

### Implementation Alternatives & Security Analysis

#### Alternative A: Full Folder Path Whitelisting (e.g., `C:\Users\...\AppData\Local\Discord\*`)
*   **How it works:** WFP rules are generated to match the folder prefix of any initiating process.
*   **Security Risk:** **HIGH.** If an attacker places a malicious executable inside the whitelisted folder, the firewall will permit its network traffic without warning. User folders are fully writeable by standard applications, making this a target for privilege escalation.
*   **Security Mitigations:**
    1.  **Digital Signature Lock:** Check the Authenticode certificate of the executing process inside that folder. If the signature is signed by a trusted authority (e.g., "Discord Inc."), allow traffic. If unsigned, block it.
    2.  **Parent Process Tree Validation:** Ensure the process was initiated by a trusted parent process.

#### Alternative B: Wildcard Filename Matching (e.g., `discord*.exe`)
*   **How it works:** Standard path parsing allowing match patterns inside approved folders, e.g., resolving the latest `app-*` folder dynamically on startup.
*   **Security Risk:** **MEDIUM.** An attacker could name a malicious file `discord_malicious.exe` and execute it from a writeable folder.
*   **Security Mitigations:**
    - Combined prefix-directory and digital signature verification.

### Core Implementation Strategy
We will implement **Alternative A combined with Digital Signature Lock** to ensure perfect usability with absolute security:
1. **Dynamic Path Resolver:** In [TinyWallService.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/TinyWallService.cs), support a wildcard/directory matching condition. When evaluating whitelists, resolve the base directory of the process.
2. **Authenticode Verification Integration:**
   - Use the existing `WinTrust.VerifyFileAuthenticode()` helper in [ExceptionSubject.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/ExceptionSubject.cs) to capture the publisher name of the executable.
   - Add a "Trust Publisher" property to whitelists, matching *only* files in the specified path that carry that exact digital signature block.

---

## 3. Change Name to FoxWall
**Goal:** Replace the Name TinyWall to FoxWall in the entire solution. License terms do not allow using the official "TinyWall" trademark name for custom forks. We need to rename the application, settings, window titles, folders, and registry entries to "FoxWall" while maintaining full, automatic backward compatibility to read existing "TinyWall" configurations so a reinstall or update preserves all custom rules.

### Rebranding & Migration Architectural Plan

#### 1. Namespace & Branding Substitution
*   **Visual Assets & Labels:** Rename window titles, UI forms, settings tabs, tray tip descriptions, and notifications from "TinyWall" to "FoxWall".
*   **Metadata Renaming:** Update project properties (`Product`, `AssemblyTitle`, `Company`, `Description`) in [TinyWall.csproj](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/TinyWall.csproj) to output `FoxWall.exe`.
*   **WFP Provider rebranding:** In the core service, rename the Windows Filtering Platform sublayers and display names to "FoxWall Service Provider" to maintain professional OS integration.

#### 2. Legacy Migration Layer (TinyWall -> FoxWall)
To prevent data loss and ensure a completely seamless transition, we will implement a migration layer inside the core path resolving logic:
*   **Path Redirection:** Define configuration paths under `FoxWall` (e.g., `%AppData%\FoxWall\` for settings database files and `%ProgramData%\FoxWall\` for service configurations).
*   **Auto-Migration Routine:** On startup, the service will execute a migration check:
    ```csharp
    string oldPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TinyWall");
    string newPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FoxWall");

    if (!Directory.Exists(newPath) && Directory.Exists(oldPath))
    {
        // 1. Create the new FoxWall configuration directory
        Directory.CreateDirectory(newPath);

        // 2. Safely copy XML configuration, database, and hosts blocker files
        foreach (string file in Directory.GetFiles(oldPath))
        {
            try 
            {
                File.Copy(file, Path.Combine(newPath, Path.GetFileName(file)), false);
            }
            catch { /* Log migration exception */ }
        }
    }
    ```
*   **Fallback Fallthrough:** If the migration folder copy fails or specific settings keys are missing, the system will fall back to reading files from the legacy `TinyWall` folder directly to ensure no active whitelists are lost.

#### 3. Service & Registry Upgrades
*   **Service Registration:** Change the Windows service name from `TinyWall` to `FoxWall`.
*   **Installer Adaptation:** Update the C# installer (`TinyWallJellyModeInstaller.exe`) to become `FoxWallInstaller.exe`. Configure it to look for both active `TinyWall` and `FoxWall` services during cleanup, stop them safely, migrate database configurations, and register the new `FoxWall` system service seamlessly!

---

## 4. Premium Dark Mode UI
**Goal:** Replace the legacy Windows classic control styling with a modern, harmonious dark theme (glowing purple/magenta accents matching Jellyfin, dark charcoal background, sleek typography, and high-contrast borders).

### Technical Implementation & Approach
WinForms does not support native dark mode out of the box. We will achieve this via a hybrid custom-painting and control-injection pattern:
1. **Dynamic Styling Registry:** Create a new helper class `pylorak.TinyWall.ThemeManager` that stores HSL-based color tokens:
   - `Background`: Dark charcoal `#121212` (or RGB `18, 18, 18`)
   - `Surface`: Elevated dark gray `#1E1E1E` (or RGB `30, 30, 30`)
   - `Accent`: Deep purple `#000A87` / magenta glow
   - `TextPrimary`: Pure white `#FFFFFF`
   - `TextSecondary`: Muted gray `#A0A0A0`
2. **Recursive Form Styling:** `ThemeManager` will expose an `ApplyDarkTheme(Form form)` method. When a form loads:
   - Recursively traverse all child controls (`Panel`, `Button`, `Label`, `ListView`, `TabControl`, `TextBox`).
   - Dynamically override background, foreground, and border properties.
   - For `Button` controls, set `FlatStyle = FlatStyle.Flat` and design custom mouse-over/click borders using flat-style colors.
   - For standard Win32 dialogs (like `TaskDialog`), implement a custom wrapper or replace them with styled dark dialog forms.
3. **Menu Strip Custom Renderer:** Create a custom `ToolStripProfessionalRenderer` with overridden color palettes to paint the tray system context menu strip in elegant dark colors with smooth purple selection highlights.

---

## 5. Dynamic Custom Modes Management
**Goal:** Allow users to create, configure, and delete their own custom Firewall Modes (e.g., *WorkMode*, *GameMode*) with unique whitelisting rulesets directly from the Settings UI and access them with two clicks from the system tray.

### Architectural Changes
To make firewall modes dynamic instead of hardcoded in a static C# `enum`:
1. **Dynamic Configuration Schema:**
   - Modify [ServerConfiguration.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net block/TinyWall/TinyWall/ServerConfiguration.cs) to transition `FirewallMode` from a rigid enum into a dynamic descriptor or class `FirewallModeDef` stored in the database.
   - Save custom modes in a structured local JSON database file (e.g., `CustomModes.json` in the `%AppData%\TinyWall` folder).
2. **WCF Controller-Service IPC Expansion:**
   - Expand the WCF IPC communication contracts in [Message.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net block/TinyWall/TinyWall/Message.cs) and [TinyWallService.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net block/TinyWall/TinyWall/TinyWallService.cs) to allow sending dynamically serialized custom mode rule definitions from the Tray controller to the core service.
3. **Dynamic Rule Compilation:**
   - In `TinyWallService.AssembleActiveRules()`, dynamically compile WFP (Windows Filtering Platform) filter entries by parsing the custom rules stored under the active custom mode definition.
4. **Dynamic Tray Menu Rendering:**
   - In `TinyWallController.InitializeComponent()` and `TrayMenu_Opening`, clear and dynamically generate the `mnuMode` dropdown list items by fetching the active custom modes list.

