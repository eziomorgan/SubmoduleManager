using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductManager
{
    public class GitSubmodule
    {
        private readonly Action<string> _logger;

        public string Name { get; set; }
        public string Path { get; set; }
        public string GitDir { get; set; }
        public string CurrentBranch { get; set; }
        public string[] Branches { get; set; } = Array.Empty<string>();

        public GitSubmodule(Action<string> logger = null)
        {
            _logger = logger;
        }

        public void CheckoutBranch(string branchName)
        {
            RunGit($"checkout {branchName}", Path);
            CurrentBranch = branchName;
        }

        public void PullLatest()
        {
            RunGit("pull --ff-only", Path);
        }

        public void RefreshBranches()
        {
            var output = RunGitWithOutput("branch --format=%(refname:short)", Path);
            if (!string.IsNullOrWhiteSpace(output))
            {
                Branches = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(s => s.Trim('*', ' ', '\t'))
                                  .Distinct()
                                  .OrderBy(b => b)
                                  .ToArray();
                return;
            }

            if (string.IsNullOrEmpty(GitDir) || !Directory.Exists(GitDir))
            {
                Branches = Array.Empty<string>();
                return;
            }

            var list = new List<string>();
            var headsDir = System.IO.Path.Combine(GitDir, "refs", "heads");
            if (Directory.Exists(headsDir))
            {
                foreach (var file in Directory.EnumerateFiles(headsDir, "*", SearchOption.AllDirectories))
                {
                    var rel = file.Substring(headsDir.Length + 1).Replace('\\', '/');
                    list.Add(rel);
                }
            }

            var packed = System.IO.Path.Combine(GitDir, "packed-refs");
            if (File.Exists(packed))
            {
                foreach (var line in File.ReadLines(packed))
                {
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(' ');
                    if (parts.Length == 2 && parts[1].StartsWith("refs/heads/"))
                    {
                        list.Add(parts[1].Substring("refs/heads/".Length));
                    }
                }
            }

            Branches = list.Distinct().OrderBy(b => b).ToArray();
        }

        private void RunGit(string args, string workingDir)
        {
            _logger?.Invoke($"> git {args} [{workingDir}]");

            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            try
            {
                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                var error = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (!string.IsNullOrWhiteSpace(output)) _logger?.Invoke(output);
                if (!string.IsNullOrWhiteSpace(error)) _logger?.Invoke(error);
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"[ERROR] git {args}: {ex.Message}");
            }
        }

        private string RunGitWithOutput(string args, string workingDir)
        {
            _logger?.Invoke($"> git {args} [{workingDir}]");

            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            try
            {
                p.Start();
                var s = p.StandardOutput.ReadToEnd();
                var error = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (!string.IsNullOrWhiteSpace(error)) _logger?.Invoke(error);
                return s;
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"[ERROR] git {args}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
