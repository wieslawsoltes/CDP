using System;
using System.Diagnostics;
using System.IO;

namespace CDP.Rendering.Comparison.Tests
{
    public static class DocumentConverter
    {
        public static void ConvertMarkdownToHtml(string mdPath, string htmlPath)
        {
            mdPath = Path.GetFullPath(mdPath);
            htmlPath = Path.GetFullPath(htmlPath);

            var pandocPath = "pandoc";
            if (!File.Exists(pandocPath))
            {
                if (File.Exists("/opt/homebrew/bin/pandoc"))
                {
                    pandocPath = "/opt/homebrew/bin/pandoc";
                }
                else if (File.Exists("/usr/local/bin/pandoc"))
                {
                    pandocPath = "/usr/local/bin/pandoc";
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = pandocPath,
                Arguments = $"-f markdown -t html -o \"{htmlPath}\" \"{mdPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) throw new Exception($"Failed to start pandoc at {pandocPath}");

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"pandoc failed with exit code {process.ExitCode}. Output: {output}. Error: {error}");
            }
        }

        public static void ConvertOfficeToPdf(string officePath, string pdfPath)
        {
            officePath = Path.GetFullPath(officePath);
            pdfPath = Path.GetFullPath(pdfPath);

            var ext = Path.GetExtension(officePath).ToLower();
            string appName;
            if (ext == ".docx")
            {
                appName = "Pages";
            }
            else if (ext == ".xlsx")
            {
                appName = "Numbers";
            }
            else if (ext == ".pptx")
            {
                appName = "Keynote";
            }
            else
            {
                throw new NotSupportedException($"Office extension '{ext}' not supported.");
            }

            // Create temporary AppleScript file to avoid shell escaping issues with osascript
            string script = $@"
tell application ""{appName}""
    activate
    set theDoc to open (POSIX file ""{officePath}"")
    export theDoc to (POSIX file ""{pdfPath}"") as PDF
    close theDoc saving no
end tell
";
            
            var tempScriptPath = Path.Combine(Path.GetTempPath(), $"convert_{Guid.NewGuid()}.scpt");
            File.WriteAllText(tempScriptPath, script);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $"\"{tempScriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) throw new Exception("Failed to start osascript");

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"osascript failed with exit code {process.ExitCode}. Output: {output}. Error: {error}");
                }
            }
            finally
            {
                if (File.Exists(tempScriptPath))
                {
                    File.Delete(tempScriptPath);
                }
            }
        }

        public static void CaptureScreenshot(string inputPath, string pngPath, int width = 1280, int height = 1024)
        {
            inputPath = Path.GetFullPath(inputPath);
            pngPath = Path.GetFullPath(pngPath);

            // If input is a PDF or HTML, Chrome needs file:// scheme
            string url = inputPath;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "file://" + inputPath;
            }

            var chromePath = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
            if (!File.Exists(chromePath))
            {
                throw new FileNotFoundException($"Google Chrome not found at expected path: {chromePath}");
            }

            // We need to pass --headless=new --disable-gpu --screenshot=... --window-size=...
            var psi = new ProcessStartInfo
            {
                FileName = chromePath,
                Arguments = $"--headless=new --disable-gpu --screenshot=\"{pngPath}\" --window-size={width},{height} \"{url}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) throw new Exception("Failed to start Google Chrome");

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // Wait a small bit to ensure file is flushed
            System.Threading.Thread.Sleep(500);

            if (!File.Exists(pngPath))
            {
                throw new Exception($"Chrome did not generate screenshot. Exit code: {process.ExitCode}. Output: {output}. Error: {error}");
            }
        }
    }
}
