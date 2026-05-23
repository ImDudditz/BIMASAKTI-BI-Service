using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BimasaktiReports.FinancialReports
{
    public enum ServerStatus
    {
        Stopped,
        Loading,
        Running,
        Error
    }

    public class ManagerForm : Form
    {
        // Core fields
        private Process _backendProcess;
        private Process _frontendProcess;
        private readonly string _godModePath;
        private bool _backendHasError;
        private bool _frontendHasError;
        private Timer _statusPollTimer;

        private readonly string _localIp;
        private readonly string _coolAlias;

        // UI Components
        private TextBox _txtAlias;
        private TextBox _txtDirect;
        private TextBox _txtLocalhost;

        private Button _btnStartServer;
        private Button _btnRestartServer;
        private Button _btnStopServer;
        private Button _btnOpenWeb;

        private Button _btnGodMode;
        private RichTextBox _txtLogs;

        // Company Management UI Components
        private Button _btnNew;
        private Button _btnModify;
        private Button _btnSave;
        private Label _lblCompanyId;
        private TextBox _txtCompanyId;
        private Label _lblSelectCompany;
        private ComboBox _cmbCompanies;
        private RichTextBox _txtSyncUrls;
        private string _companyMode = "New";

        // Modern Custom Controls
        private ModernStatusIndicator _lblBackStatus;
        private ModernStatusIndicator _lblFrontStatus;
        private ModernInputPanel _borderCompanyId;
        private ModernInputPanel _borderCmb;

        // Modern Layout Panels
        private Panel _pnlSidebar;
        private Panel _pnlMainContent;
        private Label _lblScreenHeader;
        private Panel _pnlScreenContainer;

        private ModernNavButton _btnNavServers;
        private ModernNavButton _btnNavCompany;
        private ModernNavButton _btnNavLogs;

        private Panel _pnlServerScreen;
        private Panel _pnlCompanyScreen;
        private Panel _pnlLogsScreen;

        // Draggable Title Bar drag state
        private bool _mouseDown;
        private Point _lastLocation;
        private Button _activeNavButton;

        public ManagerForm()
        {
            // Auto-detect IPs & Hostname
            _localIp = GetLocalIp();
            string hostname = Dns.GetHostName();
            _coolAlias = hostname.ToLowerInvariant() + ".local";
            
            // God Mode file path: backend/engines/.god_mode_enabled
            _godModePath = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend", "engines", ".god_mode_enabled"));

            InitializeUI();
            
            Log("Manager initialized.");
            Log("Detected Network IP: " + _localIp);
            Log("Active Alias: " + _coolAlias);
            Log("Ready to start servers.");
            CheckExternalServers();
            InitializeStatusPollTimer();
        }

        private void InitializeUI()
        {
            // Window Setup: Borderless Premium Windows XP Sky Theme
            Text = "Bimasakti Manager - Enterprise Dashboard";
            Size = new Size(820, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(216, 228, 248); // Classic Windows XP Sky/Luna Light Blue
            ForeColor = Color.FromArgb(30, 30, 40); // Dark text for readability
            Font = new Font("Tahoma", 9.5F); // Tahoma - Windows XP Standard Font
            FormBorderStyle = FormBorderStyle.None; // Borderless window
            MaximizeBox = false;

            // --- TOP CUSTOM TITLE BAR (WINDOW CHROME) ---
            Panel pnlTitleBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(820, 40),
                BackColor = Color.FromArgb(36, 93, 215) // Royale/Luna Blue Base
            };
            pnlTitleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _mouseDown = true;
                    _lastLocation = e.Location;
                }
            };
            pnlTitleBar.MouseMove += (s, e) =>
            {
                if (_mouseDown)
                {
                    Location = new Point(
                        (Location.X - _lastLocation.X) + e.X,
                        (Location.Y - _lastLocation.Y) + e.Y);
                    Update();
                }
            };
            pnlTitleBar.MouseUp += (s, e) =>
            {
                _mouseDown = false;
            };
            
            // Custom Gradient Painting for Windows XP Sky Title Bar Look
            pnlTitleBar.Paint += (s, e) =>
            {
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    pnlTitleBar.ClientRectangle,
                    Color.FromArgb(36, 93, 215), // Left: Deep blue
                    Color.FromArgb(89, 151, 244), // Right: Lighter blue
                    0F))
                {
                    e.Graphics.FillRectangle(brush, pnlTitleBar.ClientRectangle);
                }
            };
            
            Controls.Add(pnlTitleBar);

            // Title Label
            Label lblTitle = new Label
            {
                Text = "⚡ BIMASAKTI MANAGER",
                Font = new Font("Tahoma", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(15, 0),
                Size = new Size(300, 40),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlTitleBar.Controls.Add(lblTitle);

            // Close button (✕)
            Button btnClose = new Button
            {
                Text = "✕",
                Font = new Font("Tahoma", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(775, 0),
                Size = new Size(45, 40),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(224, 79, 48); // Classic XP Red
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(174, 53, 30);
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.Click += (s, e) => Close();
            pnlTitleBar.Controls.Add(btnClose);

            // Minimize button (—)
            Button btnMinimize = new Button
            {
                Text = "—",
                Font = new Font("Tahoma", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(730, 0),
                Size = new Size(45, 40),
                Cursor = Cursors.Hand
            };
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(74, 130, 232); // Luna Blue Hover
            btnMinimize.FlatAppearance.MouseDownBackColor = Color.FromArgb(42, 87, 204);
            btnMinimize.MouseEnter += (s, e) => btnMinimize.ForeColor = Color.White;
            btnMinimize.MouseLeave += (s, e) => btnMinimize.ForeColor = Color.White;
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            pnlTitleBar.Controls.Add(btnMinimize);

            // --- LEFT SIDEBAR PANEL ---
            _pnlSidebar = new Panel
            {
                Location = new Point(0, 40),
                Size = new Size(190, 640),
                BackColor = Color.FromArgb(42, 87, 204) // Royale solid blue sidebar
            };
            Controls.Add(_pnlSidebar);

            // Stylized Branding Logo
            Label lblBrand = new Label
            {
                Text = "⚡ BIMASAKTI",
                Font = new Font("Tahoma", 12.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 204, 0), // Golden brand accent
                Location = new Point(18, 20),
                Size = new Size(160, 25),
                BackColor = Color.Transparent
            };
            Label lblSubBrand = new Label
            {
                Text = "FINANCIAL REPORTS APP",
                Font = new Font("Tahoma", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 220, 255), // Soft sky-blue white
                Location = new Point(20, 45),
                Size = new Size(160, 15),
                BackColor = Color.Transparent
            };
            _pnlSidebar.Controls.Add(lblBrand);
            _pnlSidebar.Controls.Add(lblSubBrand);

            // Navigation Buttons
            _btnNavServers = CreateNavButton("Dashboard", "servers", 100);
            _btnNavCompany = CreateNavButton("Companies", "company", 145);
            _btnNavLogs = CreateNavButton("System Terminal", "logs", 190);
            _pnlSidebar.Controls.AddRange(new Control[] { _btnNavServers, _btnNavCompany, _btnNavLogs });

            // --- MAIN AREA ---
            _pnlMainContent = new Panel
            {
                Location = new Point(190, 40),
                Size = new Size(630, 640),
                BackColor = Color.FromArgb(216, 228, 248) // Light Sky Blue canvas
            };
            Controls.Add(_pnlMainContent);

            _lblScreenHeader = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(590, 35),
                Text = "DASHBOARD & SERVERS",
                Font = new Font("Tahoma", 13.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(36, 93, 215), // Royale Blue screen headers
                TextAlign = ContentAlignment.MiddleLeft
            };
            _pnlMainContent.Controls.Add(_lblScreenHeader);

            _pnlScreenContainer = new Panel
            {
                Location = new Point(20, 70),
                Size = new Size(590, 550),
                BackColor = Color.Transparent
            };
            _pnlMainContent.Controls.Add(_pnlScreenContainer);

            // Swap Screens initialization
            _pnlServerScreen = new Panel { Location = new Point(0, 0), Size = new Size(590, 550), BackColor = Color.Transparent, Visible = true };
            _pnlCompanyScreen = new Panel { Location = new Point(0, 0), Size = new Size(590, 550), BackColor = Color.Transparent, Visible = false };
            _pnlLogsScreen = new Panel { Location = new Point(0, 0), Size = new Size(590, 550), BackColor = Color.Transparent, Visible = false };
            _pnlScreenContainer.Controls.AddRange(new Control[] { _pnlServerScreen, _pnlCompanyScreen, _pnlLogsScreen });

            // --- 1. SERVER SCREEN DESIGN ---

            // Card 1: Network Configuration
            ModernCard pnlNetwork = new ModernCard
            {
                HeaderText = "NETWORK CONFIGURATION",
                Location = new Point(0, 5),
                Size = new Size(550, 160),
                BackColor = Color.FromArgb(240, 246, 254) // Soft pale blue tint
            };
            _pnlServerScreen.Controls.Add(pnlNetwork);

            Label lblAlias = new Label { Text = "✨ Custom Alias:", Location = new Point(15, 45), Size = new Size(110, 20), Font = new Font("Tahoma", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 70), BackColor = Color.Transparent };
            pnlNetwork.Controls.Add(lblAlias);
            ModernInputPanel borderAlias = new ModernInputPanel();
            borderAlias.Location = new Point(130, 42);
            borderAlias.Size = new Size(405, 26);
            _txtAlias = CreateInputTextBox("https://" + _coolAlias + ":5173", true);
            borderAlias.Controls.Add(_txtAlias);
            borderAlias.BindControl(_txtAlias);
            pnlNetwork.Controls.Add(borderAlias);

            Label lblDirect = new Label { Text = "Direct IP Link:", Location = new Point(15, 80), Size = new Size(110, 20), Font = new Font("Tahoma", 9F), ForeColor = Color.FromArgb(50, 50, 70), BackColor = Color.Transparent };
            pnlNetwork.Controls.Add(lblDirect);
            ModernInputPanel borderDirect = new ModernInputPanel();
            borderDirect.Location = new Point(130, 77);
            borderDirect.Size = new Size(405, 26);
            _txtDirect = CreateInputTextBox("https://" + _localIp + ":5173", true);
            borderDirect.Controls.Add(_txtDirect);
            borderDirect.BindControl(_txtDirect);
            pnlNetwork.Controls.Add(borderDirect);

            Label lblLocalhost = new Label { Text = "🔒 Localhost (Safe):", Location = new Point(15, 115), Size = new Size(110, 20), Font = new Font("Tahoma", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 70), BackColor = Color.Transparent };
            pnlNetwork.Controls.Add(lblLocalhost);
            ModernInputPanel borderLocalhost = new ModernInputPanel();
            borderLocalhost.Location = new Point(130, 112);
            borderLocalhost.Size = new Size(405, 26);
            _txtLocalhost = CreateInputTextBox("https://127.0.0.1:5173", true);
            borderLocalhost.Controls.Add(_txtLocalhost);
            borderLocalhost.BindControl(_txtLocalhost);
            pnlNetwork.Controls.Add(borderLocalhost);

            // Card 2: Process Health Status
            ModernCard pnlStatus = new ModernCard
            {
                HeaderText = "PROCESS HEALTH STATUS",
                Location = new Point(0, 175),
                Size = new Size(550, 80),
                BackColor = Color.FromArgb(240, 246, 254)
            };
            _pnlServerScreen.Controls.Add(pnlStatus);

            _lblBackStatus = new ModernStatusIndicator
            {
                StatusLabel = "Backend Web API",
                Location = new Point(15, 42),
                Size = new Size(250, 30)
            };
            _lblFrontStatus = new ModernStatusIndicator
            {
                StatusLabel = "Vue Frontend",
                Location = new Point(285, 42),
                Size = new Size(250, 30)
            };
            pnlStatus.Controls.AddRange(new Control[] { _lblBackStatus, _lblFrontStatus });

            // Card 3: Instance Controls
            ModernCard pnlControls = new ModernCard
            {
                HeaderText = "SERVER INSTANCE CONTROLS",
                Location = new Point(0, 265),
                Size = new Size(550, 115),
                BackColor = Color.FromArgb(240, 246, 254)
            };
            _pnlServerScreen.Controls.Add(pnlControls);

            _btnStartServer = CreateFlatButton("Start Engine", 15, 42, 120, 38, Color.FromArgb(39, 174, 96), Color.FromArgb(46, 204, 113));
            _btnStartServer.Click += new EventHandler(StartServerClick);

            _btnRestartServer = CreateFlatButton("Restart Service", 145, 42, 120, 38, Color.FromArgb(42, 87, 204), Color.FromArgb(74, 130, 232));
            _btnRestartServer.Click += new EventHandler(RestartServerClick);

            _btnStopServer = CreateFlatButton("Stop Engine", 275, 42, 120, 38, Color.FromArgb(192, 57, 43), Color.FromArgb(231, 76, 60));
            _btnStopServer.Click += new EventHandler(StopServerClick);

            _btnOpenWeb = CreateFlatButton("Launch Portal", 405, 42, 130, 38, Color.FromArgb(142, 68, 173), Color.FromArgb(155, 89, 182));
            _btnOpenWeb.Click += new EventHandler(OpenWebClick);

            pnlControls.Controls.AddRange(new Control[] { _btnStartServer, _btnRestartServer, _btnStopServer, _btnOpenWeb });

            // Card 4: Developer Engine Controls
            ModernCard pnlDev = new ModernCard
            {
                HeaderText = "DEVELOPER ENGINE CONTROLS",
                Location = new Point(0, 390),
                Size = new Size(550, 75),
                BackColor = Color.FromArgb(240, 246, 254)
            };
            _pnlServerScreen.Controls.Add(pnlDev);

            Label lblDevTitle = new Label { Text = "🔒 Bypass Admin Control Restrictions (God Mode):", Location = new Point(15, 42), Size = new Size(340, 20), Font = new Font("Tahoma", 9F), ForeColor = Color.FromArgb(50, 50, 70), BackColor = Color.Transparent };
            pnlDev.Controls.Add(lblDevTitle);

            _btnGodMode = CreateFlatButton("God Mode: ...", 365, 34, 170, 30, Color.FromArgb(180, 210, 255), Color.FromArgb(200, 225, 255));
            _btnGodMode.Click += new EventHandler(ToggleGodModeClick);
            UpdateGodModeButton();
            pnlDev.Controls.Add(_btnGodMode);

            // --- 2. COMPANY MANAGEMENT SCREEN DESIGN ---
            ModernCard pnlCompany = new ModernCard
            {
                HeaderText = "TENANT ORG PROFILE MANAGEMENT",
                Location = new Point(0, 5),
                Size = new Size(550, 480),
                BackColor = Color.FromArgb(240, 246, 254)
            };
            _pnlCompanyScreen.Controls.Add(pnlCompany);

            _btnNew = CreateFlatButton("New Tenant", 15, 40, 130, 32, Color.FromArgb(42, 87, 204), Color.FromArgb(74, 130, 232));
            _btnNew.Click += new EventHandler(NewModeClick);
            pnlCompany.Controls.Add(_btnNew);

            _btnModify = CreateFlatButton("Modify Tenant", 155, 40, 130, 32, Color.FromArgb(180, 210, 255), Color.FromArgb(200, 225, 255));
            _btnModify.Click += new EventHandler(ModifyModeClick);
            pnlCompany.Controls.Add(_btnModify);

            _lblCompanyId = new Label { Text = "Company Code (5 alphanumeric):", Location = new Point(15, 88), Size = new Size(200, 20), Font = new Font("Tahoma", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 70), BackColor = Color.Transparent };
            
            _borderCompanyId = new ModernInputPanel();
            _borderCompanyId.Location = new Point(230, 85);
            _borderCompanyId.Size = new Size(130, 26);
            _txtCompanyId = new TextBox
            {
                Text = "",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 30, 40),
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                MaxLength = 5,
                CharacterCasing = CharacterCasing.Upper
            };
            _borderCompanyId.Controls.Add(_txtCompanyId);
            _borderCompanyId.BindControl(_txtCompanyId);

            _lblSelectCompany = new Label { Text = "Select Company to Modify:", Location = new Point(15, 88), Size = new Size(200, 20), Font = new Font("Tahoma", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 70), BackColor = Color.Transparent };
            
            _borderCmb = new ModernInputPanel();
            _borderCmb.Location = new Point(230, 85);
            _borderCmb.Size = new Size(130, 26);
            _cmbCompanies = new ComboBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 30, 40),
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Tahoma", 9.5F, FontStyle.Bold)
            };
            _cmbCompanies.SelectedIndexChanged += new EventHandler(CompanySelectedIndexChanged);
            _borderCmb.Controls.Add(_cmbCompanies);
            _borderCmb.BindControl(_cmbCompanies);

            pnlCompany.Controls.AddRange(new Control[] { _lblCompanyId, _borderCompanyId, _lblSelectCompany, _borderCmb });

            Label lblUrls = new Label { Text = "Remote Endpoint Synchronization URLs (one URL per line):", Location = new Point(15, 125), Size = new Size(400, 20), Font = new Font("Tahoma", 9F), ForeColor = Color.FromArgb(50, 50, 70), BackColor = Color.Transparent };
            pnlCompany.Controls.Add(lblUrls);

            ModernInputPanel borderSyncUrls = new ModernInputPanel();
            borderSyncUrls.Location = new Point(15, 150);
            borderSyncUrls.Size = new Size(520, 260);
            _txtSyncUrls = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 30, 40),
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true
            };
            borderSyncUrls.Controls.Add(_txtSyncUrls);
            borderSyncUrls.BindControl(_txtSyncUrls);
            pnlCompany.Controls.Add(borderSyncUrls);

            _btnSave = CreateFlatButton("Save Configuration", 385, 425, 150, 36, Color.FromArgb(39, 174, 96), Color.FromArgb(46, 204, 113));
            _btnSave.Click += new EventHandler(SaveCompanyClick);
            pnlCompany.Controls.Add(_btnSave);

            SetCompanyMode("New");

            // --- 3. SYSTEM TERMINAL LOGS SCREEN DESIGN ---
            ModernCard pnlLogs = new ModernCard
            {
                HeaderText = "SYSTEM TERMINAL LOGS",
                Location = new Point(0, 5),
                Size = new Size(550, 480),
                BackColor = Color.FromArgb(240, 246, 254)
            };
            _pnlLogsScreen.Controls.Add(pnlLogs);

            ModernInputPanel borderLogs = new ModernInputPanel();
            borderLogs.Location = new Point(15, 42);
            borderLogs.Size = new Size(520, 380);
            _txtLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 255),
                ForeColor = Color.FromArgb(20, 40, 80),
                Font = new Font("Consolas", 9.5F),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };
            borderLogs.Controls.Add(_txtLogs);
            borderLogs.BindControl(_txtLogs);
            pnlLogs.Controls.Add(borderLogs);

            Button btnClearTerminal = CreateFlatButton("Clear Terminal", 385, 432, 150, 32, Color.FromArgb(180, 210, 255), Color.FromArgb(200, 225, 255));
            btnClearTerminal.Click += (s, ev) => { if (_txtLogs != null) _txtLogs.Clear(); };
            pnlLogs.Controls.Add(btnClearTerminal);

            // Set Form Rounded Corners region clipping
            UpdateFormRegion();

            // Select Servers Dashboard by default
            SetActiveNavButton(_btnNavServers);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateFormRegion();
        }

        private void UpdateFormRegion()
        {
            using (var path = GetRoundedRectanglePath(new Rectangle(0, 0, Width, Height), 8))
            {
                this.Region = new Region(path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw clean 1px border around the entire borderless rounded window
            using (Pen pen = new Pen(Color.FromArgb(36, 93, 215), 1.5f))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = GetRoundedRectanglePath(new Rectangle(0, 0, Width, Height), 8))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        // --- BUTTON EVENT HANDLERS (EXPLICIT FOR LEGACY DELEGATES) ---

        private void StartServerClick(object sender, EventArgs e) { StartAll(); }
        private void RestartServerClick(object sender, EventArgs e) { RestartAll(); }
        private void StopServerClick(object sender, EventArgs e) { StopAll(); }
        private void OpenWebClick(object sender, EventArgs e) { OpenWeb(); }
        private void ToggleGodModeClick(object sender, EventArgs e) { ToggleGodMode(); }
        private void NewModeClick(object sender, EventArgs e) { SetCompanyMode("New"); }
        private void ModifyModeClick(object sender, EventArgs e) { SetCompanyMode("Modify"); }
        private void SaveCompanyClick(object sender, EventArgs e) { SaveCompany(); }

        // --- NAVIGATION HELPERS ---

        private ModernNavButton CreateNavButton(string text, string tag, int top)
        {
            ModernNavButton btn = new ModernNavButton(text, tag)
            {
                Location = new Point(10, top),
                Size = new Size(170, 38)
            };
            btn.Click += new EventHandler(NavButtonClick);
            return btn;
        }

        private void NavButtonClick(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;
            SetActiveNavButton(btn);
        }

        private void SetActiveNavButton(Button activeBtn)
        {
            _activeNavButton = activeBtn;
            
            foreach (Control ctrl in _pnlSidebar.Controls)
            {
                ModernNavButton btn = ctrl as ModernNavButton;
                if (btn != null)
                {
                    btn.IsSelected = (btn == activeBtn);
                }
            }

            string tab = activeBtn.Tag.ToString();
            _pnlServerScreen.Visible = (tab == "servers");
            _pnlCompanyScreen.Visible = (tab == "company");
            _pnlLogsScreen.Visible = (tab == "logs");

            if (tab == "servers")
            {
                _lblScreenHeader.Text = "DASHBOARD & SERVERS";
            }
            else if (tab == "company")
            {
                _lblScreenHeader.Text = "TENANT MANAGEMENT";
            }
            else
            {
                _lblScreenHeader.Text = "SYSTEM TERMINAL LOGS";
            }
        }

        // --- MODERN UI LAYOUT CREATION HELPERS ---

        private TextBox CreateInputTextBox(string initialText, bool readOnly)
        {
            return new TextBox
            {
                Text = initialText,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                ForeColor = readOnly ? Color.FromArgb(180, 80, 0) : Color.FromArgb(30, 30, 40),
                Font = new Font("Consolas", 9.5F, FontStyle.Bold),
                ReadOnly = readOnly
            };
        }

        private Button CreateFlatButton(string text, int left, int top, int width, int height, Color backColor, Color hoverColor)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, height),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Tahoma", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hoverColor;
            btn.FlatAppearance.MouseDownBackColor = hoverColor;
            
            btn.SizeChanged += (s, ev) =>
            {
                using (var path = GetRoundedRectanglePath(new Rectangle(0, 0, btn.Width, btn.Height), 4))
                {
                    btn.Region = new Region(path);
                }
            };
            return btn;
        }

        // --- CORE PROCESS CONTROL ---

        private string GetLocalIp()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 80);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint != null ? endPoint.Address.ToString() : "127.0.0.1";
                }
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(Log), message);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            if (_txtLogs != null)
            {
                _txtLogs.AppendText("[" + timestamp + "] " + message + "\n");
                _txtLogs.ScrollToCaret();
            }
        }

        private void StartBackend()
        {
            try
            {
                _backendHasError = false;
                string backendDir = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend"));

                _backendProcess = new Process();
                _backendProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run",
                    WorkingDirectory = backendDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _backendProcess.OutputDataReceived += new DataReceivedEventHandler(BackendOutputReceived);
                _backendProcess.ErrorDataReceived += new DataReceivedEventHandler(BackendErrorReceived);

                _backendProcess.Start();
                _backendProcess.BeginOutputReadLine();
                _backendProcess.BeginErrorReadLine();

                Log("Starting C# Web API Backend on port 8001...");
                
                UpdateControlsState();
            }
            catch (Exception ex)
            {
                Log("Failed to start C# backend: " + ex.Message);
            }
        }

        private void BackendOutputReceived(object sender, DataReceivedEventArgs eventArguments)
        {
            if (eventArguments.Data != null)
            {
                Log("[Backend] " + eventArguments.Data);
                if (eventArguments.Data.IndexOf(": error CS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("Build FAILED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("Unhandled exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("fail:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("crit:", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _backendHasError = true;
                }
            }
        }

        private void BackendErrorReceived(object sender, DataReceivedEventArgs eventArguments)
        {
            if (eventArguments.Data != null)
            {
                Log("[Backend Error] " + eventArguments.Data);
                // Only flag true fatal errors — not the common dotnet diagnostic stderr messages
                // that contain "error"/"failed" in informational context (e.g. launchSettings, MSBuild noise)
                if (eventArguments.Data.IndexOf(": error CS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("Build FAILED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("Unhandled exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("System.Exception", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _backendHasError = true;
                }
            }
        }

        private void StopBackend()
        {
            if (_backendProcess != null)
            {
                Log("Stopping C# Backend process tree...");
                KillProcessTree(_backendProcess);
                _backendProcess = null;
                Log("C# Backend stopped.");
            }
            else
            {
                KillProcessOnPort(8001);
            }

            UpdateControlsState();
        }

        private void StartFrontend()
        {
            try
            {
                _frontendHasError = false;
                string frontendDir = Path.GetFullPath(Path.Combine(GetRootDirectory(), "frontend"));

                _frontendProcess = new Process();
                _frontendProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c npm run dev",
                    WorkingDirectory = frontendDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _frontendProcess.OutputDataReceived += new DataReceivedEventHandler(FrontendOutputReceived);
                _frontendProcess.ErrorDataReceived += new DataReceivedEventHandler(FrontendErrorReceived);

                _frontendProcess.Start();
                _frontendProcess.BeginOutputReadLine();
                _frontendProcess.BeginErrorReadLine();

                Log("Starting Vue Frontend on port 5173...");

                UpdateControlsState();
            }
            catch (Exception ex)
            {
                Log("Failed to start Vue Frontend: " + ex.Message);
            }
        }

        private void FrontendOutputReceived(object sender, DataReceivedEventArgs eventArguments)
        {
            if (eventArguments.Data != null)
            {
                Log("[Vite] " + eventArguments.Data);
                if (eventArguments.Data.IndexOf("failed to compile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("Failed to compile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("Syntax Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("SyntaxError", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("Internal server error", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _frontendHasError = true;
                }
            }
        }

        private void FrontendErrorReceived(object sender, DataReceivedEventArgs eventArguments)
        {
            if (eventArguments.Data != null)
            {
                Log("[Vite Error] " + eventArguments.Data);
                if (eventArguments.Data.IndexOf("failed to compile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("Failed to compile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("Syntax Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("SyntaxError", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("Internal server error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventArguments.Data.IndexOf("ERROR  ", StringComparison.Ordinal) >= 0 ||
                    eventArguments.Data.IndexOf("error: ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _frontendHasError = true;
                }
            }
        }

        private void StopFrontend()
        {
            if (_frontendProcess != null)
            {
                Log("Stopping Vue Frontend process tree...");
                KillProcessTree(_frontendProcess);
                _frontendProcess = null;
                Log("Vue Frontend stopped.");
            }
            else
            {
                KillProcessOnPort(5173);
            }

            UpdateControlsState();
        }

        private void KillProcessTree(Process process)
        {
            if (process == null || process.HasExited) return;

            try
            {
                using (Process killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = "/F /T /PID " + process.Id,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    if (killer != null) killer.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Log("Error executing process tree shutdown: " + ex.Message);
            }
        }

        // --- GOD MODE MANAGEMENT ---

        private void UpdateGodModeButton()
        {
            if (_btnGodMode == null) return;

            if (File.Exists(_godModePath))
            {
                _btnGodMode.Text = "God Mode: ON";
                _btnGodMode.BackColor = Color.FromArgb(230, 126, 34); // Warm Orange
                _btnGodMode.ForeColor = Color.White;
            }
            else
            {
                _btnGodMode.Text = "God Mode: OFF";
                _btnGodMode.BackColor = Color.FromArgb(180, 210, 255); // Soft Sky Blue
                _btnGodMode.ForeColor = Color.FromArgb(30, 30, 40);
            }
        }

        private void ToggleGodMode()
        {
            try
            {
                string parentDir = Path.GetDirectoryName(_godModePath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                if (File.Exists(_godModePath))
                {
                    File.Delete(_godModePath);
                    Log("God Mode disabled.");
                }
                else
                {
                    File.WriteAllText(_godModePath, "enabled");
                    Log("God Mode enabled! Admin bypass active.");
                }
                UpdateGodModeButton();
            }
            catch (Exception ex)
            {
                Log("Error toggling God Mode: " + ex.Message);
            }
        }

        private void SetCompanyMode(string mode)
        {
            _companyMode = mode;
            if (_companyMode == "New")
            {
                _btnNew.BackColor = Color.FromArgb(42, 87, 204); // Royale Blue (Active)
                _btnNew.ForeColor = Color.White;
                _btnModify.BackColor = Color.FromArgb(180, 210, 255); // Soft Sky Blue (Inactive)
                _btnModify.ForeColor = Color.FromArgb(30, 30, 40);
                
                _lblCompanyId.Visible = true;
                if (_borderCompanyId != null) _borderCompanyId.Visible = true;
                
                _lblSelectCompany.Visible = false;
                if (_borderCmb != null) _borderCmb.Visible = false;
                
                _txtCompanyId.Text = "";
                _txtSyncUrls.Text = "";
                
                Log("Switched to Register New Company mode.");
            }
            else // Modify
            {
                _btnModify.BackColor = Color.FromArgb(42, 87, 204); // Royale Blue (Active)
                _btnModify.ForeColor = Color.White;
                _btnNew.BackColor = Color.FromArgb(180, 210, 255); // Soft Sky Blue (Inactive)
                _btnNew.ForeColor = Color.FromArgb(30, 30, 40);
                
                _lblCompanyId.Visible = false;
                if (_borderCompanyId != null) _borderCompanyId.Visible = false;
                
                _lblSelectCompany.Visible = true;
                if (_borderCmb != null) _borderCmb.Visible = true;
                
                LoadAvailableCompanies();
                
                Log("Switched to Modify Company Sync URLs mode.");
            }
        }

        private void LoadAvailableCompanies()
        {
            _cmbCompanies.Items.Clear();
            try
            {
                string assetsDir = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend", "assets"));
                if (Directory.Exists(assetsDir))
                {
                    string[] dirs = Directory.GetDirectories(assetsDir);
                    foreach (string dir in dirs)
                    {
                        string name = Path.GetFileName(dir);
                        if (name.Length == 5 && IsValidId(name))
                        {
                            _cmbCompanies.Items.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Error listing companies: " + ex.Message);
            }
            
            if (_cmbCompanies.Items.Count > 0)
            {
                _cmbCompanies.SelectedIndex = 0;
            }
            else
            {
                _txtSyncUrls.Text = "";
            }
        }

        private void CompanySelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cmbCompanies.SelectedItem == null)
            {
                _txtSyncUrls.Text = "";
                return;
            }
            
            string selectedCompany = _cmbCompanies.SelectedItem.ToString();
            string[] urls = LoadCompanySyncUrls(selectedCompany);
            _txtSyncUrls.Text = string.Join(Environment.NewLine, urls);
            Log("Loaded " + urls.Length + " sync URL(s) for company: " + selectedCompany);
        }

        private string[] LoadCompanySyncUrls(string companyId)
        {
            string configPath = Path.Combine(GetRootDirectory(), "backend", "assets", companyId, companyId + "_config.json");
            if (!File.Exists(configPath)) return new string[0];
            
            try
            {
                string content = File.ReadAllText(configPath);
                var matches = System.Text.RegularExpressions.Regex.Matches(content, @"""[^""]+""\s*:\s*""([^""]+)""");
                var list = new System.Collections.Generic.List<string>();
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        string url = match.Groups[1].Value;
                        url = url.Replace("\\\"", "\"").Replace("\\\\", "\\");
                        list.Add(url);
                    }
                }
                return list.ToArray();
            }
            catch (Exception ex)
            {
                Log("Error loading sync URLs: " + ex.Message);
                return new string[0];
            }
        }

        private void WriteCompanyConfig(string dir, string companyId, string syncUrlsText)
        {
            string[] urls = syncUrlsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("    \"sync_urls\": {");

            bool first = true;
            foreach (string url in urls)
            {
                string trimmedUrl = url.Trim();
                if (string.IsNullOrEmpty(trimmedUrl)) continue;

                string rawKey = Path.GetFileNameWithoutExtension(trimmedUrl);
                string key = SanitizeKey(rawKey);
                if (string.IsNullOrEmpty(key))
                {
                    Log("Warning: Skipping URL due to invalid/unsanitizable key: " + trimmedUrl);
                    continue;
                }

                string escapedUrl = trimmedUrl.Replace("\\", "\\\\").Replace("\"", "\\\"");

                if (!first) sb.AppendLine(",");
                sb.Append("        \"" + key + "\": \"" + escapedUrl + "\"");
                first = false;
            }
            if (!first) sb.AppendLine();
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string configPath = Path.Combine(dir, companyId + "_config.json");
            File.WriteAllText(configPath, sb.ToString());
        }

        private void SaveCompany()
        {
            if (_companyMode == "New")
            {
                string newId = _txtCompanyId.Text.Trim().ToUpperInvariant();
                
                if (string.IsNullOrEmpty(newId))
                {
                    Log("Error: Company ID must be provided.");
                    MessageBox.Show("Company ID must be provided.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (newId.Length != 5)
                {
                    Log("Error: Company ID must be exactly 5 characters long.");
                    MessageBox.Show("Company ID must be exactly 5 characters long.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!IsValidId(newId))
                {
                    Log("Error: Company ID must contain only alphanumeric characters, dashes, or underscores.");
                    MessageBox.Show("Company ID must contain only alphanumeric characters, dashes, or underscores.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string syncUrlsText = _txtSyncUrls.Text.Trim();
                if (string.IsNullOrEmpty(syncUrlsText))
                {
                    Log("Error: Sync URLs cannot be empty when registering a new company.");
                    MessageBox.Show("Sync URLs cannot be empty when registering a new company.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string rootDir = GetRootDirectory();
                string assetsDir = Path.GetFullPath(Path.Combine(rootDir, "backend", "assets"));
                string tmplDir = Path.GetFullPath(Path.Combine(assetsDir, "BMS"));
                string newDir = Path.GetFullPath(Path.Combine(assetsDir, newId));

                if (!Directory.Exists(tmplDir))
                {
                    Log("Error: Template directory (BMS) not found: " + tmplDir);
                    MessageBox.Show("Template directory (BMS) not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (Directory.Exists(newDir))
                {
                    Log("Error: Company directory already exists: " + newDir);
                    MessageBox.Show("Company directory already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    Log("Creating new company: " + newId + " from template: BMS");
                    Directory.CreateDirectory(newDir);
                    
                    foreach (string dirPath in Directory.GetDirectories(tmplDir, "*", SearchOption.AllDirectories))
                    {
                        Directory.CreateDirectory(dirPath.Replace(tmplDir, newDir));
                    }

                    string tmplSyncPath = Path.Combine(tmplDir, "BMS_sync.py");
                    if (File.Exists(tmplSyncPath))
                    {
                        string targetSyncPath = Path.Combine(newDir, newId + "_sync.py");
                        File.Copy(tmplSyncPath, targetSyncPath, true);

                        string content = File.ReadAllText(targetSyncPath);
                        if (content.Contains("BMS"))
                        {
                            content = content.Replace("BMS", newId);
                        }
                        string lowerTmpl = "bms";
                        string lowerNewId = newId.ToLowerInvariant();
                        if (content.Contains(lowerTmpl))
                        {
                            content = content.Replace(lowerTmpl, lowerNewId);
                        }
                        File.WriteAllText(targetSyncPath, content);
                    }
                    else
                    {
                        Log("Warning: Template sync script not found: " + tmplSyncPath);
                    }

                    WriteCompanyConfig(newDir, newId, syncUrlsText);

                    Log("Success: Company " + newId + " created and configuration saved!");
                    MessageBox.Show("Company " + newId + " created and registered successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    _txtCompanyId.Text = "";
                    _txtSyncUrls.Text = "";

                    TriggerDatabaseSync(newId);
                }
                catch (Exception ex)
                {
                    Log("Error creating company: " + ex.Message);
                    MessageBox.Show("Error creating company: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else // Modify
            {
                if (_cmbCompanies.SelectedItem == null)
                {
                    Log("Error: No company selected to modify.");
                    MessageBox.Show("Please select a company to modify.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string selectedCompany = _cmbCompanies.SelectedItem.ToString();
                string syncUrlsText = _txtSyncUrls.Text.Trim();

                string rootDir = GetRootDirectory();
                string assetsDir = Path.GetFullPath(Path.Combine(rootDir, "backend", "assets"));
                string targetDir = Path.GetFullPath(Path.Combine(assetsDir, selectedCompany));

                if (!Directory.Exists(targetDir))
                {
                    Log("Error: Target company directory not found: " + targetDir);
                    MessageBox.Show("Target company directory not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    Log("Modifying configuration for company: " + selectedCompany);
                    WriteCompanyConfig(targetDir, selectedCompany, syncUrlsText);
                    
                    Log("Success: Company " + selectedCompany + " configuration updated!");
                    MessageBox.Show("Company " + selectedCompany + " configuration updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    TriggerDatabaseSync(selectedCompany);
                }
                catch (Exception ex)
                {
                    Log("Error modifying company: " + ex.Message);
                    MessageBox.Show("Error modifying company: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void TriggerDatabaseSync(string companyId)
        {
            Task.Run(() =>
            {
                try
                {
                    Log("[Sync] Launching database synchronization for " + companyId + "...");
                    string backendDir = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend"));

                    using (Process syncProcess = new Process())
                    {
                        syncProcess.StartInfo = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = "run -- --sync " + companyId,
                            WorkingDirectory = backendDir,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        syncProcess.OutputDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                Log("[Sync] " + e.Data);
                            }
                        };

                        syncProcess.ErrorDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                Log("[Sync Error] " + e.Data);
                            }
                        };

                        syncProcess.Start();
                        syncProcess.BeginOutputReadLine();
                        syncProcess.BeginErrorReadLine();
                        syncProcess.WaitForExit();

                        if (syncProcess.ExitCode == 0)
                        {
                            Log("[Sync] Database sync completed successfully for " + companyId + "!");
                        }
                        else
                        {
                            Log("[Sync] Database sync failed with exit code: " + syncProcess.ExitCode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("[Sync] Critical error during sync execution: " + ex.Message);
                }
            });
        }

        private void CheckExternalServers()
        {
            bool backRunning = IsPortActive("127.0.0.1", 8001);
            bool frontRunning = IsPortActive("127.0.0.1", 5173);

            if (backRunning)
            {
                Log("[System] Detected C# Backend is already running externally on port 8001.");
            }
            if (frontRunning)
            {
                Log("[System] Detected Vue Frontend is already running externally on port 5173.");
            }
            UpdateControlsState();
        }

        private void StartAll()
        {
            StartBackend();
            StartFrontend();
        }

        private void StopAll()
        {
            StopBackend();
            StopFrontend();
        }

        private void RestartAll()
        {
            Log("Restarting all servers...");
            StopAll();
            Timer timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                StartAll();
            };
            timer.Start();
        }

        private void OpenWeb()
        {
            try
            {
                Process.Start(new ProcessStartInfo("http://127.0.0.1:8001/docs") { UseShellExecute = true });
                Process.Start(new ProcessStartInfo("https://127.0.0.1:5173") { UseShellExecute = true });
                Log("Opened API docs and Frontend application in web browser.");
            }
            catch (Exception ex)
            {
                Log("Failed to open web links: " + ex.Message);
            }
        }

        private void InitializeStatusPollTimer()
        {
            // Poll every 500ms to keep process health indicators in sync
            _statusPollTimer = new Timer();
            _statusPollTimer.Interval = 500;
            _statusPollTimer.Tick += (s, e) => { UpdateControlsState(); };
            _statusPollTimer.Start();
        }

        private void UpdateControlsState()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateControlsState));
                return;
            }

            // A single TCP probe per service — reused for both status indicator and button logic
            bool backendPortActive  = IsPortActive("127.0.0.1", 8001);
            bool frontendPortActive = IsPortActive("127.0.0.1", 5173);

            bool backendProcessActive  = (_backendProcess  != null && !_backendProcess.HasExited)  || backendPortActive;
            bool frontendProcessActive = (_frontendProcess != null && !_frontendProcess.HasExited) || frontendPortActive;

            if (_lblBackStatus != null)
            {
                if (_backendHasError)
                {
                    _lblBackStatus.Status = ServerStatus.Error;
                }
                else if (backendPortActive)
                {
                    _lblBackStatus.Status = ServerStatus.Running;
                }
                else if (_backendProcess != null && !_backendProcess.HasExited)
                {
                    _lblBackStatus.Status = ServerStatus.Loading;
                }
                else if (_backendProcess != null && _backendProcess.HasExited)
                {
                    _lblBackStatus.Status = ServerStatus.Error;
                }
                else
                {
                    _lblBackStatus.Status = ServerStatus.Stopped;
                }
            }

            if (_lblFrontStatus != null)
            {
                if (_frontendHasError)
                {
                    _lblFrontStatus.Status = ServerStatus.Error;
                }
                else if (frontendPortActive)
                {
                    _lblFrontStatus.Status = ServerStatus.Running;
                }
                else if (_frontendProcess != null && !_frontendProcess.HasExited)
                {
                    _lblFrontStatus.Status = ServerStatus.Loading;
                }
                else if (_frontendProcess != null && _frontendProcess.HasExited)
                {
                    _lblFrontStatus.Status = ServerStatus.Error;
                }
                else
                {
                    _lblFrontStatus.Status = ServerStatus.Stopped;
                }
            }

            bool eitherRunning = backendProcessActive || frontendProcessActive;
            bool bothRunning   = backendProcessActive && frontendProcessActive;

            if (_btnStartServer   != null) _btnStartServer.Enabled   = !bothRunning;
            if (_btnRestartServer != null) _btnRestartServer.Enabled = eitherRunning;
            if (_btnStopServer    != null) _btnStopServer.Enabled    = eitherRunning;
            if (_btnOpenWeb       != null) _btnOpenWeb.Enabled       = eitherRunning;
        }

        private bool IsPortActive(string host, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(200, false); // 200ms timeout
                    if (success)
                    {
                        client.EndConnect(result);
                        return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private void KillProcessOnPort(int port)
        {
            try
            {
                Log("Searching for external process on port " + port + "...");
                Process netstat = new Process();
                netstat.StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c netstat -ano | findstr :" + port,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                netstat.Start();
                string output = netstat.StandardOutput.ReadToEnd();
                netstat.WaitForExit();

                if (string.IsNullOrEmpty(output))
                {
                    Log("No active process found on port " + port + ".");
                    return;
                }

                string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.Contains("LISTENING"))
                    {
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            string pidStr = parts[parts.Length - 1].Trim();
                            int pid;
                            if (int.TryParse(pidStr, out pid) && pid > 0)
                            {
                                Log("Found process ID " + pid + " on port " + port + ". Terminating...");
                                using (Process killer = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "taskkill",
                                    Arguments = "/F /T /PID " + pid,
                                    CreateNoWindow = true,
                                    UseShellExecute = false
                                }))
                                {
                                    if (killer != null) killer.WaitForExit(3000);
                                }
                                Log("Successfully terminated process on port " + port + ".");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Error terminating process on port " + port + ": " + ex.Message);
            }
        }

        private string GetRootDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string current = baseDir;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, "backend")) && Directory.Exists(Path.Combine(current, "frontend")))
                {
                    return current;
                }
                string parent = Path.GetDirectoryName(current);
                if (parent == current || string.IsNullOrEmpty(parent)) break;
                current = parent;
            }
            return baseDir;
        }

        private bool IsValidId(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            foreach (char c in input)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    return false;
            }
            return true;
        }

        private string SanitizeKey(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(c);
                }
            }
            string result = sb.ToString();
            if (result.Length > 0 && !char.IsLetter(result[0]))
            {
                return "";
            }
            return result;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Log("Cleaning up and exiting active servers...");
            if (_statusPollTimer != null)
            {
                _statusPollTimer.Stop();
                _statusPollTimer.Dispose();
            }
            StopBackend();
            StopFrontend();
            base.OnFormClosing(e);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ManagerForm());
        }

        // --- MODERN UTILITIES ---
        public static System.Drawing.Drawing2D.GraphicsPath GetRoundedRectanglePath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(arc, 180, 90);

            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }

    // --- MODERN CUSTOM CONTROLS ---

    public class ModernNavButton : Button
    {
        private bool _isSelected;
        private bool _isHovered;

        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; Invalidate(); }
        }

        public ModernNavButton(string text, string tag)
        {
            Text = text;
            Tag = tag;
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = Color.FromArgb(200, 220, 255);
            Font = new Font("Tahoma", 9.5F, FontStyle.Bold);
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(15, 0, 0, 0);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            // Fully clear background with the sidebar's solid background color
            // to completely eliminate GDI+ transparency trails and overlapping text issues
            pevent.Graphics.Clear(Color.FromArgb(42, 87, 204));

            Color bg = Color.Transparent;
            if (_isSelected)
            {
                bg = Color.FromArgb(74, 130, 232);
            }
            else if (_isHovered)
            {
                bg = Color.FromArgb(58, 107, 224);
            }

            if (bg != Color.Transparent)
            {
                using (SolidBrush brush = new SolidBrush(bg))
                {
                    pevent.Graphics.FillRectangle(brush, ClientRectangle);
                }
            }

            if (_isSelected)
            {
                using (SolidBrush indicatorBrush = new SolidBrush(Color.FromArgb(255, 204, 0)))
                {
                    pevent.Graphics.FillRectangle(indicatorBrush, new Rectangle(0, 4, 4, Height - 8));
                }
            }

            Color textCol = _isSelected ? Color.White : (_isHovered ? Color.White : Color.FromArgb(200, 220, 255));
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            Rectangle textRect = new Rectangle(Padding.Left + 5, 0, Width - Padding.Left - 10, Height);
            TextRenderer.DrawText(pevent.Graphics, Text, Font, textRect, textCol, flags);
        }
    }

    public class ModernCard : Panel
    {
        private string _headerText;

        // The accent colour is a fixed brand blue — no external override needed
        private readonly Color _accentColor = Color.FromArgb(42, 87, 204);

        public string HeaderText
        {
            get { return _headerText; }
            set { _headerText = value; Invalidate(); }
        }

        public ModernCard()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(240, 246, 254);
            Padding = new Padding(15, 42, 15, 15);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw filled card background
            using (SolidBrush bgBrush = new SolidBrush(BackColor))
            {
                using (var path = ManagerForm.GetRoundedRectanglePath(ClientRectangle, 6))
                {
                    e.Graphics.FillPath(bgBrush, path);
                }
            }

            // Draw card border
            using (Pen pen = new Pen(Color.FromArgb(165, 199, 247), 1f))
            {
                using (var path = ManagerForm.GetRoundedRectanglePath(new Rectangle(0, 0, Width - 1, Height - 1), 6))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }

            if (!string.IsNullOrEmpty(_headerText))
            {
                // Left accent vertical bar
                using (SolidBrush accentBrush = new SolidBrush(_accentColor))
                {
                    e.Graphics.FillRectangle(accentBrush, new Rectangle(15, 15, 3, 15));
                }

                // Title
                using (Font headerFont = new Font("Tahoma", 8.5F, FontStyle.Bold))
                {
                    TextRenderer.DrawText(
                        e.Graphics,
                        _headerText,
                        headerFont,
                        new Rectangle(24, 11, Width - 40, 20),
                        Color.FromArgb(42, 87, 204),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    );
                }
            }
        }
    }

    public class ModernStatusIndicator : Control
    {
        private ServerStatus _status = ServerStatus.Stopped;
        private string _statusLabel;

        public ServerStatus Status
        {
            get { return _status; }
            set { _status = value; Invalidate(); }
        }



        public string StatusLabel
        {
            get { return _statusLabel; }
            set { _statusLabel = value; Invalidate(); }
        }

        public ModernStatusIndicator()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(200, 30);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            Color baseColor;
            switch (_status)
            {
                case ServerStatus.Running:
                    baseColor = Color.FromArgb(46, 204, 113); // Green (Running)
                    break;
                case ServerStatus.Loading:
                    baseColor = Color.FromArgb(241, 196, 15);  // Yellow/Amber (Loading)
                    break;
                case ServerStatus.Error:
                    baseColor = Color.FromArgb(192, 57, 43);   // Dark Red (Error)
                    break;
                case ServerStatus.Stopped:
                default:
                    baseColor = Color.FromArgb(231, 76, 60);   // Red (Stopped)
                    break;
            }

            Color glowColor = Color.FromArgb(35, baseColor.R, baseColor.G, baseColor.B);

            // Draw glowing outer ring
            int glowSize = 22;
            int glowX = 4;
            int glowY = (Height - glowSize) / 2;
            using (SolidBrush glowBrush = new SolidBrush(glowColor))
            {
                e.Graphics.FillEllipse(glowBrush, new Rectangle(glowX, glowY, glowSize, glowSize));
            }

            // Draw solid center circle
            int centerSize = 10;
            int centerX = glowX + (glowSize - centerSize) / 2;
            int centerY = glowY + (glowSize - centerSize) / 2;
            using (SolidBrush centerBrush = new SolidBrush(baseColor))
            {
                e.Graphics.FillEllipse(centerBrush, new Rectangle(centerX, centerY, centerSize, centerSize));
            }

            // Draw label text
            if (!string.IsNullOrEmpty(_statusLabel))
            {
                int textX = glowX + glowSize + 10;
                using (Font labelFont = new Font("Tahoma", 9.5F, FontStyle.Bold))
                {
                    TextRenderer.DrawText(
                        e.Graphics,
                        _statusLabel,
                        labelFont,
                        new Rectangle(textX, 0, Width - textX, Height),
                        Color.FromArgb(30, 30, 40),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    );
                }
            }
        }
    }

    public class ModernInputPanel : Panel
    {
        private bool _isFocused;
        private Control _innerControl;

        public ModernInputPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Padding = new Padding(8, 6, 8, 6);
        }

        public void BindControl(Control ctrl)
        {
            _innerControl = ctrl;
            ctrl.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            ctrl.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw rounded background
            using (SolidBrush bgBrush = new SolidBrush(BackColor))
            {
                using (var path = ManagerForm.GetRoundedRectanglePath(ClientRectangle, 5))
                {
                    e.Graphics.FillPath(bgBrush, path);
                }
            }

            // Draw border: vibrant blue if focused, soft sky blue otherwise
            Color borderColor = _isFocused ? Color.FromArgb(42, 87, 204) : Color.FromArgb(165, 199, 247);
            int borderWidth = _isFocused ? 2 : 1;
            using (Pen pen = new Pen(borderColor, borderWidth))
            {
                using (var path = ManagerForm.GetRoundedRectanglePath(new Rectangle(0, 0, Width - 1, Height - 1), 5))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }
}
