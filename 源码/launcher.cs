// ============================================================================
//  DeepSeek Harness 开发者预览版 · 桌面客户端  (v2.0.0)
// ----------------------------------------------------------------------------
//  v2.0 变化：
//    1. 独立运行：内置 Node.js（runtime\node\node.exe），不再依赖 WorkBuddy。
//    2. 真·应用窗口：用 WebView2 把网页「内嵌」进程序窗口（不再弹 Edge 浏览器）。
//  原理：启动后台服务(node bin.js web) -> 等它就绪 -> WebView2 内嵌显示网页。
//
//  【给未来的智能体 / 开发者】如何升级这个客户端：
//   1. 改本文件（加功能、改界面、改逻辑）。
//   2. 运行「源码\build.bat」重新编译生成 exe（会覆盖主程序）。
//   3. 把「版本.txt」和本文件的 Version 常量版本号 +1（例如 2.0.0 -> 2.1.0）。
//   4. 详细记录写到「更新日志.txt」。
//   —— 不需要重做整个软件，改这一份源码即可。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

class LauncherForm : Form
{
    // ===================== 默认配置（会被 config.txt 覆盖） =====================
    static string AppDir = @"D:\000—1—3Agent\001-1-1-1-2DeepSeek Harness 开发者预览版\app";   // DSH 程序本体目录
    static string NodePath = "";                                                                  // Node.js 路径（空 = 自动探测：优先内置）
    static string Url = "http://127.0.0.1:3080";                                                  // 网页地址
    static string Host = "127.0.0.1";                                                             // 服务主机
    static int Port = 3080;                                                                       // 服务端口
    static string LogFile = "";                                                                   // 日志文件（空 = 自动用 exe 同目录）
    static string Version = "1.0.0";                                                              // 客户端版本号

    static string BaseDir = "";          // exe 所在目录
    static string BinJs = "";            // DSH 启动入口 bin.js

    WebView2 webView;
    Panel loadingPanel;
    Label loadingLabel;
    ProgressBar progress;
    Process server;
    bool stopping;
    StreamWriter log;

    // ============================== 配置文件读取 ==============================
    // config.txt 每行一个「键=值」，以 # 开头的是注释。
    static Dictionary<string, string> LoadConfig(string path)
    {
        Dictionary<string, string> d = new Dictionary<string, string>();
        try
        {
            if (!File.Exists(path)) return d;
            foreach (string raw in File.ReadAllLines(path))
            {
                string t = raw.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                int eq = t.IndexOf('=');
                if (eq < 0) continue;
                string k = t.Substring(0, eq).Trim();
                string v = t.Substring(eq + 1).Trim();
                if (k.Length > 0) d[k] = v;
            }
        }
        catch { }
        return d;
    }

    static string Get(Dictionary<string, string> c, string key, string fallback)
    {
        string v;
        return c.TryGetValue(key, out v) && v.Length > 0 ? v : fallback;
    }

    // 自动探测 Node：1) 客户端内置的独立 Node  →  2) WorkBuddy 的 Node  →  3) 系统 PATH
    static string DetectNode()
    {
        string builtin = Path.Combine(BaseDir, "runtime", "node", "node.exe");
        if (File.Exists(builtin)) return builtin;
        string wb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".workbuddy", "binaries", "node", "versions", "22.22.2", "node.exe");
        if (File.Exists(wb)) return wb;
        return "node";
    }

    static void ApplyConfig()
    {
        BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        Dictionary<string, string> c = LoadConfig(Path.Combine(BaseDir, "config.txt"));
        AppDir = Get(c, "APP_DIR", AppDir);
        if (AppDir.Length > 0 && !Path.IsPathRooted(AppDir)) AppDir = Path.Combine(BaseDir, AppDir);
        NodePath = Get(c, "NODE_PATH", NodePath);
        Url = Get(c, "URL", Url);
        Host = Get(c, "HOST", Host);
        int p;
        if (int.TryParse(Get(c, "PORT", Port.ToString()), out p)) Port = p;
        LogFile = Get(c, "LOG_FILE", Path.Combine(BaseDir, "启动日志.txt"));
        if (NodePath.Length == 0) NodePath = DetectNode();
        BinJs = Path.Combine(AppDir, "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js");
    }

    // ============================== 界面 ==============================
    public LauncherForm()
    {
        this.Text = "DeepSeek Harness 客户端 v" + Version;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ClientSize = new Size(1280, 820);
        this.MinimumSize = new Size(900, 600);

        // 内嵌浏览器（WebView2），铺满整个窗口
        webView = new WebView2();
        webView.Dock = DockStyle.Fill;

        // 加载覆盖层（白底蓝字，与图标呼应）
        loadingPanel = new Panel();
        loadingPanel.Dock = DockStyle.Fill;
        loadingPanel.BackColor = Color.White;

        Label logo = new Label();
        logo.Text = ">_";
        logo.Font = new Font("Consolas", 46f, FontStyle.Bold);
        logo.ForeColor = Color.FromArgb(64, 108, 235);
        logo.Dock = DockStyle.Fill;
        logo.TextAlign = ContentAlignment.MiddleCenter;

        Label title = new Label();
        title.Text = "DeepSeek Harness 客户端";
        title.Font = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold);
        title.ForeColor = Color.FromArgb(23, 35, 71);
        title.Dock = DockStyle.Fill;
        title.TextAlign = ContentAlignment.MiddleCenter;

        loadingLabel = new Label();
        loadingLabel.Text = "正在加载中，请稍候...";
        loadingLabel.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Regular);
        loadingLabel.ForeColor = Color.FromArgb(112, 122, 143);
        loadingLabel.Dock = DockStyle.Fill;
        loadingLabel.TextAlign = ContentAlignment.MiddleCenter;

        progress = new ProgressBar();
        progress.Style = ProgressBarStyle.Marquee;
        progress.MarqueeAnimationSpeed = 28;
        progress.Anchor = AnchorStyles.None;
        progress.Width = 300;
        progress.Height = 6;

        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.ColumnCount = 1;
        layout.RowCount = 6;
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(logo, 0, 1);
        layout.Controls.Add(title, 0, 2);
        layout.Controls.Add(loadingLabel, 0, 3);
        layout.Controls.Add(progress, 0, 4);
        loadingPanel.Controls.Add(layout);

        Button aboutButton = new Button();
        aboutButton.Text = "关于";
        aboutButton.FlatStyle = FlatStyle.Flat;
        aboutButton.FlatAppearance.BorderSize = 0;
        aboutButton.ForeColor = Color.FromArgb(112, 122, 143);
        aboutButton.Cursor = Cursors.Hand;
        aboutButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        aboutButton.Location = new Point(this.ClientSize.Width - 90, this.ClientSize.Height - 46);
        aboutButton.Size = new Size(64, 30);
        aboutButton.Click += delegate { ShowAbout(); };
        loadingPanel.Controls.Add(aboutButton);
        aboutButton.BringToFront();

        this.Controls.Add(webView);
        this.Controls.Add(loadingPanel);
        loadingPanel.BringToFront();

        this.FormClosing += delegate { Exit(); };
    }

    void Ui(Action a) { if (this.InvokeRequired) { this.BeginInvoke(a); } else { a(); } }

    void Log(string s)
    {
        try { if (log != null) { log.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s); log.Flush(); } } catch { }
    }

    // 关闭客户端窗口：只关窗口本身，【绝不】动后台服务。
    // 后台(DSH 服务, 127.0.0.1:3080)是复用的，可能正跑着当前 AI 会话，
    // 所以关闭客户端不杀任何进程，保证"打开能用、关了还能用、关了再开还能用"。
    void Exit()
    {
        if (stopping) return;
        stopping = true;
        try { if (log != null) { log.WriteLine("--- client window closed (backend kept alive) ---"); log.Close(); } } catch { }
        Environment.Exit(0);
    }

    bool IsUp()
    {
        try
        {
            using (TcpClient c = new TcpClient())
            {
                IAsyncResult ar = c.BeginConnect(Host, Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(700)) { return false; }
                c.EndConnect(ar);
                return true;
            }
        }
        catch { return false; }
    }

    void StartServer()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = NodePath;
            psi.Arguments = "\"" + BinJs + "\" web";
            psi.WorkingDirectory = AppDir;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            server = Process.Start(psi);
            if (server == null) { Log("Process.Start returned null"); return; }
            server.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) Log(e.Data); };
            server.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) Log(e.Data); };
            server.BeginOutputReadLine();
            server.BeginErrorReadLine();
            Log("started server pid " + server.Id + "  node=" + NodePath);
        }
        catch (Exception ex) { Log("start failed: " + ex.Message); }
    }

    // WebView2 初始化失败时的兜底：退回 Edge 独立窗口（--app）
    void OpenAppWindowFallback()
    {
        string edge = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
        if (File.Exists(edge))
        {
            try { Process.Start(new ProcessStartInfo(edge, "--app=" + Url) { UseShellExecute = true }); Log("fallback Edge app window"); return; }
            catch { }
        }
        try { Process.Start(Url); } catch { }
    }

    // 在 UI 线程上初始化 WebView2 并导航
    async void InitWebView()
    {
        try
        {
            string userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeek-Harness-Client", "WebView2Data");
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userData);
            await webView.EnsureCoreWebView2Async(env);
            webView.CoreWebView2.NavigationCompleted += delegate { ShowWebView(); };
            webView.CoreWebView2.Navigate(Url);
            Log("webview2 initialized -> " + Url);
        }
        catch (Exception ex)
        {
            Log("webview2 init failed: " + ex.Message + " -> fallback Edge");
            OpenAppWindowFallback();
            ShowWebView();
        }
    }

    void ShowWebView()
    {
        Ui(delegate() { webView.Visible = true; loadingPanel.Visible = false; });
    }

    void Run()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(LogFile)); log = new StreamWriter(LogFile, true); } catch { }
        Log("--- launcher v" + Version + " start ---");
        if (!IsUp()) { StartServer(); }
        int waited = 0;
        while (!IsUp() && waited < 90000) { Thread.Sleep(500); waited += 500; }
        if (!IsUp())
        {
            Log("server not reachable after " + waited + " ms");
            Ui(delegate()
            {
                loadingLabel.Text = "启动失败，请稍后重试。\r\n（日志见「启动日志.txt」）";
                progress.Style = ProgressBarStyle.Blocks; progress.Value = 0;
            });
            return;
        }
        Ui(delegate() { loadingLabel.Text = "已启动，正在打开界面..."; });
        Ui(delegate { InitWebView(); });
    }

    // ============================== 自检模式 ==============================
    // 运行：  DeepSeek-Harness-Client.exe --check
    static void CheckMode()
    {
        ApplyConfig();
        bool up = false;
        try { using (TcpClient c = new TcpClient()) { c.Connect(Host, Port); up = true; } } catch { }
        string[] lines = new string[]
        {
            "version: " + Version,
            "baseDir: " + BaseDir,
            "node:    " + NodePath + "  ->  " + (File.Exists(NodePath) ? "OK" : "MISSING"),
            "app:     " + AppDir + "  ->  " + (Directory.Exists(AppDir) ? "OK" : "MISSING"),
            "bin.js:  " + BinJs + "  ->  " + (File.Exists(BinJs) ? "OK" : "MISSING"),
            "webview2 runtime: " + (Directory.Exists(@"C:\Program Files (x86)\Microsoft\EdgeWebView\Application") ? "installed" : "MISSING"),
            "url:     " + Url,
            "port " + Port + ": " + (up ? "UP" : "DOWN")
        };
        try { File.WriteAllText(Path.Combine(BaseDir, "check-result.txt"), string.Join("\r\n", lines)); } catch { }
    }

    void ShowAbout()
    {
        using (AboutForm f = new AboutForm(Version)) { f.ShowDialog(this); }
    }

    const int WM_SYSCOMMAND = 0x112;
    const int IDM_ABOUT = 0x1;
    [DllImport("user32.dll")]
    static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
    [DllImport("user32.dll")]
    static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        IntPtr menu = GetSystemMenu(this.Handle, false);
        AppendMenu(menu, 0x0, IDM_ABOUT, "关于(&A)...");
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_SYSCOMMAND && m.WParam.ToInt32() == IDM_ABOUT)
        {
            ShowAbout();
            return;
        }
        base.WndProc(ref m);
    }

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--check") { CheckMode(); return; }
        ApplyConfig();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        LauncherForm f = new LauncherForm();
        f.Show();
        Thread t = new Thread(f.Run);
        t.IsBackground = true;
        t.Start();
        Application.Run(f);
    }
}


class AboutForm : Form
{
    public AboutForm(string version)
    {
        this.Text = "关于 DeepSeek Harness 客户端";
        this.StartPosition = FormStartPosition.CenterParent;
        this.ClientSize = new Size(420, 260);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ShowInTaskbar = false;

        Label title = new Label();
        title.Text = "DeepSeek Harness 客户端";
        title.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
        title.ForeColor = Color.FromArgb(23, 35, 71);
        title.TextAlign = ContentAlignment.MiddleCenter;
        title.SetBounds(20, 14, 380, 30);

        Label ver = new Label();
        ver.Text = "版本 v" + version;
        ver.Font = new Font("Microsoft YaHei UI", 10f);
        ver.ForeColor = Color.FromArgb(112, 122, 143);
        ver.TextAlign = ContentAlignment.MiddleCenter;
        ver.SetBounds(20, 46, 380, 22);

        PictureBox ghIcon = new PictureBox();
        ghIcon.Image = LoadBrandIcon("github.png");
        ghIcon.SizeMode = PictureBoxSizeMode.Zoom;
        ghIcon.SetBounds(22, 88, 22, 22);

        LinkLabel gh = new LinkLabel();
        gh.Text = "github.com/Muxue-Dev";
        gh.Font = new Font("Microsoft YaHei UI", 10f);
        gh.LinkColor = Color.FromArgb(36, 90, 200);
        gh.ActiveLinkColor = Color.FromArgb(20, 60, 150);
        gh.SetBounds(54, 88, 350, 24);
        gh.LinkClicked += delegate { try { Process.Start("https://github.com/Muxue-Dev"); } catch { } };

        PictureBox biIcon = new PictureBox();
        biIcon.Image = LoadBrandIcon("bilibili.png");
        biIcon.SizeMode = PictureBoxSizeMode.Zoom;
        biIcon.SetBounds(22, 120, 22, 22);

        LinkLabel bi = new LinkLabel();
        bi.Text = "space.bilibili.com/110628804";
        bi.Font = new Font("Microsoft YaHei UI", 10f);
        bi.LinkColor = Color.FromArgb(36, 90, 200);
        bi.ActiveLinkColor = Color.FromArgb(20, 60, 150);
        bi.SetBounds(54, 120, 350, 24);
        bi.LinkClicked += delegate { try { Process.Start("https://space.bilibili.com/110628804"); } catch { } };

        Label author = new Label();
        author.Text = "作者：Muxue-Dev（幻曦之殇）";
        author.Font = new Font("Microsoft YaHei UI", 10f);
        author.ForeColor = Color.FromArgb(112, 122, 143);
        author.SetBounds(20, 160, 380, 24);

        Button closeBtn = new Button();
        closeBtn.Text = "关闭";
        closeBtn.SetBounds(170, 205, 80, 32);
        closeBtn.Click += delegate { this.Close(); };

        this.Controls.Add(title);
        this.Controls.Add(ver);
        this.Controls.Add(ghIcon);
        this.Controls.Add(gh);
        this.Controls.Add(biIcon);
        this.Controls.Add(bi);
        this.Controls.Add(author);
        this.Controls.Add(closeBtn);
        this.AcceptButton = closeBtn;
        this.CancelButton = closeBtn;
    }

    static Image LoadBrandIcon(string filename)
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", filename);
            if (File.Exists(path)) return Image.FromFile(path);
        }
        catch { }
        return null;
    }
}