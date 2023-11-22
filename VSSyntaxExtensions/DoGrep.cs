using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace VSSyntaxExtensions
{
    /// <summary>
    /// Command handler
    /// </summary>
    public sealed class DoGrep
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 4139;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("1c6bc961-9f4d-49ac-8a11-b4f37e3382ea");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="DoGrep"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private DoGrep(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static DoGrep Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in DoGrep's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new DoGrep(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var allFiles = GetAllSolutionFiles();
            if (allFiles.Count == 0)
                return;
            var dlg = new GrepWindow()
            {
                FilePaths = allFiles,
                package = this.package,
            };
            dlg.reload();
            dlg.ShowDialog();

        }

        public static List<string> GetAllSolutionFiles()
        {
            var workspace = Helpers.GetWorkspace();
            var sol = workspace.CurrentSolution;
            if (sol == null)
                return new List<string>();
            var allFiles = sol.Projects.SelectMany(x => x.Documents.Select(y => y.FilePath)).Where(x => File.Exists(x)).ToList();
            return allFiles;
        }
        public static void DoSearch2(IReadOnlyList<string> fileNames, string search, Action<GrepResult> onResult, CancellationToken ct)
        {
            //return Task.Run(() =>
            {
                var startStuff = $"rg -i -n --no-heading '{search}' ";

                var maxLineLen = 30000;
                var maxLen = maxLineLen;
                var lines = new List<string>();
                var sb = new StringBuilder();
                sb.Append(startStuff);
                int count = 0;
                foreach (var f in fileNames)
                {
                    if (sb.Length + f.Length > maxLen)
                    {
                        lines.Add(sb.ToString());
                        sb.Clear();
                        count += 1;
                        sb.Append(startStuff);
                    }
                    var l = f.Length;
                    sb.Append(" \"");
                    sb.Append(f);
                    sb.Append("\"");
                }
                lines.Add(sb.ToString());

                var sw = Stopwatch.StartNew();
                foreach (var line in lines)
                {
                    if (ct.IsCancellationRequested)
                        break;
                    DoProc2(onResult, line);
                }
                //if (result.Length > 0)
                //{
                //    Debug.WriteLine(result);
                //}

                //Console.WriteLine(sw.Elapsed.ToString());
                //Console.ReadLine();
            }//);
        }
        public static List<GrepResult> DoSearch(IReadOnlyList<string> fileNames, string search, int maxResults, CancellationToken ct)
        {
            //return Task.Run(() =>
            {
                var startStuff = $"rg -i -n --no-heading '{search}' ";

                var maxLineLen = 30000;
                var maxLen = maxLineLen;
                var lines = new List<string>();
                var sb = new StringBuilder();
                sb.Append(startStuff);
                int count = 0;
                foreach (var f in fileNames)
                {
                    if (sb.Length + f.Length > maxLen)
                    {
                        lines.Add(sb.ToString());
                        sb.Clear();
                        count += 1;
                        sb.Append(startStuff);
                    }
                    var l = f.Length;
                    sb.Append(" \"");
                    sb.Append(f);
                    sb.Append("\"");
                }
                lines.Add(sb.ToString());

                var results = new List<GrepResult>();
                var sw = Stopwatch.StartNew();
                foreach (var line in lines)
                {
                    if (results.Count >= maxResults)
                        break;
                    if (ct.IsCancellationRequested)
                        break;
                    var r = DoProc(line);
                    results.AddRange(r);
                }
                if (results.Count > maxResults)
                    results.RemoveRange(maxResults, results.Count - maxResults);
                return results;
                //if (result.Length > 0)
                //{
                //    Debug.WriteLine(result);
                //}

                //Console.WriteLine(sw.Elapsed.ToString());
                //Console.ReadLine();
            }//);
        }


        public class GrepResult
        {
            public string FilePath;
            public int LineNum;
            public string Text;
            public static GrepResult Create(string r)
            {
                var cx = r.IndexOf(':');
                if (cx < 0)
                    return null;
                var c0 = r.IndexOf(':', cx + 1);
                if (c0 < 0)
                    return null;
                var c1 = r.IndexOf(':', c0 + 1);
                if (c1 < 0)
                    return null;

                var path = r.Substring(0, c0);
                var numstring = r.Substring(c0 + 1, c1 - c0 - 1);
                if (int.TryParse(numstring, out var num))
                {

                    return new GrepResult()
                    {
                        FilePath = path,
                        LineNum = num,
                        Text = r.Substring(c1 + 1)
                    };
                }
                return null;

            }
        }

        public static List<GrepResult> DoProc(string command)
        {
            var tempFile = @"c:\temp\rr.ps1";
            var sw = new Stopwatch();
            sw.Start();
            var proc = new Process();
            var scriptArguments = "-ExecutionPolicy Bypass -File \"" + tempFile + "\"";
            proc.StartInfo.FileName = "powershell.exe";
            System.IO.File.WriteAllText(tempFile, command);

            proc.StartInfo.Arguments = scriptArguments;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;

            proc.Start();

            var result = proc.StandardOutput.ReadToEnd();
            var err = proc.StandardError.ReadToEnd();

            var rr = new List<GrepResult>();
            foreach (var r in result.Split('\n'))
            {
                var g = GrepResult.Create(r);
                if (g != null)
                    rr.Add(g);
            }


            proc.WaitForExit();
            return rr;
        }
        private static Stopwatch DoProc2(Action<GrepResult> onResult, string command)
        {
            var tempFile = Path.GetTempFileName() + ".ps1";
            var sw = new Stopwatch();
            sw.Start();
            var proc = new Process();
            var scriptArguments = "-ExecutionPolicy Bypass -File \"" + tempFile + "\"";
            proc.StartInfo.FileName = "powershell.exe";
            System.IO.File.WriteAllText(tempFile, command);

            proc.StartInfo.Arguments = scriptArguments;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.EnableRaisingEvents = true;

            proc.ErrorDataReceived += (o, e) =>
            {
                Debug.WriteLine(e.Data);
            };

            proc.OutputDataReceived += (o, e) =>
            {
                if (e.Data == null)
                    return;
                var r = GrepResult.Create(e.Data);
                if (r != null)
                    onResult(r);
            };
            proc.Start();
            proc.BeginOutputReadLine();

            //var result = proc.StandardOutput.ReadToEnd();
            //var err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            System.IO.File.Delete(tempFile);
            return sw;
        }




        public static string TTT = @"

function SplitAndRunAgainstFilePaths()
{
    $Local:path = $($args[0])
    $Local:cmd = $($args[1..1000]) -join "" ""
    $Local:sb = [System.Text.StringBuilder]::new()
    foreach ($line in (Get-Content $path))
    {
        $local:ll = $($sb.Length + $line.Length)
        if ($ll -gt 30000) {
            Invoke-Expression $($cmd + ' ' + $sb.ToString())
            $sb = [System.Text.StringBuilder]::new()
        }
        [void]$sb.Append('""""')
        [void]$sb.Append($line)
        [void]$sb.Append('"""" ')

    }

    Invoke-Expression $($cmd + ' ' + $sb.ToString())

}


";
        public static string CCC = @"rg --color=always --line-number -i --no-heading --smart-case";

        public static string CMD = @"rg --color=always --line-number -i --no-heading --smart-case '${*:-}' ";
        public static string CMD2 = @" | fzf --ansi `
    --color 'hl:-1:underline,hl+:-1:underline:reverse' `
      --delimiter : `
      --preview 'bat --color=always {1}:{2} --highlight-line {3}'";

        public static string DoFZF(string files_path, string searchString)
        {
            var command = $"SplitAndRunAgainstFilePaths \"{files_path}\" " + CMD + "\"" + searchString + "\" " + CMD2 + " | echo";
            command = TTT + "\n" + command;
            var tempFile = Path.GetTempFileName() + ".ps1";
            var proc = new Process();
            var scriptArguments = "-ExecutionPolicy Bypass -File \"" + tempFile + "\"";
            proc.StartInfo.FileName = "powershell.exe";
            System.IO.File.WriteAllText(tempFile, command);

            proc.StartInfo.Arguments = scriptArguments;
            proc.StartInfo.RedirectStandardOutput = true;
            //proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = false;
            proc.EnableRaisingEvents = true;

            proc.ErrorDataReceived += (o, e) =>
            {
                Debug.WriteLine(e.Data);
            };

            proc.OutputDataReceived += (o, e) =>
            {
                //if (e.Data == null)
                //    return;
                Console.WriteLine(e.Data);
            };
            proc.Start();
            //proc.BeginOutputReadLine();
            proc.WaitForExit();
            var xx = proc.StandardOutput.ReadToEnd();
            System.IO.File.Delete(tempFile);
            return xx;
        }
    }
}
