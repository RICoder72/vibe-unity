#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System;

namespace VibeUnity.Editor
{
    /// <summary>
    /// Automatically updates CLAUDE.md with current Vibe Unity integration information
    /// </summary>
    public static class VibeUnityDocumentationUpdater
    {
        private const string SECTION_START_MARKER = "⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄VIBE-UNITY⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄";
        private const string SECTION_END_MARKER = "^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^VIBE-UNITY^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^";
        private const string VERSION_PATTERN = @"v(\d+\.\d+\.\d+)";
        
        /// <summary>
        /// Initialize documentation updater on Unity startup
        /// </summary>
        private const string VERSION_TRACKING_KEY = "VibeUnity_LastDocumentedVersion";
        
        [InitializeOnLoadMethod]
        public static void InitializeDocumentationUpdater()
        {
            // Run after a short delay to ensure other systems are initialized
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // Check if package version has changed
                    string currentVersion = GetPackageVersion();
                    string lastDocumentedVersion = EditorPrefs.GetString(VERSION_TRACKING_KEY, "");
                    
                    if (currentVersion != lastDocumentedVersion)
                    {
                        if (UpdateClaudeMdDocumentation())
                        {
                            // Save the version we just documented
                            EditorPrefs.SetString(VERSION_TRACKING_KEY, currentVersion);
                            Debug.Log($"[VibeUnity] Updated CLAUDE.md documentation for version {currentVersion}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[VibeUnity] Failed to update CLAUDE.md documentation: {e.Message}");
                }
            };
        }
        
        /// <summary>
        /// Updates the CLAUDE.md file with current Vibe Unity integration information
        /// </summary>
        public static bool UpdateClaudeMdDocumentation()
        {
            try
            {
                // Find CLAUDE.md file in project root
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string claudeMdPath = Path.Combine(projectRoot, "CLAUDE.md");
                
                if (!File.Exists(claudeMdPath))
                {
                    Debug.Log("[VibeUnity] CLAUDE.md not found - creating new file");
                    // Create a basic CLAUDE.md file with project instructions
                    string initialContent = @"# Project Instructions

This Unity project uses Vibe Unity for automated development workflows.

## About Vibe Unity
Vibe Unity enables claude-code integration for Unity scene creation and project automation.

";
                    File.WriteAllText(claudeMdPath, initialContent);
                    Debug.Log($"[VibeUnity] Created new CLAUDE.md file at: {claudeMdPath}");
                }
                
                // Get current package version
                string packageVersion = GetPackageVersion();
                if (string.IsNullOrEmpty(packageVersion))
                {
                    Debug.LogWarning("[VibeUnity] Could not determine package version - skipping documentation update");
                    return false;
                }
                
                // Read existing content
                string existingContent = File.ReadAllText(claudeMdPath);
                
                // Check if update is needed
                if (!NeedsUpdate(existingContent, packageVersion))
                {
                    Debug.Log($"[VibeUnity] CLAUDE.md documentation is up to date (v{packageVersion})");
                    return true;
                }
                
                // Generate new documentation section
                string newSection = GenerateDocumentationSection(packageVersion);

                // Update the file
                string updatedContent = UpdateDocumentationSection(existingContent, newSection);

                // If the updater declined to modify (e.g. malformed markers), leave
                // the file untouched rather than risk destroying user content.
                if (ReferenceEquals(updatedContent, existingContent) || updatedContent == existingContent)
                {
                    return false;
                }

                // Defensive backup before overwriting the user's CLAUDE.md.
                try { File.Copy(claudeMdPath, claudeMdPath + ".bak", true); }
                catch (Exception be) { Debug.LogWarning($"[VibeUnity] Could not write CLAUDE.md.bak: {be.Message}"); }

                // Write back to file
                File.WriteAllText(claudeMdPath, updatedContent);
                
                Debug.Log($"[VibeUnity] ✅ Updated CLAUDE.md with Vibe Unity integration guide (v{packageVersion})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibeUnity] Error updating CLAUDE.md: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Gets the current package version from package.json
        /// </summary>
        public static string GetPackageVersion()
        {
            try
            {
                // Resolve via the package manager rather than a hardcoded path + regex,
                // which broke when the package id/location differed from the assumption.
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VibeUnityDocumentationUpdater).Assembly);
                if (pkg != null && !string.IsNullOrEmpty(pkg.version))
                    return pkg.version;

                Debug.LogWarning("[VibeUnity] Could not resolve package version via PackageInfo.");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VibeUnity] Error resolving package version: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Checks if the documentation section needs updating
        /// </summary>
        private static bool NeedsUpdate(string existingContent, string currentVersion)
        {
            // If section doesn't exist, we need to add it
            if (!existingContent.Contains(SECTION_START_MARKER))
                return true;
            
            // Check if version has changed
            var versionMatch = Regex.Match(existingContent, $@"{SECTION_START_MARKER}.*?{VERSION_PATTERN}", RegexOptions.Singleline);
            if (!versionMatch.Success)
                return true;
                
            string existingVersion = versionMatch.Groups[1].Value;
            return existingVersion != currentVersion;
        }
        
        /// <summary>
        /// Generates the complete documentation section
        /// </summary>
        private static string GenerateDocumentationSection(string version)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄VIBE-UNITY⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄⌄");
            sb.AppendLine();
            sb.AppendLine($"# Vibe Unity Integration Guide (Auto-generated - v{version})");
            sb.AppendLine();
            sb.AppendLine("## Claude-Code Automated Workflow");
            sb.AppendLine();
            sb.AppendLine("### Primary Development Pattern");
            sb.AppendLine("```bash");
            sb.AppendLine("# 1. Validate compilation after code changes");
            sb.AppendLine("./claude-compile-check.sh");
            sb.AppendLine("# Exit codes: 0=success, 1=errors, 2=timeout, 3=script error");
            sb.AppendLine("");
            sb.AppendLine("# 2. Create scenes via JSON (automatic processing)");
            sb.AppendLine("echo '{\"action\":\"create-scene\",\"name\":\"TestScene\",\"path\":\"Assets/Scenes\"}' > .vibe-unity/commands/test.json");
            sb.AppendLine("");
            sb.AppendLine("# 3. Verify results (check logs after 3 seconds)");
            sb.AppendLine("sleep 3 && cat .vibe-unity/commands/logs/latest.log");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("### Automated Success/Failure Detection");
            sb.AppendLine("- ✅ **Success Indicators**: Log contains \"Scene created successfully\" or \"STATUS: SUCCESS\"");
            sb.AppendLine("- ❌ **Failure Indicators**: Log contains \"ERROR\", \"FAILED\", or compilation errors");
            sb.AppendLine("- 🔄 **Claude Action**: On failure, immediately report specific error and stop workflow");
            sb.AppendLine();
            sb.AppendLine("### File Locations for Claude-Code");
            sb.AppendLine("- **Compilation Check**: `./claude-compile-check.sh` (auto-installed)");
            sb.AppendLine("- **JSON Commands**: Drop files in `.vibe-unity/commands/` directory");
            sb.AppendLine("- **Log Verification**: Check `.vibe-unity/commands/logs/latest.log`");
            sb.AppendLine("- **Coverage Reports**: `.vibe-unity/commands/coverage-analysis/`");
            sb.AppendLine("- **Test Template**: `.vibe-unity/commands/test-scene-creation.json`");
            sb.AppendLine();
            sb.AppendLine($"### Current Component Support (v{version})");
            sb.AppendLine("- ✅ **UI**: Canvas, Button, Text, Image, ScrollView, TextMeshPro");
            sb.AppendLine("- ✅ **3D**: Cube, Sphere, Plane, Cylinder, Capsule, Camera, Light");
            sb.AppendLine("- ⚠️ **Partial**: Rigidbody, Colliders");
            sb.AppendLine("- ❌ **Missing**: ParticleSystem, custom scripts, animations");
            sb.AppendLine();
            sb.AppendLine("### JSON Command Examples for Claude-Code");
            sb.AppendLine("```json");
            sb.AppendLine("// Basic scene creation");
            sb.AppendLine("{\"action\":\"create-scene\",\"name\":\"MyScene\",\"path\":\"Assets/Scenes\"}");
            sb.AppendLine("");
            sb.AppendLine("// Multiple commands in batch file");
            sb.AppendLine("{");
            sb.AppendLine("  \"commands\": [");
            sb.AppendLine("    {\"action\":\"create-scene\",\"name\":\"MenuScene\",\"path\":\"Assets/Scenes/UI\"},");
            sb.AppendLine("    {\"action\":\"add-canvas\",\"name\":\"MainCanvas\"},");
            sb.AppendLine("    {\"action\":\"add-button\",\"name\":\"PlayButton\",\"parent\":\"MainCanvas\",\"text\":\"Play\"}");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine("");
            sb.AppendLine("// Add 3D objects");
            sb.AppendLine("{\"action\":\"add-cube\",\"name\":\"TestCube\",\"position\":[0,1,0],\"scale\":[2,2,2]}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("### Claude-Code Decision Tree");
            sb.AppendLine("1. **After C# changes**: Run `./claude-compile-check.sh`");
            sb.AppendLine("   - Exit code 0: Proceed with scene creation");
            sb.AppendLine("   - Exit code 1: Fix compilation errors immediately, report to user");
            sb.AppendLine("   - Exit code 2+: Report timeout/system issues to user");
            sb.AppendLine();
            sb.AppendLine("2. **For scene operations**: Use JSON commands with automatic verification");
            sb.AppendLine("   - Success: Continue workflow");
            sb.AppendLine("   - Failure: Report specific error from logs, ask user for guidance");
            sb.AppendLine();
            sb.AppendLine("3. **Error Handling**: ");
            sb.AppendLine("   - **Compilation errors**: STOP and fix errors");
            sb.AppendLine("   - **Scene creation failures**: STOP, report error, ask user to check Unity Console");
            sb.AppendLine("   - **Missing components**: Note in summary, continue with supported components");
            sb.AppendLine();
            sb.AppendLine("### Development Workflow Status");
            sb.AppendLine("- **File Watcher**: ✅ ENABLED (automatic JSON processing)");
            sb.AppendLine("- **Compilation Check**: ✅ AUTOMATED (`./claude-compile-check.sh`)");
            sb.AppendLine("- **Log Verification**: ✅ AUTOMATED (structured log parsing)");
            sb.AppendLine("- **Error Detection**: ✅ AUTOMATED (exit codes + log analysis)");
            sb.AppendLine();
            sb.AppendLine("## Automated Claude Instructions");
            sb.AppendLine("* **ALWAYS** run `./claude-compile-check.sh` after modifying C# scripts");
            sb.AppendLine("* **ONLY proceed** if compilation check returns exit code 0");
            sb.AppendLine("* **VERIFY scene creation** by checking `.vibe-unity/commands/logs/latest.log` for success/error messages");
            sb.AppendLine("* **REPORT failures immediately** with specific error details from logs");
            sb.AppendLine("* **DO NOT** create .meta files unless explicitly requested");
            sb.AppendLine("* **ASK USER** for guidance only when encountering system-level failures or unsupported features");
            sb.AppendLine();
            sb.AppendLine("## For Detailed Usage");
            sb.AppendLine("- **Full Documentation**: [Package README](./Packages/com.ricoder.vibe-unity/README.md)");
            sb.AppendLine("- **JSON Schema Examples**: [Package Test Files](./Packages/com.ricoder.vibe-unity/.vibe-unity/commands/)");
            sb.AppendLine("- **Coverage Analysis**: Check latest report in `.vibe-unity/commands/coverage-analysis/`");
            sb.AppendLine();
            sb.AppendLine("^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^VIBE-UNITY^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Updates or adds the documentation section in the existing content
        /// </summary>
        private static string UpdateDocumentationSection(string existingContent, string newSection)
        {
            // Find existing section boundaries
            int sectionStart = existingContent.IndexOf(SECTION_START_MARKER);
            
            if (sectionStart >= 0)
            {
                // Find the end of the existing section
                int sectionEndSearch = existingContent.IndexOf(SECTION_END_MARKER, sectionStart);
                if (sectionEndSearch >= 0)
                {
                    // Find the end of the line containing the end marker
                    int sectionEnd = existingContent.IndexOf('\n', sectionEndSearch);
                    if (sectionEnd >= 0)
                        sectionEnd++; // Include the newline
                    else
                        sectionEnd = existingContent.Length;
                    
                    // Replace the existing section
                    return existingContent.Substring(0, sectionStart) + 
                           newSection + 
                           (sectionEnd < existingContent.Length ? "\n" + existingContent.Substring(sectionEnd) : "");
                }
                else
                {
                    // Section start found but NO end marker: the file is malformed.
                    // Previously this truncated everything after the start marker,
                    // silently destroying any user content below it. Refuse instead
                    // and leave the file untouched (caller skips the write).
                    Debug.LogWarning("[VibeUnity] CLAUDE.md has a Vibe Unity start marker but no end marker; " +
                                     "refusing to modify to avoid deleting user content. Fix or remove the marker.");
                    return existingContent;
                }
            }
            else
            {
                // No existing section - add at the end
                return existingContent.TrimEnd() + "\n\n" + newSection + "\n";
            }
        }
        
        /// <summary>
        /// Force documentation update (called from menu in VibeUnityMenu.cs)
        /// </summary>
        public static void ForceUpdateDocumentation()
        {
            if (UpdateClaudeMdDocumentation())
            {
                EditorUtility.DisplayDialog("Documentation Update", 
                    "CLAUDE.md has been updated with current Vibe Unity integration information.", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Documentation Update Failed", 
                    "Failed to update CLAUDE.md. Check the console for error details.", 
                    "OK");
            }
        }
    }
}
#endif