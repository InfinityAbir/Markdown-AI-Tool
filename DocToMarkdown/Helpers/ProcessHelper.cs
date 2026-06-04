using System.Diagnostics;
using System.Text;

namespace DocToMarkdown.Helpers
{
    public static class ProcessHelper
    {
        public static async Task<(int exitCode, string output, string error)> RunProcess(
            string fileName,
            string arguments,
            int timeoutSeconds = 120)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,   // ✅ capture output
                    RedirectStandardError = true,    // ✅ capture error
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            var tcs = new TaskCompletionSource<bool>();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    outputBuilder.AppendLine(args.Data);
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    errorBuilder.AppendLine(args.Data);
            };

            process.Exited += (sender, args) =>
            {
                tcs.TrySetResult(true);
            };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // ⏳ Timeout handling
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));

            if (completedTask != tcs.Task)
            {
                try
                {
                    process.Kill(true);
                }
                catch { }

                throw new Exception($"Process timeout: {fileName} {arguments}");
            }

            await tcs.Task;

            return (
                process.ExitCode,
                outputBuilder.ToString(),
                errorBuilder.ToString()
            );
        }
    }
}