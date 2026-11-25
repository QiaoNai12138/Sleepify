using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Sleepify
{
    internal static class Nat
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PE32
        {
            public uint dwSize; public uint cntUsage; public uint th32ProcessID;
            public IntPtr th32DefaultHeapID; public uint th32ModuleID; public uint cntThreads;
            public uint th32ParentProcessID; public int pcPriClassBase; public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PBS
        {
            public Guid g;
            public uint len;
            public byte data;
        }

        [DllImport("ntdll.dll")] public static extern int NtSuspendProcess(IntPtr h);
        [DllImport("ntdll.dll")] public static extern int NtResumeProcess(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(int a, bool i, int pid);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr CreateToolhelp32Snapshot(uint f, uint p);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool Process32FirstW(IntPtr h, ref PE32 pe);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool Process32NextW(IntPtr h, ref PE32 pe);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr RegisterPowerSettingNotification(IntPtr h, ref Guid g, int f);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterPowerSettingNotification(IntPtr h);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool QueryFullProcessImageNameW(IntPtr h, int flags, StringBuilder buf, ref int size);

        public const int PROCESS_SUSPEND_RESUME = 0x0800;
        public const int WM_POWERBROADCAST = 0x0218;
        public const int PBT_POWERSETTINGCHANGE = 0x8013;
        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_MONITORPOWER = 0xF170;
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        public static Guid GUID_LID = new Guid("BA3E0F4D-B817-4094-A2D1-D56379E6A0F3");

        public static List<PE32> Snapshot()
        {
            var list = new List<PE32>();
            IntPtr snap = CreateToolhelp32Snapshot(0x2, 0);
            if (snap == (IntPtr)(-1)) return list;
            var pe = new PE32();
            pe.dwSize = (uint)Marshal.SizeOf(typeof(PE32));
            if (Process32FirstW(snap, ref pe)) { do { list.Add(pe); } while (Process32NextW(snap, ref pe)); }
            CloseHandle(snap);
            return list;
        }

        public static bool Suspend(int pid)
        {
            IntPtr h = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
            if (h == IntPtr.Zero) return false;
            int r = NtSuspendProcess(h); CloseHandle(h); return r == 0;
        }
        public static bool Resume(int pid)
        {
            IntPtr h = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
            if (h == IntPtr.Zero) return false;
            int r = NtResumeProcess(h); CloseHandle(h); return r == 0;
        }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator { int N1(); int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice dev); }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice { int Activate(ref Guid iid, int clsctx, IntPtr act, [MarshalAs(UnmanagedType.IUnknown)] out object o); }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        int N1(); int N2();
        int GetChannelCount(out uint c);
        int SetMasterVolumeLevel(float l, Guid g);
        int SetMasterVolumeLevelScalar(float l, Guid g);
        int GetMasterVolumeLevel(out float l);
        int GetMasterVolumeLevelScalar(out float l);
        int SetChannelVolumeLevel(uint ch, float l, Guid g);
        int SetChannelVolumeLevelScalar(uint ch, float l, Guid g);
        int GetChannelVolumeLevel(uint ch, out float l);
        int GetChannelVolumeLevelScalar(uint ch, out float l);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool m, Guid g);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool m);
    }

    internal static class Audio
    {
        static IAudioEndpointVolume Vol()
        {
            var en = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
            IMMDevice dev; int hr = en.GetDefaultAudioEndpoint(0, 1, out dev);
            if (hr != 0) throw new COMException("endpoint", hr);
            Guid iid = typeof(IAudioEndpointVolume).GUID; object o;
            hr = dev.Activate(ref iid, 23, IntPtr.Zero, out o);
            if (hr != 0) throw new COMException("activate", hr);
            return (IAudioEndpointVolume)o;
        }
        public static bool GetMute() { bool m; Vol().GetMute(out m); return m; }
        public static void SetMute(bool m) { Vol().SetMute(m, Guid.Empty); }
    }

    public class MainForm : Form
    {
        const string TaskName = "Sleepify";
        const int CPU_CAP = 50;
        const string SUB_PROC = "54533251-82be-4824-96c1-47b60b740d00";
        const string SET_MAXCPU = "bc5038f7-23e0-4960-96da-33abaf5935ec";
        const string SET_COOLPOL = "94d3a615-a899-4ac5-ae2b-e4d8f634367f";
        const string PS_ROOT = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";

        static readonly string[] Deny = new string[] {
            "explorer.exe","dwm.exe","csrss.exe","winlogon.exe","wininit.exe","services.exe","lsass.exe",
            "smss.exe","svchost.exe","audiodg.exe","System","Registry","MemCompression","Memory Compression",
            "powershell.exe","pwsh.exe","DeepSeek Harness.exe","conhost.exe","dllhost.exe","WUDFHost.exe",
            "SearchApp.exe","ShellExperienceHost.exe","StartMenuExperienceHost.exe","ApplicationFrameHost.exe",
            "RuntimeBroker.exe","TextInputHost.exe","ctfmon.exe","sihost.exe","taskhostw.exe","fontdrvhost.exe",
            "LogonUI.exe","LockApp.exe","UserOOBEBroker.exe","SgrmBroker.exe","smartscreen.exe","WmiPrvSE.exe",
            "SecurityHealthService.exe","SecurityHealthSystray.exe","MsMpEng.exe","NisSrv.exe",
            "MpDefenderCoreService.exe","MoUsoCoreWorker.exe","TiWorker.exe","TrustedInstaller.exe",
            "spoolsv.exe","SearchIndexer.exe","StartMenuExperienceHost.exe","SystemSettings.exe",
            "Sleepify.exe","explorer.exe","dwm.exe"
        };

        Button btn;
        Label lbl;
        NotifyIcon tray;
        Icon trayIcon;
        IntPtr lastHicon = IntPtr.Zero;
        bool enabled;
        bool lidWarn = false;
        bool healthOk = true;
        bool exiting = false;
        IntPtr hLid = IntPtr.Zero;
        string dataDir, logFile, flagFile, stateFile, declinedFile, configFile, whitelistFile;
        ComboBox cbCap;
        CheckBox chkKeepWlan;
        CheckBox chkFreezeAll;
        Button btnWhite;
        int cpuCapLid = 50;
        bool keepWlan = true;
        bool freezeAll = true;
        bool loading = true;
        bool blocked = false;
        readonly bool silent;
        bool shownOnce = false;
        System.Windows.Forms.Timer startupTimer;
        readonly List<string> whitelist = new List<string>();

        volatile bool lidClosed = false;
        volatile bool preMuteKnown = false;
        volatile bool preMutePrev = false;
        int lidGen = 0;
        readonly object gate = new object();

        public MainForm(bool silent)
        {
            this.silent = silent;
            // Settings, state and the per-machine device profile live in <exeDir>\config
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            dataDir = Path.Combine(exeDir, "config");
            try { Directory.CreateDirectory(dataDir); }
            catch
            {
                // read-only location -> fall back to ProgramData
                dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Sleepify");
                Directory.CreateDirectory(dataDir);
            }
            // Logs live in <exeDir>\logs, settings in <exeDir>\config
            string logsDir;
            try
            {
                logsDir = Path.Combine(exeDir, "logs");
                Directory.CreateDirectory(logsDir);
            }
            catch { logsDir = dataDir; }
            logFile = Path.Combine(logsDir, "sleepify.log");
            flagFile = Path.Combine(dataDir, "enabled.flag");
            stateFile = Path.Combine(dataDir, "sleepify.state");
            declinedFile = Path.Combine(dataDir, "lidfix.declined");
            configFile = Path.Combine(dataDir, "config.ini");
            whitelistFile = Path.Combine(dataDir, "whitelist.txt");
            RotateLog();

            Text = "\u7761\u7720\u6A21\u62DF\u5668  Sleepify";
            ClientSize = new Size(380, 384);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Application;

            LoadConfig();
            LoadWhitelist();
            LoadDeviceProfile();

            btn = new Button();
            btn.SetBounds(0, 0, 380, 210);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold);
            btn.ForeColor = Color.White;
            btn.Click += new EventHandler(OnToggle);
            Controls.Add(btn);

            var pnl = new Panel();
            pnl.SetBounds(0, 210, 380, 140);
            pnl.BackColor = Color.FromArgb(30, 30, 30);
            Controls.Add(pnl);

            var capLabel = new Label();
            capLabel.Text = "\u5408\u76D6\u540E CPU \u4E0A\u9650";
            capLabel.SetBounds(14, 14, 120, 22);
            capLabel.ForeColor = Color.Gainsboro;
            pnl.Controls.Add(capLabel);

            cbCap = new ComboBox();
            cbCap.DropDownStyle = ComboBoxStyle.DropDownList;
            cbCap.SetBounds(140, 11, 80, 24);
            cbCap.Items.AddRange(new object[] { "5%", "10%", "20%", "30%", "40%", "50%", "60%", "75%", "100%" });
            cbCap.SelectedIndexChanged += delegate { if (loading) return; SaveConfig(); };
            pnl.Controls.Add(cbCap);

            var capHint = new Label();
            capHint.Text = "\u8D8A\u4F4E\u8D8A\u7701\u7535";
            capHint.SetBounds(232, 14, 140, 22);
            capHint.ForeColor = Color.Gray;
            pnl.Controls.Add(capHint);

            chkKeepWlan = new CheckBox();
            chkKeepWlan.Text = "\u5408\u76D6\u4FDD\u7559 WLAN\uFF08\u53EA\u5173\u5176\u4ED6\u5C04\u9891\uFF0C\u4E0D\u65AD\u7F51\uFF09";
            chkKeepWlan.SetBounds(14, 44, 356, 24);
            chkKeepWlan.ForeColor = Color.Gainsboro;
            chkKeepWlan.CheckedChanged += delegate { if (loading) return; SaveConfig(); };
            pnl.Controls.Add(chkKeepWlan);

            chkFreezeAll = new CheckBox();
            chkFreezeAll.Text = "\u51BB\u7ED3\u5168\u90E8\u7A0B\u5E8F\uFF08\u767D\u540D\u5355\u9664\u5916\uFF09";
            chkFreezeAll.SetBounds(14, 74, 230, 24);
            chkFreezeAll.ForeColor = Color.Gainsboro;
            chkFreezeAll.CheckedChanged += delegate { if (loading) return; SaveConfig(); };
            pnl.Controls.Add(chkFreezeAll);

            btnWhite = new Button();
            btnWhite.Text = "\u767D\u540D\u5355...";
            btnWhite.SetBounds(250, 72, 116, 26);
            btnWhite.Click += delegate { EditWhitelist(); };
            pnl.Controls.Add(btnWhite);

            var note = new Label();
            note.Text = "Windows \u7CFB\u7EDF\u8FDB\u7A0B\u4E0E PPL \u8FDB\u7A0B\u59CB\u7EC8\u4E0D\u4F1A\u88AB\u51BB\u7ED3";
            note.SetBounds(14, 106, 356, 22);
            note.ForeColor = Color.Gray;
            pnl.Controls.Add(note);

            lbl = new Label();
            lbl.SetBounds(0, 350, 380, 34);
            lbl.TextAlign = ContentAlignment.MiddleCenter;
            lbl.Font = new Font("Microsoft YaHei UI", 9f);
            lbl.BackColor = Color.FromArgb(20, 20, 20);
            lbl.ForeColor = Color.Gainsboro;
            Controls.Add(lbl);

            cbCap.SelectedIndex = CapToIndex(cpuCapLid);
            chkKeepWlan.Checked = keepWlan;
            chkFreezeAll.Checked = freezeAll;
            loading = false;

            tray = new NotifyIcon();
            tray.Text = "Sleepify";
            var menu = new ContextMenuStrip();
            menu.Items.Add("\u663E\u793A\u7A97\u53E3", null, delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            menu.Items.Add("\u9000\u51FA", null, delegate { exiting = true; Close(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; Activate(); };
            tray.Visible = true;

            enabled = File.Exists(flagFile);

            // Risky platform (Ryzen 5000/6000/7000 mobile H-series): warn, never refuse.
            // Consent is collected by the installer and by start.cmd.
            if (IsForbiddenCpu())
            {
                blocked = true;
                Log("RISKY PLATFORM: " + CpuName() + " - running anyway, warning shown");
            }

            UpdateUi();

            // Built-in first-run setup / self-heal (replaces install.cmd entirely)
            healthOk = AutoHeal();

            if (File.Exists(stateFile))
            {
                Log("found leftover state on startup -> restoring");
                try { RestoreAll(); } catch (Exception ex) { Log("startup restore ERR " + ex.Message); }
            }

            // A second launch (double-click) asks this instance to show its window.
            EventWaitHandle ev = Program.ShowSignal;
            if (ev != null)
            {
                var watcher = new Thread(delegate ()
                {
                    while (true)
                    {
                        try
                        {
                            ev.WaitOne();
                            BeginInvoke((MethodInvoker)delegate
                            {
                                Show();
                                WindowState = FormWindowState.Normal;
                                Activate();
                            });
                        }
                        catch { break; }
                    }
                });
                watcher.IsBackground = true;
                watcher.Start();
            }

            // Silent mode never shows the window, so OnShown will not fire - toast on a timer.
            if (silent)
            {
                startupTimer = new System.Windows.Forms.Timer();
                startupTimer.Interval = 1200;
                startupTimer.Tick += delegate
                {
                    startupTimer.Stop();
                    startupTimer.Dispose();
                    startupTimer = null;
                    StartupToast();
                };
                startupTimer.Start();
            }
        }

        void RotateLog()
        {
            try
            {
                if (File.Exists(logFile) && new FileInfo(logFile).Length > 1024 * 1024)
                    File.Move(logFile, logFile + ".old");
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Guid g = Nat.GUID_LID;
            hLid = Nat.RegisterPowerSettingNotification(this.Handle, ref g, 0);
            Log("lid notification registered handle=" + hLid);
        }

        // Launched by the logon task (--silent): stay out of the way, only show a toast.
        protected override void SetVisibleCore(bool value)
        {
            if (silent && !shownOnce)
            {
                shownOnce = true;
                base.SetVisibleCore(false);
                return;
            }
            base.SetVisibleCore(value);
        }

        void StartupToast()
        {
            if (blocked)
                Toast("\u9AD8\u98CE\u9669\u5E73\u53F0\uFF08Ryzen \u6807\u538B\uFF09\uFF0C\u5DF2\u6309\u786E\u8BA4\u7EE7\u7EED\u8FD0\u884C");
            else if (!healthOk)
                Toast("\u670D\u52A1\u51FA\u9519\uFF0C\u65E0\u6CD5\u6B63\u5E38\u8FD0\u884C");
            else if (enabled)
                Toast("\u670D\u52A1\u8FD0\u884C\u4E2D");
            else
                Toast("\u670D\u52A1\u672A\u542F\u7528");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (silent) return;
            StartupToast();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            Log("=== exiting, restoring ===");
            try { RestoreAll(); } catch (Exception ex) { Log("exit restore ERR " + ex.Message); }
            if (hLid != IntPtr.Zero) { Nat.UnregisterPowerSettingNotification(hLid); hLid = IntPtr.Zero; }
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            if (trayIcon != null) { try { trayIcon.Dispose(); } catch { } }
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Nat.WM_POWERBROADCAST && (int)m.WParam == Nat.PBT_POWERSETTINGCHANGE)
            {
                Nat.PBS ps = (Nat.PBS)Marshal.PtrToStructure(m.LParam, typeof(Nat.PBS));
                if (ps.g == Nat.GUID_LID)
                {
                    bool open = ps.data != 0;
                    int myGen;
                    lock (gate) { lidGen++; myGen = lidGen; lidClosed = !open; }
                    Log("EVENT lid " + (open ? "OPENED" : "CLOSED") + " data=" + ps.data + " gen=" + myGen);
                    if (!enabled) return;
                    if (open) ThreadPool.QueueUserWorkItem(delegate (object o) { try { GoUp(); } catch (Exception ex) { Log("GoUp ERR " + ex.Message); } });
                    else
                    {
                        // Mute right now, before the debounce delay - this is what the user
                        // notices first when the lid closes.
                        preMuteKnown = false;
                        try { preMutePrev = Audio.GetMute(); Audio.SetMute(true); preMuteKnown = true; Log("  muted instantly (pre-debounce)"); }
                        catch (Exception ex) { Log("  mute ERR " + ex.Message); }

                        ThreadPool.QueueUserWorkItem(delegate (object o)
                        {
                            Thread.Sleep(1200);
                            lock (gate) { if (myGen != lidGen || !lidClosed) { Log("  reopened fast, skip GoDown"); return; } }
                            try { GoDown(); } catch (Exception ex) { Log("GoDown ERR " + ex.Message); }
                        });
                    }
                }
            }
            base.WndProc(ref m);
        }

        static string LidName(int v)
        {
            switch (v)
            {
                case 0: return "\u4E0D\u91C7\u53D6\u4EFB\u4F55\u64CD\u4F5C";
                case 1: return "\u7761\u7720";
                case 2: return "\u4F11\u7720";
                case 3: return "\u5173\u673A";
                default: return "\u672A\u77E5";
            }
        }

        void SetLidDoNothing()
        {
            PsCmd("powercfg /setacvalueindex SCHEME_CURRENT 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 0;" +
                  "powercfg /setdcvalueindex SCHEME_CURRENT 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 0;" +
                  "powercfg /setactive SCHEME_CURRENT");
        }

        bool AutoHeal()
        {
            bool ok = true;

            // ---- 1) self-heal a CPU cap left behind by an older build ----
            try
            {
                if (!File.Exists(stateFile))
                {
                    int ac = ReadCpu("AC");
                    int dc = ReadCpu("DC");
                    // Any value equal to our configured cap with no active session means
                    // a previous run died before restoring. Reset it, whatever the cap is.
                    if (cpuCapLid < 100 && (ac == cpuCapLid || dc == cpuCapLid))
                    {
                        Log("autoheal: CPU max stuck at " + ac + "/" + dc + " -> resetting to 100");
                        PsCmd("powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100;" +
                              "powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100;" +
                              "powercfg /setactive SCHEME_CURRENT");
                        ac = ReadCpu("AC"); dc = ReadCpu("DC");
                        if (ac == CPU_CAP || dc == CPU_CAP) { ok = false; Log("autoheal: CPU reset FAILED"); }
                        else Log("autoheal: CPU reset ok (" + ac + "/" + dc + ")");
                    }
                }
            }
            catch (Exception ex) { ok = false; Log("autoheal cpu ERR " + ex.Message); }

            // ---- 2) lid action must be "Do nothing" - fixed silently ----
            lidWarn = false;
            int lidAc = ReadLidAction("AC");
            int lidDc = ReadLidAction("DC");
            if (lidAc > 0 || lidDc > 0)
            {
                Log("autoheal: lid action is " + LidName(lidAc) + "/" + LidName(lidDc) + " -> fixing silently");
                SetLidDoNothing();
                lidAc = ReadLidAction("AC");
                lidDc = ReadLidAction("DC");
                if (lidAc > 0 || lidDc > 0)
                {
                    ok = false;
                    lidWarn = true;
                    Log("autoheal: lid fix FAILED");
                    lbl.Text = "\u8B66\u544A: \u5408\u76D6\u52A8\u4F5C\u65E0\u6CD5\u4FEE\u6B63";
                    lbl.BackColor = Color.DarkRed;
                    lbl.ForeColor = Color.White;
                }
                else Log("autoheal: lid action fixed to Do-nothing");
            }

            // ---- 3) autostart task must exist and point at our current path ----
            if (enabled)
            {
                if (!CreateTask()) { ok = false; Log("autoheal: task registration FAILED"); }
                else Log("autoheal: task ok -> " + Application.ExecutablePath);
            }

            Log("autoheal: overall " + (ok ? "OK" : "FAILED"));
            return ok;
        }

        void UpdateUi()
        {
            if (enabled)
            {
                btn.BackColor = Color.FromArgb(198, 40, 40);
                btn.Text = "\u5DF2\u542F\u7528\n\u70B9\u51FB\u5173\u95ED";
                lbl.Text = "\u5408\u76D6: \u5173\u5C4F \u00B7 \u65AD\u65E0\u7EBF \u00B7 \u9759\u97F3 \u00B7 \u51BB\u7ED3\u524D\u53F0 \u00B7 \u964DCPU";
                SetTrayIcon(Color.FromArgb(198, 40, 40));
            }
            else
            {
                btn.BackColor = Color.FromArgb(46, 160, 67);
                btn.Text = "\u5DF2\u505C\u7528\n\u70B9\u51FB\u542F\u7528";
                lbl.Text = "\u670D\u52A1\u672A\u8FD0\u884C";
                SetTrayIcon(Color.FromArgb(46, 160, 67));
            }

            if (blocked)
            {
                lbl.Text = "\u26A0 \u9AD8\u98CE\u9669\u5E73\u53F0\uFF08Ryzen \u6807\u538B\uFF09\u00B7 " + lbl.Text;
                lbl.BackColor = Color.DarkRed;
                lbl.ForeColor = Color.White;
            }
        }

        void SetTrayIcon(Color c)
        {
            try
            {
                var bmp = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    using (var b = new SolidBrush(c)) g.FillEllipse(b, 1, 1, 14, 14);
                }
                IntPtr h = bmp.GetHicon();
                if (trayIcon != null) { try { trayIcon.Dispose(); } catch { } }
                if (lastHicon != IntPtr.Zero) { try { Nat.DestroyIcon(lastHicon); } catch { } }
                lastHicon = h;
                trayIcon = Icon.FromHandle(h);
                tray.Icon = trayIcon;
                bmp.Dispose();
            }
            catch { }
        }

        void OnToggle(object sender, EventArgs e)
        {
            if (blocked) Log("toggle on risky platform (" + CpuName() + ")");
            enabled = !enabled;
            if (enabled)
            {
                File.WriteAllText(flagFile, "1");
                CreateTask();
                Log("=== enabled by user ===");
                Toast("\u5DF2\u5F00\u542F");
            }
            else
            {
                try { if (File.Exists(flagFile)) File.Delete(flagFile); } catch { }
                try { RestoreAll(); } catch (Exception ex) { Log("restore ERR " + ex.Message); }
                DeleteTask();
                Log("=== disabled by user ===");
                Toast("\u5DF2\u5173\u95ED");
            }
            UpdateUi();
        }

        static readonly int[] CAPS = new int[] { 5, 10, 20, 30, 40, 50, 60, 75, 100 };
        static int CapToIndex(int cap) { for (int i = 0; i < CAPS.Length; i++) if (CAPS[i] == cap) return i; return 5; }

        void LoadConfig()
        {
            try
            {
                if (!File.Exists(configFile)) return;
                foreach (string l in File.ReadAllLines(configFile))
                {
                    if (l.StartsWith("cpucap=")) { int v; if (int.TryParse(l.Substring(7), out v) && v >= 5 && v <= 100) cpuCapLid = v; }
                    else if (l.StartsWith("keepwlan=")) keepWlan = l.EndsWith("1");
                    else if (l.StartsWith("freezeall=")) freezeAll = l.EndsWith("1");
                }
                Log("config loaded: cpucap=" + cpuCapLid + " keepwlan=" + keepWlan + " freezeall=" + freezeAll);
            }
            catch (Exception ex) { Log("config ERR " + ex.Message); }
        }

        void SaveConfig()
        {
            try
            {
                if (cbCap != null && cbCap.SelectedIndex >= 0) cpuCapLid = CAPS[cbCap.SelectedIndex];
                if (chkKeepWlan != null) keepWlan = chkKeepWlan.Checked;
                if (chkFreezeAll != null) freezeAll = chkFreezeAll.Checked;
                File.WriteAllText(configFile, "cpucap=" + cpuCapLid + "\nkeepwlan=" + (keepWlan ? "1" : "0") + "\nfreezeall=" + (freezeAll ? "1" : "0") + "\n");
                Log("config saved: cpucap=" + cpuCapLid + " keepwlan=" + keepWlan + " freezeall=" + freezeAll);
            }
            catch (Exception ex) { Log("config ERR " + ex.Message); }
        }

        void LoadWhitelist()
        {
            try
            {
                if (!File.Exists(whitelistFile)) return;
                foreach (string l in File.ReadAllLines(whitelistFile))
                {
                    string t = l.Trim().ToLowerInvariant();
                    if (t.EndsWith(".exe")) t = t.Substring(0, t.Length - 4);
                    if (t.Length > 0 && !whitelist.Contains(t)) whitelist.Add(t);
                }
                Log("whitelist loaded: " + whitelist.Count + " entries");
            }
            catch (Exception ex) { Log("whitelist ERR " + ex.Message); }
        }

        void SaveWhitelist()
        {
            try { File.WriteAllLines(whitelistFile, whitelist.ToArray()); Log("whitelist saved: " + whitelist.Count + " entries"); }
            catch (Exception ex) { Log("whitelist ERR " + ex.Message); }
        }

        void EditWhitelist()
        {
            var f = new Form();
            f.Text = "\u767D\u540D\u5355  -  \u62D6\u52A8\u56FE\u6807\u52A0\u5165\uFF0C\u53CC\u51FB\u79FB\u9664";
            f.ClientSize = new Size(760, 500);
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MaximizeBox = false; f.MinimizeBox = false;
            f.Font = new Font("Microsoft YaHei UI", 9f);

            var lblL = new Label();
            lblL.Text = "\u6B63\u5728\u8FD0\u884C\u7684\u7A0B\u5E8F\uFF08\u62D6\u5230\u53F3\u4FA7\uFF09";
            lblL.SetBounds(12, 8, 350, 20);
            f.Controls.Add(lblL);

            var lblR = new Label();
            lblR.Text = "\u767D\u540D\u5355\uFF08\u5408\u76D6\u65F6\u4E0D\u4F1A\u88AB\u51BB\u7ED3\uFF09";
            lblR.SetBounds(400, 8, 350, 20);
            f.Controls.Add(lblR);

            var il = new ImageList();
            il.ImageSize = new Size(24, 24);
            il.ColorDepth = ColorDepth.Depth32Bit;

            var left = new ListView();
            left.SetBounds(12, 30, 368, 400);
            left.View = View.Details;
            left.FullRowSelect = true;
            left.MultiSelect = true;
            left.SmallImageList = il;
            left.Columns.Add("\u7A0B\u5E8F", 330);
            f.Controls.Add(left);

            var right = new ListView();
            right.SetBounds(400, 30, 348, 400);
            right.View = View.Details;
            right.FullRowSelect = true;
            right.MultiSelect = true;
            right.AllowDrop = true;
            right.SmallImageList = il;
            right.Columns.Add("\u7A0B\u5E8F", 310);
            f.Controls.Add(right);

            Action<string> addRight = delegate (string nm)
            {
                if (nm == null || nm.Length == 0) return;
                foreach (ListViewItem x in right.Items)
                    if (string.Equals(x.Text, nm, StringComparison.OrdinalIgnoreCase)) return;
                int idx = -1;
                foreach (ListViewItem x in left.Items)
                    if (string.Equals(x.Text, nm, StringComparison.OrdinalIgnoreCase)) { idx = x.ImageIndex; break; }
                if (idx < 0)
                {
                    try { il.Images.Add(SystemIcons.Application); idx = il.Images.Count - 1; }
                    catch { idx = 0; }
                }
                var it = new ListViewItem(nm, idx);
                right.Items.Add(it);
            };

            // ---- populate left with running user apps (icon + real name) ----
            int mySess = Process.GetCurrentProcess().SessionId;
            string sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToLowerInvariant();
            var seen = new Dictionary<string, int>();
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.SessionId != mySess) continue;
                    string path = ProcPath(p.Id);
                    if (path == null || path.Length == 0) continue;
                    if (path.ToLowerInvariant().StartsWith(sysRoot)) continue;
                    string nm = p.ProcessName;
                    if (nm.Length == 0) continue;
                    if (string.Equals(nm, "Sleepify", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(nm, "DeepSeek Harness", StringComparison.OrdinalIgnoreCase)) continue;
                    if (seen.ContainsKey(nm)) continue;
                    int idx;
                    try { il.Images.Add(Icon.ExtractAssociatedIcon(path)); idx = il.Images.Count - 1; }
                    catch { il.Images.Add(SystemIcons.Application); idx = il.Images.Count - 1; }
                    seen[nm] = idx;
                    var it = new ListViewItem(nm, idx);
                    it.ToolTipText = path;
                    left.Items.Add(it);
                }
                catch { }
            }
            left.Sorting = SortOrder.Ascending;

            // ---- existing whitelist on the right ----
            foreach (string nm in whitelist.ToArray()) addRight(nm);
            right.Sorting = SortOrder.Ascending;

            // ---- drag & drop ----
            left.ItemDrag += delegate (object s, ItemDragEventArgs e)
            {
                var it = (ListViewItem)e.Item;
                if (it != null) left.DoDragDrop(it.Text, DragDropEffects.Copy);
            };
            right.DragEnter += delegate (object s, DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(DataFormats.StringFormat) ? DragDropEffects.Copy : DragDropEffects.None;
            };
            right.DragDrop += delegate (object s, DragEventArgs e)
            {
                string nm = e.Data.GetData(DataFormats.StringFormat) as string;
                addRight(nm);
            };
            left.DoubleClick += delegate (object s, EventArgs e)
            {
                if (left.SelectedItems.Count > 0) addRight(left.SelectedItems[0].Text);
            };
            right.DoubleClick += delegate (object s, EventArgs e)
            {
                if (right.SelectedItems.Count > 0)
                {
                    var kill = new List<ListViewItem>();
                    foreach (ListViewItem x in right.SelectedItems) kill.Add(x);
                    foreach (ListViewItem x in kill) right.Items.Remove(x);
                }
            };

            var hint = new Label();
            hint.Text = "\u5DE6\u4FA7\u53CC\u51FB\u4E5F\u53EF\u52A0\u5165\uFF1B\u53F3\u4FA7\u53CC\u51FB\u79FB\u9664";
            hint.SetBounds(12, 436, 500, 20);
            hint.ForeColor = Color.Gray;
            f.Controls.Add(hint);

            var ok = new Button();
            ok.Text = "\u4FDD\u5B58"; ok.SetBounds(560, 460, 90, 30); ok.DialogResult = DialogResult.OK;
            var cancel = new Button();
            cancel.Text = "\u53D6\u6D88"; cancel.SetBounds(658, 460, 90, 30); cancel.DialogResult = DialogResult.Cancel;
            f.Controls.Add(ok); f.Controls.Add(cancel);
            f.AcceptButton = ok; f.CancelButton = cancel;

            if (f.ShowDialog(this) == DialogResult.OK)
            {
                whitelist.Clear();
                foreach (ListViewItem x in right.Items)
                {
                    string t = x.Text.Trim().ToLowerInvariant();
                    if (t.EndsWith(".exe")) t = t.Substring(0, t.Length - 4);
                    if (t.Length > 0 && !whitelist.Contains(t)) whitelist.Add(t);
                }
                SaveWhitelist();
            }
            f.Dispose();
        }

        string devWlan = "", devBt = "", devBright = "", devLid = "", devModel = "";

        // Per-machine profile written by start.ps1. Device names differ between models,
        // so they are read once and reused instead of being guessed every time.
        void LoadDeviceProfile()
        {
            string f = Path.Combine(dataDir, "device.ini");
            if (!File.Exists(f)) { Log("device profile: not found (run start.cmd once)"); return; }
            try
            {
                foreach (string l in File.ReadAllLines(f))
                {
                    int i = l.IndexOf('=');
                    if (i <= 0) continue;
                    string k = l.Substring(0, i).Trim().ToLowerInvariant();
                    string v = l.Substring(i + 1).Trim();
                    if (k == "model") devModel = v;
                    else if (k == "wlan") devWlan = v;
                    else if (k == "bluetooth") devBt = v;
                    else if (k == "brightness") devBright = v;
                    else if (k == "lid") devLid = v;
                }
                Log("device profile: model='" + devModel + "' wlan='" + devWlan + "' lid=" + devLid);
            }
            catch (Exception ex) { Log("device profile ERR " + ex.Message); }
        }

        static string CpuName()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (k == null) return "";
                    string s = k.GetValue("ProcessorNameString") as string;
                    return s == null ? "" : s.Trim();
                }
            }
            catch { return ""; }
        }

        // AMD Ryzen 5000 / 6000 / 7000 mobile H-series (H / HS / HX) are known to degrade:
        // once degraded, the SoC - the memory controller in particular - becomes unstable at
        // LOW load and LOW voltage. This tool keeps the machine awake at near-zero load for
        // hours, which is exactly that failing regime, so these platforms are refused.
        static bool IsForbiddenCpu()
        {
            string s = CpuName().ToUpperInvariant();
            int i = s.IndexOf("RYZEN");
            if (i < 0) return false;
            string[] tok = s.Substring(i).Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            // expected shape: RYZEN <3-9> <5xxx|6xxx|7xxx><H|HS|HX>
            for (int n = 0; n < tok.Length; n++)
            {
                string t = tok[n];
                if (t.Length < 5) continue;
                char c0 = t[0];
                if (c0 < '5' || c0 > '7') continue;
                bool digits = true;
                for (int d = 1; d < 4; d++) if (t[d] < '0' || t[d] > '9') { digits = false; break; }
                if (!digits) continue;
                string rest = t.Substring(4);
                if (rest == "H" || rest == "HS" || rest == "HX") return true;
            }
            return false;
        }

        static int ReadCoolPol(string kind)
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(PS_ROOT))
                {
                    string scheme = k.GetValue("ActivePowerScheme") as string;
                    if (scheme == null) return 1;
                    using (RegistryKey s = Registry.LocalMachine.OpenSubKey(PS_ROOT + "\\" + scheme + "\\" + SUB_PROC + "\\" + SET_COOLPOL))
                    {
                        if (s == null) return 1;
                        object v = s.GetValue(kind + "SettingIndex");
                        if (v == null) return 1;
                        return Convert.ToInt32(v);
                    }
                }
            }
            catch { return 1; }
        }

        static readonly string[] QUIET_SVCS = new string[] { "SysMain", "WSearch", "DoSvc", "DiagTrack" };
        static readonly string[] QUIET_SVCS_WU = new string[] { "UsoSvc", "wuauserv" };
        const string MAINT_KEY = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance";

        // Defender itself is a PPL (tamper-protected) process and can never be suspended
        // from user mode. What we CAN do is remove the reasons it wakes up: disable the
        // automatic maintenance window and stop the background services that keep working
        // while the lid is closed.
        void QuietBackground()
        {
            // Always on: while the lid is closed the machine should behave like real sleep,
            // which never runs maintenance. Defender itself is PPL and cannot be touched,
            // so we remove the things that make it wake up instead.

            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(MAINT_KEY, true))
                {
                    if (k == null) { Log("  quiet: maintenance key missing"); }
                    else
                    {
                        object v = k.GetValue("MaintenanceDisabled");
                        int prev = v == null ? -1 : Convert.ToInt32(v);
                        AppendState("maint=" + prev + "\n");
                        k.SetValue("MaintenanceDisabled", 1, RegistryValueKind.DWord);
                        Log("  quiet: automatic maintenance disabled (was " + prev + ")");
                    }
                }
            }
            catch (Exception ex) { Log("  quiet maint ERR " + ex.Message); }

            bool wuSafe = Process.GetProcessesByName("TiWorker").Length == 0;
            var list = new List<string>(QUIET_SVCS);
            if (wuSafe) list.AddRange(QUIET_SVCS_WU);
            else Log("  quiet: TiWorker running -> keep Windows Update services alone");

            string cmd = "";
            foreach (string s in list)
                cmd += "$s=Get-Service '" + s + "' -ErrorAction SilentlyContinue;" +
                       "if($s -and $s.Status -eq 'Running'){Stop-Service '" + s + "' -Force -ErrorAction SilentlyContinue;" +
                       "if((Get-Service '" + s + "').Status -eq 'Stopped'){'STOPPED_" + s + "'}};";
            string o = PsCmd(cmd);
            int n = 0;
            foreach (string line in SplitLines(o))
                if (line.StartsWith("STOPPED_")) { AppendState("svc=" + line.Substring(8) + "\n"); n++; }
            Log("  quiet: stopped " + n + " background service(s)");
        }

        void RestoreQuiet(string[] lines)
        {
            int maint = -1; bool hasMaint = false;
            var svcs = new List<string>();
            foreach (string l in lines)
            {
                if (l.StartsWith("maint=")) { hasMaint = true; int.TryParse(l.Substring(6), out maint); }
                else if (l.StartsWith("svc=")) svcs.Add(l.Substring(4).Trim());
            }
            if (hasMaint)
            {
                try
                {
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(MAINT_KEY, true))
                    {
                        if (k != null)
                        {
                            if (maint < 0) { try { k.DeleteValue("MaintenanceDisabled", false); } catch { } }
                            else k.SetValue("MaintenanceDisabled", maint, RegistryValueKind.DWord);
                        }
                    }
                    Log("  quiet: maintenance restored (was " + maint + ")");
                }
                catch (Exception ex) { Log("  quiet maint ERR " + ex.Message); }
            }
            if (svcs.Count > 0)
            {
                string cmd = "";
                foreach (string s in svcs) cmd += "Start-Service '" + s + "' -ErrorAction SilentlyContinue;";
                PsCmd(cmd);
                Log("  quiet: restarted " + svcs.Count + " background service(s)");
            }
        }

        readonly object logLock = new object();
        void Log(string s)
        {
            lock (logLock)
            {
                try { File.AppendAllText(logFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + Environment.NewLine); } catch { }
            }
        }

        readonly object stateLock = new object();
        void AppendState(string text)
        {
            lock (stateLock) { try { File.AppendAllText(stateFile, text); } catch { } }
        }

        string PsCmd(string cmd)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe");
                psi.Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"" + cmd + "\"";
                psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardOutput = true;
                using (var p = Process.Start(psi))
                {
                    string o = "";
                    var reader = new Thread(delegate ()
                    {
                        try { o = p.StandardOutput.ReadToEnd(); } catch { }
                    });
                    reader.IsBackground = true;
                    reader.Start();
                    if (!p.WaitForExit(60000))
                    {
                        try { p.Kill(); } catch { }
                        Log("  ps[TIMEOUT killed] " + cmd);
                        return "";
                    }
                    reader.Join(3000);
                    Log("  ps[" + p.ExitCode + "] " + cmd);
                    return o;
                }
            }
            catch (Exception ex) { Log("  ps ERR " + ex.Message); return ""; }
        }

        // Runs a PowerShell script via -EncodedCommand (Base64 UTF-16LE).
        // Needed because the WinRT radio script contains backticks and quotes.
        string PsCmdEncoded(string script)
        {
            try
            {
                string b64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo("powershell.exe");
                psi.Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " + b64;
                psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardOutput = true;
                using (var p = Process.Start(psi))
                {
                    string o = "";
                    var reader = new Thread(delegate () { try { o = p.StandardOutput.ReadToEnd(); } catch { } });
                    reader.IsBackground = true; reader.Start();
                    if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } Log("  psenc[TIMEOUT]"); return ""; }
                    reader.Join(3000);
                    Log("  psenc[" + p.ExitCode + "]");
                    return o == null ? "" : o.Trim();
                }
            }
            catch (Exception ex) { Log("  psenc ERR " + ex.Message); return ""; }
        }

        static string XmlEsc(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        bool CreateTask()
        {
            string exe = Application.ExecutablePath;
            string sid = "";
            try { sid = WindowsIdentity.GetCurrent().User.Value; }
            catch (Exception ex) { Log("task: SID ERR " + ex.Message); }
            if (sid == null || sid.Length == 0) { Log("task: cannot resolve user SID"); return false; }

            // schtasks /create defaults to DisallowStartIfOnBatteries=true and a PT72H
            // execution limit: on battery the logon start is silently skipped, and the
            // process is killed after three days. Register an explicit XML task instead.
            string xml =
                "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                "<RegistrationInfo><Description>Sleepify - software sleep watcher</Description></RegistrationInfo>" +
                "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + XmlEsc(sid) + "</UserId>" +
                "<Delay>PT5S</Delay></LogonTrigger></Triggers>" +
                "<Principals><Principal id=\"Author\"><UserId>" + XmlEsc(sid) + "</UserId>" +
                "<LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>" +
                "<Settings>" +
                "<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>" +
                "<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
                "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
                "<AllowHardTerminate>true</AllowHardTerminate>" +
                "<StartWhenAvailable>true</StartWhenAvailable>" +
                "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>" +
                "<IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>" +
                "<AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden>" +
                "<RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun>" +
                "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority>" +
                "</Settings>" +
                "<Actions Context=\"Author\"><Exec><Command>" + XmlEsc(exe) + "</Command>" +
                "<Arguments>--silent</Arguments></Exec></Actions>" +
                "</Task>";

            string tmp = Path.Combine(Path.GetTempPath(), "sleepify-task.xml");
            try { File.WriteAllText(tmp, xml, Encoding.Unicode); }
            catch (Exception ex) { Log("task: xml write ERR " + ex.Message); return false; }

            int rc = RunHidden("schtasks.exe", "/create /tn \"" + TaskName + "\" /xml \"" + tmp + "\" /f");
            try { File.Delete(tmp); } catch { }
            Log("task create (xml) rc=" + rc + " for " + exe);
            return rc == 0;
        }

        void DeleteTask()
        {
            RunHidden("schtasks.exe", "/delete /tn \"" + TaskName + "\" /f");
            Log("task deleted");
        }

        int RunHidden(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(30000)) { try { p.Kill(); } catch { } return -1; }
                    return p.ExitCode;
                }
            }
            catch (Exception ex) { Log("run ERR " + file + ": " + ex.Message); return -1; }
        }

        // Windows notification (rendered as a toast and kept in the notification centre)
        void Toast(string text)
        {
            try
            {
                tray.BalloonTipTitle = "Sleepify";
                tray.BalloonTipText = text;
                tray.BalloonTipIcon = ToolTipIcon.Info;
                tray.ShowBalloonTip(6000);
                Log("toast: " + text);
            }
            catch (Exception ex) { Log("toast ERR " + ex.Message); }
        }

        static int ReadLidAction(string kind)
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(PS_ROOT))
                {
                    string scheme = k.GetValue("ActivePowerScheme") as string;
                    if (scheme == null) return -1;
                    using (RegistryKey s = Registry.LocalMachine.OpenSubKey(
                        PS_ROOT + "\\" + scheme + "\\4f971e89-eebd-4455-a8de-9e59040e7347\\5ca83367-6e45-459f-a27b-476b1d01c936"))
                    {
                        if (s == null) return -1;
                        object v = s.GetValue(kind + "SettingIndex");
                        if (v == null) return -1;
                        return Convert.ToInt32(v);
                    }
                }
            }
            catch { return -1; }
        }

        static string[] SplitLines(string s)
        {
            if (s == null || s.Trim().Length == 0) return new string[0];
            var list = new List<string>();
            foreach (string raw in s.Split('\n'))
            {
                string t = raw.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }

        static bool DeniedName(string n)
        {
            foreach (string d in Deny) if (string.Equals(d, n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Returns 100 (Windows default) when the value is not explicitly set.
        // Never returns -1: a -1 would make RestoreAll skip the restore and leave
        // the CPU permanently throttled.
        static int ReadCpu(string kind)
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(PS_ROOT))
                {
                    string scheme = k.GetValue("ActivePowerScheme") as string;
                    if (scheme == null) return 100;
                    using (RegistryKey s = Registry.LocalMachine.OpenSubKey(PS_ROOT + "\\" + scheme + "\\" + SUB_PROC + "\\" + SET_MAXCPU))
                    {
                        if (s == null) return 100;
                        object v = s.GetValue(kind + "SettingIndex");
                        if (v == null) return 100;
                        return Convert.ToInt32(v);
                    }
                }
            }
            catch { return 100; }
        }

        int GetBrightness()
        {
            try
            {
                string o = PsCmd("(Get-CimInstance -Namespace root\\wmi -ClassName WmiMonitorBrightness -ErrorAction SilentlyContinue).CurrentBrightness");
                int v;
                if (o != null && int.TryParse(o.Trim(), out v)) return v;
            }
            catch { }
            return -1;
        }

        void SetBrightness(int v)
        {
            if (v < 0) return;
            PsCmd("$m=Get-CimInstance -Namespace root\\wmi -ClassName WmiMonitorBrightnessMethods -ErrorAction SilentlyContinue; if($m){$m.WmiSetBrightness(1," + v + ")}");
        }

        // ---------- radio control: software level only (same API as Airplane mode) ----------
        static string RadioScriptOff(bool keepWlan)
        {
            return
                "Add-Type -AssemblyName System.Runtime.WindowsRuntime -ErrorAction Stop;" +
                "$asTask=([System.WindowsRuntimeSystemExtensions].GetMethods()|Where-Object{$_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'})[0];" +
                "function Await($op,$t){$asTask.MakeGenericMethod($t).Invoke($null,@($op)).GetAwaiter().GetResult()};" +
                "[Windows.Devices.Radios.Radio,Windows.Devices.Radios,ContentType=WindowsRuntime]|Out-Null;" +
                "$rs=Await ([Windows.Devices.Radios.Radio]::GetRadiosAsync()) ([System.Collections.Generic.IReadOnlyList[Windows.Devices.Radios.Radio]]);" +
                "$skip='" + (keepWlan ? "WiFi" : "") + "';$n=0;$bt=$false;" +
                "foreach($r in $rs){ if($r.Kind.ToString() -eq 'Bluetooth'){$bt=$true};" +
                "  if($skip -and $r.Kind.ToString() -eq $skip){continue};" +
                "  if($r.State.ToString() -eq 'On'){ Await ($r.SetStateAsync([Windows.Devices.Radios.RadioState]::Off)) ([Windows.Devices.Radios.RadioAccessStatus])|Out-Null; $n++ } };" +
                "'radios off=' + $n + ' bluetoothExposed=' + $bt";
        }

        static string RadioScriptOn()
        {
            return
                "Add-Type -AssemblyName System.Runtime.WindowsRuntime -ErrorAction Stop;" +
                "$asTask=([System.WindowsRuntimeSystemExtensions].GetMethods()|Where-Object{$_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'})[0];" +
                "function Await($op,$t){$asTask.MakeGenericMethod($t).Invoke($null,@($op)).GetAwaiter().GetResult()};" +
                "[Windows.Devices.Radios.Radio,Windows.Devices.Radios,ContentType=WindowsRuntime]|Out-Null;" +
                "$rs=Await ([Windows.Devices.Radios.Radio]::GetRadiosAsync()) ([System.Collections.Generic.IReadOnlyList[Windows.Devices.Radios.Radio]]);" +
                "foreach($r in $rs){ if($r.State.ToString() -eq 'Off'){ Await ($r.SetStateAsync([Windows.Devices.Radios.RadioState]::On)) ([Windows.Devices.Radios.RadioAccessStatus])|Out-Null } };" +
                "'RADIOS_ON'";
        }

        bool Whitelisted(string name) { return whitelist.Contains(name.ToLowerInvariant()); }

        static string ProcPath(int pid)
        {
            IntPtr h = Nat.OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return null;
            try
            {
                int cap = 1024;
                var sb = new StringBuilder(cap);
                if (Nat.QueryFullProcessImageNameW(h, 0, sb, ref cap)) return sb.ToString();
                return null;
            }
            catch { return null; }
            finally { Nat.CloseHandle(h); }
        }

        // Freeze everything we safely can: current session only, never Windows binaries,
        // never the built-in deny list, never the user's whitelist.
        void FreezeAll()
        {
            int myPid = Process.GetCurrentProcess().Id;
            int mySess = Process.GetCurrentProcess().SessionId;
            string sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToLowerInvariant();
            int n = 0, skipped = 0;
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == myPid || p.Id <= 4) continue;
                    if (p.SessionId != mySess) continue;
                    string nm = p.ProcessName + ".exe";
                    if (DeniedName(nm) || Whitelisted(p.ProcessName)) { skipped++; continue; }
                    string path = ProcPath(p.Id);
                    if (path != null && path.ToLowerInvariant().StartsWith(sysRoot)) { skipped++; continue; }
                    if (Nat.Suspend(p.Id)) { n++; AppendState("frozen=" + p.Id + "|" + nm + "\n"); }
                }
                catch { }
            }
            Log("  froze " + n + " process(es), skipped " + skipped + " [freeze-all, whitelist " + whitelist.Count + "]");
        }

        void FreezeForeground(int fg)
        {
            var snap = Nat.Snapshot();
            var names = new Dictionary<int, string>();
            var parent = new Dictionary<int, int>();
            foreach (var pe in snap) { int id = (int)pe.th32ProcessID; names[id] = pe.szExeFile; parent[id] = (int)pe.th32ParentProcessID; }

            string fgName; names.TryGetValue(fg, out fgName); if (fgName == null) fgName = "";
            if (fg <= 0 || fgName.Length == 0 || DeniedName(fgName)) { Log("  freeze skipped ('" + fgName + "')"); return; }

            var tree = new List<int>(); tree.Add(fg);
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var kv in parent)
                {
                    if (tree.Contains(kv.Key)) continue;
                    if (tree.Contains(kv.Value)) { tree.Add(kv.Key); grew = true; }
                }
            }
            int n = 0;
            foreach (int id in tree)
            {
                string nm; names.TryGetValue(id, out nm); if (nm == null) nm = "?";
                if (DeniedName(nm)) continue;
                if (Nat.Suspend(id)) { n++; AppendState("frozen=" + id + "|" + nm + "\n"); }
            }
            Log("  froze '" + fgName + "' tree: " + n);
        }

        void GoDown()
        {
            var sw = Stopwatch.StartNew();
            var st = new StringBuilder();

            // ---------- instant native actions first: mute right now ----------
            bool prevMute;
            if (preMuteKnown)
            {
                prevMute = preMutePrev;
                Log("  mute already applied before debounce");
            }
            else
            {
                prevMute = false;
                try { prevMute = Audio.GetMute(); Audio.SetMute(true); Log("  muted immediately"); }
                catch (Exception ex) { Log("  mute ERR " + ex.Message); }
            }
            st.AppendLine("prevMute=" + (prevMute ? "1" : "0"));

            // sample the foreground process NOW, before anything else changes the desktop
            int fg = 0;
            try { Nat.GetWindowThreadProcessId(Nat.GetForegroundWindow(), out fg); } catch { }

            int acV = ReadCpu("AC"), dcV = ReadCpu("DC");
            AppendState("prevMute=" + (prevMute ? "1" : "0") + "\n" +
                        "cpuAc=" + acV + "\n" +
                        "cpuDc=" + dcV + "\n" +
                        "radios=1\n");

            // ---------- everything slow runs in parallel, starting right now ----------
            var workers = new List<Thread>();

            workers.Add(new Thread(delegate ()
            {
                try
                {
                    int prev = GetBrightness();
                    if (prev < 0) { Log("  brightness unavailable -> skip dim"); AppendState("bright=-1\n"); return; }
                    AppendState("bright=" + prev + "\n");   // record BEFORE dimming
                    SetBrightness(0);
                    Log("  brightness -> 0 (was " + prev + ")");
                }
                catch (Exception ex) { Log("  bright ERR " + ex.Message); }
            }));
            workers.Add(new Thread(delegate ()
            {
                try { Log("  radios: " + PsCmdEncoded(RadioScriptOff(keepWlan))); }
                catch (Exception ex) { Log("  radios ERR " + ex.Message); }
            }));
            workers.Add(new Thread(delegate ()
            {
                try { if (freezeAll) FreezeAll(); else FreezeForeground(fg); }
                catch (Exception ex) { Log("  freeze ERR " + ex.Message); }
            }));
            workers.Add(new Thread(delegate ()
            {
                try { QuietBackground(); }
                catch (Exception ex) { Log("  quiet ERR " + ex.Message); }
            }));
            workers.Add(new Thread(delegate ()
            {
                try
                {
                    PsCmd("powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX " + cpuCapLid + ";" +
                          "powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX " + cpuCapLid + ";" +
                          "powercfg /setactive SCHEME_CURRENT");
                    Log("  cpu capped to " + cpuCapLid + "% (was " + acV + "/" + dcV + ")");
                }
                catch (Exception ex) { Log("  cpu ERR " + ex.Message); }
            }));
            foreach (Thread t in workers) { t.IsBackground = true; t.Start(); }
            foreach (Thread t in workers) t.Join(60000);
            Log("  GoDown complete in " + sw.ElapsedMilliseconds + " ms");
        }

        void GoUp()
        {
            var sw = Stopwatch.StartNew();
            string[] lines = null;
            try { if (File.Exists(stateFile)) lines = File.ReadAllLines(stateFile); } catch { }

            var frozen = new List<string[]>();
            int bright = -1, acV = -1, dcV = -1, coolAc = -1, coolDc = -1;
            bool muteKnown = false, prevMute = false, radios = false, quiet = false;
            if (lines != null)
            {
                foreach (string l in lines)
                {
                    if (l.StartsWith("frozen=")) frozen.Add(l.Substring(7).Split('|'));
                    else if (l.StartsWith("prevMute=")) { muteKnown = true; prevMute = l.EndsWith("1"); }
                    else if (l.StartsWith("cpuAc=")) int.TryParse(l.Substring(6), out acV);
                    else if (l.StartsWith("cpuDc=")) int.TryParse(l.Substring(6), out dcV);
                    else if (l.StartsWith("coolAc=")) int.TryParse(l.Substring(7), out coolAc);
                    else if (l.StartsWith("coolDc=")) int.TryParse(l.Substring(7), out coolDc);
                    else if (l.StartsWith("bright=")) int.TryParse(l.Substring(7), out bright);
                    else if (l.StartsWith("radios=")) radios = true;
                    else if (l.StartsWith("svc=") || l.StartsWith("maint=")) quiet = true;
                }
            }

            // ---------- everything at once ----------
            var workers = new List<Thread>();

            if (radios) workers.Add(new Thread(delegate ()
            {
                try { Log("  radios: " + PsCmdEncoded(RadioScriptOn())); }
                catch (Exception ex) { Log("  radios ERR " + ex.Message); }
            }));

            if (quiet) workers.Add(new Thread(delegate ()
            {
                try { RestoreQuiet(lines); } catch (Exception ex) { Log("  quiet ERR " + ex.Message); }
            }));

            if (muteKnown) workers.Add(new Thread(delegate ()
            {
                try { Audio.SetMute(prevMute); Log("  mute restored"); } catch { }
            }));

            if (bright >= 0) workers.Add(new Thread(delegate ()
            {
                try { SetBrightness(bright); Log("  brightness restored to " + bright); }
                catch (Exception ex) { Log("  bright ERR " + ex.Message); }
            }));

            if (acV >= 0 || dcV >= 0 || coolAc >= 0 || coolDc >= 0) workers.Add(new Thread(delegate ()
            {
                try
                {
                    string c = "";
                    if (acV >= 0) c += "powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX " + acV + ";";
                    if (dcV >= 0) c += "powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX " + dcV + ";";
                    if (coolAc >= 0) c += "powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR SYSCOOLPOL " + coolAc + ";";
                    if (coolDc >= 0) c += "powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR SYSCOOLPOL " + coolDc + ";";
                    c += "powercfg /setactive SCHEME_CURRENT";
                    PsCmd(c);
                    Log("  cpu/cooling restored");
                }
                catch (Exception ex) { Log("  cpu ERR " + ex.Message); }
            }));

            if (frozen.Count > 0) workers.Add(new Thread(delegate () { ResumeFrozenList(frozen); }));

            foreach (Thread t in workers) { t.IsBackground = true; t.Start(); }
            foreach (Thread t in workers) t.Join(60000);

            try { File.Delete(stateFile); } catch { }
            Log("  GoUp complete in " + sw.ElapsedMilliseconds + " ms");
        }

        void RestoreAll() { RestoreAll(true); }

        void RestoreAll(bool includeWireless)
        {
            if (!File.Exists(stateFile)) { Log("  no state -> nothing to restore"); return; }
            string[] lines;
            try { lines = File.ReadAllLines(stateFile); } catch { return; }

            var frozen = new List<string[]>();
            var btIds = new List<string>();
            var netNames = new List<string>();
            int acV = -1, dcV = -1, bright = -1, coolAc = -1, coolDc = -1;
            bool muteKnown = false, prevMute = false, quiet = false;
            foreach (string l in lines)
            {
                if (l.StartsWith("frozen=")) frozen.Add(l.Substring(7).Split('|'));
                else if (l.StartsWith("prevMute=")) { muteKnown = true; prevMute = l.EndsWith("1"); }
                else if (l.StartsWith("cpuAc=")) int.TryParse(l.Substring(6), out acV);
                else if (l.StartsWith("cpuDc=")) int.TryParse(l.Substring(6), out dcV);
                else if (l.StartsWith("coolAc=")) int.TryParse(l.Substring(7), out coolAc);
                else if (l.StartsWith("coolDc=")) int.TryParse(l.Substring(7), out coolDc);
                else if (l.StartsWith("bright=")) int.TryParse(l.Substring(7), out bright);
                else if (l.StartsWith("svc=") || l.StartsWith("maint=")) quiet = true;
                else if (l.StartsWith("offbt=")) btIds.Add(l.Substring(6));
                else if (l.StartsWith("offnet=")) netNames.Add(l.Substring(7));
            }

            // 1) radios first (software-level, same API as Airplane mode)
            if (includeWireless)
            {
                bool radios = false;
                foreach (string l in lines) if (l.StartsWith("radios=")) { radios = true; break; }
                if (radios) { try { Log("  radios: " + PsCmdEncoded(RadioScriptOn())); } catch (Exception ex) { Log("  radios ERR " + ex.Message); } }
            }

            // 2) everything else in parallel (one shell spawn each, running concurrently)
            var workers = new List<Thread>();
            workers.Add(new Thread(delegate () { RestoreBrightness(bright); }));
            workers.Add(new Thread(delegate () { RestoreMute(muteKnown, prevMute); }));
            workers.Add(new Thread(delegate () { RestoreBt(btIds); }));
            workers.Add(new Thread(delegate () { RestoreCpu(acV, dcV, coolAc, coolDc); }));
            if (quiet) workers.Add(new Thread(delegate () { try { RestoreQuiet(lines); } catch (Exception ex) { Log("  quiet ERR " + ex.Message); } }));
            foreach (Thread t in workers) { t.IsBackground = true; t.Start(); }

            // 3) resume frozen processes here (native call, no shell spawn)
            ResumeFrozenList(frozen);

            // 4) wait for the parallel workers
            foreach (Thread t in workers) t.Join(90000);
            Log("  restore complete (wireless-first + parallel)");

            try { File.Delete(stateFile); } catch { }
        }

        void EnableWirelessList(List<string> names)
        {
            if (names.Count == 0) { Log("  net: nothing to restore"); return; }
            string cmd = "";
            foreach (string n in names)
                cmd += "Enable-NetAdapter -Name '" + n.Replace("'", "''") + "' -Confirm:$false -ErrorAction SilentlyContinue;";
            Log("  NET FIRST: enabling " + names.Count + " wireless adapter(s)");
            PsCmd(cmd);
            Log("  NET: enable command returned");
        }

        void ResumeFrozenList(List<string[]> frozen)
        {
            foreach (string[] p in frozen)
            {
                int pid;
                if (p.Length < 1 || !int.TryParse(p[0], out pid)) continue;
                string want = p.Length > 1 ? p[1] : "";
                string have = "";
                try { using (var pr = Process.GetProcessById(pid)) { have = pr.ProcessName + ".exe"; } } catch { have = ""; }
                if (want.Length > 0 && !string.Equals(want, have, StringComparison.OrdinalIgnoreCase))
                {
                    Log("  skip pid " + pid + " (expected " + want + ", now " + (have.Length > 0 ? have : "gone") + ")");
                    continue;
                }
                if (Nat.Resume(pid)) Log("  resumed " + pid + " (" + have + ")");
                else Log("  resume failed pid " + pid);
            }
        }

        void RestoreBrightness(int bright)
        {
            if (bright < 0) return;
            try { SetBrightness(bright); Log("  brightness restored to " + bright); }
            catch (Exception ex) { Log("  bright ERR " + ex.Message); }
        }

        void RestoreMute(bool known, bool prevMute)
        {
            if (!known) return;
            try { Audio.SetMute(prevMute); Log("  mute restored to " + (prevMute ? "muted" : "unmuted")); }
            catch { }
        }

        void RestoreBt(List<string> ids)
        {
            if (ids.Count == 0) return;
            string cmd = "";
            foreach (string id in ids)
                cmd += "Enable-PnpDevice -InstanceId '" + id.Replace("'", "''") + "' -Confirm:$false -ErrorAction SilentlyContinue;";
            PsCmd(cmd);
            Log("  bluetooth on");
        }

        void RestoreCpu(int acV, int dcV, int coolAc, int coolDc)
        {
            if (acV < 0 && dcV < 0 && coolAc < 0 && coolDc < 0) return;
            string cmd = "";
            if (acV >= 0) cmd += "powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX " + acV + ";";
            if (dcV >= 0) cmd += "powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX " + dcV + ";";
            if (coolAc >= 0) cmd += "powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR SYSCOOLPOL " + coolAc + ";";
            if (coolDc >= 0) cmd += "powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR SYSCOOLPOL " + coolDc + ";";
            cmd += "powercfg /setactive SCHEME_CURRENT";
            PsCmd(cmd);
            Log("  cpu/cooling restored to " + acV + "/" + dcV + " cool " + coolAc + "/" + coolDc);
        }
    }

    static class Program
    {
        static Mutex single;
        static EventWaitHandle showSignal;

        public static EventWaitHandle ShowSignal { get { return showSignal; } }

        static bool IsAdmin()
        {
            try
            {
                var id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        [STAThread]
        static void Main(string[] args)
        {
            bool silent = false;
            foreach (string a in args)
                if (string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)) silent = true;

            if (!IsAdmin())
            {
                try
                {
                    var psi = new ProcessStartInfo(Application.ExecutablePath, silent ? "--silent" : "");
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    Process.Start(psi);
                }
                catch { }
                return;
            }

            bool created;
            single = new Mutex(true, "Global\\SleepifySingleInstance", out created);
            showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Global\\SleepifyShow");
            if (!created)
            {
                // Already running (usually the silent logon instance): ask it to show up.
                try { showSignal.Set(); } catch { }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(silent));
            GC.KeepAlive(single);
        }
    }
}
