# Guidelines for AI Coding Agents in FoxWall

Welcome, Antigravity AI! Please read and adhere to these guidelines strictly for any modifications made to this codebase.

## ⚠️ CRITICAL ARCHITECTURAL REQUIREMENT ⚠️

**Keep Changes Clean and Isolated for Seamless Upstream Synchronization.**

This project has a dedicated **"Update from Upstream"** feature that pulls updates from the original, official TinyWall repository and merges them into our codebase. To avoid merge hell and breaking future updates, we must keep our codebase easy to synchronize.

### Mandatory Coding Rules

1. **Prefer Separate Files over Patches:**
   - Any new feature, custom component, styling utility, or helper class **MUST** be written in its own, new, dedicated file/class.
   - Do **NOT** rewrite, bloat, or add large chunks of logic directly inside existing original files.

2. **Keep Original File Modifications Minimal (Lightweight Hooks):**
   - If an existing original file (e.g., `SettingsForm.cs`, `TinyWallController.cs`, `TinyWallService.cs`) must trigger our custom features, only add a **minimal, lightweight hook call**.
   - Example of a correct lightweight hook:
     ```csharp
     // In SettingsForm.cs (minimal modification)
     private void btnImport_Click(object sender, EventArgs e)
     {
         // Lightweight hook delegates to separate, custom class logic
         CustomImportManager.HandleImport(this);
     }
     ```
   - Keep the custom code cleanly segregated in its own helper/class (e.g., `CustomImportManager.cs`).

3. **Comments & Demarcation:**
   - Always wrap any modified lines in original files with clear comment demarcations:
     ```csharp
     // [FoxWall Enhancement] - Start of Custom Import Logic
     ...
     // [FoxWall Enhancement] - End of Custom Import Logic
     ```

4. **Verify Upstream Mergability:**
   - Before completing a task, verify that no core abstractions have been refactored in a way that makes git-merges with upstream TinyWall conflict-heavy.

## 🏷️ VERSIONING POLICY

Every custom feature, enhancement, or bug fix should increment the custom **FoxWall** version.
- **Version Format:** `FoxWall X.Y.Z` (e.g., currently `1.0.1` after adding the settings import/export feature).
- **Minor Changes / Features:** Every new feature should trigger a new small version update by incrementing the patch number (e.g., `1.0.0` -> `1.0.1`).
- **Settings UI Display:** Programmatically append the `FoxWall X.Y.Z` string to `lblVersion.Text` in `SettingsForm.cs` and adjust layout elements down programmatically to prevent any overlap.
- **Installer Rebranding:** Ensure the installer (`InstallerForm.cs`) titles, header banners, logs, and completion message box are updated to match the active FoxWall version to let the user confirm they are installing/updating to the correct version.
