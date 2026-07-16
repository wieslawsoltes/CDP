using System;
using SkiaSharp;

namespace CDP.Rendering.Comparison.Tests
{
    public class ImageComparisonResult
    {
        public double SSIM { get; set; }
        public double PixelDeltaPercent { get; set; }
        public int TotalPixels { get; set; }
        public int MismatchedPixels { get; set; }
    }

    public static class ImageComparator
    {
        public static ImageComparisonResult Compare(SKBitmap actual, SKBitmap expected, string diffOutputPath)
        {
            int width = Math.Max(actual.Width, expected.Width);
            int height = Math.Max(actual.Height, expected.Height);

            using var diff = new SKBitmap(width, height);
            using var canvas = new SKCanvas(diff);
            canvas.Clear(SKColors.Red); // default for mismatch/padding

            int minWidth = Math.Min(actual.Width, expected.Width);
            int minHeight = Math.Min(actual.Height, expected.Height);

            int mismatchedPixels = 0;
            int totalPixels = width * height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < minWidth && y < minHeight)
                    {
                        var p1 = actual.GetPixel(x, y);
                        var p2 = expected.GetPixel(x, y);

                        if (p1.Red != p2.Red || p1.Green != p2.Green || p1.Blue != p2.Blue || p1.Alpha != p2.Alpha)
                        {
                            mismatchedPixels++;
                            diff.SetPixel(x, y, SKColors.Red);
                        }
                        else
                        {
                            // Matches in muted opacity
                            diff.SetPixel(x, y, new SKColor(p1.Red, p1.Green, p1.Blue, 50));
                        }
                    }
                    else
                    {
                        mismatchedPixels++;
                        diff.SetPixel(x, y, SKColors.Red);
                    }
                }
            }

            // Block-based SSIM calculation (luminance-based)
            double ssimSum = 0;
            int blockCount = 0;
            int blockSize = 8;

            // Pre-calculate luminance for both images to avoid redundant calculations
            double[,] lum1 = new double[minWidth, minHeight];
            double[,] lum2 = new double[minWidth, minHeight];

            for (int y = 0; y < minHeight; y++)
            {
                for (int x = 0; x < minWidth; x++)
                {
                    var p1 = actual.GetPixel(x, y);
                    var p2 = expected.GetPixel(x, y);
                    lum1[x, y] = 0.299 * p1.Red + 0.587 * p1.Green + 0.114 * p1.Blue;
                    lum2[x, y] = 0.299 * p2.Red + 0.587 * p2.Green + 0.114 * p2.Blue;
                }
            }

            const double C1 = 6.5025; // (0.01 * 255)^2
            const double C2 = 58.5225; // (0.03 * 255)^2

            for (int y = 0; y <= minHeight - blockSize; y += blockSize)
            {
                for (int x = 0; x <= minWidth - blockSize; x += blockSize)
                {
                    // Calculate stats for current 8x8 block
                    double sumX = 0;
                    double sumY = 0;
                    for (int by = 0; by < blockSize; by++)
                    {
                        for (int bx = 0; bx < blockSize; bx++)
                        {
                            sumX += lum1[x + bx, y + by];
                            sumY += lum2[x + bx, y + by];
                        }
                    }

                    double muX = sumX / 64.0;
                    double muY = sumY / 64.0;

                    double varX = 0;
                    double varY = 0;
                    double covXY = 0;

                    for (int by = 0; by < blockSize; by++)
                    {
                        for (int bx = 0; bx < blockSize; bx++)
                        {
                            double dx = lum1[x + bx, y + by] - muX;
                            double dy = lum2[x + bx, y + by] - muY;
                            varX += dx * dx;
                            varY += dy * dy;
                            covXY += dx * dy;
                        }
                    }

                    varX /= 63.0;
                    varY /= 63.0;
                    covXY /= 63.0;

                    double numerator = (2 * muX * muY + C1) * (2 * covXY + C2);
                    double denominator = (muX * muX + muY * muY + C1) * (varX + varY + C2);
                    double blockSsim = numerator / (denominator == 0 ? 1e-9 : denominator);

                    ssimSum += blockSsim;
                    blockCount++;
                }
            }

            double averageSsim = blockCount > 0 ? ssimSum / blockCount : 0.0;
            // If the sizes differ, scale SSIM down as penalty
            if (actual.Width != expected.Width || actual.Height != expected.Height)
            {
                double penalty = (double)(minWidth * minHeight) / (width * height);
                averageSsim *= penalty;
            }

            // Save the diff image
            using (var fs = System.IO.File.OpenWrite(diffOutputPath))
            {
                diff.Encode(fs, SKEncodedImageFormat.Png, 100);
            }

            return new ImageComparisonResult
            {
                SSIM = averageSsim,
                PixelDeltaPercent = (double)mismatchedPixels / totalPixels * 100.0,
                TotalPixels = totalPixels,
                MismatchedPixels = mismatchedPixels
            };
        }
    }
}
