# FoxWall - Future Roadmap & Architecture Plan

This document outlines the detailed technical design, architectural changes, and security considerations for the custom enhancements of our modified FoxWall firewall, ordered strictly from easiest/lowest implementation difficulty to hardest/highest complexity in code.

### 📊 Roadmap Status Summary
- `[x]` **1. Import & Export of Application Exceptions List with Settings** (Completed in v1.0.1 - *Difficulty: Very Low*)
- `[x]` **2. Context-Menu Verification & Auditing Toolkit** (Completed in v1.0.4 - *Difficulty: Low-Medium*)
- `[x]` **3. Clipboard Copy with Column Selection Dialogue** (Completed in v1.0.3 - *Difficulty: Low*)
- `[ ]` **4. Immediate Network "Panic / Kill Switch"** (Pending - *Difficulty: Low-Medium*)
- `[ ]` **5. Rule Optimizer & Obsolete Exceptions Cleaner** (Pending - *Difficulty: Low-Medium*)
- `[ ]` **6. Change Name to FoxWall** (Pending - *Difficulty: Low-Medium*)
- `[x]` **7. Application Exceptions Classification & Customizable Columns** (Completed in v1.0.4 - *Difficulty: Medium*)
- `[x]` **8. Advanced Path-Based & Wildcard Whitelisting** (Completed in v1.0.2 - *Difficulty: Medium*)
- `[ ]` **9. Whitelist Rule "Snoozing" (Time-Limited Exceptions)** (Pending - *Difficulty: Medium*)
- `[x]` **10. Premium Dark Mode UI** (Completed - *Difficulty: Medium-High*)
- `[ ]` **11. Dynamic Custom Modes Management** (Pending - *Difficulty: High*)
- `[ ]` **12. Auto-Learn Mode with Interactive Prompts** (Pending - *Difficulty: High / Very High*)
- `[ ]` **13. Geo-IP Real-Time Map & Flag Auditing** (Pending - *Difficulty: High / Very High*)
- `[ ]` **14. Parent-Process Security Guard (Anti-Exploit Blocker)** (Pending - *Difficulty: Very High / Extremely High*)
- `[x]` **15. FoxWall Security Monitor Dashboard** (Completed in v1.2.0 - *Difficulty: Extremely High*)
- `[ ]` **16. Full Web-Based Settings & Rules UI Migration** (Pending - *Difficulty: High*)
- `[x]` **17. Scheduled PC Power Manager (Shutdown/Restart/Sleep/Lock)** (Completed in v1.2.5 - *Difficulty: Medium-High*)

---

## 1. Import & Export of Application Exceptions List with Settings [COMPLETED]
**Difficulty:** Very Low / Low
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

## 2. Context-Menu Verification & Auditing Toolkit [COMPLETED]
**Difficulty:** Low-Medium
**Goal:** Empower users to easily inspect, verify, and audit whitelisted or blocked applications directly from the exceptions list with standard right-click actions:
1. **Open File Location:** Open Windows Explorer highlighting the target executable file.
2. **Verify Digital Signature:** Instantly verify and present the Authenticode signature state and publisher info in a premium styled WinForms dialogue.
3. **Check Hash on VirusTotal:** Hash the file locally (SHA-1) and launch the default system browser directly searching for the threat analysis.
4. **Google Search Verification:** Direct safety verification search on Google.
5. **Audit Active Sockets:** Open the Connections Form pre-filtered only to show the selected application's sockets.
6. **Quick Toggle Policy:** Immediate rule toggling ("Quick Allow (Unrestricted)" / "Quick Block (Hard Block)") for single or multiple selected rules.

### Architectural Changes & Implementation Plan

#### 1. Rich Context Menu Initialisation
- Construct a detailed `ContextMenuStrip` context menu programmatically in [SettingsForm.Import.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/SettingsForm.Import.cs).
- Design a dynamic context enabling hook on the `Opening` event:
  - If a single row is selected and it is a file subject (Executable or Service): Enable "Open File Location", "Verify Digital Signature", "Check Hash on VirusTotal", and "Audit Active Sockets".
  - If any count of exceptions is selected: Enable "Quick Toggle Policy" (supporting multi-select allowance or hard blocking on the fly).

#### 2. WinExplorer Selection Highlights
- Retrieve the selected file path, resolve any path wildcards utilizing `WildcardHelper.ResolveWildcardPath(rawPath)`.
- Fire standard Explorer shell command to highlight the file inside a folder viewport:
  ```csharp
  Process.Start("explorer.exe", $"/select,\"{resolvedPath}\"");
  ```

#### 3. Premium Signature Dialogue (`SignatureDetailsForm.cs`)
- Implement a custom, DPI-aware dialog inheriting from `Form` and styled elegantly under `ThemeManager.Apply(this)` guidelines.
- Render dynamic status badges: green for a completely valid certificate, yellow for self-signed or untrusted certificates, and red for unsigned executables.
- Render read-only text fields detailing the fully resolved path, publisher names, and certificate status details.

#### 4. Local File Hashing & VirusTotal Integration
- Compute the SHA-1 hash of the resolved file path via the existing `Hasher.HashFileSha1(resolvedPath)` helper.
- Formulate the lookup URL: `https://www.virustotal.com/gui/search/{hash}` and invoke system shell process securely.

#### 5. Dynamic Connections Filtering
- Declared a partial property `public string PathFilter { get; set; }` inside a newly segregated `ConnectionsForm.Filter.cs` partial class file.
- Implement `OnLoad` programmatic layout inserts: if a filter is active, inject a top banner notifying the user, alongside a "Clear Filter" flat button to reset list view filters cleanly.
- Integrate the filtering pass directly in the core `UpdateList()` method of `ConnectionsForm.cs` right before adding rows to the grid.

#### 6. Quick Policy Updates
- Toggles selected application rules in the background memory database (`TmpConfig.Service.ActiveProfile.AppExceptions`) dynamically.
- Automatically invokes `RebuildExceptionsList()` on the fly, instantly updating row backgrounds to hard block colors (e.g. glowing soft-red highlight) or whitelisted colors immediately.

---

## 3. Clipboard Copy with Column Selection Dialogue [COMPLETED]
**Difficulty:** Low
**Goal:** Provide an easy way to export and document rules textually. With a "Copy to Clipboard" trigger, users can copy all or selected exceptions to their clipboard. An interactive modal popup lets users customize exactly which data fields (e.g. Application Name, Type, Priority, Path/Details, or Creation Date) are compiled into the final clipboard payload, formatted as a clean tab-delimited table.

### Architectural Changes & Implementation Plan

#### 1. GUI Integration (Button and Context Menu)
- Add a "Copy to Clipboard..." button programmatically inside [SettingsForm.Import.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/SettingsForm.Import.cs) alongside our custom import/export exception controls.
- Inject a corresponding "Copy to Clipboard..." option to our right-click `ContextMenuStrip` associated with `listApplications`.

#### 2. Column Selection Dialogue (`CopySelectionForm.cs`)
- Implement a dedicated, lightweight utility modal Form styled under our custom dark-purple palette.
- Embed styled checkboxes mapping directly to ListView properties:
  - `[x] Application Name`
  - `[x] Exception Type`
  - `[x] Classification / Importance`
  - `[x] File Path / Details`
  - `[x] Last Modified`
- Add selection filters: a radio group or toggles allowing users to select between:
  - **Copy Selected Items Only** (Enabled when rows are active).
  - **Copy All Items** (Copies all rows passing the current text filter).

#### 3. Data Compilation & Clipboard Integration
- On clicking "Copy", determine the target ListView rows based on user options.
- Loop through the rows, building a tab-separated string payload:
  - *Row 1:* Selected headers (e.g. `Application\tPriority\tDetails`)
  - *Rows 2+:* Properties separated by tab (`\t`) characters.
- Safely assign the compiled text to the Windows Clipboard API using a robust try-catch handler:
  ```csharp
  Clipboard.SetText(stringBuilder.ToString(), TextDataFormat.Text);
  ```

---

## 4. Immediate Network "Panic / Kill Switch" [PENDING]
**Difficulty:** Low-Medium
**Goal:** Implement a quick-access global network block toggle. Clicking the "Panic / Kill Switch" in the tray context menu or the monitoring panel instantly stops all network communication on all adapters and forcefully terminates all active socket connections immediately.

### Architectural Changes & Implementation Plan

#### 1. WFP Global Override Rule
- Register a high-priority persistent block-all filter weight in `TinyWallService.cs` under a specific key (e.g. `KillSwitchActive`).
- When activated, this WFP rule resides at the absolute top of the sublayer hierarchy, bypassing all whitelists/rules except loopback.

#### 2. Socket Termination Logic
- Create a native utility wrapper in `pylorak.Windows` using IP Helper APIs (`iphlpapi.dll`).
- Call `SetTcpEntry` programmatically inside a loop over the active TCP connection table to drop every active connection (`MIB_TCP_STATE_DELETE`) immediately.

#### 3. Tray Context Menu Integration
- Add a glowing, red-accented **"PANIC / KILL SWITCH"** toggle to the tray controller context menu.
- Display a striking red flashing tray icon and notification when the Kill Switch is active to prevent user confusion.

---

## 5. Rule Optimizer & Obsolete Exceptions Cleaner [PENDING]
**Difficulty:** Low-Medium
**Goal:** Provide an auditing toolkit to clean up rule bloat. The optimizer scans all configured custom whitelists, identifies dead paths (files no longer on disk), duplicate rules, and redundant port settings, presenting a clean review checklist to delete obsolete rules.

### Architectural Changes & Implementation Plan

#### 1. Rules Auditor Engine (`RulesAuditor.cs`)
- Build an isolated analyzer class to traverse the loaded `AppExceptions` list.
- Check each file path: flag paths containing no wildcards where `File.Exists(path)` returns false.
- Flag redundancies: rules sharing the same executable path, where one rule fully covers the ports/protocols of another.

#### 2. Audit Dashboard UI (`OptimizerForm.cs`)
- Create an elegant dark WinForms report form listing:
  - Dead Path exceptions (with paths grayed out).
  - Overlapping / Redundant rule exception groups.
- Allow the user to check/uncheck suggested optimizations.

#### 3. Transactional Clean-Up Execution
- Upon clicking "Apply Optimization", purge checked exceptions from the active profile.
- Perform a atomic WCF save transaction to update the local service configuration database and refresh WFP instantly.

---

## 6. Change Name to FoxWall [PENDING]
**Difficulty:** Low-Medium
**Goal:** Replace the Name TinyWall to FoxWall in the entire solution. License terms do not allow using the official "TinyWall" trademark name for custom forks. We need to rename the application, settings, window titles, folders, and registry entries to "FoxWall" while maintaining full, automatic backward compatibility to read existing "TinyWall" configurations so a reinstall or update preserves all custom rules.

### Rebranding & Migration Architectural Plan

#### 1. Namespace & Branding Substitution
*   **Visual Assets & Labels:** Rename window titles, UI forms, settings tabs, tray tip descriptions, and notifications from "TinyWall" to "FoxWall".
*   **Metadata Renaming:** Update project properties (`Product`, `AssemblyTitle`, `Company`, `Description`) in [TinyWall.csproj](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/TinyWall.csproj) to output `FoxWall.exe`.
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

## 7. Application Exceptions Classification & Customizable Columns [PENDING]
**Difficulty:** Medium
**Goal:** Provide the ability to classify application exception rules with custom priority/importance tags (e.g. `Critical`, `Important`, `Optional`, `Unnecessary`, or `Unclassified`) to help users easily identify and filter non-essential system or background processes (like `smartscreen.exe` or telemetry processes) that can be safely disabled or blocked. Additionally, support customizable/configurable columns in the Exception List view.

### Architectural Changes & Implementation Plan

#### 1. Extended Exception Metadata Schema
- Define an enum `RuleImportance` in [ExceptionSubject.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/ExceptionSubject.cs) or [FirewallExceptionV3.cs]:
  ```csharp
  public enum RuleImportance
  {
      Unclassified = 0,
      Critical = 1,      // Core system services or principal web browsers
      Important = 2,     // Primary user applications
      Optional = 3,      // Secondary helper apps, updaters, or peripheral tools
      Unnecessary = 4    // Telemetry, adware, or low-priority background hooks (e.g., smartscreen.exe)
  }
  ```
- Add an `Importance` property of type `RuleImportance` to the `FirewallExceptionV3` class.
- Update XML and JSON serialization layers (e.g., in `ServerConfiguration.cs` and `SerializationHelper.cs`) to dynamically default missing XML elements to `RuleImportance.Unclassified` to prevent crashes when reading legacy databases.

#### 2. Interactive Importance Selector Control
- Add a styled, flat ComboBox control to the exception editor dialog (`ApplicationExceptionForm.cs`).
- Populate the ComboBox with user-friendly strings representing the classification categories.
- Bind the selected item to load and save settings directly from/to the editing exception record.

#### 3. Customizable Columns & ListView Upgrades
- Add a new column `columnImportance` (header name "Importance" / "Priority") to the `listApplications` ListView.
- Update `ListItemFromAppException()` in `SettingsForm.cs` to add the localized priority string to the ListView subitems.
- **Auditing Aesthetics:** Apply subtle background or text styling differences to item rows based on priority (e.g., coloring `Unnecessary` rules in a slightly faded or desaturated state to signify low priority).
- Adapt the `ListViewItemComparer` class in `SettingsForm.cs` to cleanly support sorting by this new column dynamically.

#### 4. Header Context Menu Column Visibility Toggle
- Add a Header Context Menu (`ContextMenuStrip`) that triggers when the user right-clicks on the `listApplications` header row.
- List all available columns with checkboxes (Application, Type, Importance, Details, Last Modified).
- Checking or unchecking a menu item dynamically toggles column visibility (by setting `ColumnHeader.Width` to `0` to hide, or restoring its saved width value to show).
- Persist column layout preferences in the user's `ControllerSettings` config file so the selected column layout is loaded automatically.

---

## 8. Advanced Path-Based & Wildcard Whitelisting [COMPLETED]
**Difficulty:** Medium
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
We implemented **Alternative A combined with Digital Signature Lock** to ensure perfect usability with absolute security:
1. **Dynamic Path Resolver:** In [TinyWallService.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/TinyWallService.cs), support a wildcard/directory matching condition. When evaluating whitelists, resolve the base directory of the process.
2. **Authenticode Verification Integration:**
   - Use the existing `WinTrust.VerifyFileAuthenticode()` helper in [ExceptionSubject.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/ExceptionSubject.cs) to capture the publisher name of the executable.
   - Enforce digital signature checks (`testee.IsSigned && testee.CertValid`) inside both the reload routine and the real-time process monitoring triggers to guarantee that only validly signed executables can match wildcard folder rules.

---

## 9. Whitelist Rule "Snoozing" (Time-Limited Exceptions) [PENDING]
**Difficulty:** Medium
**Goal:** Enable time-decaying whitelists. Users can whitelist an application temporarily (e.g. for "30 Minutes", "2 Hours", or "Until System Reboot"). When the time expires, FoxWall's background service automatically locks the application out from the network.

### Architectural Changes & Implementation Plan

#### 1. Extended Rule Lifecycle Properties
- Add an optional `ExpirationTimestamp` (nullable `DateTime`) property to `FirewallExceptionV3`.
- Set `ExpirationTimestamp` programmatically when creating rules via the Snooze option dropdown in the whitelisting prompt dialog.

#### 2. Service Monitoring Loop
- Maintain a WCF-driven background cleanup runner in `TinyWallService.cs` (e.g., within `MinuteTimer_Tick`).
- Check active exceptions list: if any rule has an `ExpirationTimestamp` that is past the current system local time, remove the entry from the active profile.
- Re-install WFP firewall filters inside a single transaction to execute a seamless, silent cleanup without service interruption.

---

## 10. Premium Dark Mode UI [COMPLETED]
**Difficulty:** Medium-High
**Goal:** Replace the legacy Windows classic control styling with a modern, harmonious dark theme (glowing purple/magenta accents matching Jellyfin, dark charcoal background, sleek typography, and high-contrast borders).

### Technical Implementation & Approach
WinForms does not support native dark mode out of the box. We achieved this via a hybrid custom-painting and control-injection pattern:
1. **Dynamic Styling Registry:** Create a new helper class `pylorak.TinyWall.ThemeManager` that stores HSL-based color tokens:
   - `Background`: Dark charcoal `#121212` (or RGB `18, 18, 18`)
   - `Surface`: Elevated dark gray `#1E1E1E` (or RGB `30, 30, 30`)
   - `Accent`: Deep purple `#000A87` / magenta glow
   - `TextPrimary`: Pure white `#FFFFFF`
   - `TextSecondary`: Muted gray `#A0A0A0`
2. **Recursive Form Styling:** `ThemeManager` exposes an `ApplyDarkTheme(Form form)` method. When a form loads:
   - Recursively traverse all child controls (`Panel`, `Button`, `Label`, `ListView`, `TabControl`, `TextBox`).
   - Dynamically override background, foreground, and border properties.
   - For `Button` controls, set `FlatStyle = FlatStyle.Flat` and design custom mouse-over/click borders using flat-style colors.
   - For standard Win32 dialogs (like `TaskDialog`), implement a custom wrapper or replace them with styled dark dialog forms.
3. **Menu Strip Custom Renderer:** Create a custom `ToolStripProfessionalRenderer` with overridden color palettes to paint the tray system context menu strip in elegant dark colors with smooth purple selection highlights.

---

## 11. Dynamic Custom Modes Management [PENDING]
**Difficulty:** High
**Goal:** Allow users to create, configure, and delete their own custom Firewall Modes (e.g., *WorkMode*, *GameMode*) with unique whitelisting rulesets directly from the Settings UI and access them with two clicks from the system tray.

### Architectural Changes
To make firewall modes dynamic instead of hardcoded in a static C# `enum`:
1. **Dynamic Configuration Schema:**
   - Modify [ServerConfiguration.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/ServerConfiguration.cs) to transition `FirewallMode` from a rigid enum into a dynamic descriptor or class `FirewallModeDef` stored in the database.
   - Save custom modes in a structured local JSON database file (e.g., `CustomModes.json` in the `%AppData%\TinyWall` folder).
2. **WCF Controller-Service IPC Expansion:**
   - Expand the WCF IPC communication contracts in [Message.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/Message.cs) and [TinyWallService.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/TinyWallService.cs) to allow sending dynamically serialized custom mode rule definitions from the Tray controller to the core service.
3. **Dynamic Rule Compilation:**
   - In `TinyWallService.AssembleActiveRules()`, dynamically compile WFP (Windows Filtering Platform) filter entries by parsing the custom rules stored under the active custom mode definition.
4. **Dynamic Tray Menu Rendering:**
   - In `TinyWallController.InitializeComponent()` and `TrayMenu_Opening`, clear and dynamically generate the `mnuMode` dropdown list items by fetching the active custom modes list.

---

## 12. Auto-Learn Mode with Interactive Prompts [PENDING]
**Difficulty:** High / Very High
**Goal:** Replace passive, silent learning modes with an active, interactive whitelisting assistant. When an unrecognized process attempts to establish a network connection, the firewall suspends/buffers the transaction, captures detailed execution context, and alerts the user via a sleek, interactive notification modal requesting permission.

### Architectural Changes & Implementation Plan

#### 1. Core Block Hooking & Context Extraction
- Leverage the Windows Filtering Platform (WFP) logging layers or active connections event audits in `FirewallLogWatcher.cs` in real-time.
- On connection attempt by an unknown process, hold/suspend the traffic or extract details:
  - Process ID (PID) and full Executable Path.
  - Direction (Inbound/Outbound), Target/Source IP, Remote Port, and Network Protocol (TCP/UDP).
  - Process signature status via `WinTrust.VerifyFileAuthenticode()`.

#### 2. Duplex WCF IPC IPC Notifications
- Expand the IPC system in [Message.cs](file:///d:/Giraf%20Dropbox/Giraf%20Creatives/zOo%20Backup/Jellyfin%20Net%20block/TinyWall/TinyWall/Message.cs) to support a duplex callback architecture. 
- The background `TinyWallService` will invoke a callback (`INotifyBlockedProcess`) to push the connection event details to the active session's Tray Controller (`TinyWallController`).

#### 3. Premium Interactive Prompt Form (`InteractivePromptForm.cs`)
- Build a custom, dark-themed WinForms prompt window displaying:
  - **Process Context:** Executable name, publisher information (from the certificate), and full system path.
  - **Connection Details:** "Attempted outbound connection to 104.244.42.1 on Port 443 (HTTPS)".
  - **Quick-Action Options:**
    - *Allow (Web Access Only):* Automatically creates rules allowing TCP 80/443.
    - *Allow (Unrestricted):* Whitelists the executable fully.
    - *Block (Once):* Blocks the current attempt, but prompts again next time.
    - *Block (Always):* Creates an explicit block exception rule.

#### 4. Dynamic Transactional Rule Insertion
- Upon user selection, serialize the decision and transmit it back to `TinyWallService` via WCF.
- Programmatically construct the WFP `Filter` parameters, register the new filter dynamically, and reload WFP rules immediately without service disruption.

---

## 13. Geo-IP Real-Time Map & Flag Auditing [PENDING]
**Difficulty:** High / Very High
**Goal:** Track the geographical locations of network requests. The Security Monitor resolves remote destination IPs to their country of origin, displaying country flag icons, details, and aggregated geographic bandwidth charts to visually highlight rogue off-shore telemetry transfers.

### Architectural Changes & Implementation Plan

#### 1. Embedded Geo-IP Database Integration
- Package a lightweight, highly optimized local Geo-IP database (e.g. MaxMind GeoLite2 Country binary database `.mmdb` format) inside the application directory.
- Build a local lookup helper `GeoIPResolver.cs` that performs fast, zero-latency binary IP-to-Country searches on background threads.

#### 2. Live Geo Auditing in Monitor Feed
- Add flag icons and Country Name columns to the Security Monitor Live Grid.
- Run queries asynchronously: as connection records stream in, queue their remote IPs in a lookup worker thread, updating the UI elements once resolved to prevent GUI freezes.

#### 3. Visual Regional Metrics Dashboard
- Add a geographic breakdown chart (e.g., a styled donut chart or vertical bars) mapping total bandwidth consumed by geographic regions (e.g. USA, China, Ireland, etc.).

---

## 14. Parent-Process Security Guard (Anti-Exploit Blocker) [PENDING]
**Difficulty:** Very High / Extremely High
**Goal:** Add context-aware exploit mitigation. FoxWall evaluates process creation trees to prevent compromised applications (e.g., browsers, MS Word, or PDF readers) from spawning command shells (`cmd.exe`, `powershell.exe`) that bypass standard whitelists.

### Architectural Changes & Implementation Plan

#### 1. Real-Time Process Tree Tracker
- Create an asynchronous WMI process watcher or hook into native ETW (Event Tracing for Windows) process trace logs.
- Map and maintain an active memory tree representation of parent-child PID relationships.

#### 2. Anomaly Exploitation Heuristics Engine
- Define a dictionary of high-risk parent-child mappings:
  - *Parents:* Web browsers (`chrome.exe`, `msedge.exe`), document readers (`winword.exe`, `acrord32.exe`, `excel.exe`).
  - *Blocked Shells:* `cmd.exe`, `powershell.exe`, `wscript.exe`, `mshta.exe`.
- If an outbound connection is initiated by a blocked shell, check the mapped process tree. If its parent tree links back to a high-risk application, intercept and forcefully block the network transaction.

#### 3. Security Warning Dialog
- Trigger a specialized notification highlighting the parent-child attack vector (e.g., "Exploit Blocked: MS Word attempted to connect to the internet via hidden PowerShell shell").

---

## 15. FoxWall Security Monitor Dashboard [PENDING]
**Difficulty:** Extremely High
**Goal:** Design and build a standalone, dedicated Security Monitor window separate from general settings. This visual dashboard monitors, tracks, and charts all internet traffic in real-time, displaying live bandwidth usage, dynamic graphs, payload exfiltration auditing, and behavioral anti-keylogger heuristics.

### Architectural Changes & Implementation Plan

#### 1. Separate Security Monitor Window (`SecurityMonitorForm.cs`)
- Implement a dedicated dashboard UI using our high-end premium Dark Purple visual palette.
- Form layout tabs:
  - **Live Sockets Feed:** A clean, sorting-enabled datagrid showing active processes, target IPs, remote ports, protocols, and data rates (KB/s).
  - **Bandwidth Analytics:** Dynamic, smooth-rendering bar graphs showing total bytes sent/received per process, sorted automatically from heaviest consumer to lowest.
  - **Flow Detail Visualizations:** Selected processes display real-time sparklines or area graphs mapping bandwidth over a moving time window (last 60 seconds/5 minutes).

#### 2. Asynchronous Sockets and Volume Auditing
- Create an auditing engine in the background service that tracks active sockets and bandwidth.
- Regularly fetch stats using native helper layers like `GetExtendedTcpTable` / `GetExtendedUdpTable` or direct WFP flow-associated metadata.
- Cache a rolling window history database of data flow rates (bytes sent/received) mapped by executable path to avoid high CPU overhead.

#### 3. Media Transfer Auditing & Exfiltration Defense
- Flag high-volume outbound data transfers to unclassified external IPs.
- Add basic packet-type validation checking: analyze initial transfer bytes to confirm media files (e.g., matching JPEG, PNG, MP4 magic number signatures) matching their claimed protocol footprints, flagging anomalous binary dumps as potential exfiltration risks.

#### 4. Heuristic Keylogger & Spyware Monitoring
- Integrate pattern analytics designed to proactively flag keyloggers and spyware:
  - *Low-and-Slow Beaconing:* Flag background, non-interactive, unsigned processes that periodically transmit tiny telemetry packets (heartbeats) to untrusted or low-reputation IP blocks at precise intervals.
  - *Correlation Heuristics:* Monitor network outbound surges correlating closely with physical human keystroke/mouse activity in non-active UI processes.

#### 5. Integration Hooks & Segregation
- To respect the **AGENT.md** rules, keep this entire monitoring dashboard completely isolated inside dedicated files (e.g., `SecurityMonitorForm.cs`, `TrafficAuditorEngine.cs`, `ConnectionTracker.cs`).
- Trigger this dashboard directly via a lightweight hook on the Tray Controller context menu (e.g., "Show Security Monitor...").

---

## 16. Full Web-Based Settings & Rules UI Migration [PENDING]
**Difficulty:** High
**Goal:** Migrate the entire, original legacy C# WinForms application setup, configuration panels, application exceptions listings, password locks, and controller prompts into a fully unified, modern single-page web application.

### Architectural Changes & Implementation Plan

#### 1. Transition to Embedded Web App Architecture
- Replace all legacy Windows Forms forms (`SettingsForm.cs`, `ApplicationExceptionForm.cs`, `PasswordForm.cs`, etc.) with a single, premium web-based administration dashboard.
- The controller will serve this comprehensive control center over the local HTTP server, ensuring high-speed access to all setting profiles, exception rulesets, and blocking states.

#### 2. Bidirectional API Expansion
- Build out full RESTful API endpoints and WebSocket listeners on the local `HttpListener` backend to map all read/write settings operations.
- Actions like password lock validation, profile switches, custom rule additions, and system audits will run via highly efficient, secure JSON messaging over loopback connection.

#### 3. Legacy UI Deprecation
- Ditch native WinForms controls and windows entirely, except for the tiny system tray notifications and context hooks.
- Offloading GUI rendering entirely to the browser prevents main thread blocks, resolves high-DPI scaling issues natively, and supports unlimited custom styling and animations with zero CPU overhead for C# code.

---

## 17. Scheduled PC Power Manager (Shutdown/Restart/Sleep/Lock) [COMPLETED]
**Difficulty:** Medium-High
**Goal:** Empower users to automate PC power operations (Shutdown, Restart, Sleep, and Lock) through time-based durations, exact daily times, system idle checks, network bandwidth checks, or media streaming activity (Jellyfin streams).

### Architectural Changes & Technical Design
1. **Thread-Safe Core Scheduling Engine (`PowerScheduler.cs`):**
   - Keeps track of running countdowns, target date-times, trigger types, and execution modes.
   - Monitors user idle time via native Win32 `GetLastInputInfo` inside a background polling thread.
   - Audits inbound Jellyfin network traffic on default streaming ports (`8096` / `8920`) to hold/suspend power countdowns automatically while actively playing movies/tv shows.
2. **REST API Endpoint Registration (`DashboardServer.cs`):**
   - Exposes clean local endpoints (`/api/power/status`, `/api/power/schedule`, `/api/power/cancel`) running securely on loopback interfaces.
   - Verifies admin passwords before allowing cancellation if settings are locked.
3. **Smart Hybrid Delay Execution:**
   - Pre-configures a default transition sequence waiting exactly 5 minutes gracefully to let background processes close and save files, followed by an immediate forced power-off command.
4. **Premium React View (`PowerScheduler.jsx`):**
   - Designed with beautiful visual cards, customized dual sliders, password checks, final 60-second flashing warning alert bars, and chime sounds.

