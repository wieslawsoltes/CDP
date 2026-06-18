using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class AuditsDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return new JsonObject();

            case "checkFormsIssues":
                return new JsonObject
                {
                    ["formIssues"] = new JsonArray()
                };

            case "getEncodedResponse":
                return new JsonObject
                {
                    ["body"] = "",
                    ["originalSize"] = 0,
                    ["encodedSize"] = 0
                };

            case "runDiagnostics":
                {
                    if (session.Window == null)
                    {
                        return new JsonObject
                        {
                            ["accessibilityScore"] = 100,
                            ["bestPracticesScore"] = 100,
                            ["layoutScore"] = 100,
                            ["issues"] = new JsonArray()
                        };
                    }

                    return await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var issues = new List<JsonObject>();
                        int a11yScore = 100;
                        int bestPracticesScore = 100;
                        int layoutScore = 100;

                        RunDiagnosticAudits(session.Window, 0, issues, ref a11yScore, ref bestPracticesScore, ref layoutScore, session.NodeMap);

                        var issuesArray = new JsonArray();
                        foreach (var issue in issues)
                        {
                            issuesArray.Add(issue);
                        }

                        return new JsonObject
                        {
                            ["accessibilityScore"] = a11yScore,
                            ["bestPracticesScore"] = bestPracticesScore,
                            ["layoutScore"] = layoutScore,
                            ["issues"] = issuesArray
                        };
                    });
                }

            default:
                throw new Exception($"Method Audits.{action} is not implemented");
        }
    }

    private static void RunDiagnosticAudits(
        Visual visual, 
        int depth, 
        List<JsonObject> issues, 
        ref int a11yScore, 
        ref int bestPracticesScore, 
        ref int layoutScore,
        NodeMap nodeMap)
    {
        string typeName = visual.GetType().Name;
        int nodeId = nodeMap.GetOrAdd(visual);

        // Rule 1: Accessibility (A11y)
        bool isInteractive = visual is Avalonia.Controls.Button ||
                            visual is Avalonia.Controls.TextBox ||
                            visual is Avalonia.Controls.ComboBox ||
                            visual is Avalonia.Controls.CheckBox ||
                            visual is Avalonia.Controls.ListBox ||
                            (visual is Avalonia.Controls.Control ctrl && ctrl.IsTabStop);
        
        if (isInteractive)
        {
            string? autoName = Avalonia.Automation.AutomationProperties.GetName(visual);
            if (string.IsNullOrEmpty(autoName))
            {
                bool hasText = false;
                if (visual is Avalonia.Controls.ContentControl cc && cc.Content is string s && !string.IsNullOrWhiteSpace(s))
                {
                    hasText = true;
                }
                else if (visual is Avalonia.Controls.TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
                {
                    hasText = true;
                }

                if (!hasText)
                {
                    issues.Add(new JsonObject
                    {
                        ["category"] = "Accessibility",
                        ["severity"] = "warning",
                        ["nodeId"] = nodeId,
                        ["controlType"] = typeName,
                        ["message"] = $"Interactive control '{typeName}' is missing an accessible name (AutomationProperties.Name)."
                    });
                    a11yScore = Math.Max(0, a11yScore - 15);
                }
            }
        }

        // Rule 2: Best Practices
        if (depth > 12)
        {
            issues.Add(new JsonObject
            {
                ["category"] = "Best Practices",
                ["severity"] = "info",
                ["nodeId"] = nodeId,
                ["controlType"] = typeName,
                ["message"] = $"Deep layout nesting detected at '{typeName}' (nesting depth: {depth}). Consider flattening the visual tree."
            });
            bestPracticesScore = Math.Max(0, bestPracticesScore - 5);
        }

        if (visual is Avalonia.Controls.Panel panel && panel.Children.Count == 0)
        {
            issues.Add(new JsonObject
            {
                ["category"] = "Best Practices",
                ["severity"] = "warning",
                ["nodeId"] = nodeId,
                ["controlType"] = typeName,
                ["message"] = $"Empty layout panel '{typeName}' has 0 children. Consider removing it to improve performance."
            });
            bestPracticesScore = Math.Max(0, bestPracticesScore - 10);
        }

        if (visual is Avalonia.Controls.Border border && border.BorderThickness == default && border.Background == null && border.Child != null)
        {
            issues.Add(new JsonObject
            {
                ["category"] = "Best Practices",
                ["severity"] = "warning",
                ["nodeId"] = nodeId,
                ["controlType"] = typeName,
                ["message"] = $"Redundant border '{typeName}' has no background or thickness but wraps a child control. Consider removing it."
            });
            bestPracticesScore = Math.Max(0, bestPracticesScore - 10);
        }

        // Rule 3: Layout
        if (visual is Avalonia.Controls.Control control)
        {
            var margin = control.Margin;
            if (margin.Left < 0 || margin.Top < 0 || margin.Right < 0 || margin.Bottom < 0)
            {
                issues.Add(new JsonObject
                {
                    ["category"] = "Layout",
                    ["severity"] = "warning",
                    ["nodeId"] = nodeId,
                    ["controlType"] = typeName,
                    ["message"] = $"Control '{typeName}' has a negative margin {margin}. This can cause rendering overlapping."
                });
                layoutScore = Math.Max(0, layoutScore - 10);
            }
        }

        // Recurse children
        foreach (var child in visual.GetVisualChildren())
        {
            RunDiagnosticAudits(child, depth + 1, issues, ref a11yScore, ref bestPracticesScore, ref layoutScore, nodeMap);
        }
    }
}
