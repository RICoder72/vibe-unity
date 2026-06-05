#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace VibeUnity.Editor
{
    /// <summary>
    /// Handles automatic setup of Vibe Unity when package is imported
    /// </summary>
    [InitializeOnLoad]
    public static class VibeUnitySetup
    {
        // Project-scoped key. EditorPrefs is machine-global, so an unqualified key
        // would let the first project that runs setup mark EVERY other project on
        // the machine as "already set up", silently skipping their script install.
        private static string SetupCompleteKey => $"VibeUnity_SetupComplete_{ProjectId()}";

        static VibeUnitySetup()
        {
            // Only run setup once per project
            if (!EditorPrefs.GetBool(SetupCompleteKey, false))
            {
                EditorApplication.delayCall += RunSetup;
            }
        }

        private static void RunSetup()
        {
            Debug.Log("[Vibe Unity] Setting up CLI tools...");

            var report = new List<string>();
            bool essentialInstalled = CopyBashScriptsToProjectRoot(report);

            // Only remember success when the essential script actually landed, so a
            // failed/partial setup is retried on the next reload rather than skipped
            // forever (previously setup was marked complete unconditionally).
            if (essentialInstalled)
            {
                EditorPrefs.SetBool(SetupCompleteKey, true);
            }

            ShowWelcomeDialog(essentialInstalled, report);
        }

        /// <returns>true if the essential claude-compile-check.sh was installed.</returns>
        private static bool CopyBashScriptsToProjectRoot(List<string> report)
        {
            bool essentialInstalled = false;
            try
            {
                string packagePath = GetPackagePath();
                if (string.IsNullOrEmpty(packagePath))
                {
                    report.Add("FAILED: could not locate the Vibe Unity package path");
                    Debug.LogWarning("[Vibe Unity] Could not locate package path; no scripts copied.");
                    return false;
                }

                string scriptsPath = Path.Combine(packagePath, "Scripts");
                string runtimePath = Path.Combine(packagePath, "Runtime");
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;

                // 1) claude-compile-check.sh lives in Scripts/ -> project root (ESSENTIAL)
                string sourceCompileCheck = Path.Combine(scriptsPath, "claude-compile-check.sh");
                string targetCompileCheck = Path.Combine(projectRoot, "claude-compile-check.sh");
                if (File.Exists(sourceCompileCheck))
                {
                    WriteWithUnixEndings(sourceCompileCheck, targetCompileCheck);
                    MakeExecutable(targetCompileCheck);
                    AddToGitIgnore(projectRoot, "claude-compile-check.sh");
                    essentialInstalled = true;
                    report.Add("OK: claude-compile-check.sh");
                    Debug.Log($"[Vibe Unity] Installed compile-check script: {targetCompileCheck}");
                }
                else
                {
                    report.Add($"MISSING SOURCE: {sourceCompileCheck}");
                    Debug.LogWarning($"[Vibe Unity] Essential source script not found: {sourceCompileCheck}");
                }

                // 2) vibe-unity CLI lives in Runtime/ (NOT Scripts/) -> project root
                string sourceScript = Path.Combine(runtimePath, "vibe-unity");
                string targetScript = Path.Combine(projectRoot, "vibe-unity");
                if (File.Exists(sourceScript))
                {
                    WriteWithUnixEndings(sourceScript, targetScript);
                    MakeExecutable(targetScript);
                    report.Add("OK: vibe-unity");
                    Debug.Log($"[Vibe Unity] Installed CLI script: {targetScript}");
                }
                else
                {
                    report.Add($"SKIPPED: vibe-unity not found at {sourceScript}");
                }

                // 3) install-vibe-unity lives in Runtime/ -> project Scripts/
                string sourceInstaller = Path.Combine(runtimePath, "install-vibe-unity");
                if (File.Exists(sourceInstaller))
                {
                    string targetScriptsDir = Path.Combine(projectRoot, "Scripts");
                    Directory.CreateDirectory(targetScriptsDir);
                    WriteWithUnixEndings(sourceInstaller, Path.Combine(targetScriptsDir, "install-vibe-unity"));
                    report.Add("OK: install-vibe-unity");
                }
                else
                {
                    report.Add($"SKIPPED: install-vibe-unity not found at {sourceInstaller}");
                }

                return essentialInstalled;
            }
            catch (System.Exception e)
            {
                report.Add($"ERROR: {e.Message}");
                Debug.LogWarning($"[Vibe Unity] Could not copy bash scripts: {e.Message}");
                return essentialInstalled;
            }
        }

        private static void WriteWithUnixEndings(string sourcePath, string targetPath)
        {
            string content = File.ReadAllText(sourcePath).Replace("\r\n", "\n").Replace("\r", "\n");
            File.WriteAllText(targetPath, content);
        }

        private static void MakeExecutable(string path)
        {
            // chmod only matters on Unix-like editors (Linux/macOS). On Windows the
            // bit is irrelevant and WSL/git-bash run the file regardless.
            if (System.Environment.OSVersion.Platform != System.PlatformID.Unix) return;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("chmod", $"+x \"{path}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(5000);
                    if (proc.ExitCode != 0)
                        Debug.LogWarning($"[Vibe Unity] chmod +x returned {proc.ExitCode} for {path}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Vibe Unity] chmod failed for {path}: {e.Message}");
            }
        }

        public static void AddToGitIgnore(string projectRoot, string fileName)
        {
            try
            {
                string gitIgnorePath = Path.Combine(projectRoot, ".gitignore");
                if (File.Exists(gitIgnorePath))
                {
                    // Match a full trimmed line, not a substring, so a comment or a
                    // longer path containing the name doesn't cause a false "present".
                    bool present = false;
                    foreach (string line in File.ReadAllLines(gitIgnorePath))
                    {
                        if (line.Trim() == fileName) { present = true; break; }
                    }
                    if (!present)
                    {
                        File.AppendAllText(gitIgnorePath, $"\n# Vibe Unity - auto-generated script with Unix line endings\n{fileName}\n");
                        Debug.Log($"[Vibe Unity] Added {fileName} to .gitignore");
                    }
                }
                else
                {
                    File.WriteAllText(gitIgnorePath, $"# Vibe Unity - auto-generated script with Unix line endings\n{fileName}\n");
                    Debug.Log($"[Vibe Unity] Created .gitignore and added {fileName}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Vibe Unity] Could not update .gitignore: {e.Message}");
            }
        }

        private static string GetPackagePath()
        {
            // Resolve the real installed location regardless of install method
            // (git URL, local file:, or PackageCache) instead of guessing hardcoded,
            // version-pinned, and wrongly-named paths.
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VibeUnitySetup).Assembly);
            if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath) && Directory.Exists(pkg.resolvedPath))
                return pkg.resolvedPath;

            // Fallback for an embedded copy under Assets/.
            string local = Path.Combine(Application.dataPath, "VibeUnity");
            return Directory.Exists(local) ? local : null;
        }

        private static string ProjectId()
        {
            // Deterministic short id from the project path (GetHashCode is not stable
            // across runs and must not be used for a persisted key).
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Application.dataPath));
                return System.BitConverter.ToString(hash, 0, 4).Replace("-", "");
            }
        }

        private static void ShowWelcomeDialog(bool essentialInstalled, List<string> report)
        {
            EditorApplication.delayCall += () =>
            {
                string summary = string.Join("\n", report.ToArray());
                if (essentialInstalled)
                {
                    bool open = EditorUtility.DisplayDialog(
                        "Vibe Unity",
                        "Vibe Unity CLI tools installed.\n\n" +
                        summary + "\n\n" +
                        "Usage:\n" +
                        "- Drop JSON commands in .vibe-unity/commands/\n" +
                        "- Run ./claude-compile-check.sh to validate compilation\n" +
                        "- Use the Tools > Vibe Unity menu\n\n" +
                        "Open the documentation?",
                        "Open Documentation",
                        "Close");
                    if (open)
                        Application.OpenURL("https://github.com/RICoder72/vibe-unity#readme");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Vibe Unity - Setup Incomplete",
                        "Vibe Unity could not install its essential CLI script.\n\n" +
                        summary + "\n\n" +
                        "Setup will retry on the next editor reload. See the Console for details.",
                        "OK");
                }
            };
        }
    }
}
#endif
