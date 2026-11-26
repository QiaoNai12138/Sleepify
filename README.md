# Sleepify

A solution for Windows devices that cannot sleep or hibernate properly.
面向无法正常睡眠、休眠的 Windows 设备的解决方案。

**English** · [简体中文](README.zh-CN.md)

**Download:** [Releases](../../releases) — installer or portable build

| Build | File |
|---|---|
| Installer (recommended) | `SleepifySetup.exe` |
| Portable | `sleepify.zip` |

---

## ⚠️ Warning: standard-voltage mobile Ryzen 5000 / 6000 / 7000

Standard-voltage mobile Ryzen 5000–7000 series (H / HS / HX) are known for low-voltage stability problems.
**If your CPU falls in this range, use this tool with caution.**

## How it works

Sleepify does not perform real sleep. It simulates it by freezing processes and turning off non-essential devices and
services, leaving the machine powered but idle.

## Installation

### Portable build

1. Unzip the archive.
2. Run `start.cmd`. It inspects your device and generates a sleep profile tailored to it.
3. Double-click `Sleepify.exe`.

### Installer

The installer runs `start.cmd` by default. Once installed, simply double-click `Sleepify.exe`.

## Uninstallation

Double-click `uninstall.cmd` in the install folder. It reverts every change Sleepify made while running.

After that you can click **Delete** to remove the tool, or delete the install folder manually. Everything Sleepify uses
is kept inside that folder.

## Repair / reconfigure

If your device configuration changes or the tool stops behaving correctly, reconfigure it:

1. Run `uninstall.cmd` and let it revert every change.
2. When it finishes, **do not click "Delete"** — close the dialog instead.
3. Run `start.cmd` again. It reconfigures the tool for your device.

## Requirements

- Windows 10 (1903+) or Windows 11
- .NET Framework 4.8 (included with the OS)
- Administrator rights — the scripts and the application self-elevate
- The binaries are unsigned, so SmartScreen will warn; choose "Run anyway"

## License

MIT — Copyright (c) QiaoNai12138. See [LICENSE](LICENSE).

## About this project

这是我的第一个开源项目，最开始他是为了解决我的松下笔记本合盖后睡死而诞生的，他的运行逻辑由我设计 程序代码由DeepSeek V4.1 Flash编写，不是什么复杂的东西 不过我见过很多笔记本电脑有睡眠休眠相关问题，算是我给出的一个下位替代的解决方案吧，希望能帮到您。

