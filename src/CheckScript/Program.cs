using Console = System.Console;
using DateTime = System.DateTime;
using Environment = System.Environment;
using Path = System.IO.Path;
using Process = System.Diagnostics.Process;
using Random = System.Random;
using TextWriter = System.IO.TextWriter;

using System.Collections.Generic;  // can't alias

namespace CheckScript
{
    internal class Logger
    {
        private readonly string _session;
        private readonly TextWriter _out;
        private readonly TextWriter _err;

        public Logger(string session, TextWriter @out, TextWriter err)
        {
            _session = session;
            _out = @out;
            _err = err;
        }


        private string Prefix() =>  $"{_session}:{DateTime.Now:HH:mm:ss}";

        public void Info(string message)
        {
            _out.WriteLine($"{Prefix()}: {message}");
        }

        public void InfoLabeled(string label, string message)
        {
            _out.WriteLine($"{Prefix()}: {label}: {message}");
        }

        public void Error(string message)
        {
            _err.WriteLine($"{Prefix()}:E: {message}");
        }
        public void ErrorLabeled(string label, string message) { 
            _err.WriteLine($"{Prefix()}: {label}:E: {message}");
        }
    }
    
    /// <summary>
    /// Run all pre-commit checks.
    ///
    /// The original Check.ps1 became too unwieldy.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Log which expression to execute and actually execute it.
        /// </summary>
        /// <param name="log">Logger to write messages (both info and error)</param>
        /// <param name="expression">Powershell expression</param>
        /// <param name="label">Label to be included in the prefix</param>
        /// <returns>exit code</returns>
        private static int LogAndExecute(Logger log, string expression, string label)
        {
            var title = $"Running: {expression}";
            var border = new string('-', title.Length);

            log.Info("");
            log.Info($"+-{border}-+");
            log.Info($"  {title}");
            log.Info($"+-{border}-+");
            log.Info("");

            var process = new Process
            {
                StartInfo =
                {
                    FileName = "powershell.exe",
                    Arguments = expression,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                log.InfoLabeled(label, e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    log.ErrorLabeled(label, e.Data);
                }
            };

            process.Start();
            try
            {
                // Asynchronously read the standard output and error
                // of the spawned process.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    log.Info($"The exit code was 0 for: {expression}");
                    return process.ExitCode;
                }

                log.Error(
                    $"Failed to execute {expression}, " +
                    $"the exit code was: {process.ExitCode}");
                return process.ExitCode;
            }
            finally
            {
                process.Close();
            }
        }

        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Console.Error.WriteLine(
                    $"Unexpected arguments: {string.Join(" ", args)}");
            }

            string session = ThreeLetterWords.TheList[
                new Random().Next(ThreeLetterWords.TheList.Count)];

            var log = new Logger(session, Console.Out, Console.Error);
            
            log.Info(
                $"Starting the check session with a randomized identifier: " +
                $">>> {session} <<<");
            
            var scriptsLabels = new List<(string, string)>
            {
                ("CheckFormat.ps1", "Format"),
                ("Test.ps1", "Test"),
                ("InspectCode.ps1", "Inspect")
            };

            foreach (var (script, label) in scriptsLabels)
            {
                var exitCode = LogAndExecute(
                    log, Path.Join(".", script), label);
                if (exitCode == 0) continue;

                Environment.ExitCode = exitCode;
                return;
            }

            log.Info(
                "All checks passed successfully. You can now push the commits.");
        }
    }
}
