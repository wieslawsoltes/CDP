using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp.Domains;

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
                    if (session.Window == null || session.Window.Content == null)
                    {
                        return new JsonObject
                        {
                            ["accessibilityScore"] = 100,
                            ["bestPracticesScore"] = 100,
                            ["layoutScore"] = 100,
                            ["issues"] = new JsonArray()
                        };
                    }

                    return await session.Window.Dispatcher.InvokeAsync(() =>
                    {
                        var issues = new List<JsonObject>();
                        int a11yScore = 100;
                        int bestPracticesScore = 100;
                        int layoutScore = 100;

                        if (session.Window.Content is UIElement root)
                        {
                            RunDiagnosticAudits(root, 0, issues, ref a11yScore, ref bestPracticesScore, ref layoutScore, session.NodeMap);
                        }

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

    private static Color ResolveCompositeBackground(UIElement startVisual)
    {
        double rAcc = 0, gAcc = 0, bAcc = 0, aAcc = 0;
        var current = startVisual;
        while (current != null)
        {
            var propBg = current.GetType().GetProperty("Background");
            if (propBg != null && propBg.GetValue(current) is SolidColorBrush brush && brush.Color.A > 0)
            {
                var color = brush.Color;
                double alpha = color.A / 255.0;
                if (aAcc == 0)
                {
                    rAcc = color.R;
                    gAcc = color.G;
                    bAcc = color.B;
                    aAcc = alpha;
                }
                else
                {
                    double newA = aAcc + alpha * (1.0 - aAcc);
                    if (newA > 0)
                    {
                        rAcc = (rAcc * aAcc + color.R * alpha * (1.0 - aAcc)) / newA;
                        gAcc = (gAcc * aAcc + color.G * alpha * (1.0 - aAcc)) / newA;
                        bAcc = (bAcc * aAcc + color.B * alpha * (1.0 - aAcc)) / newA;
                    }
                    aAcc = newA;
                }

                if (aAcc >= 0.999)
                {
                    break;
                }
            }
            current = VisualTreeHelper.GetParent(current) as UIElement;
        }

        if (aAcc > 0)
        {
            if (aAcc < 0.999)
            {
                rAcc = rAcc * aAcc + 255.0 * (1.0 - aAcc);
                gAcc = gAcc * aAcc + 255.0 * (1.0 - aAcc);
                bAcc = bAcc * aAcc + 255.0 * (1.0 - aAcc);
                aAcc = 1.0;
            }
            return Color.FromArgb((byte)Math.Clamp(aAcc * 255.0, 0, 255), (byte)Math.Clamp(rAcc, 0, 255), (byte)Math.Clamp(gAcc, 0, 255), (byte)Math.Clamp(bAcc, 0, 255));
        }

        return Colors.White;
    }

    private static double GetRelativeLuminance(Color color)
    {
        double r = ConvertChannel(color.R);
        double g = ConvertChannel(color.G);
        double b = ConvertChannel(color.B);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double ConvertChannel(byte channelValue)
    {
        double c = channelValue / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static void RunDiagnosticAudits(
        UIElement visual, 
        int depth, 
        List<JsonObject> issues, 
        ref int a11yScore, 
        ref int bestPracticesScore, 
        ref int layoutScore,
        NodeMap nodeMap)
    {
        string typeName = visual.GetType().Name;
        string nameOrType = (visual is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name)) ? fe.Name : typeName;
        int nodeId = nodeMap.GetOrAdd(visual);

        // Rule 1: Accessibility (A11y)
        bool isInteractive = visual is Button ||
                            visual is TextBox ||
                            visual is ComboBox ||
                            visual is CheckBox ||
                            visual is ListBox ||
                            (visual is Control ctrl && ctrl.IsTabStop);

        if (isInteractive)
        {
            string? autoName = AutomationProperties.GetName(visual);
            if (string.IsNullOrEmpty(autoName))
            {
                bool hasText = false;
                if (visual is ContentControl cc && cc.Content is string s && !string.IsNullOrWhiteSpace(s))
                {
                    hasText = true;
                }
                else if (visual is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
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
                        ["message"] = $"Interactive control '{nameOrType}' is missing an accessible name (AutomationProperties.Name)."
                    });
                    a11yScore = Math.Max(0, a11yScore - 15);
                }
            }
        }

        // WCAG Color Contrast Calculator
        if (visual is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
        {
            if (textBlock.Foreground is SolidColorBrush solidFore)
            {
                var foreColor = solidFore.Color;
                var backColor = ResolveCompositeBackground(textBlock);

                if (foreColor.A < 255)
                {
                    double fAlpha = foreColor.A / 255.0;
                    double rComp = foreColor.R * fAlpha + backColor.R * (1.0 - fAlpha);
                    double gComp = foreColor.G * fAlpha + backColor.G * (1.0 - fAlpha);
                    double bComp = foreColor.B * fAlpha + backColor.B * (1.0 - fAlpha);
                    foreColor = Color.FromArgb(255, (byte)Math.Clamp(rComp, 0, 255), (byte)Math.Clamp(gComp, 0, 255), (byte)Math.Clamp(bComp, 0, 255));
                }

                double l1 = GetRelativeLuminance(foreColor);
                double l2 = GetRelativeLuminance(backColor);
                double ratio = (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);

                double minRatio = 4.5;
                bool isBold = textBlock.FontWeight == FontWeights.Bold || textBlock.FontWeight == FontWeights.SemiBold || textBlock.FontWeight == FontWeights.UltraBold || textBlock.FontWeight == FontWeights.Black;
                if (textBlock.FontSize >= 18 || (textBlock.FontSize >= 14 && isBold))
                {
                    minRatio = 3.0;
                }

                if (ratio < minRatio)
                {
                    issues.Add(new JsonObject
                    {
                        ["category"] = "Accessibility",
                        ["severity"] = "warning",
                        ["nodeId"] = nodeId,
                        ["controlType"] = typeName,
                        ["message"] = $"Text contrast ratio {ratio:F2}:1 is below the required WCAG AA threshold of {minRatio}:1."
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

        if (visual is Panel panel && panel.Children.Count == 0)
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

        if (visual is Border border && border.BorderThickness == default && border.Background == null && border.Child != null)
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
        if (visual is FrameworkElement frameworkElement)
        {
            var margin = frameworkElement.Margin;
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
        int childCount = VisualTreeHelper.GetChildrenCount(visual);
        for (int i = 0; i < childCount; i++)
        {
            if (VisualTreeHelper.GetChild(visual, i) is UIElement child)
            {
                RunDiagnosticAudits(child, depth + 1, issues, ref a11yScore, ref bestPracticesScore, ref layoutScore, nodeMap);
            }
        }
    }
}
