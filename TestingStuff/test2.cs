using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestingStuff
{
    public class test2
    {
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

        public static void Testx(string files_path, string searchString)
        {

            var proc = new Process();
            var scriptArguments = $"SplitAndRunAgainstFilePaths \"{files_path}\" " + CMD + "\"" + searchString + "\" " + CMD2;
            proc.StartInfo.FileName = "powershell.exe";

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
                //if (e.Data == null)
                //    return;
                Console.WriteLine(e.Data);
            };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.WaitForExit();
        }
        public static string Test(string files_path, string searchString)
        {
            var command = $"SplitAndRunAgainstFilePaths \"{files_path}\" " + CMD + "\"" + searchString + "\" " + CMD2 +" | echo";
            command = TTT + "\n" + command;
            var tempFile = Path.GetTempFileName() + ".ps1";
            System.IO.File.WriteAllText(tempFile, command);
            var proc = new Process();
            var scriptArguments = "-NoProfile -w Maximized -ExecutionPolicy Bypass -File \"" + tempFile + "\"";
            proc.StartInfo.FileName = "pwsh";

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
        public static void DoSearch2(string files_path, string cmd)
        {

            var fileNames = File.ReadAllLines(files_path);
            var maxLineLen = 30000;
            var sb = new StringBuilder(cmd, maxLineLen);
            foreach (var f in fileNames)
            {
                if (sb.Length + f.Length > maxLineLen)
                {
                    Console.WriteLine(cmd + " " + sb.ToString());
                    DoProc2(sb.ToString());
                    sb = new StringBuilder(cmd, maxLineLen);
                }
                sb.Append($" \"{f}\"");
            }
            Console.WriteLine(cmd + " " + sb.ToString());
        }
        private static void DoProc2(string command)
        {
            var tempFile = Path.GetTempFileName() + ".ps1";
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
                //if (e.Data == null)
                //    return;
                Console.WriteLine(e.Data);
            };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.WaitForExit();
            System.IO.File.Delete(tempFile);
        }
    }
}
