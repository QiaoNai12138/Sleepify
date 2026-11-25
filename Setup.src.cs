using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace SleepifySetup
{
    static class Loc
    {
        public static readonly string[] Names = {
            "中文（简体）", "English", "Français", "Русский", "Español", "العربية"
        };

        // 0 title, 1 langLabel, 2 pathLabel, 3 browse, 4 autoCheck, 5 next,
        // 6 cancel, 7 back, 8 install, 9 finish,
        // 10 warnTitle, 11 warnBody, 12 warnAmd, 13 agree,
        // 14 doneTitle, 15 manualHint
        public static readonly string[][] S = new string[][] {
            new string[] {
                "Sleepify 安装程序", "语言 / Language / 言語 / Язык / Idioma / اللغة",
                "安装位置：", "浏览...", "安装后自动运行设备检查",
                "下一步 >", "取消", "< 上一步", "安装", "完成",
                "此程序仅适用于无法休眠、无法睡眠的设备",
                "它不能完全代替系统的睡眠与休眠。如果您的机器可以正常睡眠或休眠，请谨慎使用本软件。",
                "5–7 代 锐龙 移动端 标压处理器 机型，严禁使用！！！",
                "我已知晓上述警告内容",
                "安装完成", "请手动运行以下位置的 start.cmd 进行设备检查：",
                "此工具会大幅拉低 CPU 使用率，并长时间维持低负载、低电压状态。\n\n对 AMD Ryzen 5000 / 6000 / 7000 移动端标压（H / HS / HX）而言，低负载低压正是硅片退化后 SoC（尤其内存控制器）失稳甚至崩溃的工况，可能造成毁灭性的稳定性影响。\n\n本机 CPU：\n{0}\n\n是否仍要安装？"
            },
            new string[] {
                "Sleepify Setup", "Language / 语言 / 言語 / Язык / Idioma / اللغة",
                "Install folder:", "Browse...", "Run device check automatically after installation",
                "Next >", "Cancel", "< Back", "Install", "Finish",
                "This program is ONLY for machines that cannot sleep or hibernate",
                "It cannot fully replace system sleep or hibernation. If your machine sleeps normally, use this software with care.",
                "AMD Ryzen 5th-7th generation MOBILE H-series CPUs: DO NOT USE!!!",
                "I have read and understood the warning above",
                "Installation complete", "Please run start.cmd manually from this folder:",
                "This tool forces a very low CPU load for hours.\n\nOn AMD Ryzen 5000 / 6000 / 7000 mobile H-series (H / HS / HX), low load and low voltage are exactly where a degraded SoC — the memory controller in particular — becomes unstable or crashes.\n\nDetected CPU:\n{0}\n\nInstall anyway?"
            },
            new string[] {
                "Installation de Sleepify", "Langue / Language / 语言 / Язык / Idioma / اللغة",
                "Dossier d'installation :", "Parcourir...", "Lancer la vérification du matériel après l'installation",
                "Suivant >", "Annuler", "< Retour", "Installer", "Terminer",
                "Ce programme est destiné UNIQUEMENT aux machines qui ne peuvent pas se mettre en veille ou en hibernation",
                "Il ne remplace pas complètement la veille ou l'hibernation du système. Si votre machine fonctionne normalement, utilisez ce logiciel avec prudence.",
                "Processeurs AMD Ryzen mobiles H de 5e à 7e génération : NE PAS UTILISER !!!",
                "J'ai lu et compris cet avertissement",
                "Installation terminée", "Veuillez exécuter start.cmd manuellement depuis ce dossier :",
                "Cet outil impose une charge CPU très faible pendant des heures.\n\nSur les AMD Ryzen 5000 / 6000 / 7000 mobiles série H (H / HS / HX), une charge et une tension faibles sont précisément le régime où un SoC dégradé — le contrôleur mémoire en particulier — devient instable ou plante.\n\nProcesseur détecté :\n{0}\n\nInstaller quand même ?"
            },
            new string[] {
                "Установка Sleepify", "Язык / Language / 语言 / 言語 / Idioma / اللغة",
                "Папка установки:", "Обзор...", "Запустить проверку оборудования после установки",
                "Далее >", "Отмена", "< Назад", "Установить", "Готово",
                "Эта программа предназначена ТОЛЬКО для машин, которые не могут уходить в сон или гибернацию",
                "Она не заменяет полностью системный сон и гибернацию. Если ваш компьютер работает нормально, используйте её с осторожностью.",
                "Мобильные процессоры AMD Ryzen H 5–7 поколений: НЕ ИСПОЛЬЗОВАТЬ!!!",
                "Я прочитал и понял предупреждение",
                "Установка завершена", "Запустите start.cmd вручную из этой папки:",
                "Эта программа на много часов принудительно снижает нагрузку на CPU.\n\nНа мобильных AMD Ryzen 5000 / 6000 / 7000 серии H (H / HS / HX) низкая нагрузка и низкое напряжение — это именно тот режим, в котором деградировавший SoC (особенно контроллер памяти) становится нестабильным или падает.\n\nОбнаруженный процессор:\n{0}\n\nВсё равно установить?"
            },
            new string[] {
                "Instalación de Sleepify", "Idioma / Language / 语言 / 言語 / Язык / اللغة",
                "Carpeta de instalación:", "Examinar...", "Ejecutar la comprobación del equipo después de instalar",
                "Siguiente >", "Cancelar", "< Atrás", "Instalar", "Finalizar",
                "Este programa es SOLO para equipos que no pueden suspenderse ni hibernar",
                "No sustituye por completo la suspensión ni la hibernación del sistema. Si su equipo funciona bien, use este software con precaución.",
                "Procesadores AMD Ryzen móviles H de 5.ª a 7.ª generación: ¡NO USAR!",
                "He leído y entendido la advertencia",
                "Instalación completada", "Ejecute start.cmd manualmente desde esta carpeta:",
                "Esta herramienta fuerza una carga de CPU muy baja durante horas.\n\nEn los AMD Ryzen 5000 / 6000 / 7000 móviles serie H (H / HS / HX), la carga y el voltaje bajos son justo el régimen en el que un SoC degradado —especialmente el controlador de memoria— se vuelve inestable o falla.\n\nCPU detectada:\n{0}\n\n¿Instalar de todos modos?"
            },
            new string[] {
                "تثبيت Sleepify", "اللغة / Language / 语言 / 言語 / Язык / Idioma",
                "مجلد التثبيت:", "استعراض...", "تشغيل فحص الجهاز تلقائياً بعد التثبيت",
                "التالي >", "إلغاء", "< رجوع", "تثبيت", "إنهاء",
                "هذا البرنامج مخصص فقط للأجهزة التي لا يمكنها السكون أو الإسبات",
                "لا يغني تماماً عن السكون أو الإسبات في النظام. إذا كان جهازك يعمل بشكل طبيعي فاستخدمه بحذر.",
                "معالجات AMD Ryzen المحمولة من الفئة H (الجيل 5–7): ممنوع الاستخدام!!!",
                "لقد قرأت التحذير وفهمته",
                "اكتمل التثبيت", "يرجى تشغيل start.cmd يدوياً من هذا المجلد:",
                "تفرض هذه الأداة حمل معالج منخفضاً جداً لساعات.\n\nفي معالجات AMD Ryzen 5000 / 6000 / 7000 المحمولة من الفئة H (H / HS / HX)، يُعد الحمل والجهد المنخفضان تحديداً النظام الذي يصبح فيه SoC المتدهور — وخاصة متحكم الذاكرة — غير مستقر أو ينهار.\n\nالمعالج المكتشف:\n{0}\n\nهل تريد التثبيت على أي حال؟"
            }
        };

        public static string Get(int lang, int idx)
        {
            if (lang < 0 || lang >= S.Length) lang = 1;
            return S[lang][idx];
        }
    }

    static class Hw
    {
        public static string CpuName()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (k == null) return "";
                    string s = k.GetValue("ProcessorNameString") as string;
                    return s == null ? "" : s.Trim();
                }
            }
            catch { return ""; }
        }

        // AMD Ryzen 5000/6000/7000 mobile H-series: unstable at low load / low voltage
        // once degraded. Warn instead of blocking, so the user can knowingly proceed.
        public static bool IsRiskyCpu()
        {
            string s = CpuName().ToUpperInvariant();
            if (s.IndexOf("RYZEN") < 0) return false;
            string[] tok = s.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
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
    }

    static class Payload
    {
        public static readonly string[] LangCodes = new string[] { "zh", "en", "fr", "ru", "es", "ar" };

        public static void Extract(string res, string dest)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(res))
            {
                if (s == null) throw new Exception("missing resource: " + res);
                using (FileStream f = File.Create(dest)) { s.CopyTo(f); }
            }
        }

        public static void Install(string dir)
        {
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(Path.Combine(dir, "config"));
            Directory.CreateDirectory(Path.Combine(dir, "logs"));
            Extract("payload_sleepify", Path.Combine(dir, "Sleepify.exe"));
            Extract("payload_start", Path.Combine(dir, "start.cmd"));
            Extract("payload_startps", Path.Combine(dir, "start.ps1"));
            Extract("payload_uninst", Path.Combine(dir, "uninstall.cmd"));
            Extract("payload_uninstps", Path.Combine(dir, "uninstall.ps1"));
            Extract("payload_i18n", Path.Combine(dir, "i18n.ps1"));
        }
    }

    class SetupForm : Form
    {
        int lang = -1;
        string installDir = @"C:\Sleepify";
        bool installOk = false;

        Panel p1, p2, p3;
        Label lblTitle, lblLang, lblPath;
        ComboBox cbLang;
        TextBox txtPath;
        Button btnBrowse, btnNext, btnCancel1;
        CheckBox chkAuto;

        Label lblWarnTitle, lblWarnBody, lblWarnAmd;
        CheckBox chkAgree;
        Button btnInstall, btnBack, btnCancel2;

        Label lblDone, lblManual;
        Button btnFinish;

        public SetupForm()
        {
            Text = "Sleepify Setup";
            ClientSize = new Size(700, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9f);
            BackColor = Color.FromArgb(245, 245, 248);

            BuildPage1();
            BuildPage2();
            BuildPage3();
            ShowPage(p1);
            ApplyLang();
        }

        void ShowPage(Panel p)
        {
            p1.Visible = p == p1;
            p2.Visible = p == p2;
            p3.Visible = p == p3;
            if (p == p2) p2.BringToFront();
            if (p == p3) p3.BringToFront();
            if (p == p1) p1.BringToFront();
        }

        void BuildPage1()
        {
            p1 = new Panel();
            p1.SetBounds(0, 0, 700, 470);
            p1.BackColor = Color.FromArgb(245, 245, 248);
            Controls.Add(p1);

            lblTitle = new Label();
            lblTitle.SetBounds(28, 22, 640, 34);
            lblTitle.Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(30, 30, 40);
            p1.Controls.Add(lblTitle);

            lblLang = new Label();
            lblLang.SetBounds(28, 92, 640, 22);
            p1.Controls.Add(lblLang);

            cbLang = new ComboBox();
            cbLang.DropDownStyle = ComboBoxStyle.DropDownList;
            cbLang.SetBounds(28, 116, 400, 26);
            cbLang.Items.Add("");
            foreach (string n in Loc.Names) cbLang.Items.Add(n);
            cbLang.SelectedIndex = 0;
            cbLang.SelectedIndexChanged += delegate
            {
                lang = cbLang.SelectedIndex - 1;
                ApplyLang();
            };
            p1.Controls.Add(cbLang);

            lblPath = new Label();
            lblPath.SetBounds(28, 168, 640, 22);
            p1.Controls.Add(lblPath);

            txtPath = new TextBox();
            txtPath.SetBounds(28, 192, 520, 26);
            txtPath.Text = installDir;
            p1.Controls.Add(txtPath);

            btnBrowse = new Button();
            btnBrowse.SetBounds(558, 191, 110, 28);
            btnBrowse.Click += delegate
            {
                using (var d = new FolderBrowserDialog())
                {
                    d.Description = "Sleepify";
                    if (d.ShowDialog(this) == DialogResult.OK) txtPath.Text = d.SelectedPath;
                }
            };
            p1.Controls.Add(btnBrowse);

            chkAuto = new CheckBox();
            chkAuto.SetBounds(28, 240, 620, 24);
            chkAuto.Checked = true;
            p1.Controls.Add(chkAuto);

            btnNext = new Button();
            btnNext.SetBounds(470, 412, 100, 32);
            btnNext.Enabled = false;
            btnNext.Click += delegate { ShowPage(p2); };
            p1.Controls.Add(btnNext);

            btnCancel1 = new Button();
            btnCancel1.SetBounds(580, 412, 90, 32);
            btnCancel1.Click += delegate { Close(); };
            p1.Controls.Add(btnCancel1);
        }

        void BuildPage2()
        {
            p2 = new Panel();
            p2.SetBounds(0, 0, 700, 470);
            p2.BackColor = Color.FromArgb(245, 245, 248);
            p2.Visible = false;
            Controls.Add(p2);

            lblWarnTitle = new Label();
            lblWarnTitle.SetBounds(28, 26, 640, 60);
            lblWarnTitle.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            lblWarnTitle.ForeColor = Color.FromArgb(150, 20, 20);
            p2.Controls.Add(lblWarnTitle);

            lblWarnBody = new Label();
            lblWarnBody.SetBounds(28, 96, 640, 80);
            lblWarnBody.Font = new Font("Microsoft YaHei UI", 10f);
            lblWarnBody.ForeColor = Color.FromArgb(40, 40, 40);
            p2.Controls.Add(lblWarnBody);

            lblWarnAmd = new Label();
            lblWarnAmd.SetBounds(28, 190, 640, 80);
            lblWarnAmd.Font = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold);
            lblWarnAmd.ForeColor = Color.Red;
            p2.Controls.Add(lblWarnAmd);

            var line = new Label();
            line.SetBounds(28, 280, 640, 2);
            line.BorderStyle = BorderStyle.Fixed3D;
            p2.Controls.Add(line);

            chkAgree = new CheckBox();
            chkAgree.SetBounds(28, 300, 640, 28);
            chkAgree.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            chkAgree.CheckedChanged += delegate { btnInstall.Enabled = chkAgree.Checked; };
            p2.Controls.Add(chkAgree);

            btnInstall = new Button();
            btnInstall.SetBounds(360, 412, 100, 32);
            btnInstall.Enabled = false;
            btnInstall.Click += delegate { DoInstall(); };
            p2.Controls.Add(btnInstall);

            btnBack = new Button();
            btnBack.SetBounds(470, 412, 100, 32);
            btnBack.Click += delegate { ShowPage(p1); };
            p2.Controls.Add(btnBack);

            btnCancel2 = new Button();
            btnCancel2.SetBounds(580, 412, 90, 32);
            btnCancel2.Click += delegate { Close(); };
            p2.Controls.Add(btnCancel2);
        }

        void BuildPage3()
        {
            p3 = new Panel();
            p3.SetBounds(0, 0, 700, 470);
            p3.BackColor = Color.FromArgb(245, 245, 248);
            p3.Visible = false;
            Controls.Add(p3);

            lblDone = new Label();
            lblDone.SetBounds(28, 40, 640, 40);
            lblDone.Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
            lblDone.ForeColor = Color.FromArgb(20, 110, 50);
            p3.Controls.Add(lblDone);

            lblManual = new Label();
            lblManual.SetBounds(28, 100, 640, 180);
            lblManual.Font = new Font("Microsoft YaHei UI", 10f);
            p3.Controls.Add(lblManual);

            btnFinish = new Button();
            btnFinish.SetBounds(580, 412, 90, 32);
            btnFinish.Click += delegate { Close(); };
            p3.Controls.Add(btnFinish);
        }

        void ApplyLang()
        {
            Text = "Sleepify Setup";
            if (lang < 0)
            {
                lblTitle.Text = "Sleepify";
                lblLang.Text = Loc.S[1][1];
                lblPath.Text = Loc.S[1][2];
                btnBrowse.Text = Loc.S[1][3];
                chkAuto.Text = Loc.S[1][4];
                btnNext.Text = Loc.S[1][5];
                btnCancel1.Text = Loc.S[1][6];
                btnNext.Enabled = false;
                return;
            }

            lblTitle.Text = "Sleepify - " + Loc.Get(lang, 0);
            lblLang.Text = Loc.Get(lang, 1);
            lblPath.Text = Loc.Get(lang, 2);
            btnBrowse.Text = Loc.Get(lang, 3);
            chkAuto.Text = Loc.Get(lang, 4);
            btnNext.Text = Loc.Get(lang, 5);
            btnCancel1.Text = Loc.Get(lang, 6);
            btnNext.Enabled = true;

            lblWarnTitle.Text = Loc.Get(lang, 10);
            lblWarnBody.Text = Loc.Get(lang, 11);
            lblWarnAmd.Text = Loc.Get(lang, 12);
            chkAgree.Text = Loc.Get(lang, 13);
            btnInstall.Text = Loc.Get(lang, 8);
            btnBack.Text = Loc.Get(lang, 7);
            btnCancel2.Text = Loc.Get(lang, 6);

            lblDone.Text = Loc.Get(lang, 14);
            btnFinish.Text = Loc.Get(lang, 9);
        }

        void DoInstall()
        {
            // Risky platform: explicit second confirmation, with the actual CPU name.
            if (Hw.IsRiskyCpu())
            {
                string prompt = Loc.Get(lang, 16).Replace("{0}", Hw.CpuName());
                if (MessageBox.Show(prompt, "Sleepify", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                    != DialogResult.Yes)
                    return;
            }

            installDir = txtPath.Text.Trim();
            if (installDir.Length == 0) installDir = @"C:\Sleepify";
            try
            {
                Payload.Install(installDir);
                // Remember the chosen language so start.ps1 / uninstall.ps1 follow it.
                string code = (lang >= 0 && lang < Payload.LangCodes.Length) ? Payload.LangCodes[lang] : "en";
                File.WriteAllText(Path.Combine(installDir, "config", "lang.txt"), code);
                installOk = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Sleepify Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string startCmd = Path.Combine(installDir, "start.cmd");
            string extra;

            if (chkAuto.Checked)
            {
                try
                {
                    var psi = new ProcessStartInfo(startCmd);
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    psi.WorkingDirectory = installDir;
                    Process.Start(psi);
                    extra = "";
                }
                catch (Exception ex)
                {
                    extra = ex.Message;
                }
            }
            else
            {
                extra = Loc.Get(lang, 15) + Environment.NewLine + startCmd;
            }

            lblManual.Text = "Sleepify  ->  " + installDir + Environment.NewLine + Environment.NewLine + (extra ?? "");
            ShowPage(p3);
        }
    }

    static class Program
    {
        static bool IsAdmin()
        {
            try
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(id)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        [STAThread]
        static void Main()
        {
            // Installing into C:\Sleepify requires elevation. Relaunch elevated once.
            if (!IsAdmin())
            {
                try
                {
                    var psi = new ProcessStartInfo(Application.ExecutablePath);
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    Process.Start(psi);
                }
                catch { }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }
}
