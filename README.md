# Sleepify

> A software stand-in for "sleep" on Windows 10 / 11 machines whose suspend path hangs.

**English** · [简体中文](README.zh-CN.md)

**Download:** [Releases](../../releases) — installer (recommended) or portable ZIP

---

## ⚠️ Platform restriction (read this first)

### Do NOT use on: AMD Ryzen 5000 / 6000 / 7000 mobile H-series (H / HS / HX)

**It is not about heat — it is about idling.**

These three generations of mobile Ryzen are known for **silicon degradation** ("缩肛" in Chinese). Once degraded, the SoC becomes highly unstable at **low load and low voltage**, and the **memory controller is usually the first to fail — sometimes it simply crashes**.

The counter-intuitive part: **high load with high voltage is safe; idle at low voltage is the failure region.** And this tool's entire design lives in that region:

| What Sleepify does | What it means for a degraded chip |
|---|---|
| Freezes every process → CPU utilisation ≈ 0 | the most unstable load range |
| Caps `PROCTHROTTLEMAX` → lowest P-state | lowest voltage |
| Radios off / maintenance paused → even less load | closer to true idle |
| **Sustained for as long as the lid stays closed** | tens of minutes to hours in the failing regime |

Using it on such a machine means **pinning the chip to its known failure regime for hours**. The crash usually happens with the lid closed, the panel dark and every process frozen — no visible BSOD, dirty buffers never flushed, and the only way out is a hard power-off, which **risks filesystem corruption**.

That is why the program makes you **confirm the risk three times**:

- **Installer**: after ticking the warning and pressing *Install*, if such a CPU is detected a second confirmation appears with the actual CPU name; only *Yes* continues
- **`start.cmd`**: on detecting such a CPU it shows the same confirmation **before changing anything**; choosing *No* exits immediately and the lid action is never touched
- **`Sleepify.exe`**: keeps a red "⚠ Risky platform (Ryzen H-series)" line in the status bar and mentions it in the startup notification, but **never blocks you**

Match rule: `Ryzen [3-9] [5-7]xxx(H|HS|HX)` — e.g. 5800H / 5900HX / 6600H / 6800HS / 7840HS / 7945HX / 7535H.
U-series parts (5800U / 7730U …) and desktop parts (5600X / 5800X3D) are not affected.

### Everything else

- ✅ Intended for machines whose lid-close sleep / hibernate hangs, fails to wake, or cannot hibernate on battery
- ⚠️ If your machine sleeps normally, be careful — this changes the lid behaviour and **cannot truly replace** the power savings of real sleep

---

## How it works

This is not real sleep. On lid close it performs (everything parallel where possible):

| Action | Implementation |
|---|---|
| Instant mute | CoreAudio `IAudioEndpointVolume` (sub-millisecond, runs *before* the debounce delay) |
| Brightness to 0 | WMI `WmiMonitorBrightnessMethods` — the **panel stays powered**, avoiding the DPMS power-down path that deadlocks on some platforms |
| Radio control | Windows **Radio API** (the same interface behind Airplane mode / Wi-Fi toggles); WLAN can be kept on |
| Freeze processes | `NtSuspendProcess` freezes every freezable process in the current session, except the whitelist |
| Suppress maintenance | Disable the automatic maintenance window and pause `SysMain` / `WSearch` / `DoSvc` / `DiagTrack` (update services only when `TiWorker` is not running) |
| Limit power | Configurable CPU cap via `PROCTHROTTLEMAX` |

On lid open everything is **restored in parallel**; a full cycle measures 1.6–1.8 s (the early serial implementation took ~20 s).

---

## Files

| File | Purpose |
|---|---|
| `Sleepify.exe` | *(release asset)* Main program (single file: self-configuring, self-checking, autostart, notifications, platform guard) |
| `SleepifySetup.exe` | *(release asset)* Installer (self-elevating): picks a language and the install folder, carries the five runtime files, optionally runs the device check |
| `start.cmd` | Entry point: device check + sleep plan (self-elevating) |
| `start.ps1` | Engine of `start.cmd`: platform check, hardware probe, writes `device.ini`, sets the lid action |
| `uninstall.cmd` | Entry point: revert every change (self-elevating) |
| `uninstall.ps1` | Engine of `uninstall.cmd`: 8 revert steps + final dialog |
| `i18n.ps1` | Six-language string table used by `start.ps1` / `uninstall.ps1`; reads `config\lang.txt` first, otherwise follows the OS UI language |
| `Sleepify.cs` | Main program source |
| `Setup.src.cs` | Installer source (escape non-ASCII to `\uXXXX`, then package with `csc /resource`) |

> `Sleepify.exe`, `SleepifySetup.exe` and the portable `sleepify.zip` are attached to [Releases](../../releases).
> The installer is self-contained — it carries the five runtime files — so installing needs nothing else.

---

## Installation

### Option 1 — installer (recommended)

1. Run `SleepifySetup.exe` (self-elevating)
2. Pick a language (zh / en / fr / ru / es / ar), the install folder (default `C:\Sleepify`), and whether to run the device check automatically
3. Read the warning, tick the confirmation, then install

### Option 2 — manual

Put `Sleepify.exe`, `start.cmd`, `start.ps1`, `uninstall.cmd` and `uninstall.ps1` in one folder, then
right-click `start.cmd` → **Run as administrator**.

`start.cmd` will: verify the platform → probe the hardware → write `config\device.ini` → write the sleep plan to
`config\config.ini` → set the lid-close action to "Do nothing" (AC and DC) → enable the service and start the app.

---

## Usage

- **Tray icon**: red dot = enabled, green dot = disabled; click to toggle
- **Whitelist**: click "Whitelist...", drag **icons** from the left pane to the right; whitelisted processes are never frozen
- **`--silent`**: used by the logon task — shows a notification only, never the window. A manual double-click (no argument) shows the UI
- Double-clicking the exe while it is already running asks the running instance to show its window

### Runtime folders

```
<install dir>\
    config\   config.ini  device.ini  whitelist.txt  enabled.flag  (sleepify.state is transient)
    logs\     sleepify.log  setup-log.txt  sleep-plan.txt  uninstall-log.txt
```

`device.ini` holds **per-machine** device names (model / CPU / WLAN adapter / Bluetooth radio / brightness instance /
whether a lid exists). `start.cmd` writes it once and the app reuses it — device names differ between models, so this
step cannot be skipped.

---

## Uninstall

Double-click `uninstall.cmd` → confirm → it performs eight revert steps:

1. Stop the process
2. Delete the scheduled task `Sleepify`
3. Restore the lid-close action to "Sleep"
4. Restore CPU max to 100% and the cooling policy to "Active"
5. Remove `MaintenanceDisabled` (re-enable automatic maintenance)
6. Restart the background services that were paused
7. Turn all radios back on
8. Delete `%ProgramData%\Sleepify`

A final dialog offers **Delete everything** (folder and scripts included) or **Close** (keep the folder so `start.cmd`
can redeploy later).

---

## Known limitations

- **Platform guard**: Ryzen 5000/6000/7000 mobile H/HS/HX is refused outright (see the top of this document)
- **Microsoft Defender cannot be frozen**: `MsMpEng` and friends are PPL (protected processes); user mode cannot even open a handle. The tool instead suppresses the **maintenance window that makes them work**
- **Bluetooth**: only machines that expose a Bluetooth radio through the Radio API can switch it off; otherwise the tool **leaves it completely alone** (force-stopping `bthserv` leaves the radio in an `Error` state)
- **Brightness control** relies on WMI; some desktops and external displays do not support it — when unavailable it is skipped, never leaving a black screen
- **Freeze scope** is limited to the current user session; system services (session 0) are untouched
- Do **not** unplug removable drives while frozen, and avoid closing the lid in the middle of write-heavy work
- Requires **.NET Framework 4.8** (built into Windows 10 1903+ and Windows 11)

---

## License

[MIT](LICENSE). Do whatever you want with it — use, modify, redistribute, sell — just keep the copyright notice.

---

## Disclaimer

This project simulates sleep by freezing processes, changing power settings and pausing system services. It is **not a
Microsoft-supported sleep or hibernate implementation**. Make sure your machine really has a sleep/hibernate fault
before using it. The author takes no responsibility for data loss or hardware damage caused by its use.

AMD Ryzen 5000 / 6000 / 7000 mobile H-series (H / HS / HX) is **strictly unsupported**, and the program enforces it.
