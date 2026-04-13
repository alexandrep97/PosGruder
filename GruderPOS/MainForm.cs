using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using GruderPOS.Data;
using GruderPOS.Bridge;
using GruderPOS.Printing;

namespace GruderPOS;

public class MainForm : Form
{
    private WebView2 _webView = null!;
    private WebBridge _bridge = null!;
    private DatabaseManager _dbManager = null!;
    private SerialPortManager _serialPort = null!;

    // Title bar controls
    private Panel _titleBar = null!;
    private Label _titleLabel = null!;
    private Button _minimizeButton = null!;
    private Button _maximizeButton = null!;
    private Button _closeButton = null!;
    private Point _dragStartPoint;
    private bool _isDragging;

    public MainForm()
    {
        InitializeComponent();
        InitializeDatabase();
        InitializeWebView();
    }

    private void InitializeComponent()
    {
        this.Text = "POS GRUDER";
        this.Size = new Size(1280, 800);
        this.MinimumSize = new Size(1024, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.None;
        this.BackColor = Color.FromArgb(26, 26, 26);

        // Set taskbar and window icon
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "assets", "logo.ico");
        if (File.Exists(iconPath))
        {
            this.Icon = new Icon(iconPath);
        }

        // Custom title bar
        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(26, 26, 26),
            Padding = new Padding(10, 0, 0, 0)
        };

        _titleLabel = new Label
        {
            Text = "  ⚽ POS GRUDER",
            ForeColor = Color.FromArgb(245, 197, 24),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 200,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _closeButton = CreateTitleBarButton("✕", Color.FromArgb(232, 17, 35));
        _closeButton.Click += (s, e) => this.Close();

        _maximizeButton = CreateTitleBarButton("☐", Color.FromArgb(60, 60, 60));
        _maximizeButton.Click += (s, e) =>
        {
            this.WindowState = this.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal : FormWindowState.Maximized;
        };

        _minimizeButton = CreateTitleBarButton("─", Color.FromArgb(60, 60, 60));
        _minimizeButton.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

        _closeButton.Dock = DockStyle.Right;
        _maximizeButton.Dock = DockStyle.Right;
        _minimizeButton.Dock = DockStyle.Right;

        _titleBar.Controls.Add(_titleLabel);
        // Add in reverse order for DockStyle.Right: last added = innermost (leftmost)
        _titleBar.Controls.Add(_minimizeButton);
        _titleBar.Controls.Add(_maximizeButton);
        _titleBar.Controls.Add(_closeButton);

        // Title bar drag
        _titleBar.MouseDown += TitleBar_MouseDown;
        _titleBar.MouseMove += TitleBar_MouseMove;
        _titleBar.MouseUp += TitleBar_MouseUp;
        _titleLabel.MouseDown += TitleBar_MouseDown;
        _titleLabel.MouseMove += TitleBar_MouseMove;
        _titleLabel.MouseUp += TitleBar_MouseUp;

        // Double click to maximize
        _titleBar.DoubleClick += (s, e) =>
        {
            this.WindowState = this.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal : FormWindowState.Maximized;
        };
        _titleLabel.DoubleClick += (s, e) =>
        {
            this.WindowState = this.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal : FormWindowState.Maximized;
        };

        // WebView2
        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        this.Controls.Add(_webView);
        this.Controls.Add(_titleBar);
    }

    private Button CreateTitleBarButton(string text, Color hoverColor)
    {
        var btn = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(26, 26, 26),
            Font = new Font("Segoe UI", 10),
            Size = new Size(46, 36),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = hoverColor;
        return btn;
    }

    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragStartPoint = e.Location;
        }
    }

    private void TitleBar_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
                _dragStartPoint = new Point(this.Width / 2, _titleBar.Height / 2);
            }
            this.Location = new Point(
                this.Location.X + e.X - _dragStartPoint.X,
                this.Location.Y + e.Y - _dragStartPoint.Y);
        }
    }

    private void TitleBar_MouseUp(object? sender, MouseEventArgs e)
    {
        _isDragging = false;
    }

    private void InitializeDatabase()
    {
        _dbManager = new DatabaseManager();
        _dbManager.Initialize();

        _serialPort = new SerialPortManager();

        // Load serial port settings
        var settingsRepo = new Data.SettingsRepository(_dbManager);
        var port = settingsRepo.GetAsync("SerialPort").GetAwaiter().GetResult() ?? "COM3";
        var baudRate = int.Parse(settingsRepo.GetAsync("BaudRate").GetAwaiter().GetResult() ?? "9600");
        _serialPort.Configure(port, baudRate);

        _bridge = new WebBridge(_dbManager, _serialPort);
    }

    private async void InitializeWebView()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.GetTempPath());
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true; // Allow dev tools for debugging

            // Map virtual host to wwwroot folder
            var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "gruderpos.local", wwwrootPath,
                CoreWebView2HostResourceAccessKind.Allow);

            // Handle messages from JavaScript
            _webView.CoreWebView2.WebMessageReceived += async (s, e) =>
            {
                var message = e.WebMessageAsJson;
                var script = await _bridge.HandleMessage(message);
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            };

            // Navigate to the app
            _webView.CoreWebView2.Navigate("https://gruderpos.local/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao inicializar WebView2:\n{ex.Message}\n\nCertifique-se que o WebView2 Runtime está instalado.",
                "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _serialPort?.Dispose();
        base.OnFormClosing(e);
    }

    // Allow resize from edges
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;

        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);

            var cursor = this.PointToClient(Cursor.Position);
            const int gripSize = 8;

            if (cursor.X <= gripSize)
            {
                if (cursor.Y <= gripSize) m.Result = (IntPtr)HTTOPLEFT;
                else if (cursor.Y >= ClientSize.Height - gripSize) m.Result = (IntPtr)HTBOTTOMLEFT;
                else m.Result = (IntPtr)HTLEFT;
            }
            else if (cursor.X >= ClientSize.Width - gripSize)
            {
                if (cursor.Y <= gripSize) m.Result = (IntPtr)HTTOPRIGHT;
                else if (cursor.Y >= ClientSize.Height - gripSize) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else m.Result = (IntPtr)HTRIGHT;
            }
            else if (cursor.Y <= gripSize) m.Result = (IntPtr)HTTOP;
            else if (cursor.Y >= ClientSize.Height - gripSize) m.Result = (IntPtr)HTBOTTOM;

            return;
        }

        base.WndProc(ref m);
    }
}
