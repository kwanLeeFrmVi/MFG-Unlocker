using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
namespace MfgLauncher
{
    public static partial class GuideMarkdown
    {
        static string Encode(string text) { return WebUtility.HtmlEncode(text ?? ""); }
        static string Link(string label, string target, Dictionary<string,string> references, int depth)
        {
            string content = Format(label, references, depth + 1); Uri uri;
            if (target.StartsWith("#", StringComparison.Ordinal) && Pattern(@"\A#[A-Za-z0-9_%-]+\z").IsMatch(target)) return "<a href=\"about:blank" + Encode(target) + "\">" + content + "</a>";
            if (!Uri.TryCreate(LinkBase, target, out uri) || !IsSafeLink(uri)) return content;
            return "<a href=\"" + Encode(uri.AbsoluteUri) + "\">" + content + "</a>";
        }
        static string Format(string text, Dictionary<string,string> references, int depth)
        {
            if (depth > 8) return Encode(text); var html = new StringBuilder(); int position = 0;
            foreach (Match match in Inline.Matches(text)) {
                html.Append(Encode(text.Substring(position, match.Index - position)));
                if (match.Groups["code"].Success) html.Append("<code>" + Encode(match.Groups["code"].Value) + "</code>");
                else if (match.Groups["label"].Success) html.Append(match.Groups["image"].Success ? Encode(match.Groups["label"].Value) : Link(match.Groups["label"].Value, match.Groups["target"].Value, references, depth));
                else if (match.Groups["refLabel"].Success) { string label = match.Groups["refLabel"].Value, key = match.Groups["refKey"].Value, target; if (key.Length == 0) key = label; html.Append(references.TryGetValue(key, out target) ? Link(label, target, references, depth) : Encode(match.Value)); }
                else if (match.Groups["shortcut"].Success) { string target, label = match.Groups["shortcut"].Value; html.Append(references.TryGetValue(label, out target) ? Link(label, target, references, depth) : Encode(match.Value)); }
                else if (match.Groups["bold"].Success) html.Append("<strong>" + Format(match.Groups["bold"].Value, references, depth + 1) + "</strong>");
                else if (match.Groups["italic"].Success) html.Append("<em>" + Format(match.Groups["italic"].Value, references, depth + 1) + "</em>");
                else html.Append(Link(match.Groups["auto"].Value, match.Groups["auto"].Value, references, depth)); position = match.Index + match.Length;
            }
            html.Append(Encode(text.Substring(position))); return html.ToString();
        }
        static string[] Cells(string line) { return line.Trim().Trim('|').Split('|').Select(s => s.Trim()).ToArray(); }
        static bool TableSeparator(string line) { var cells = Cells(line); return cells.Length > 1 && cells.All(c => Pattern(@"\A:?-{3,}:?\z").IsMatch(c)); }
        static string Anchor(string text) { string plain = Regex.Replace(text, @"[`*_]", ""); return Regex.Replace(new string(plain.ToLowerInvariant().Where(c => Char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray()).Replace(' ', '-'), "-+", "-"); }
        public static string Render(string markdown)
        {
            if (markdown == null || Encoding.UTF8.GetByteCount(markdown) > MaxBytes) throw new InvalidDataException("The guide is missing or exceeds 512 KB.");
            var references = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); var lines = markdown.Replace("\r", "").Split('\n'); foreach (string line in lines) { var reference = Reference.Match(line); if (reference.Success) references[reference.Groups[1].Value] = reference.Groups[2].Value; }
            var html = new StringBuilder(); string list = null;
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i].Trim(); if (Reference.IsMatch(line)) continue;
                if (line.Length == 0) { if (list != null) { html.Append("</" + list + ">"); list = null; } continue; }
                if (line.StartsWith("```", StringComparison.Ordinal)) { if (list != null) { html.Append("</" + list + ">"); list = null; } var code = new StringBuilder(); while (++i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal)) code.AppendLine(lines[i]); html.Append("<pre><code>" + Encode(code.ToString()) + "</code></pre>"); continue; }
                if (i + 1 < lines.Length && line.Contains("|") && TableSeparator(lines[i + 1])) {
                    if (list != null) { html.Append("</" + list + ">"); list = null; }
                    var headers = Cells(line); html.Append("<div class=\"table-scroll\"><table><thead><tr>"); foreach (string cell in headers) html.Append("<th>" + Format(cell, references, 0) + "</th>"); html.Append("</tr></thead><tbody>"); i++;
                    while (i + 1 < lines.Length && lines[i + 1].Trim().Length > 0 && lines[i + 1].Contains("|") && !Reference.IsMatch(lines[i + 1])) { var cells = Cells(lines[++i]); html.Append("<tr>"); for (int j = 0; j < headers.Length; j++) html.Append("<td>" + Format(j < cells.Length ? cells[j] : "", references, 0) + "</td>"); html.Append("</tr>"); } html.Append("</tbody></table></div>"); continue;
                }
                var heading = Pattern(@"\A(#{1,6})\s+(.+?)\s*#*\z").Match(line);
                if (heading.Success) { if (list != null) { html.Append("</" + list + ">"); list = null; } int level = heading.Groups[1].Length; string title = heading.Groups[2].Value; html.Append("<h" + level + " id=\"" + Encode(Anchor(title)) + "\">" + Format(title, references, 0) + "</h" + level + ">"); continue; }
                var item = Pattern(@"\A(?:(?<number>[0-9]+)\.\s+|[-+*]\s+)(?<text>.+)\z").Match(line);
                if (item.Success) { string kind = item.Groups["number"].Success ? "ol" : "ul"; if (list != kind) { if (list != null) html.Append("</" + list + ">"); html.Append("<" + kind + (kind == "ol" ? " start=\"" + item.Groups["number"].Value + "\"" : "") + ">"); list = kind; } var itemText = new StringBuilder(item.Groups["text"].Value); while (i + 1 < lines.Length && lines[i + 1].Length > 0 && Char.IsWhiteSpace(lines[i + 1][0]) && !lines[i + 1].Contains("|")) itemText.Append(" " + lines[++i].Trim()); html.Append("<li>" + Format(itemText.ToString(), references, 0) + "</li>"); continue; }
                if (list != null) { html.Append("</" + list + ">"); list = null; } var paragraph = new StringBuilder(line);
                while (i + 1 < lines.Length) { string next = lines[i + 1].Trim(); if (next.Length == 0 || Reference.IsMatch(next) || next.StartsWith("#", StringComparison.Ordinal) || next.StartsWith("```", StringComparison.Ordinal) || Pattern(@"\A(?:[0-9]+\.\s+|[-+*]\s+)").IsMatch(next) || (i + 2 < lines.Length && next.Contains("|") && TableSeparator(lines[i + 2]))) break; paragraph.Append(" " + next); i++; } html.Append("<p>" + Format(paragraph.ToString(), references, 0) + "</p>");
            }
            if (list != null) html.Append("</" + list + ">"); return Document(html.ToString());
        }
        public static string ErrorDocument(string error) { return Document("<h1>Guide unavailable</h1><p>" + Encode(error) + "</p><p>Check your connection, then select Reload.</p>"); }
        static string Document(string body)
        {
            return "<!doctype html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><style>html,body{background:#313338;color:#f2f3f5;font-family:'Segoe UI',sans-serif;font-size:15px;line-height:1.55;}body{margin:20px 28px 40px;}h1{font-size:30px;}h2{font-size:23px;margin-top:30px;}h3{font-size:18px;margin-top:24px;}a{color:#a1a9ff;text-decoration:none;}a:hover{text-decoration:underline;}p,li{max-width:1050px;}li{margin:7px 0;}code{font-family:Consolas,monospace;font-size:13px;background:#1e1f22;padding:2px 4px;}pre{background:#1e1f22;padding:16px;overflow:auto;}pre code{padding:0;}.table-scroll{overflow-x:auto;margin:18px 0;}table{width:100%;border-collapse:collapse;font-size:14px;text-align:left;}th{background:#232428;}td,th{padding:10px 12px;border-bottom:1px solid #41434a;vertical-align:top;}td code,th code{white-space:nowrap;}</style></head><body>" + body + "</body></html>";
        }
    }
}
