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
            if (string.IsNullOrWhiteSpace(branchName))
                return;

            if (branchName.Contains('/'))
            {
                var local = branchName.Substring(branchName.IndexOf('/') + 1);
                RunGit($"checkout --track -B {local} {branchName}", Path);
                CurrentBranch = local;
            }
            else
            {
                RunGit($"checkout {branchName}", Path);
                CurrentBranch = branchName;
            }
        }

        public void PullLatest()
        {
            RunGit("pull --ff-only", Path);
        }

        public void RefreshBranches()
        {
            var output = RunGitWithOutput("branch --format=%(refname:short) --all", Path);
            if (!string.IsNullOrWhiteSpace(output))
            {
                var entries = new List<(string Name, bool IsRemote)>();
                foreach (var raw in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var cleaned = raw.Trim('*', ' ', '\t');
                    if (string.IsNullOrWhiteSpace(cleaned) || cleaned == "HEAD" || cleaned.EndsWith("/HEAD") || cleaned.Contains(" -> "))
                        continue;

                    var isRemote = cleaned.StartsWith("remotes/");
                    var name = isRemote ? cleaned.Substring("remotes/".Length) : cleaned;
                    if (string.IsNullOrWhiteSpace(name) || name.EndsWith("/HEAD"))
                        continue;

                    entries.Add((name, isRemote));
                }

                Branches = SortBranches(entries);
                return;
            }

            if (string.IsNullOrEmpty(GitDir) || !Directory.Exists(GitDir))
            {
                Branches = Array.Empty<string>();
                return;
            }

            var branchEntries = new List<(string Name, bool IsRemote)>();
            var headsDir = System.IO.Path.Combine(GitDir, "refs", "heads");
            if (Directory.Exists(headsDir))
            {
                foreach (var file in Directory.EnumerateFiles(headsDir, "*", SearchOption.AllDirectories))
                {
                    var rel = file.Substring(headsDir.Length + 1).Replace('\\', '/');
                    branchEntries.Add((rel, false));
                }
            }

            var remotesDir = System.IO.Path.Combine(GitDir, "refs", "remotes");
            if (Directory.Exists(remotesDir))
            {
                foreach (var file in Directory.EnumerateFiles(remotesDir, "*", SearchOption.AllDirectories))
                {
                    var rel = file.Substring(remotesDir.Length + 1).Replace('\\', '/');
                    if (!rel.EndsWith("/HEAD"))
                        branchEntries.Add((rel, true));
                }
            }

            var packed = System.IO.Path.Combine(GitDir, "packed-refs");
            if (File.Exists(packed))
            {
                foreach (var line in File.ReadLines(packed))
                {
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(' ');
                    if (parts.Length == 2)
                    {
                        if (parts[1].StartsWith("refs/heads/"))
                        {
                            branchEntries.Add((parts[1].Substring("refs/heads/".Length), false));
                        }
                        else if (parts[1].StartsWith("refs/remotes/"))
                        {
                            var rel = parts[1].Substring("refs/remotes/".Length);
                            if (!rel.EndsWith("/HEAD"))
                                branchEntries.Add((rel, true));
                        }
                    }
                }
            }

            Branches = SortBranches(branchEntries);
        }

        private static string[] SortBranches(IEnumerable<(string Name, bool IsRemote)> branches)
        {
            if (branches == null)
                return Array.Empty<string>();

            return branches
                .GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Name = g.Key, IsRemote = g.Any(b => b.IsRemote) })
                .OrderBy(b => b.IsRemote ? 0 : 1)
                .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .Select(b => b.Name)
                .ToArray();
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
