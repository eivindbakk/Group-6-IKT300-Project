# Microkernel - IKT300 Project

A microkernel architecture implementation with plugin-based process isolation. 

**Group 6 - University of Agder**

---

## Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows is recommended

---

## How to Run

### Option 1: Using the provided run.bat

```batch
.\run.bat
```

### Option 2: Manual Commands

```powershell
dotnet build
dotnet run --project Microkernel
```

---

## Kernel Commands

| Command | Description |
|---------|-------------|
| `help` | Show all commands |
| `status` | Show kernel status |
| `plugins` | List loaded plugins |
| `load <path>` | Load a plugin executable |
| `unload <plugin>` | Unload a plugin |
| `crash <plugin>` | Kill a plugin (fault isolation test) |
| `restart <plugin>` | Restart a plugin |
| `debug on` | Enable debug output |
| `debug off` | Disable debug output |
| `mute` | Mute console output |
| `unmute` | Unmute console output |
| `exit` | Stop kernel and exit |

---

## Event Commands

| Command | Description |
|---------|-------------|
| `demo` | Run demo with all event types |
| `userlogin` | Send UserLoggedInEvent (random user) |
| `userlogin <name>` | Send UserLoggedInEvent (specific user) |
| `dataprocessed` | Send DataProcessedEvent (random records) |
| `dataprocessed <n>` | Send DataProcessedEvent (n records) |
| `metrics` | Send SystemMetricsEvent |
| `send <topic>` | Publish event with topic only |
| `send <topic> <payload>` | Publish event with topic and JSON payload |

---

## Event Generator Plugin Commands

| Command | Description |
|---------|-------------|
| `generate start` | Start automatic event generation |
| `generate stop` | Stop automatic event generation |
| `generate toggle` | Toggle event generation on/off |
| `generate now` | Generate one event immediately |
| `generate interval <ms>` | Set generation interval in milliseconds |

---

## Logs

Plugin logs are written to:
```
Logs\metrics_YYYY-MM-DD. log
```