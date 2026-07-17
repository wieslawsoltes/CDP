using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class CdpProtocolComplianceTests
{
    private static readonly string BrowserProtocolUrl = "https://raw.githubusercontent.com/chromedevtools/devtools-protocol/master/json/browser_protocol.json";
    private static readonly string JsProtocolUrl = "https://raw.githubusercontent.com/chromedevtools/devtools-protocol/master/json/js_protocol.json";

    [Fact]
    public async Task GenerateCdpComplianceReport()
    {
        // 1. Locate repository root and domains directory
        var repoRoot = FindRepositoryRoot();
        var domainsDir = Path.Combine(repoRoot, "src", "Avalonia.Diagnostics.Cdp", "Domains");
        Assert.True(Directory.Exists(domainsDir), $"Domains directory not found at: {domainsDir}");

        // 2. Scan local domain files and parse implemented actions
        var implementedDomains = new Dictionary<string, List<string>>();
        var domainFiles = Directory.GetFiles(domainsDir, "*Domain.cs");
        foreach (var file in domainFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var domainName = fileName.EndsWith("Domain") ? fileName.Substring(0, fileName.Length - 6) : fileName;
            if (domainName.Equals("Dom", StringComparison.OrdinalIgnoreCase)) domainName = "DOM";
            else if (domainName.Equals("Css", StringComparison.OrdinalIgnoreCase)) domainName = "CSS";
            else if (domainName.Equals("DomDebugger", StringComparison.OrdinalIgnoreCase)) domainName = "DOMDebugger";

            var content = await File.ReadAllTextAsync(file);
            var methods = ParseImplementedMethods(content);
            if (implementedDomains.TryGetValue(domainName, out var existingMethods))
            {
                existingMethods.AddRange(methods);
            }
            else
            {
                implementedDomains[domainName] = methods;
            }
        }

        // 3. Fetch official CDP specification json files
        string? browserProtocolJson = null;
        string? jsProtocolJson = null;

        var resourcesDir = Path.Combine(repoRoot, "tests", "Avalonia.Diagnostics.Cdp.Tests", "Resources");
        Directory.CreateDirectory(resourcesDir);
        var localBrowserSpec = Path.Combine(resourcesDir, "browser_protocol.json");
        var localJsSpec = Path.Combine(resourcesDir, "js_protocol.json");

        using (var http = new HttpClient())
        {
            http.Timeout = TimeSpan.FromSeconds(5);
            try
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; CDP-Compliance-Scanner/1.0)");
                browserProtocolJson = await http.GetStringAsync(BrowserProtocolUrl);
                await File.WriteAllTextAsync(localBrowserSpec, browserProtocolJson);
            }
            catch
            {
                if (File.Exists(localBrowserSpec))
                {
                    browserProtocolJson = await File.ReadAllTextAsync(localBrowserSpec);
                }
            }

            try
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; CDP-Compliance-Scanner/1.0)");
                jsProtocolJson = await http.GetStringAsync(JsProtocolUrl);
                await File.WriteAllTextAsync(localJsSpec, jsProtocolJson);
            }
            catch
            {
                if (File.Exists(localJsSpec))
                {
                    jsProtocolJson = await File.ReadAllTextAsync(localJsSpec);
                }
            }
        }

        // 4. Parse official spec commands
        var specDomains = new Dictionary<string, HashSet<string>>();
        if (browserProtocolJson != null) ParseSpecJson(browserProtocolJson, specDomains);
        if (jsProtocolJson != null) ParseSpecJson(jsProtocolJson, specDomains);

        // 5. Build report
        var sb = new StringBuilder();
        sb.AppendLine("# Chrome DevTools Protocol (CDP) Compliance Report");
        sb.AppendLine();
        sb.AppendLine($"Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("This report lists the level of compliance of the `Avalonia.Diagnostics.Cdp` library against the official Chrome DevTools Protocol specification.");
        sb.AppendLine();

        int totalStandardSupported = 0;
        int totalStandardSpec = 0;
        int totalCustomSupported = 0;

        var tableRows = new List<string>();

        // Sort domains alphabetically
        var allDomains = implementedDomains.Keys.Union(specDomains.Keys).OrderBy(d => d).ToList();
        foreach (var domain in allDomains)
        {
            var hasSpec = specDomains.TryGetValue(domain, out var specMethods);
            var hasImpl = implementedDomains.TryGetValue(domain, out var implMethods);

            var specMethodsSet = (hasSpec && specMethods != null) ? specMethods : new HashSet<string>();
            var implMethodsList = (hasImpl && implMethods != null) ? implMethods : new List<string>();

            var standardSupported = implMethodsList.Intersect(specMethodsSet).ToList();
            var customSupported = implMethodsList.Except(specMethodsSet).ToList();
            var unsupported = specMethodsSet.Except(implMethodsList).ToList();

            totalStandardSupported += standardSupported.Count;
            totalStandardSpec += specMethodsSet.Count;
            totalCustomSupported += customSupported.Count;

            string status = "Unsupported";
            if (hasImpl && hasSpec)
            {
                double percentage = specMethodsSet.Count > 0 ? (double)standardSupported.Count / specMethodsSet.Count * 100.0 : 0.0;
                status = percentage >= 100.0 ? "Fully Compliant" : $"{standardSupported.Count}/{specMethodsSet.Count} ({percentage:F1}%)";
            }
            else if (hasImpl && !hasSpec)
            {
                status = $"Custom Domain ({implMethodsList.Count} actions)";
            }

            tableRows.Add($"| **{domain}** | {status} | {standardSupported.Count} | {customSupported.Count} | {unsupported.Count} |");
        }

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"* **Total Standard CDP Methods Supported**: {totalStandardSupported} / {totalStandardSpec} ({(double)totalStandardSupported/totalStandardSpec*100.0:F1}%)");
        sb.AppendLine($"* **Total Custom/Extension Methods Supported**: {totalCustomSupported}");
        sb.AppendLine();
        sb.AppendLine("| Domain | Status / Coverage | Standard Supported | Custom Extensions | Missing Standard |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
        foreach (var row in tableRows)
        {
            sb.AppendLine(row);
        }
        sb.AppendLine();

        sb.AppendLine("## Domain Details");
        sb.AppendLine();

        foreach (var domain in allDomains)
        {
            var hasSpec = specDomains.TryGetValue(domain, out var specMethods);
            var hasImpl = implementedDomains.TryGetValue(domain, out var implMethods);

            var specMethodsSet = (hasSpec && specMethods != null) ? specMethods : new HashSet<string>();
            var implMethodsList = (hasImpl && implMethods != null) ? implMethods : new List<string>();

            var standardSupported = implMethodsList.Intersect(specMethodsSet).OrderBy(m => m).ToList();
            var customSupported = implMethodsList.Except(specMethodsSet).OrderBy(m => m).ToList();
            var unsupported = specMethodsSet.Except(implMethodsList).OrderBy(m => m).ToList();

            sb.AppendLine($"### {domain}");
            sb.AppendLine();
            if (standardSupported.Count > 0)
            {
                sb.AppendLine($"* **Standard Supported ({standardSupported.Count})**: `{string.Join("`, `", standardSupported)}`");
            }
            if (customSupported.Count > 0)
            {
                sb.AppendLine($"* **Custom Extensions ({customSupported.Count})**: `{string.Join("`, `", customSupported)}`");
            }
            if (unsupported.Count > 0)
            {
                sb.AppendLine($"* **Missing Standard ({unsupported.Count})**: `{string.Join("`, `", unsupported)}`");
            }
            sb.AppendLine();
        }

        if (string.Equals(Environment.GetEnvironmentVariable("GENERATE_CDP_REPORT"), "true", StringComparison.OrdinalIgnoreCase))
        {
            var reportPath = Path.Combine(repoRoot, "cdp_compliance_report.md");
            await File.WriteAllTextAsync(reportPath, sb.ToString());
            Console.WriteLine($"Compliance report generated successfully at: {reportPath}");
        }
        else
        {
            var tempReportPath = Path.Combine(Path.GetTempPath(), "cdp_compliance_report.md");
            await File.WriteAllTextAsync(tempReportPath, sb.ToString());
            Console.WriteLine($"Compliance report generated in temp directory: {tempReportPath}");
        }
    }

    private string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Any() || dir.GetFiles("*.slnx").Any() || dir.GetDirectories(".git").Any())
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private List<string> ParseImplementedMethods(string code)
    {
        var methods = new List<string>();
        
        int handleAsyncIdx = code.IndexOf("HandleAsync");
        if (handleAsyncIdx == -1) return methods;

        int switchIdx = code.IndexOf("switch", handleAsyncIdx);
        if (switchIdx == -1) return methods;

        int braceStart = code.IndexOf('{', switchIdx);
        if (braceStart == -1) return methods;

        int braceCount = 1;
        int idx = braceStart + 1;
        while (idx < code.Length && braceCount > 0)
        {
            if (code[idx] == '{') braceCount++;
            else if (code[idx] == '}') braceCount--;
            idx++;
        }

        if (braceCount > 0) return methods;

        string switchBlock = code.Substring(braceStart, idx - braceStart);

        var matches = Regex.Matches(switchBlock, @"case\s+""([^""]+)""\s*:");
        foreach (Match match in matches)
        {
            var method = match.Groups[1].Value;
            if (!methods.Contains(method))
            {
                methods.Add(method);
            }
        }
        return methods;
    }

    private void ParseSpecJson(string json, Dictionary<string, HashSet<string>> domains)
    {
        try
        {
            var root = JsonNode.Parse(json);
            var domainsArray = root?["domains"] as JsonArray;
            if (domainsArray != null)
            {
                foreach (var domainNode in domainsArray)
                {
                    var domainName = domainNode?["domain"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(domainName) || domainNode == null) continue;

                    if (!domains.ContainsKey(domainName))
                    {
                        domains[domainName] = new HashSet<string>();
                    }

                    var commandsArray = domainNode["commands"] as JsonArray;
                    if (commandsArray != null)
                    {
                        foreach (var commandNode in commandsArray)
                        {
                            var commandName = commandNode?["name"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(commandName))
                            {
                                domains[domainName].Add(commandName);
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }
}
