using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace MfgLauncher
{
    public sealed class ThemedButton : Button
    {
        bool hovered, pressed;
        public ThemedButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        static System.Drawing.Drawing2D.GraphicsPath Shape(int width, int height, float diameter)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0, 0, diameter, diameter, 180, 90); path.AddArc(width - diameter, 0, diameter, diameter, 270, 90);
            path.AddArc(width - diameter, height - diameter, diameter, diameter, 0, 90); path.AddArc(0, height - diameter, diameter, diameter, 90, 90); path.CloseFigure(); return path;
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e); if (Width < 2 || Height < 2) return;
            using (var path = Shape(Width, Height, Math.Min(12, Math.Min(Width, Height)))) { var old = Region; Region = new Region(path); if (old != null) old.Dispose(); }
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Control parent = Parent; while (parent != null && parent.BackColor.A == 0) parent = parent.Parent;
            e.Graphics.Clear(parent == null ? Theme.Background : parent.BackColor);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Color fill = !Enabled ? Theme.Raised : pressed ? FlatAppearance.MouseDownBackColor : hovered ? FlatAppearance.MouseOverBackColor : BackColor;
            float diameter = Math.Min(12f * e.Graphics.DpiX / 96f, Math.Min(Width - 1, Height - 1));
            using (var path = Shape(Width, Height, diameter)) {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), Theme.Text, fill);
        }
    }
    public sealed class BorderlessComboBox : ComboBox
    {
        public BorderlessComboBox() { DrawMode = DrawMode.OwnerDrawFixed; }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Bounds.Width <= 0 || e.Bounds.Height <= 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0 && DroppedDown;
            using (var brush = new SolidBrush(selected ? Theme.Raised : BackColor)) e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Text;
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 4), e.Bounds.Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000f || Width < 26 || Height < 10) return;
            using (var graphics = Graphics.FromHwnd(Handle)) {
                using (var pen = new Pen(BackColor, 2)) graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                using (var brush = new SolidBrush(BackColor)) graphics.FillRectangle(brush, Width - 23, 0, 23, Height);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(Theme.Muted, 1.5f)) graphics.DrawLines(pen, new[] { new Point(Width - 16, Height / 2 - 2), new Point(Width - 12, Height / 2 + 2), new Point(Width - 8, Height / 2 - 2) });
            }
        }
    }
    public static class Theme
    {
        public static readonly Color Background = ColorTranslator.FromHtml("#313338"), Surface = ColorTranslator.FromHtml("#2b2d31"), Raised = ColorTranslator.FromHtml("#383a40"), Border = ColorTranslator.FromHtml("#4e5058"), Text = ColorTranslator.FromHtml("#f2f3f5"), Muted = ColorTranslator.FromHtml("#b5bac1"), Accent = ColorTranslator.FromHtml("#5865f2"), Good = ColorTranslator.FromHtml("#7dd8a8"), Warning = ColorTranslator.FromHtml("#f0b96c"), Bad = ColorTranslator.FromHtml("#f38ba8");
        public static Font Font(float size, bool bold = false) { return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular); }
        public static Label Label(string text, float size, Color color, bool bold = false) { return new Label { Text = text, ForeColor = color, Font = Font(size, bold), AutoSize = false, BackColor = Color.Transparent }; }
        public static Button Button(string text, bool primary = false)
        {
            var b = new ThemedButton { Text = text, Height = 38, Width = 108, FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Raised, ForeColor = Text, Font = Font(10, true), Cursor = Cursors.Hand, UseVisualStyleBackColor = false, AccessibleName = text, Margin = new Padding(0, 0, 8, 8) };
            b.FlatAppearance.BorderColor = primary ? Accent : Border; b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(71, 82, 196) : Color.FromArgb(78, 80, 88); b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(60, 69, 165) : Color.FromArgb(53, 55, 61); return b;
        }
        public static void Form(Form form) { form.BackColor = Background; form.ForeColor = Text; form.Font = Font(10); form.StartPosition = FormStartPosition.CenterParent; form.AutoScaleMode = AutoScaleMode.Dpi; form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        public static TextBox TextBox() { return new TextBox { BackColor = ColorTranslator.FromHtml("#1e1f22"), ForeColor = Text, BorderStyle = BorderStyle.None, Font = Font(11) }; }
        public static ComboBox Combo() { return new BorderlessComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = ColorTranslator.FromHtml("#1e1f22"), ForeColor = Text, FlatStyle = FlatStyle.Flat, Font = Font(10) }; }
        public static Color StatusColor(GameStatus status) { return status == GameStatus.Installed || status == GameStatus.Reported ? Good : status == GameStatus.Blocked ? Bad : status == GameStatus.Unsupported ? Muted : Warning; }
        public static string StatusText(GameStatus s) { return s == GameStatus.Reported ? "Reported compatible" : s == GameStatus.Candidate ? "Candidate" : s == GameStatus.Review ? "Needs review" : s == GameStatus.Blocked ? "Blocked" : s == GameStatus.Installed ? "Installed" : "No FG evidence"; }
    }
    public sealed partial class MainForm : Form
    {
        Settings settings; ScanResult library = new ScanResult(); PackageInfo package;
        readonly Panel sidebar = new Panel(), content = new Panel();
        readonly FlowLayoutPanel cards = new FlowLayoutPanel(); readonly TextBox search = Theme.TextBox(); readonly ComboBox filter = Theme.Combo();
        readonly Label totals = Theme.Label("Discovering your games", 10, Theme.Muted), packageLabel = Theme.Label("No release loaded", 10, Theme.Muted), scanLabel = Theme.Label("Ready", 10, Theme.Muted);
        readonly Button scanButton = Theme.Button("Rescan"), addButton = Theme.Button("Add folder", true), downloadButton = Theme.Button("Check for updates"), importButton = Theme.Button("Import ZIP"), rootsButton = Theme.Button("Manage folders"), errorsButton = Theme.Button("Scan report");
        CancellationTokenSource cancellation; bool busy, closePending;
        public MainForm() : this(null) { }
        public MainForm(ScanResult fixture)
        {
            Theme.Form(this); Text = "MFG Launcher"; MinimumSize = new Size(940, 650); ClientSize = new Size(1250, 820); StartPosition = FormStartPosition.CenterScreen;
            try { settings = StateStore.Read<Settings>(StateStore.SettingsPath); if (settings.Roots == null || settings.ExeChoices == null) throw new InvalidDataException("Invalid settings structure."); }
            catch (Exception ex) { settings = new Settings(); MessageBox.Show("Could not read settings. Your games have not been changed.\n\n" + ex.Message, "MFG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            try { package = StateStore.Read<PackageInfo>(StateStore.PackagePath); if (String.IsNullOrEmpty(package.DllPath) || !File.Exists(package.DllPath) || Util.HashFile(package.DllPath) != package.Sha256 || !Util.IsX64Pe(package.DllPath, true)) package = null; } catch { package = null; }
            BuildLayout(); UpdatePackage(); if (fixture == null) StartUpdateTimer(); if (fixture == null) Shown += async delegate { await Scan(); if (!closePending) await CheckUpdates(false); }; else { library = fixture; RenderGames(); SetStatus("Fixture library for desktop verification. No real game files changed."); } FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) { e.Cancel = true; closePending = true; if (cancellation != null) cancellation.Cancel(); SetStatus("Finishing the current operation before closing..."); } };
        }
        void BuildLayout()
        {
            sidebar.Dock = DockStyle.Left; sidebar.AutoScroll = true; sidebar.AutoScrollMinSize = new Size(0, 820); sidebar.Width = 234; sidebar.BackColor = Theme.Surface; sidebar.Padding = new Padding(22, 25, 22, 22); Controls.Add(sidebar);
            var brand = Theme.Label("MFG", 30, Theme.Accent, true); brand.SetBounds(22, 23, 190, 54); sidebar.Controls.Add(brand);
            var appName = Theme.Label("Game launcher", 12, Theme.Text, true); appName.SetBounds(24, 80, 180, 30); sidebar.Controls.Add(appName);
            var description = Theme.Label("Your library.\nFrame generation, in reach.", 10, Theme.Muted); description.SetBounds(24, 121, 187, 60); sidebar.Controls.Add(description);
            var libraryButton = Theme.Button("Game library"); libraryButton.SetBounds(22, 205, 190, 42); libraryButton.Click += delegate { search.Clear(); filter.SelectedIndex = 0; }; sidebar.Controls.Add(libraryButton);
            rootsButton.SetBounds(22, 260, 190, 38); rootsButton.Click += delegate { ManageRoots(); }; sidebar.Controls.Add(rootsButton);
            errorsButton.SetBounds(22, 307, 190, 38); errorsButton.Click += delegate { ShowReport(); }; sidebar.Controls.Add(errorsButton);
            var releaseTitle = Theme.Label("MFG Unlocker release", 11, Theme.Text, true); releaseTitle.SetBounds(24, 390, 190, 30); sidebar.Controls.Add(releaseTitle);
            packageLabel.SetBounds(24, 427, 186, 80); sidebar.Controls.Add(packageLabel);
            downloadButton.SetBounds(22, 520, 190, 38); downloadButton.Click += async delegate { await CheckUpdates(true); }; sidebar.Controls.Add(downloadButton);
            importButton.SetBounds(22, 567, 190, 38); importButton.Click += async delegate { await LoadPackage(true); }; sidebar.Controls.Add(importButton);
            var updateSettings = Theme.Button("Update settings"); updateSettings.SetBounds(22, 614, 190, 38); updateSettings.Click += delegate { if (!busy) ConfigureUpdates(); }; sidebar.Controls.Add(updateSettings);
            var docs = new LinkLabel { Text = "Compatibility guide", LinkColor = Theme.Muted, ActiveLinkColor = Theme.Accent, VisitedLinkColor = Theme.Muted, Font = Theme.Font(10), AutoSize = true, Left = 24, Top = 676, Anchor = AnchorStyles.Left | AnchorStyles.Top }; docs.LinkClicked += delegate { TryAction(LaunchService.OpenDocs); }; sidebar.Controls.Add(docs);
            var footer = Theme.Label("Independent launcher\nWindows x64  /  v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + "", 9, Theme.Muted); footer.SetBounds(24, 742, 190, 50); footer.Anchor = AnchorStyles.Left | AnchorStyles.Top; sidebar.Controls.Add(footer);
            content.Dock = DockStyle.Fill; content.Padding = new Padding(28, 24, 24, 16); Controls.Add(content); content.BringToFront();
            var heading = new Panel { Dock = DockStyle.Top, Height = 87 };
            var title = Theme.Label("Game library", 26, Theme.Text, true); title.SetBounds(0, 0, 360, 48); heading.Controls.Add(title);
            totals.SetBounds(2, 52, 500, 26); heading.Controls.Add(totals);
            addButton.Anchor = AnchorStyles.Right | AnchorStyles.Top; scanButton.Anchor = AnchorStyles.Right | AnchorStyles.Top; heading.Controls.Add(scanButton); heading.Controls.Add(addButton);
            heading.Resize += delegate { addButton.SetBounds(heading.Width - 126, 7, 126, 40); scanButton.SetBounds(heading.Width - 242, 7, 104, 40); };
            scanButton.Click += async delegate { if (busy && cancellation != null) cancellation.Cancel(); else await Scan(); }; addButton.Click += async delegate { await AddRoot(); };
            var searchRow = new Panel { Dock = DockStyle.Top, Height = 69 };
            var searchName = Theme.Label("Search games", 9, Theme.Muted); searchName.SetBounds(0, 0, 280, 22); searchRow.Controls.Add(searchName);
            search.SetBounds(0, 25, 400, 30); search.AccessibleName = "Search games"; search.TextChanged += delegate { RenderGames(); }; searchRow.Controls.Add(search);
            var filterName = Theme.Label("Status", 9, Theme.Muted); filterName.SetBounds(420, 0, 210, 22); filterName.Anchor = AnchorStyles.Top | AnchorStyles.Right; searchRow.Controls.Add(filterName);
            filter.Items.AddRange(new object[] { "All games", "Reported compatible", "Candidates", "Installed", "Needs review", "Blocked", "No FG evidence" }); filter.SelectedIndex = 0; filter.AccessibleName = "Filter by compatibility status"; filter.SelectedIndexChanged += delegate { RenderGames(); }; searchRow.Controls.Add(filter);
            searchRow.Resize += delegate { int w = searchRow.Width; search.Width = Math.Max(180, w - 244); filterName.Left = w - 224; filter.SetBounds(w - 224, 25, 224, 30); };
            var statusRow = new Panel { Dock = DockStyle.Bottom, Height = 45 }; scanLabel.Dock = DockStyle.Fill; scanLabel.TextAlign = ContentAlignment.MiddleLeft; scanLabel.AutoEllipsis = true; statusRow.Controls.Add(scanLabel);
            cards.Dock = DockStyle.Fill; cards.AutoScroll = true; cards.WrapContents = true; cards.Padding = new Padding(0, 9, 0, 0); cards.BackColor = Theme.Background; cards.Resize += delegate { ResizeCards(); };
            content.Controls.Add(cards); content.Controls.Add(statusRow); content.Controls.Add(searchRow); content.Controls.Add(heading);
        }
        void SetBusy(bool value, bool scan = false)
        {
            busy = value; addButton.Enabled = rootsButton.Enabled = downloadButton.Enabled = importButton.Enabled = !value;
            scanButton.Enabled = !value || scan; scanButton.Text = value && scan ? "Cancel scan" : "Rescan"; cards.Enabled = !value; if (!value && closePending) BeginInvoke((Action)(() => Close()));
        }
        void SetStatus(string text) { if (!IsDisposed && IsHandleCreated) { if (InvokeRequired) BeginInvoke((Action)(() => scanLabel.Text = text)); else scanLabel.Text = text; } }
        async Task Scan()
        {
            if (busy) return; SetBusy(true, true); cancellation = new CancellationTokenSource(); SetStatus("Finding Steam libraries and scanning game folders...");
            try {
                var roots = settings.Roots.ToList(); var token = cancellation.Token;
                var result = await Task.Run(() => Discovery.Discover(roots, SetStatus, token));
                await Task.Run(() => { foreach (var game in result.Games) { token.ThrowIfCancellationRequested(); string choice; if (settings.ExeChoices.TryGetValue(game.Root, out choice) && game.Executables.Contains(choice, StringComparer.OrdinalIgnoreCase)) game.SelectedExe = choice; GameEvaluation.Refresh(game); } });
                library = result; RenderGames(); SetStatus("Scan complete. " + library.Libraries.Count + " Steam libraries, " + library.Errors.Count + " scan notices. " + DateTime.Now.ToString("HH:mm"));
                errorsButton.Text = library.Errors.Count > 0 ? "Scan report (" + library.Errors.Count + ")" : "Scan report";
            } catch (OperationCanceledException) { SetStatus("Scan canceled. Previous library kept."); }
            catch (Exception ex) { SetStatus("Scan failed: " + ex.Message); ShowError(ex); }
            finally { cancellation.Dispose(); cancellation = null; SetBusy(false); }
        }
        async Task AddRoot()
        {
            using (var chooser = new FolderBrowserDialog { Description = "Choose a game folder or a parent folder containing several games.", ShowNewFolderButton = false }) {
                if (chooser.ShowDialog(this) != DialogResult.OK) return;
                string path = Util.Canonical(chooser.SelectedPath); bool parent;
                using (var mode = new RootModeDialog(path)) { if (mode.ShowDialog(this) != DialogResult.OK) return; parent = mode.IsParent; }
                if (settings.Roots.Any(r => String.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show(this, "This folder is already in your library.", "MFG Launcher"); return; }
                settings.Roots.Add(new ScanRoot { Path = path, IsParent = parent });
                try { StateStore.Save(StateStore.SettingsPath, settings); } catch (Exception ex) { settings.Roots.RemoveAt(settings.Roots.Count - 1); ShowError(ex); return; }
                await Scan();
            }
        }
        void ManageRoots()
        {
            using (var dialog = new RootsDialog(settings.Roots)) {
                if (dialog.ShowDialog(this) == DialogResult.OK) { var old = settings.Roots; settings.Roots = dialog.Roots; try { StateStore.Save(StateStore.SettingsPath, settings); SetStatus("Folders saved. Rescan to refresh your library."); } catch (Exception ex) { settings.Roots = old; ShowError(ex); } }
            }
        }
        bool Matches(Game game)
        {
            if ((game.Name ?? "").IndexOf(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) < 0) return false;
            var statuses = new[] { GameStatus.Reported, GameStatus.Reported, GameStatus.Candidate, GameStatus.Installed, GameStatus.Review, GameStatus.Blocked, GameStatus.Unsupported };
            return filter.SelectedIndex <= 0 || game.Status == statuses[filter.SelectedIndex];
        }
        void RenderGames()
        {
            if (IsDisposed) return; cards.SuspendLayout();
            var old = cards.Controls.Cast<Control>().ToList(); cards.Controls.Clear(); foreach (var c in old) c.Dispose();
            var shown = library.Games.Where(Matches).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            totals.Text = library.Games.Count + " games  /  " + library.Games.Count(g => g.Status == GameStatus.Reported) + " reported compatible  /  " + library.Games.Count(g => g.Installed != null) + " installed";
            if (shown.Count == 0) {
                var empty = new Panel { Height = 230, Width = Math.Max(400, cards.ClientSize.Width - 24), Margin = new Padding(0, 18, 0, 0) };
                var title = Theme.Label(library.Games.Count == 0 ? "Make room for your games" : "No matching games", 22, Theme.Text, true); title.SetBounds(18, 25, 600, 45); empty.Controls.Add(title);
                var copy = Theme.Label(library.Games.Count == 0 ? "Steam libraries are discovered automatically.\nAdd a game folder or a folder containing several games to get started.\nOpen Scan report if a library could not be read." : "Try a different search or status filter.", 11, Theme.Muted); copy.SetBounds(18, 85, 640, 105); empty.Controls.Add(copy); cards.Controls.Add(empty);
            } else foreach (var game in shown) cards.Controls.Add(new GameCard(game, () => TryAction(() => LaunchService.Launch(game)), () => ShowDetails(game), async () => await ChangeInstall(game)));
            ResizeCards(); cards.ResumeLayout();
        }
        void ResizeCards()
        {
            int available = Math.Max(1, cards.ClientSize.Width - 24); int count = available >= 1100 ? 3 : available >= 740 ? 2 : 1; int width = (available - 14 * count) / count;
            foreach (Control card in cards.Controls) card.Width = Math.Max(320, width);
        }
        void ShowDetails(Game game)
        {
            using (var dialog = new DetailsDialog(game, package)) {
                dialog.ShowDialog(this);
                if (!String.IsNullOrEmpty(game.SelectedExe)) settings.ExeChoices[game.Root] = game.SelectedExe; else settings.ExeChoices.Remove(game.Root); TryAction(() => StateStore.Save(StateStore.SettingsPath, settings));
            }
            GameEvaluation.Refresh(game); RenderGames();
        }
        async Task LoadPackage(bool local)
        {
            if (busy) return; string zip = null, version = null;
            if (local) using (var file = new OpenFileDialog { Title = "Import the official MFG Unlocker release ZIP", Filter = "ZIP archives (*.zip)|*.zip", CheckFileExists = true }) {
                if (file.ShowDialog(this) != DialogResult.OK) return; zip = file.FileName;
                var match = Regex.Match(Path.GetFileName(zip), @"v?([0-9]+\.[0-9]+\.[0-9]+)", RegexOptions.IgnoreCase); if (!match.Success) { MessageBox.Show(this, "Keep the official release filename, for example MFGUnlocker-v1.7.0.zip, so its version can be identified.", "Release version required"); return; } version = "v" + match.Groups[1].Value;
            }
            SetBusy(true); SetStatus(local ? "Validating archive and x64 DLL..." : "Checking the official GitHub release...");
            try {
                package = await Task.Run(() => local ? PackageManager.Import(zip, version, "Local ZIP: " + Path.GetFileName(zip), null) : PackageManager.DownloadLatest(SetStatus));
                StateStore.Save(StateStore.PackagePath, package); UpdatePackage(); SetStatus("Release ready. Select Install on an eligible game.");
            } catch (Exception ex) { SetStatus("Release could not be loaded. You can import the official ZIP instead."); ShowError(ex); }
            finally { SetBusy(false); }
        }
        void UpdatePackage()
        {
            packageLabel.Text = package == null ? "Load the official release to\ninstall on eligible games." : package.Version + " ready\n" + (package.DigestVerified ? "Published digest verified" : "No published digest verified") + "\n" + (package.Source.StartsWith("Local") ? "Imported locally" : "Official GitHub release");
        }
        async Task ChangeInstall(Game game)
        {
            if (busy) return;
            if (game.Installed != null) {
                if (MessageBox.Show(this, "Remove the owned " + game.Installed.ProxyName + " from " + game.Name + "?\n\nOnly the unchanged mod DLL and its ownership receipt will be removed. Settings are kept.", "Uninstall MFG Unlocker", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                SetBusy(true); try { await Task.Run(() => InstallService.Uninstall(game)); GameEvaluation.Refresh(game); SetStatus("MFG Unlocker removed from " + game.Name + "."); } catch (Exception ex) { ShowError(ex); GameEvaluation.Refresh(game); } finally { SetBusy(false); RenderGames(); } return;
            }
            if (package == null) { MessageBox.Show(this, "Use Get latest release or Import ZIP first.", "Release required"); return; }
            GameEvaluation.Refresh(game);
            if (game.Status != GameStatus.Reported && game.Status != GameStatus.Candidate) { ShowDetails(game); return; }
            using (var dialog = new InstallDialog(game, package)) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return; string proxy = dialog.Proxy;
                SetBusy(true); SetStatus("Installing into " + game.Name + "...");
                try { await Task.Run(() => InstallService.Install(game, package, proxy)); GameEvaluation.Refresh(game); SetStatus("Installed in " + game.Name + ". Enable Frame Generation in-game; Backspace opens the menu."); }
                catch (Exception ex) { ShowError(ex); GameEvaluation.Refresh(game); }
                finally { SetBusy(false); RenderGames(); }
            }
        }
        void ShowReport()
        {
            var lines = new List<string> { "Steam libraries", "" }; lines.AddRange(library.Libraries); lines.Add(""); lines.Add("Added folders"); lines.AddRange(settings.Roots.Select(r => r.Path + (r.IsParent ? " (parent folder)" : " (single game)"))); lines.Add(""); lines.Add("Scan notices"); lines.AddRange(library.Errors.Count == 0 ? (IEnumerable<string>)new[] { "No scan errors reported." } : library.Errors); lines.Add(""); lines.Add("Search is bounded to Steam libraries and selected folders. Entire drives are not recursively scanned.");
            using (var dialog = new TextDialog("Scan report", String.Join(Environment.NewLine, lines))) dialog.ShowDialog(this);
        }
        void TryAction(Action action) { try { action(); } catch (Exception ex) { ShowError(ex); } }
        void ShowError(Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, "MFG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    public sealed class GameCard : Panel
    {
        public GameCard(Game game, Action launch, Action details, Action install)
        {
            DoubleBuffered = true; Height = 234; Width = 375; BackColor = Theme.Surface; Margin = new Padding(0, 0, 14, 16); Padding = new Padding(17); Cursor = Cursors.Hand; AccessibleName = "Launch " + game.Name;
            var source = Theme.Label(game.Source ?? "Folder", 9, Theme.Muted); source.SetBounds(18, 13, Width - 36, 23); source.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            var title = Theme.Label(game.Name, 16, Theme.Text, true); title.SetBounds(17, 40, Width - 34, 33); title.AutoEllipsis = true; title.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            var status = Theme.Label(Theme.StatusText(game.Status) + (game.Installed == null ? "" : "  " + game.Installed.Version), 10, Theme.StatusColor(game.Status), true); status.SetBounds(18, 80, Width - 36, 24); status.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            var evidence = Theme.Label(game.Evidence.FirstOrDefault() ?? "Open Details to inspect this game.", 10, Theme.Muted); evidence.SetBounds(18, 111, Width - 36, 52); evidence.AutoEllipsis = true; evidence.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            var buttons = new FlowLayoutPanel { Left = 18, Top = 177, Width = Width - 28, Height = 44, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, WrapContents = false, BackColor = Color.Transparent };
            var launchButton = Theme.Button("Launch"); launchButton.Width = 87; launchButton.Enabled = !String.IsNullOrEmpty(game.SteamId) || !String.IsNullOrEmpty(game.SelectedExe); launchButton.Click += delegate { launch(); };
            var detailButton = Theme.Button("Details"); detailButton.Width = 82; detailButton.Click += delegate { details(); };
            var installButton = Theme.Button(game.Installed != null ? "Uninstall" : "Install", game.Installed == null); installButton.Width = 94; installButton.Enabled = game.Installed != null || game.Status == GameStatus.Reported || game.Status == GameStatus.Candidate; installButton.Click += delegate { install(); };
            buttons.Controls.Add(launchButton); buttons.Controls.Add(detailButton); buttons.Controls.Add(installButton); Controls.Add(source); Controls.Add(title); Controls.Add(status); Controls.Add(evidence); Controls.Add(buttons);
            Click += delegate { launch(); }; foreach (var label in new[] { source, title, status, evidence }) label.Click += delegate { launch(); };
        }
    }
}











