using System;
using System.IO;
using System.Text.RegularExpressions;
namespace MfgLauncher
{
    public static class GuideTests
    {
        public static void Run(Action<bool,string> assert)
        {
            string html = GuideMarkdown.Render("# Guide\n\nA **bold** note with `version.dll` and *emphasis*.\n\n| Game | Use |\n|---|---|\n| Fixture | `winmm.dll` |\n\n2. Second step\n   Continued text\n3. Third step\n\n- A bullet\n\n[Install](install.md#supported-filenames) [README](../README.md) [#3]\n\n[#3]: https://github.com/dashdogy/MFG-Unlocker/issues/3\n");
            assert(html.Contains("<h1 id=\"guide\">Guide</h1>"), "Live guide headings render with internal anchors");
            assert(html.Contains("<strong>bold</strong>") && html.Contains("<code>version.dll</code>") && html.Contains("<em>emphasis</em>"), "Guide inline Markdown formatting renders");
            assert(Regex.Matches(html,"<th>").Count == 2 && Regex.Matches(html,"<td>").Count == 2 && html.Contains("<table>"), "Guide Markdown tables render with header and data cells");
            assert(html.Contains("<ol start=\"2\">") && html.Contains("Second step Continued text") && html.Contains("<ul>"), "Guide numbered lists retain starting number and continuations");
            assert(html.Contains("https://github.com/dashdogy/MFG-Unlocker/blob/main/docs/install.md#supported-filenames") && html.Contains("https://github.com/dashdogy/MFG-Unlocker/blob/main/README.md"), "Guide relative links resolve to upstream documentation");
            assert(html.Contains("href=\"https://github.com/dashdogy/MFG-Unlocker/issues/3\""), "Guide shortcut reference links resolve");
            assert(!html.Contains("[#3]:"), "Markdown reference definitions are hidden from rendered guide");
            string untrusted = GuideMarkdown.Render("<script>alert(1)</script>\n\n[Run](javascript:alert(1)) ![Remote image](https://example.com/image.png)\n\n```html\n<img src=x onerror=alert(1)>\n```\n");
            assert(!untrusted.Contains("<script") && untrusted.Contains("&lt;script&gt;"), "Guide escapes raw HTML rather than executing it");
            assert(!untrusted.Contains("href=\"javascript:") && !untrusted.Contains("<img") && !untrusted.Contains("https://example.com/image.png"), "Guide rejects script links and remote image loading");
            assert(untrusted.Contains("<pre><code>") && untrusted.Contains("&lt;img"), "Guide fenced code remains inert text");
            assert(GuideMarkdown.Render("## Reporting a game\n\n[Report](#reporting-a-game)").Contains("href=\"about:blank#reporting-a-game\""), "Guide same-document links stay inside the rendered article");
            assert(!GuideMarkdown.IsSafeLink(new Uri("http://example.com")) && !GuideMarkdown.IsSafeLink(new Uri("file:///C:/Windows")) && !GuideMarkdown.IsSafeLink(new Uri("https://user:password@example.com")) && GuideMarkdown.IsSafeLink(new Uri("https://github.com/dashdogy/MFG-Unlocker")), "Guide only opens HTTPS links without credentials");
            bool refused = false; try { GuideMarkdown.Render(new string('x', 512 * 1024 + 1)); } catch (InvalidDataException) { refused = true; }
            assert(refused, "Oversized Markdown guide is refused"); assert(GuideMarkdown.ErrorDocument("<broken>").Contains("&lt;broken&gt;"), "Guide error text is escaped");
        }
    }
}
