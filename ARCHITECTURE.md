# FoxWall Architecture Reference

> **Purpose:** This file captures the full architecture, data flow, file roles, and coding patterns of the FoxWall firewall project. AI agents should read THIS FILE FIRST before reading any source files. In most cases, this document alone provides enough context to implement features without reading the entire codebase.
>
> **Last updated:** 2026-06-04 (FoxWall v1.3.6)

---

## Table of Contents
1. [Project Overview](#1-project-overview)
2. [Solution & Project Structure](#2-solution--project-structure)
3. [Architecture: Two-Process Model](#3-architecture-two-process-model)
4. [The FirewallMode System](#4-the-firewallmode-system)
5. [IPC Messaging (Named Pipes)](#5-ipc-messaging-named-pipes)
6. [WFP (Windows Filtering Platform) Rule System](#6-wfp-windows-filtering-platform-rule-system)
7. [Configuration & Persistence](#7-configuration--persistence)
8. [UI Layer (WinForms + Web Dashboard)](#8-ui-layer-winforms--web-dashboard)
9. [Key Data Classes](#9-key-data-classes)
10. [Common Patterns & Conventions](#10-common-patterns--conventions)
11. [How to Add a New Firewall Mode](#11-how-to-add-a-new-firewall-mode)
12. [How to Add a New IPC Message](#12-how-to-add-a-new-ipc-message)
13. [How to Add a New Tray Menu Item](#13-how-to-add-a-new-tray-menu-item)
14. [How to Add a New Web API Endpoint](#14-how-to-add-a-new-web-api-endpoint)
15. [File Quick Reference](#15-file-quick-reference)
16. [Files You May Need to Read](#16-files-you-may-need-to-read)

---

## 1. Project Overview

FoxWall is a **custom fork of TinyWall**, a lightweight Windows firewall that uses the **Windows Filtering Platform (WFP)** to control network access. It runs as a Windows Service + tray controller pair. Our fork adds dark mode, a web dashboard, a power scheduler, wildcard whitelisting, and more.

**Key facts:**
- Language: **C# (.NET 6+ / Windows Desktop)**
- UI: **WinForms** (native tray app) + **React web dashboard** (served via embedded HTTP server)
- Firewall engine: **WFP** (Windows Filtering Platform) via P/Invoke and custom managed wrappers
- IPC: **Named Pipes** (custom serialization, NOT WCF despite old comments)
- Config storage: **Encrypted JSON** file on disk
- Build: `msbuild TinyWall.sln /p:Configuration=Release`

---

## 2. Solution & Project Structure

```
TinyWall/                          ← Solution root
├── TinyWall.sln                   ← Main solution file
├── AGENT.md                       ← AI coding rules (MUST READ before any changes)
├── plan.md                        ← Feature roadmap
├── version.json                   ← FoxWall version number {"Version": "1.3.6"}
│
├── TinyWall/                      ← Main C# project (both Service + Controller in one binary)
│   ├── TinyWall.csproj
│   ├── Program.cs                 ← Entry point - decides if running as Service or Controller
│   ├── TinyWallService.cs         ← The Windows Service (runs as SYSTEM) - 2200+ lines
│   ├── TinyWallController.cs      ← The tray app (runs as user) - 1550+ lines
│   ├── Controller.cs              ← Thin IPC client wrapper (sends commands to service)
│   ├── DashboardServer.cs         ← Embedded HTTP server for React web dashboard
│   ├── ServerConfiguration.cs     ← Config model + FirewallMode enum
│   ├── Message.cs                 ← IPC message types and serialization
│   ├── MessageType.cs             ← Enum of all IPC message codes
│   ├── FirewallLogWatcher.cs      ← Watches Windows Security Event Log for WFP events
│   ├── FirewallLogEntry.cs        ← Data model for firewall log events
│   ├── ExceptionSubject.cs        ← Rule subject types (Executable, Service, AppContainer, Global)
│   ├── ExceptionPolicy.cs         ← Rule policy definitions (TCP/UDP ports, full access, etc.)
│   ├── FirewallException.cs       ← FirewallExceptionV3 - a single whitelist/block rule
│   ├── RuleDef.cs                 ← Intermediate WFP rule definition
│   ├── ThemeManager.cs            ← Dark mode styling for all WinForms
│   ├── SettingsForm.cs            ← Main settings dialog
│   ├── SettingsForm.Import.cs     ← Partial: import/export and context menu logic
│   ├── PowerScheduler.cs          ← PC power management (shutdown/restart/sleep/lock)
│   ├── Web_React/                 ← React source for the web dashboard
│   │   ├── src/
│   │   └── vite.config.js
│   ├── Web/                       ← Built web assets served by DashboardServer
│   └── Resources/                 ← Icons, localization strings
│
├── pylorak.Utilities/             ← Utility library (threading, serialization helpers)
├── pylorak.Windows/               ← Windows API wrappers (processes, registry, crypto)
├── pylorak.Windows.WFP/           ← WFP managed wrapper (Engine, Filter, Sublayer, etc.)
├── pylorak.Windows.Services/      ← Windows Service base class
└── TinyWallJellyModeInstaller/    ← Custom installer project
```

---

## 3. Architecture: Two-Process Model

```
┌──────────────────────────────────────────────────────────────────────┐
│                        SYSTEM SERVICE                                │
│  TinyWallServer (TinyWallService.cs)                                │
│  - Runs as NT SYSTEM                                                │
│  - Owns the WFP engine (installs/removes firewall filters)          │
│  - Holds the authoritative ServerConfiguration                      │
│  - Listens on Named Pipe "TinyWallController" for commands          │
│  - Watches firewall event log (Learning mode)                       │
│  - Manages rule inheritance, wildcard resolution                    │
│  - Processes commands on a single-threaded BlockingCollection queue  │
└──────────────────────┬───────────────────────────────────────────────┘
                       │ Named Pipe IPC
                       │ (TwMessage objects, JSON serialized)
┌──────────────────────▼───────────────────────────────────────────────┐
│                     TRAY CONTROLLER                                  │
│  TinyWallController (TinyWallController.cs)                         │
│  - Runs as current user                                             │
│  - System tray icon + context menu                                  │
│  - All WinForms UI dialogs (Settings, Connections, etc.)            │
│  - Sends commands via Controller.cs → PipeClientEndpoint            │
│  - Polls server for config changes (on menu open, timer, etc.)      │
│  - Runs DashboardServer (HTTP on localhost:5678)                     │
│  - Shows balloon tips, handles hotkeys                              │
└──────────────────────────────────────────────────────────────────────┘
```

**Critical flow:** The Controller NEVER touches WFP directly. All firewall changes go through IPC to the Service.

**Single binary:** `Program.cs` checks command-line args to decide whether to run as service (`TinyWallService`) or as tray controller (`TinyWallController`). Both classes live in the same project/assembly.

---

## 4. The FirewallMode System

### Enum Definition (ServerConfiguration.cs, line ~9)
```csharp
public enum FirewallMode
{
    Normal,         // 0 - Block all, allow only whitelisted
    BlockAll,       // 1 - Block everything (no exceptions)
    AllowOutgoing,  // 2 - Block inbound, allow all outbound
    Disabled,       // 3 - Allow everything (firewall off)
    Learning,       // 4 - Allow everything + watch event log + auto-whitelist
    JellyMode,      // 5 - Block all except local Jellyfin
    Unknown = 100   // Service not yet contacted
}
```

### How Each Mode Affects WFP Rules (TinyWallService.cs → AssembleActiveRules())

| Mode | Default WFP Rule | User Rules Applied? | LogWatcher Enabled? |
|------|------------------|--------------------|--------------------|
| Normal | Block all | ✅ Yes | ❌ No |
| BlockAll | Block all | ❌ No | ❌ No |
| AllowOutgoing | Block all + Allow outbound | ✅ Yes | ❌ No |
| Disabled | Allow all | ❌ No | ❌ No |
| Learning | Allow all | ✅ Yes | ✅ Yes |
| JellyMode | Block all + Allow Jellyfin local | ❌ No | ❌ No |

### Mode Switch Flow
1. User clicks mode in tray menu → `mnuModeXxx_Click()` in TinyWallController.cs
2. Calls `SetMode(FirewallMode.Xxx)` → `GlobalInstances.Controller.SwitchFirewallMode(mode)`
3. Controller.cs sends `TwMessageModeSwitch` over named pipe
4. Service receives in `ProcessCmd()` case `MessageType.MODE_SWITCH`:
   - Enables/disables LogWatcher
   - Commits any pending learned rules
   - Sets `VisibleState.Mode = newMode`
   - Optionally saves as `StartupMode` (not for Learning/Disabled)
   - Calls `InstallFirewallRules()` to rebuild all WFP filters
5. Service returns response, controller shows balloon tip + updates tray icon

---

## 5. IPC Messaging (Named Pipes)

### Message Types (MessageType.cs)
```
Read commands (>31):      GET_SETTINGS, GET_PROCESS_PATH, READ_FW_LOG, IS_LOCKED
Unprivileged write (>1023): UNLOCK
Privileged write (>2047):   MODE_SWITCH, REINIT, PUT_SETTINGS, LOCK, SET_PASSPHRASE, STOP_SERVICE, etc.
Service-to-client:          DATABASE_UPDATED
Service-internal (>4095):   ADD_TEMPORARY_EXCEPTION, RELOAD_WFP_FILTERS, DISPLAY_POWER_EVENT
```

### Message Classes (Message.cs)
All message classes inherit from `TwMessage`. Key pattern:
```csharp
public class TwMessageModeSwitch : TwMessage
{
    public FirewallMode Mode;
    public static TwMessageModeSwitch CreateRequest(FirewallMode mode) { ... }
    public TwMessageModeSwitch CreateResponse(FirewallMode mode) { ... }
}
```

### How to send a command from controller to service:
```csharp
// In Controller.cs:
public MessageType SwitchFirewallMode(FirewallMode mode)
{
    return Endpoint.QueueMessage(TwMessageModeSwitch.CreateRequest(mode)).Response.Type;
}
```

### Config sync pattern:
- Service has authoritative config + a `ServerChangeset` GUID
- Controller calls `GetServerConfig()` with its cached changeset
- If changesets differ, service sends full config + state back
- Controller updates `ActiveConfig.Service` and `FirewallState`

### Client Notifications:
- Service can push notification types via `VisibleState.ClientNotifs` list
- Controller checks this list in `LoadSettingsFromServer()` and processes them
- Currently only `DATABASE_UPDATED` exists

---

## 6. WFP (Windows Filtering Platform) Rule System

### Rule Assembly Pipeline
```
ServerConfiguration.ActiveProfile.AppExceptions (user rules)
    ↓
AssembleActiveRules() in TinyWallService.cs
    ↓ Creates List<RuleDef>
    ↓ Resolves wildcards, handles child process inheritance
    ↓ Converts paths to kernel format (NativeNt)
    ↓
InstallFirewallRules()
    ↓ Opens WFP transaction
    ↓ Deletes all existing TinyWall WFP objects
    ↓ Re-registers provider, sublayers
    ↓ Installs all filters via ConstructFilter()
    ↓ Commits transaction
```

### Filter Weights (priority, higher = evaluated first)
```
Blocklist       = 9,000,000
RawSocketPermit = 8,000,000
RawSocketBlock  = 7,000,000
UserBlock       = 6,000,000
UserPermit      = 5,000,000
DefaultPermit   = 4,000,000
DefaultBlock    = 3,000,000
```

### WFP Layers Used
The firewall installs filters on these WFP layers (both IPv4 and IPv6):
- `ALE_AUTH_CONNECT` (outbound connections)
- `ALE_AUTH_RECV_ACCEPT` (inbound connections)
- `ALE_AUTH_LISTEN` (listening sockets)
- `INBOUND_TRANSPORT_DISCARD`
- `OUTBOUND_ICMP_ERROR` / `INBOUND_ICMP_ERROR`
- `ALE_RESOURCE_ASSIGNMENT` (raw sockets)

### RuleDef (RuleDef.cs)
Intermediate representation before WFP filter:
```csharp
public class RuleDef {
    Guid ExceptionId;
    string Name;
    ExceptionSubject Subject;  // Who (exe, service, global)
    RuleAction Action;         // Allow or Block
    RuleDirection Direction;   // In, Out, InOut
    Protocol Protocol;         // TCP, UDP, Any, TcpUdp
    ulong Weight;              // Filter priority
    string? RemoteAddresses;   // IP filter (or "LocalSubnet")
    string? LocalPorts;        // Port filter
    string? RemotePorts;
    string? Application;       // Exe path (kernel format)
    // ...
}
```

---

## 7. Configuration & Persistence

### ServerConfiguration (ServerConfiguration.cs)
```csharp
ServerConfiguration
├── ConfigVersion: int
├── Blocklists: BlockListSettings
├── LockHostsFile: bool
├── AutoUpdateCheck: bool
├── StartupMode: FirewallMode (saved mode for next boot)
├── ActiveProfileName: string
├── Profiles: List<ServerProfileConfiguration>
│   └── ServerProfileConfiguration
│       ├── ProfileName: string
│       ├── SpecialExceptions: List<string> (named app groups from Database)
│       ├── AllowLocalSubnet: bool
│       ├── DisplayOffBlock: bool
│       └── AppExceptions: List<FirewallExceptionV3> (the actual rules)
```

### Storage
- **Service config:** Encrypted JSON at `%ProgramData%\TinyWall\` (or exe dir in DEBUG)
- **Controller settings:** `ControllerSettings` saved to user's `%AppData%`
- **Database:** `AppDatabase` — bundled known-app definitions for auto-detection
- **Save path:** `ConfigSavePath` in TinyWallService.cs

### ActiveConfig (static)
```csharp
ActiveConfig.Service    // ServerConfiguration (authoritative on service side)
ActiveConfig.Controller // ControllerSettings (UI preferences, language, etc.)
```

### GlobalInstances (GlobalInstances.cs)
```csharp
GlobalInstances.Controller              // The IPC client (Controller.cs instance)
GlobalInstances.TinyWallControllerInstance // The tray controller instance
GlobalInstances.AppDatabase              // Known application database
GlobalInstances.ServerChangeset          // Current config version GUID
GlobalInstances.ClientChangeset          // Controller's cached version GUID
```

---

## 8. UI Layer (WinForms + Web Dashboard)

### Tray Controller (TinyWallController.cs)
- Inherits `ApplicationContext` (not a Form)
- Creates `NotifyIcon` (Tray) with `ContextMenuStrip` (TrayMenu)
- Key menu items: Mode submenu, Manage (settings), Connections, Lock, Whitelist actions, Security Monitor, Quit
- `UpdateDisplay()` updates tray icon + tooltip based on current mode
- `SetMode()` sends mode switch command to service
- `AddExceptions()` is the central method for whitelisting (used by all whitelist paths)
- Left-click on tray = simulates right-click (opens menu)
- Middle-click = opens Connections form

### Dark Theme (ThemeManager.cs)
- `ThemeManager.ApplyDarkTheme(Form)` — recursively themes all controls
- `ThemeManager.GetToolStripRenderer()` — custom renderer for menus
- Colors: Background `#121212`, Surface `#1E1E1E`, Accent `#000A87`, Text `#FFFFFF`
- Applied in constructor of every Form: `ThemeManager.ApplyDarkTheme(this);`

### Web Dashboard (DashboardServer.cs)
- Embedded `HttpListener` on `http://localhost:5678/`
- Serves React build from `Web/` directory
- REST API endpoints:
  - `GET /api/status` — mode, locked, traffic speeds, version
  - `GET /api/connections` — active TCP/UDP sockets
  - `GET /api/logs` — firewall event log entries
  - `GET /api/analytics/history?range=5m` — traffic history
  - `POST /api/action/panic` — toggle kill switch
  - `POST /api/action/whitelist?path=...` — whitelist an exe
  - `POST /api/action/terminate?pid=...` — kill a process
  - `POST /api/action/open-folder?path=...` — open in Explorer
  - `POST /api/power/schedule`, `GET /api/power/status`, `POST /api/power/cancel`

### React App (Web_React/)
- Vite + React
- Connects to `localhost:5678/api/*`
- Components: SocketsFeed, ProcessActions, PowerScheduler, etc.
- Build output goes to `Web/` directory

---

## 9. Key Data Classes

### FirewallExceptionV3 (FirewallException.cs)
A single firewall rule:
```csharp
public class FirewallExceptionV3 {
    Guid Id;                          // Unique rule ID
    ExceptionSubject Subject;         // WHO: exe path, service name, app container, or global
    ExceptionPolicy Policy;           // WHAT: ports/protocols allowed or blocked
    AppExceptionTimer Timer;          // Permanent, Until_Reboot, or minutes
    DateTime CreationDate;
    bool ChildProcessesInherit;       // Child processes get same rules
    RuleImportance Importance;        // Classification: Critical/Important/Optional/etc.
}
```

### ExceptionSubject Hierarchy (ExceptionSubject.cs)
```
ExceptionSubject (abstract)
├── ExecutableSubject      — matches by exe path
├── ServiceSubject         — matches by exe + Windows service name
├── AppContainerSubject    — matches by UWP package SID
└── GlobalSubject          — matches all processes (singleton)
```

### ExceptionPolicy Hierarchy (ExceptionPolicy.cs)
```
ExceptionPolicy (abstract)
├── TcpUdpPolicy          — granular port-level rules
│   Properties: AllowedRemoteTcpConnectPorts, AllowedLocalTcpListenPorts,
│               AllowedRemoteUdpConnectPorts, AllowedLocalUdpListenPorts
├── HardBlockPolicy       — explicit block
├── UnrestrictedPolicy    — full network access
└── PolicyOfExceptions    — container of sub-policies (for known apps)
```

### FirewallLogEntry (FirewallLogEntry.cs)
Parsed from Windows Security Event Log (events 5154-5159):
```csharp
public record FirewallLogEntry {
    DateTime Timestamp;
    EventLogEvent Event;    // ALLOWED_CONNECTION, BLOCKED_CONNECTION, etc.
    uint ProcessId;
    Protocol Protocol;
    RuleDirection Direction;
    string LocalIp, RemoteIp;
    int LocalPort, RemotePort;
    string AppPath;
}
```

---

## 10. Common Patterns & Conventions

### AGENT.md Rules (CRITICAL — always follow)
1. **New features in NEW files.** Don't bloat existing files.
2. **Minimal hooks in original files.** Only add thin call-sites, delegate to separate classes.
3. **Wrap modifications** with `// [FoxWall Enhancement] - Start/End` comments.
4. **Version bump** in `version.json` for every feature.

### How settings flow Controller → Service:
```csharp
// 1. Clone current config
ServerConfiguration confCopy = Utils.DeepClone(ActiveConfig.Service);
// 2. Modify the copy
confCopy.ActiveProfile.AddExceptions(newExceptions);
// 3. Send to service (which saves, reloads WFP rules)
ApplyFirewallSettings(confCopy);
```

### How mode switch works:
```csharp
// Controller side:
SetMode(FirewallMode.Normal);  // → Controller.SwitchFirewallMode() → pipe → service
UpdateDisplay();

// Service side (ProcessCmd, case MODE_SWITCH):
VisibleState.Mode = newMode;
InstallFirewallRules();  // Rebuilds all WFP filters from scratch
```

### Threading:
- Service processes all commands on a **single thread** via `BlockingCollection<TwRequest> Q`
- `PipeServerDataReceived()` receives from pipe, adds to Q, waits for response
- The service main loop dequeues and calls `ProcessCmd()`
- `FirewallLogWatcher` fires events on a **thread pool thread** — lock `LearningNewExceptions` when accessing

### Learning Mode Flow:
1. Service enables `LogWatcher.Enabled = true` (turns on Windows audit logging)
2. WFP rule = "Allow everything" (so connections succeed)
3. `LogWatcher_EventRecordWritten()` fires for each connection event
4. `AutoLearnLogEntry()` checks if process is already known, if not adds to `LearningNewExceptions`
5. On mode switch or timer tick, `CommitLearnedRules()` merges them into `ActiveConfig.Service`

### Serialization:
- `ISerializable<T>` with `GetJsonTypeInfo()` for source-generated JSON
- `SourceGenerationContext` class provides type info for all serializable types
- Config saved via `SerializationHelper.SerializeToEncryptedFile()`
- IPC messages serialized via `SerializationHelper.Serialize()` / `Deserialize()`

### Wildcard Whitelisting:
- Exe paths can contain `*` and `?` (e.g., `C:\Users\*\AppData\Local\Discord\*\Discord.exe`)
- `WildcardHelper.ResolveAllWildcardPaths()` expands to actual paths on disk
- `WildcardHelper.IsWildcardMatch()` checks if a runtime path matches a wildcard pattern
- Wildcard paths cannot be registered directly in WFP (skipped in ConstructFilter)

---

## 11. How to Add a New Firewall Mode

1. **Add enum value** in `ServerConfiguration.cs` → `FirewallMode` enum
2. **Add WFP rules** in `TinyWallService.cs` → `AssembleActiveRules()` → new `case` in the switch
3. **Add mode switch handling** in `TinyWallService.cs` → `ProcessCmd()` → `MODE_SWITCH` case (enable/disable LogWatcher, decide if saving as StartupMode)
4. **Add tray menu item** in `TinyWallController.cs` → `InitializeComponent()` (declare field, create ToolStripMenuItem, add to mnuMode.DropDownItems, wire Click handler)
5. **Add `MemberNotNull`** attribute for the new field in InitializeComponent's attribute list
6. **Add UpdateDisplay case** in `TinyWallController.cs` → `UpdateDisplay()` (icon + label)
7. **Add SetMode message** in `TinyWallController.cs` → `SetMode()` (balloon tip text)
8. **Add ActiveModeName case** in `TinyWallController.cs` → `ActiveModeName` property
9. **Add click handler** — e.g., `mnuModeAutoAsk_Click()` that calls `SetMode(FirewallMode.AutoAsk)`
10. **Add icon setup** in `InitController()` (set `.Image` for the menu item)

---

## 12. How to Add a New IPC Message

1. **Add enum value** in `MessageType.cs`
2. **Add message class** in `Message.cs`:
   ```csharp
   public class TwMessageMyFeature : TwMessage
   {
       public MyDataType Data;
       public static TwMessageMyFeature CreateRequest(...) { return new() { Type = MessageType.MY_FEATURE, ... }; }
       public TwMessageMyFeature CreateResponse(...) { return new() { Type = MessageType.MY_FEATURE, ... }; }
   }
   ```
3. **Register in SourceGenerationContext** if the type needs JSON serialization
4. **Handle in service** → `ProcessCmd()` → new `case MessageType.MY_FEATURE:`
5. **Send from controller** → add method in `Controller.cs`

---

## 13. How to Add a New Tray Menu Item

1. Declare field: `private ToolStripMenuItem mnuMyItem;` in TinyWallController.cs (field declarations area, ~line 280-296)
2. In `InitializeComponent()`: create, set properties, wire Click event
3. Add to `TrayMenu.Items` or `mnuMode.DropDownItems` at the right position
4. Add the field name to the `[MemberNotNull(...)]` attribute on `InitializeComponent()`
5. Create click handler method

---

## 14. How to Add a New Web API Endpoint

In `DashboardServer.cs` → `HandleApiRequest()` method:
```csharp
case "/api/myendpoint":
    if (request.HttpMethod == "GET")
    {
        responseData = new { ... };
    }
    break;
```

---

## 15. File Quick Reference

### MUST READ before any changes:
| File | Why |
|------|-----|
| `AGENT.md` | Coding rules, isolation requirements, versioning policy |

### Core Architecture (read if you need deep understanding):
| File | Role | Lines |
|------|------|-------|
| `TinyWallService.cs` | Service: WFP engine, rule assembly, command processing, learning mode | ~2250 |
| `TinyWallController.cs` | Controller: tray UI, mode switching, exception management | ~1550 |
| `ServerConfiguration.cs` | Config model, FirewallMode enum, profile management | ~258 |
| `Controller.cs` | IPC client wrapper (thin, sends commands to service) | ~110 |
| `Message.cs` | All IPC message type classes | ~500 |
| `MessageType.cs` | Enum of IPC message codes | ~40 |

### Rule System:
| File | Role |
|------|------|
| `FirewallException.cs` | `FirewallExceptionV3` — a single whitelist/block rule |
| `ExceptionSubject.cs` | Rule subjects (Executable, Service, AppContainer, Global) |
| `ExceptionPolicy.cs` | Rule policies (TcpUdp, HardBlock, Unrestricted) |
| `RuleDef.cs` | Intermediate WFP rule definition |
| `WildcardHelper.cs` | Wildcard path matching and resolution |

### Event Monitoring:
| File | Role |
|------|------|
| `FirewallLogWatcher.cs` | Watches Windows Security Event Log (5154-5159) for connection events |
| `FirewallLogEntry.cs` | Data model for a single firewall event |

### UI:
| File | Role |
|------|------|
| `ThemeManager.cs` | Dark mode styling engine for all WinForms |
| `DashboardServer.cs` | Embedded HTTP server, REST API, serves React app |
| `SettingsForm.cs` | Main settings dialog |
| `SettingsForm.Import.cs` | Import/export, context menu, column management |
| `ApplicationExceptionForm.cs` | Exception editor dialog |
| `ConnectionsForm.cs` | Active connections viewer |
| `PowerScheduler.cs` | PC power management scheduler |

### Infrastructure:
| File | Role |
|------|------|
| `GlobalInstances.cs` | Static singletons (Controller, AppDatabase, Changesets) |
| `SerializationHelper.cs` | JSON serialization + encryption helpers |
| `Utils.cs` | General utilities (process paths, deep clone, logging) |
| `PipeClientEndpoint.cs` | Named pipe client |
| `PipeServerEndpoint.cs` | Named pipe server |

---

## 16. Files You May Need to Read

**For most tasks, this document is sufficient.** Only read source files if you need:

- **Exact method signatures or line numbers** for surgical edits
- **Complex logic understanding** (e.g., how wildcard resolution chains work — read `TinyWallService.cs` lines 200-360)
- **Adding new serializable types** — read `Message.cs` to see the pattern for new TwMessage subclasses
- **UI styling details** — read `ThemeManager.cs` to match color tokens and control styling patterns

**Priority read list when implementing a new feature:**
1. This file (`ARCHITECTURE.md`) — always first
2. `AGENT.md` — coding rules
3. The specific section of the file you're modifying (use line number references from this doc)
