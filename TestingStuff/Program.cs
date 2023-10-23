using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VSSyntaxExtensions;

namespace TestingStuff
{
    public class Program
    {
        public static void Main(string[] args)
        //{
        //    MainAsync(args).Wait();
        //}
        //public async static Task MainAsync(string[] args)
        {
            var basePath = @"d:\Code\BeamTool-11Dev";
            //basePath = @"D:\Code\BeamTool-11Dev\Eclipse\ESBeamTool";
            var allFiles = Directory.GetFiles(basePath, "*.cs", SearchOption.AllDirectories);//.Select(x=>x.Substring(basePath.Length)).ToList();
            //var workspace = Helpers.GetWorkspace();
            //var sol = workspace.CurrentSolution;
            //var allFiles = sol.Projects.SelectMany(x => x.Documents.Select(y => y.FilePath)).ToList();

            var ss = string.Join(" ", allFiles);

            var file = string.Join(" ", allFiles.Take(2000).Select(x => "\"" + x + "\""));


            var search = "hover";
            var cts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                await Task.Delay(2000);
                cts.Cancel();
            });
            //DoGrepX(allFiles, search,
            //    s => Console.Write(s), cts.Token);

            var rr = VSSyntaxExtensions.DoGrep.DoSearch(allFiles, search, 20, cts.Token);

        }

        private static void DoGrepX(string[] fileNames, string search, Action<string> onResult, CancellationToken ct)
        {
            //return Task.Run(() =>
            {
                var startStuff = $"rg -i -n '{search}' ";

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
                foreach(var line in lines)
                {
                    if (ct.IsCancellationRequested)
                        break;
                    DoProc(onResult, line);
                }
                //if (result.Length > 0)
                //{
                //    Debug.WriteLine(result);
                //}

                Console.WriteLine(sw.Elapsed.ToString());
                Console.ReadLine();
            }//);
        }

        private static Stopwatch DoProc(Action<string> onResult, string command)
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
            proc.StartInfo.CreateNoWindow = false;
            proc.EnableRaisingEvents = true;

            proc.ErrorDataReceived += (o, e) =>
            {
                Debug.WriteLine(e.Data);
            };

            proc.OutputDataReceived += (o, e) =>
            {
                var results = new List<string>();
                onResult(e.Data);
            };
            proc.Start();
            proc.BeginOutputReadLine();

            //var result = proc.StandardOutput.ReadToEnd();
            //var err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            return sw;
        }
    }
}
