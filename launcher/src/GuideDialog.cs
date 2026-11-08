using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace MfgLauncher
{
    public sealed class GuideDialog : Form
    {
        readonly WebBrowser browser = new WebBrowser { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true, IsWebBrowserContextMenuEnabled = false, AllowWebBrowserDrop = false, WebBrowserShortcutsEnabled = false, AccessibleName = "Live compatibility guide" };
        readonly Button reload = Theme.Button("Reload"), close = Theme.Button("Close");
        readonly Label status = Theme.Label("Loading the live guide...", 9, Theme.Muted);
        CancellationTokenSource loading;
        public GuideDialog()
        {
            Theme.Form(this); Text = "Compatibility guide"; Size = new Size(1080, 800); MinimumSize = new Size(760, 570); Padding = new Padding(20, 16, 20, 12);
            var header = new Panel { Dock = DockStyle.Top, Height = 98 };
            var title = Theme.Label("Compatibility guide", 22, Theme.Text, true); title.SetBounds(0, 0, 650, 42); header.Controls.Add(title);
            var source = Theme.Label("Live source: " + GuideMarkdown.SourceUrl, 9, Theme.Muted); source.SetBounds(1, 48, 850, 46); source.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; header.Controls.Add(source);
            reload.SetBounds(918, 0, 100, 38); reload.Anchor = AnchorStyles.Top | AnchorStyles.Right; reload.Click += async delegate { await Reload(); }; header.Controls.Add(reload);
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 48 };
            close.Dock = DockStyle.Right; close.DialogResult = DialogResult.OK; CancelButton = close;
            status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft; status.AutoEllipsis = true; footer.Controls.Add(status); footer.Controls.Add(close);
            browser.NewWindow += delegate(object sender, System.ComponentModel.CancelEventArgs e) { e.Cancel = true; };
            browser.Navigating += delegate(object sender, WebBrowserNavigatingEventArgs e) {
                if (e.Url != null && e.Url.Scheme == "about" && e.Url.AbsolutePath == "blank") return;
                e.Cancel = true; if (!GuideMarkdown.IsSafeLink(e.Url)) return;
                try { Process.Start(new ProcessStartInfo(e.Url.AbsoluteUri) { UseShellExecute = true }); } catch (Exception ex) { status.Text = "Could not open link: " + ex.Message; }
            };
            Controls.Add(browser); Controls.Add(footer); Controls.Add(header); Shown += async delegate { await Reload(); }; FormClosing += delegate { if (loading != null) loading.Cancel(); };
        }
        async Task Reload()
        {
            if (loading != null || IsDisposed) return; reload.Enabled = false; status.Text = "Loading the live Markdown guide...";
            loading = new CancellationTokenSource(); var cancellation = loading;
            try {
                string html = await Task.Run(() => GuideMarkdown.Render(GuideMarkdown.Fetch(cancellation.Token)), cancellation.Token);
                if (!IsDisposed && !cancellation.IsCancellationRequested) { browser.DocumentText = html; status.Text = "Loaded live " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ". Reload to fetch the latest guide."; }
            } catch (OperationCanceledException) { if (!IsDisposed && !cancellation.IsCancellationRequested) { status.Text = "The live guide request timed out. Select Reload to try again."; browser.DocumentText = GuideMarkdown.ErrorDocument(status.Text); } }
            catch (Exception ex) { if (!IsDisposed) { status.Text = "Could not load the live guide. Use Reload to try again."; browser.DocumentText = GuideMarkdown.ErrorDocument(ex.Message); } }
            finally { loading = null; cancellation.Dispose(); if (!IsDisposed) reload.Enabled = true; }
        }
    }
    public static partial class GuideMarkdown
    {
        public const string SourceUrl = "https://raw.githubusercontent.com/dashdogy/MFG-Unlocker/refs/heads/main/docs/compatibility.md";
        const int MaxBytes = 512 * 1024;
        static readonly Uri LinkBase = new Uri("https://github.com/dashdogy/MFG-Unlocker/blob/main/docs/compatibility.md");
        static Regex Pattern(string value) { return new Regex(value, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)); }
        static readonly Regex Reference = Pattern(@"^\s*\[([^\]]+)\]:\s*(\S+)(?:\s+.*)?$");
        static readonly Regex Inline = Pattern(@"`(?<code>[^`\r\n]+)`|(?<image>!)?\[(?<label>[^\]\r\n]+)\]\((?<target>[^)\s]+)\)|\[(?<refLabel>[^\]\r\n]+)\]\[(?<refKey>[^\]\r\n]*)\]|\[(?<shortcut>[^\]\r\n]+)\]|\*\*(?<bold>[^*\r\n]+)\*\*|(?<!\*)\*(?<italic>[^*\r\n]+)\*(?!\*)|<(?<auto>https://[^<>\s]+)>");
        public static bool IsSafeLink(Uri uri) { return uri != null && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && !String.IsNullOrEmpty(uri.Host); }
        public static string Fetch(CancellationToken token)
        {
            for (int attempt = 0; ; attempt++) { try { return FetchOnce(token); } catch (HttpRequestException) { if (attempt >= 2 || token.IsCancellationRequested) throw; if (token.WaitHandle.WaitOne(500 * (attempt + 1))) token.ThrowIfCancellationRequested(); } }
        }
        static string FetchOnce(CancellationToken token)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) using (var client = new HttpClient(handler)) using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                timeout.CancelAfter(TimeSpan.FromSeconds(25)); client.Timeout = TimeSpan.FromSeconds(25); client.DefaultRequestHeaders.UserAgent.ParseAdd("MFG-Launcher/1.0");
                using (var response = client.GetAsync(SourceUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token).GetAwaiter().GetResult()) {
                    response.EnsureSuccessStatusCode(); if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > MaxBytes) throw new InvalidDataException("The guide exceeds the 512 KB limit.");
                    using (var source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()) using (var memory = new MemoryStream()) {
                        byte[] buffer = new byte[8192]; int read;
                        while ((read = source.ReadAsync(buffer, 0, buffer.Length, timeout.Token).GetAwaiter().GetResult()) != 0) { if (memory.Length + read > MaxBytes) throw new InvalidDataException("The guide exceeds the 512 KB limit."); memory.Write(buffer, 0, read); } return Encoding.UTF8.GetString(memory.ToArray());
                    }
                }
            }
        }
    }
}
