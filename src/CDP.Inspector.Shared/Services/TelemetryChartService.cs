#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Chrome.DevTools.Protocol;
using SkiaSharp;

namespace CdpInspectorApp.Services;

public class TelemetryChartService : ITelemetryChartService
{
    public Bitmap? RenderPerformanceChart(List<RunMetricSample> samples, double relativeStartMs, double durationMs, int width = 800, int height = 150)
    {
        try
        {
            using var bitmap = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(bitmap))
            {
                // Clear background with dark theme color
                canvas.Clear(new SKColor(15, 16, 21));

                if (samples == null || samples.Count == 0)
                {
                    return null;
                }

                double maxMem = samples.Max(s => s.MemoryJsHeapUsed);
                if (maxMem <= 0) maxMem = 128.0;
                double maxCpu = 100.0;

                float padLeft = 60;
                float padRight = 60;
                float padTop = 25;
                float padBottom = 20;
                float chartWidth = width - padLeft - padRight;
                float chartHeight = height - padTop - padBottom;

                double totalTime = samples.Last().RelativeTimeMs;
                if (totalTime <= 0) totalTime = 1.0;

                // Draw horizontal gridlines (at 25%, 50%, 75%)
                using var gridPaint = new SKPaint { Color = new SKColor(45, 49, 63, 100), Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
                for (int i = 1; i <= 3; i++)
                {
                    float yGrid = padTop + chartHeight * (i / 4f);
                    canvas.DrawLine(padLeft, yGrid, padLeft + chartWidth, yGrid, gridPaint);
                }

                // Draw step range highlight
                float stepLeft = padLeft + (float)(relativeStartMs / totalTime * chartWidth);
                float stepWidth = (float)(durationMs / totalTime * chartWidth);
                if (stepWidth < 2f) stepWidth = 2f;
                if (stepLeft + stepWidth > padLeft + chartWidth) stepWidth = padLeft + chartWidth - stepLeft;

                using var highlightPaint = new SKPaint
                {
                    Color = new SKColor(37, 99, 235, 38),
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(new SKRect(stepLeft, padTop, stepLeft + stepWidth, padTop + chartHeight), highlightPaint);
                
                using var highlightBorder = new SKPaint
                {
                    Color = new SKColor(37, 99, 235, 128),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1f
                };
                canvas.DrawRect(new SKRect(stepLeft, padTop, stepLeft + stepWidth, padTop + chartHeight), highlightBorder);

                // Draw paths for Memory and CPU
                using var memPath = new SKPath();
                using var cpuPath = new SKPath();

                for (int idx = 0; idx < samples.Count; idx++)
                {
                    var s = samples[idx];
                    float x = padLeft + (float)(s.RelativeTimeMs / totalTime * chartWidth);
                    float yMem = padTop + chartHeight - (float)(s.MemoryJsHeapUsed / maxMem * chartHeight);
                    float yCpu = padTop + chartHeight - (float)(s.CpuUsage / maxCpu * chartHeight);

                    // Bound-checking to stay inside plot area
                    x = Math.Max(padLeft, Math.Min(padLeft + chartWidth, x));
                    yMem = Math.Max(padTop, Math.Min(padTop + chartHeight, yMem));
                    yCpu = Math.Max(padTop, Math.Min(padTop + chartHeight, yCpu));

                    if (idx == 0)
                    {
                        memPath.MoveTo(x, yMem);
                        cpuPath.MoveTo(x, yCpu);
                    }
                    else
                    {
                        memPath.LineTo(x, yMem);
                        cpuPath.LineTo(x, yCpu);
                    }
                }

                using var paintMem = new SKPaint { Color = new SKColor(16, 185, 129), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
                using var paintCpu = new SKPaint { Color = new SKColor(245, 158, 11), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };

                canvas.DrawPath(memPath, paintMem);
                canvas.DrawPath(cpuPath, paintCpu);

                // Draw plot area border
                using var plotBorderPaint = new SKPaint { Color = new SKColor(45, 49, 63), Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
                canvas.DrawRect(new SKRect(padLeft, padTop, padLeft + chartWidth, padTop + chartHeight), plotBorderPaint);

                // Draw Y-Axis Labels
                using var memLabelPaint = new SKPaint { Color = new SKColor(16, 185, 129), TextSize = 9, IsAntialias = true, TextAlign = SKTextAlign.Right };
                canvas.DrawText($"{maxMem:F2} MB", padLeft - 6, padTop + 8, memLabelPaint);
                canvas.DrawText("0.00 MB", padLeft - 6, padTop + chartHeight, memLabelPaint);

                using var cpuLabelPaint = new SKPaint { Color = new SKColor(245, 158, 11), TextSize = 9, IsAntialias = true, TextAlign = SKTextAlign.Left };
                canvas.DrawText("100% CPU", padLeft + chartWidth + 6, padTop + 8, cpuLabelPaint);
                canvas.DrawText("0% CPU", padLeft + chartWidth + 6, padTop + chartHeight, cpuLabelPaint);

                // Draw X-Axis Labels
                using var timeLabelPaint = new SKPaint { Color = new SKColor(138, 143, 152), TextSize = 9, IsAntialias = true };
                canvas.DrawText("0.0s", padLeft, padTop + chartHeight + 14, timeLabelPaint);

                string totalTimeStr = $"{(totalTime / 1000.0):F1}s";
                using var timeLabelPaintRight = new SKPaint { Color = new SKColor(138, 143, 152), TextSize = 9, IsAntialias = true, TextAlign = SKTextAlign.Right };
                canvas.DrawText(totalTimeStr, padLeft + chartWidth, padTop + chartHeight + 14, timeLabelPaintRight);

                // Draw Legend at the top (centered relative to the plot area)
                float legendY = 15;
                float startLegendX = padLeft + (chartWidth - 250) / 2f;
                using var paintLegendText = new SKPaint { Color = new SKColor(232, 234, 237), TextSize = 10, IsAntialias = true };
                
                // Memory legend
                using var paintLegendMemDot = new SKPaint { Color = new SKColor(16, 185, 129), Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawCircle(startLegendX + 10, legendY - 3, 3, paintLegendMemDot);
                canvas.DrawText("Memory", startLegendX + 18, legendY, paintLegendText);
                
                // CPU legend
                using var paintLegendCpuDot = new SKPaint { Color = new SKColor(245, 158, 11), Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawCircle(startLegendX + 80, legendY - 3, 3, paintLegendCpuDot);
                canvas.DrawText("CPU", startLegendX + 88, legendY, paintLegendText);

                // Step Duration legend
                using var paintLegendDurBorder = new SKPaint { Color = new SKColor(37, 99, 235, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
                using var paintLegendDurFill = new SKPaint { Color = new SKColor(37, 99, 235, 38), Style = SKPaintStyle.Fill, IsAntialias = true };
                SKRect durRect = new SKRect(startLegendX + 130, legendY - 7, startLegendX + 142, legendY - 1);
                canvas.DrawRect(durRect, paintLegendDurFill);
                canvas.DrawRect(durRect, paintLegendDurBorder);
                canvas.DrawText("Step Duration", startLegendX + 148, legendY, paintLegendText);
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
