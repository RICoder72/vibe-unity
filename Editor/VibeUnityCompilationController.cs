#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace VibeUnity.Editor
{
    /// <summary>
    /// Simplified Unity compilation controller for external tool integration
    /// Provides real-time compilation status with persistent state files
    /// </summary>
    [InitializeOnLoad]
    public class VibeUnityCompilationController
    {
        // Compilation state
        private static List<CompilerMessage> compilationMessages = new List<CompilerMessage>();
        private static DateTime compilationStartTime;
        private static DateTime compilationEndTime;
        private static bool isCurrentlyCompiling = false;
        
        // Paths
        private static string projectHash;
        private static string compilationDir;
        private static string projectHashFile;
        private static string currentStatusFile;
        private static string lastErrorsFile;
        private static string commandQueueDir;
        
        // Constants
        private const string COMPILATION_DIR_NAME = "compilation";
        private const string STATUS_FILENAME = "current-status.json";
        private const string HASH_FILENAME = "project-hash.txt";
        private const string ERRORS_FILENAME = "last-errors.json";
        private const string COMMAND_QUEUE_DIR = "command-queue";
        
        static VibeUnityCompilationController()
        {
            Initialize();
        }
        
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            SetupPaths();
            SetupEventHandlers();
            WriteProjectHash();
            UpdateCurrentStatus("idle", 0, 0, new List<string>());
            ProcessCommandQueue();
            
            // Initialize settings system
            var settings = VibeUnitySettings.GetSettings();
            
            Debug.Log($"[VibeUnity] Compilation Controller initialized");
            Debug.Log($"[VibeUnity] Project Hash: {projectHash}");
            Debug.Log($"[VibeUnity] Status location: {currentStatusFile}");
            Debug.Log($"[VibeUnity] Settings loaded - Force Recompile: {ShortcutSettings.GetShortcutDescription(settings.shortcuts.forceRecompile)}");
        }
        
        private static void SetupPaths()
        {
            // Generate project hash from Application.dataPath
            string projectPath = Application.dataPath;
            projectHash = Math.Abs(projectPath.GetHashCode()).ToString("X8");
            
            // Setup directory paths
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string vibeUnityDir = Path.Combine(projectRoot, ".vibe-unity");
            compilationDir = Path.Combine(vibeUnityDir, COMPILATION_DIR_NAME);
            
            // Setup file paths
            projectHashFile = Path.Combine(compilationDir, HASH_FILENAME);
            currentStatusFile = Path.Combine(compilationDir, STATUS_FILENAME);
            lastErrorsFile = Path.Combine(compilationDir, ERRORS_FILENAME);
            commandQueueDir = Path.Combine(compilationDir, COMMAND_QUEUE_DIR);
            
            // Ensure directories exist
            Directory.CreateDirectory(compilationDir);
            Directory.CreateDirectory(commandQueueDir);
        }
        
        private static void SetupEventHandlers()
        {
            // Unsubscribe first to avoid duplicates
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            EditorApplication.update -= ProcessCommandQueue;
            
            // Subscribe to compilation events
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            
            // Process commands periodically
            EditorApplication.update += ProcessCommandQueue;
        }
        
        private static void WriteProjectHash()
        {
            try
            {
                File.WriteAllText(projectHashFile, projectHash);
                Debug.Log($"[VibeUnity] Project hash written to: {projectHashFile}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibeUnity] Failed to write project hash: {e.Message}");
            }
        }
        
        private static void OnCompilationStarted(object context)
        {
            isCurrentlyCompiling = true;
            compilationStartTime = DateTime.UtcNow;
            compilationMessages.Clear();
            
            Debug.Log($"[VibeUnity] Compilation started at {compilationStartTime:HH:mm:ss.fff}");
            UpdateCurrentStatus("compiling", 0, 0, new List<string> { "Compilation in progress..." });
        }
        
        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            // Collect all messages
            compilationMessages.AddRange(messages);
            
            int errors = messages.Count(m => m.type == CompilerMessageType.Error);
            int warnings = messages.Count(m => m.type == CompilerMessageType.Warning);
            
            if (errors > 0 || warnings > 0)
            {
                string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
                Debug.Log($"[VibeUnity] Assembly '{assemblyName}': {errors} errors, {warnings} warnings");
            }
            
            // Update status with current totals
            var (totalErrors, totalWarnings, details) = GetCompilationSummary();
            UpdateCurrentStatus("compiling", totalErrors, totalWarnings, details);
        }
        
        private static void OnCompilationFinished(object context)
        {
            isCurrentlyCompiling = false;
            compilationEndTime = DateTime.UtcNow;
            var duration = compilationEndTime - compilationStartTime;
            
            var (errors, warnings, details) = GetCompilationSummary();
            
            Debug.Log($"[VibeUnity] Compilation finished at {compilationEndTime:HH:mm:ss.fff}");
            Debug.Log($"[VibeUnity] Duration: {duration.TotalMilliseconds:F0}ms");
            Debug.Log($"[VibeUnity] Results: {errors} errors, {warnings} warnings");
            
            // Determine final status
            string status = errors > 0 ? "error" : "success";
            UpdateCurrentStatus(status, errors, warnings, details);
            
            // Save error details if there were any
            if (errors > 0 || warnings > 0)
            {
                SaveErrorDetails(errors, warnings, details);
            }
        }
        
        private static (int errors, int warnings, List<string> details) GetCompilationSummary()
        {
            int errorCount = 0;
            int warningCount = 0;
            var details = new List<string>();
            
            foreach (var message in compilationMessages)
            {
                if (message.type == CompilerMessageType.Error)
                {
                    errorCount++;
                    string detail = FormatCompilerMessage(message, "ERROR");
                    details.Add(detail);
                }
                else if (message.type == CompilerMessageType.Warning)
                {
                    warningCount++;
                    string detail = FormatCompilerMessage(message, "WARNING");
                    details.Add(detail);
                }
            }
            
            // If no messages but Unity reports failure, add generic error
            if (errorCount == 0 && EditorUtility.scriptCompilationFailed)
            {
                errorCount = 1;
                details.Add("ERROR: Script compilation failed (details not available)");
            }
            
            return (errorCount, warningCount, details);
        }
        
        private static string FormatCompilerMessage(CompilerMessage message, string type)
        {
            if (string.IsNullOrEmpty(message.file))
            {
                return $"{type}: {message.message}";
            }
            
            // Clean up file path for readability
            string cleanPath = message.file
                .Replace('\\', '/')
                .Replace(Application.dataPath, "Assets");
            
            return $"[{cleanPath}:{message.line}:{message.column}] {type}: {message.message}";
        }
        
        private static void UpdateCurrentStatus(string status, int errors, int warnings, List<string> details)
        {
            try
            {
                var statusData = new CompilationStatus
                {
                    status = status,
                    errors = errors,
                    warnings = warnings,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    projectHash = projectHash,
                    unityVersion = Application.unityVersion,
                    details = details.Take(50).ToArray() // Limit details to prevent huge files
                };
                
                string json = JsonUtility.ToJson(statusData, true);
                File.WriteAllText(currentStatusFile, json);

                // NOTE: status/compilation.json is intentionally NOT written here.
                // VibeUnitySystem's compilation tracker owns that file and writes it
                // with an exclusive file lock during compilation (the compile-check
                // script relies on that lock for its "compiling" detection). The
                // previous write here both raced with that writer and was broken -
                // JsonUtility cannot serialize an anonymous type, so it produced "{}".
                // This controller owns the richer current-status.json / last-errors.json.
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibeUnity] Failed to update status: {e.Message}");
            }
        }
        
        private static void SaveErrorDetails(int errors, int warnings, List<string> details)
        {
            try
            {
                var errorData = new
                {
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    errors = errors,
                    warnings = warnings,
                    details = details.ToArray()
                };
                
                string json = JsonUtility.ToJson(errorData, true);
                File.WriteAllText(lastErrorsFile, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibeUnity] Failed to save error details: {e.Message}");
            }
        }
        
        private static void ProcessCommandQueue()
        {
            if (!Directory.Exists(commandQueueDir))
                return;
            
            try
            {
                var commandFiles = Directory.GetFiles(commandQueueDir, "*.json")
                    .OrderBy(File.GetCreationTime)
                    .ToArray();
                
                foreach (string commandFile in commandFiles)
                {
                    try
                    {
                        string json = File.ReadAllText(commandFile);
                        var command = JsonUtility.FromJson<CompileCommand>(json);
                        
                        if (command != null)
                        {
                            ProcessCommand(command);
                        }
                        
                        File.Delete(commandFile);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[VibeUnity] Failed to process command {commandFile}: {e.Message}");
                        File.Delete(commandFile); // Remove corrupted file
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibeUnity] Error processing command queue: {e.Message}");
            }
        }
        
        private static void ProcessCommand(CompileCommand command)
        {
            Debug.Log($"[VibeUnity] Processing command: {command.action}");
            
            switch (command.action.ToLower())
            {
                case "force-compile":
                case "recompile":
                    ForceRecompile();
                    break;
                    
                case "check-status":
                    // Status is always current in the file, just log it
                    var (errors, warnings, _) = GetCompilationSummary();
                    Debug.Log($"[VibeUnity] Current status - Errors: {errors}, Warnings: {warnings}");
                    break;
                    
                case "clear-cache":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    Debug.Log("[VibeUnity] Asset cache cleared");
                    break;
                    
                default:
                    Debug.LogWarning($"[VibeUnity] Unknown command: {command.action}");
                    break;
            }
        }
        
        private static void ForceRecompile()
        {
            Debug.Log("[VibeUnity] Forcing recompilation...");
            
            // Method 1: Force asset refresh
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            
            // Method 2: Reimport all scripts
            AssetDatabase.ImportAsset("Assets", ImportAssetOptions.ImportRecursive);
            
            // Method 3: Request script compilation
            CompilationPipeline.RequestScriptCompilation();
        }
        
        // Menu Items with Dynamic Keyboard Shortcuts
        [MenuItem("Tools/Vibe Unity/Compilation/Check Status %#k")] // Default: Ctrl+Shift+K
        public static void CheckCompilationStatus()
        {
            var (errors, warnings, details) = GetCompilationSummary();
            
            Debug.Log("[VibeUnity] === Compilation Status ===");
            Debug.Log($"[VibeUnity] Status: {(isCurrentlyCompiling ? "Compiling..." : "Idle")}");
            Debug.Log($"[VibeUnity] Errors: {errors}");
            Debug.Log($"[VibeUnity] Warnings: {warnings}");
            Debug.Log($"[VibeUnity] Project Hash: {projectHash}");
            Debug.Log($"[VibeUnity] Status File: {currentStatusFile}");
            
            if (errors > 0 && details.Count > 0)
            {
                Debug.Log("[VibeUnity] First few errors:");
                foreach (var detail in details.Take(5))
                {
                    Debug.Log($"[VibeUnity]   {detail}");
                }
            }
        }
        
        [MenuItem("Tools/Vibe Unity/Compilation/Force Recompile %#u")] // Default: Ctrl+Shift+U
        public static void MenuForceRecompile()
        {
            ForceRecompile();
        }
        
        [MenuItem("Tools/Vibe Unity/Compilation/Clear Cache %#l")] // Default: Ctrl+Shift+L
        public static void ClearCompilationCache()
        {
            compilationMessages.Clear();
            UpdateCurrentStatus("idle", 0, 0, new List<string>());
            
            if (File.Exists(lastErrorsFile))
            {
                File.Delete(lastErrorsFile);
            }
            
            Debug.Log("[VibeUnity] Compilation cache cleared");
        }
        
        [MenuItem("Tools/Vibe Unity/Compilation/Open Status Directory")]
        public static void OpenStatusDirectory()
        {
            if (Directory.Exists(compilationDir))
            {
                EditorUtility.RevealInFinder(compilationDir);
            }
            else
            {
                Debug.LogError($"[VibeUnity] Status directory not found: {compilationDir}");
            }
        }
        
        // Public API for backward compatibility
        public static string GetProjectHash() => projectHash;
        public static bool IsCompiling() => isCurrentlyCompiling;
        public static void TriggerCompilation() => ForceRecompile();
        
        // Settings integration
        public static void UpdateShortcuts(ShortcutSettings shortcuts)
        {
            // Note: Unity's MenuItem shortcuts are compile-time only
            // This method is for future dynamic shortcut support
            Debug.Log($"[VibeUnity] Shortcuts updated - Check Status: {ShortcutSettings.GetShortcutDescription(shortcuts.checkStatus)}");
            Debug.Log($"[VibeUnity] Shortcuts updated - Force Recompile: {ShortcutSettings.GetShortcutDescription(shortcuts.forceRecompile)}");  
            Debug.Log($"[VibeUnity] Shortcuts updated - Clear Cache: {ShortcutSettings.GetShortcutDescription(shortcuts.clearCache)}");
        }
        
        // Data structures
        [Serializable]
        private class CompilationStatus
        {
            public string status;
            public int errors;
            public int warnings;
            public long timestamp;
            public string projectHash;
            public string unityVersion;
            public string[] details;
        }
        
        [Serializable]
        private class CompileCommand
        {
            public string action;
            public string requestId;
            public long timestamp;
        }
    }
}
#endif