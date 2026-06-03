# AutoAsk Mode — Interactive Learning Mode with Permission Prompts

## Overview

Add a new firewall mode called **AutoAsk** that behaves like the existing **Learning** mode (allows all traffic, watches the event log for new connections), but instead of silently auto-whitelisting every process, it **blocks by default** and **prompts the user** with an interactive dialog asking them whether to Allow or Block each new unrecognized process before creating a permanent rule.

### How Existing Learning Mode Works (for context)
1. `FirewallMode.Learning` installs a WFP "Allow everything" rule.
2. `FirewallLogWatcher` watches the Windows Security event log for connection events (5154–5159).
3. When a new process is seen, `AutoLearnLogEntry()` silently adds it to `LearningNewExceptions` — no user interaction.
4. Those rules are committed on mode switch or timer tick.

### How AutoAsk Mode Will Differ
1. Installs WFP rules identical to **Normal mode** (block all by default, allow only whitelisted apps) — so unknown processes are **blocked immediately**.
2. `FirewallLogWatcher` is also enabled to capture blocked connection attempts.
3. When a **blocked** event is detected for an unknown process, the service queues it and **notifies the controller** (tray app).
4. The controller displays a **premium dark-themed interactive prompt** asking the user to Allow or Block.
5. Based on the user's decision, a permanent exception rule is created (or a permanent block rule).

---

## User Review Required

> [!IMPORTANT]
> **WFP cannot "hold" traffic.** Windows Filtering Platform drops blocked packets immediately — it cannot buffer/suspend a connection attempt and release it later. This means:
> - In AutoAsk mode, the first connection attempt from an unknown app **will fail** (be dropped by WFP).
> - Once the user clicks "Allow", the rule is added and the app's **next** connection attempt will succeed.
> - Most apps retry connections automatically, so this is seamless in practice.
> - This is the same behavior as all professional interactive firewalls (e.g., Comodo, ESET, ZoneAlarm).

> [!IMPORTANT]
> **Prompt Form Design:** The interactive prompt will be a WinForms dialog (matching the existing dark theme) rather than a web-based prompt. This keeps it fast, always-on-top, and independent of the browser dashboard. Does this approach work for you, or would you prefer the prompt to appear in the web dashboard?

## Open Questions

1. **Deduplication cooldown:** When a blocked app retries rapidly (e.g., 10 attempts/second), should we suppress duplicate prompts for the same executable? I suggest a **30-second cooldown** per unique executable path — one prompt per app, ignore repeats for 30s. Sound good?

2. **Block (Always) persistence:** Should "Block (Always)" create a visible entry in the Exceptions list marked as a Hard Block, or should it be a separate hidden blocklist? I recommend adding it as a visible Hard Block exception (same as existing block rules).

---

## Proposed Changes

### Component 1: FirewallMode Enum

#### [MODIFY] [ServerConfiguration.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/ServerConfiguration.cs)
- Add `AutoAsk` to the `FirewallMode` enum (value `6`, before `Unknown = 100`).
- Minimal one-line change.

---

### Component 2: Service — WFP Rules & Event Handling

#### [MODIFY] [TinyWallService.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/TinyWallService.cs)
Lightweight hooks only (per AGENT.md):

1. **`AssembleActiveRules()`** — Add a `case FirewallMode.AutoAsk:` that installs the same "Block everything" + user rules as `Normal` mode. (~3 lines)

2. **`ProcessCmd()` MODE_SWITCH case** — Enable `LogWatcher` for both `Learning` and `AutoAsk` modes. (~1 line change)

3. **`ProcessCmd()` MODE_SWITCH case** — Prevent `AutoAsk` from being saved as `StartupMode` (same as `Learning`). (~1 line)

4. **`AutoLearnLogEntry()`** — When in `AutoAsk` mode, instead of silently whitelisting, push blocked entries to a new `AutoAskPendingQueue` and signal the controller via `VisibleState.ClientNotifs`. (Delegate to new helper class)

#### [NEW] [AutoAskManager.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/AutoAskManager.cs)
Isolated helper class containing:
- `AutoAskPendingEntry` — record for queued blocked process info (path, remote IP, port, protocol, timestamp).
- `AutoAskPendingQueue` — thread-safe queue of pending entries with deduplication (30s cooldown per exe path).
- `GetPendingEntries()` / `ClearPendingEntries()` — methods for the controller to poll/consume pending prompts.

---

### Component 3: IPC Messages

#### [MODIFY] [MessageType.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/MessageType.cs)
- Add `AUTOASK_PENDING_ENTRIES` to the service-to-client messages section. (~1 line)

#### [MODIFY] [Message.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/Message.cs)
- Add a `TwMessageAutoAskEntries` class for the controller to poll pending AutoAsk entries from the service.

---

### Component 4: Controller — Tray Menu & Prompt UI

#### [MODIFY] [TinyWallController.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/TinyWallController.cs)
Lightweight hooks only:

1. **`InitializeComponent()`** — Add `mnuModeAutoAsk` menu item to mode dropdown. (~10 lines, matching JellyMode pattern)
2. **`UpdateDisplay()`** — Add `case FirewallMode.AutoAsk:` for tray icon and label. (~5 lines)
3. **`SetMode()`** — Add `FirewallMode.AutoAsk` user message string. (~1 line)
4. **`ActiveModeName`** — Add `AutoAsk` case. (~1 line)
5. **New `mnuModeAutoAsk_Click()`** handler — delegates to `SetMode(FirewallMode.AutoAsk)`. (~8 lines, same as other mode handlers)
6. **`LoadSettingsFromServer()`** — Add `AUTOASK_PENDING_ENTRIES` notification handler that delegates to `AutoAskPromptManager.ShowPendingPrompts()`. (~3 lines)

#### [NEW] [AutoAskPromptManager.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/AutoAskPromptManager.cs)
Controller-side manager that:
- Polls the service for pending AutoAsk entries on a timer (every 2–3 seconds while in AutoAsk mode).
- For each pending entry, shows the `AutoAskPromptForm`.
- On "Allow", calls `AddExceptions()` to whitelist the app.
- On "Block (Always)", adds a hard-block exception rule.
- On "Block (Once)", suppresses for current session only.

#### [NEW] [AutoAskPromptForm.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/AutoAskPromptForm.cs)
Premium dark-themed WinForms dialog displaying:
- **Process Info:** Executable name, full path, digital signature status (signed/unsigned), publisher name.
- **Connection Details:** "Attempted outbound connection to `104.244.42.1` on port `443` (HTTPS)".
- **Action Buttons:**
  - ✅ **Allow (Unrestricted)** — Whitelist fully.
  - 🌐 **Allow (Web Only)** — TCP 80/443 only.
  - 🚫 **Block (Once)** — Skip this time, ask again later.
  - ⛔ **Block (Always)** — Permanent hard block rule.
- Styled with `ThemeManager.ApplyDarkTheme()` — matches the existing dark purple UI.
- Always-on-top, non-modal (allows multiple to queue but shows one at a time).

---

### Component 5: Dashboard Integration

#### [MODIFY] [DashboardServer.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/DashboardServer.cs)
- The `/api/status` endpoint already reports `mode` — `AutoAsk` will automatically appear.
- No additional web UI changes needed for v1 (the prompts are native WinForms).

---

### Component 6: Tray Icon

The existing `Resources.Icons` already has colored shield icons. For AutoAsk mode I'll use `shield_blue_small` with a slight twist — or reuse an existing icon. No new icon resource needed for v1 (we can use the orange/amber shield if available, or fall back to the blue one used by Learning mode with a different tray tooltip).

> [!NOTE]
> If you want a distinct icon color for AutoAsk (e.g., orange), we would need to add a new icon resource. For v1, I'll reuse `shield_blue_small` and differentiate via the tooltip text "AutoAsk Mode".

---

## Summary of Files Changed

| File | Type | Change Size |
|------|------|-------------|
| [ServerConfiguration.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/ServerConfiguration.cs) | MODIFY | +1 line (enum value) |
| [MessageType.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/MessageType.cs) | MODIFY | +1 line |
| [Message.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/Message.cs) | MODIFY | +~30 lines |
| [TinyWallService.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/TinyWallService.cs) | MODIFY | +~15 lines (hooks) |
| [TinyWallController.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/TinyWallController.cs) | MODIFY | +~30 lines (hooks) |
| [AutoAskManager.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/AutoAskManager.cs) | **NEW** | ~100 lines |
| [AutoAskPromptManager.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/AutoAskPromptManager.cs) | **NEW** | ~120 lines |
| [AutoAskPromptForm.cs](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/TinyWall/AutoAskPromptForm.cs) | **NEW** | ~250 lines |
| [version.json](file:///d:/Giraf Dropbox/Giraf Creatives/zOo Backup/Jellyfin Net block/TinyWall/version.json) | MODIFY | Version bump to 1.4.0 |

---

## Verification Plan

### Build
- `msbuild TinyWall.sln /p:Configuration=Release` — must compile cleanly with zero errors.

### Manual Verification
1. Launch FoxWall, right-click tray icon → Change Mode → AutoAsk Mode.
2. Open an unwhitelisted application (e.g., a random browser or `curl.exe`).
3. Verify the prompt appears with process name, path, and connection details.
4. Click "Allow (Unrestricted)" → verify the app can now connect.
5. Block an app → verify it remains blocked and no further prompts appear for it.
6. Switch back to Normal mode → verify normal firewall behavior resumes.
