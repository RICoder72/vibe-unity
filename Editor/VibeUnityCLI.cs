#region File Documentation
/// <summary>
/// VIBEUNITYCLI.CS - Command Line Interface Tools for Unity Development
/// 
/// PURPOSE:
/// Provides CLI-based tools for Unity development workflow automation including scene creation,
/// canvas management, and project structure operations. Designed for rapid development workflows
/// and integration with external automation tools and scripts.
/// 
/// KEY FEATURES:
/// • CLI-based scene creation using Unity's built-in scene templates
/// • Canvas creation and management with configurable parameters
/// • Scene type discovery and listing functionality
/// • Project structure analysis and export capabilities
/// • Command-line driven workflow for automation and scripting
/// • Support for Unity's NewSceneSetup modes (Empty, DefaultGameObjects, etc.)
/// • Integration with Unity's scene template system for consistent setups
/// 
/// CLI USAGE DOCUMENTATION:
/// ========================
/// 
/// SCENE CREATION:
/// ---------------
/// Create scenes using Unity's built-in scene templates:
/// 
/// // Create empty scene
/// VibeUnity.CLI.CreateScene("MyScene", "Assets/Scenes", "Empty");
/// 
/// // Create scene with default GameObjects
/// VibeUnity.CLI.CreateScene("GameScene", "Assets/Scenes/Game", "DefaultGameObjects");
/// 
/// // Create scene with specific template
/// VibeUnity.CLI.CreateScene("UIScene", "Assets/Scenes/UI", "2D");
/// 
/// Available Scene Types (use --listtypes to see current install):
/// • Empty - Completely empty scene
/// • DefaultGameObjects - Scene with Camera and Light
/// • 2D - 2D optimized scene setup
/// • 3D - 3D optimized scene setup
/// • URP - Universal Render Pipeline scene
/// • HDRP - High Definition Render Pipeline scene
/// • VR - Virtual Reality scene setup
/// • AR - Augmented Reality scene setup
/// 
/// LIST SCENE TYPES:
/// -----------------
/// Get available scene types for current Unity installation:
/// 
/// VibeUnity.CLI.ListSceneTypes();
/// // Output: Available scene types: Empty, DefaultGameObjects, 2D, 3D, URP, HDRP
/// 
/// CANVAS CREATION:
/// ----------------
/// Add canvas to existing scene with configurable parameters:
/// 
/// // Basic canvas
/// VibeUnity.CLI.AddCanvas("MyCanvas", "ScreenSpaceOverlay");
/// 
/// // Canvas with custom settings
/// VibeUnity.CLI.AddCanvas("UICanvas", "ScreenSpaceOverlay", 1920, 1080, "ScaleWithScreenSize");
/// 
/// // World space canvas
/// VibeUnity.CLI.AddCanvas("WorldCanvas", "WorldSpace", 100, 100);
/// 
/// Canvas Parameters:
/// • canvasName: Name for the canvas GameObject
/// • renderMode: "ScreenSpaceOverlay", "ScreenSpaceCamera", "WorldSpace"
/// • referenceWidth: Reference resolution width (default: 1920)
/// • referenceHeight: Reference resolution height (default: 1080)
/// • scaleMode: "ConstantPixelSize", "ScaleWithScreenSize", "ConstantPhysicalSize"
/// • sortingOrder: Canvas sorting order (default: 0)
/// • worldPosition: Position for WorldSpace canvas (Vector3, default: Vector3.zero)
/// 
/// INTEGRATION EXAMPLES:
/// =====================
/// 
/// BATCH SCENE CREATION:
/// foreach(string sceneName in sceneList) {
///     VibeUnity.CLI.CreateScene(sceneName, basePath, "DefaultGameObjects");
/// }
/// 
/// AUTOMATED SETUP:
/// VibeUnity.CLI.CreateScene("MainMenu", "Assets/Scenes/UI", "2D");
/// VibeUnity.CLI.AddCanvas("MenuCanvas", "ScreenSpaceOverlay", 1920, 1080, "ScaleWithScreenSize");
/// 
/// EXTERNAL SCRIPT INTEGRATION:
/// // Call from external tools or build scripts
/// var cliType = System.Type.GetType("VibeUnity.Editor.CLI");
/// var createMethod = cliType.GetMethod("CreateScene");
/// createMethod.Invoke(null, new object[] { "TestScene", "Assets/Testing", "Empty" });
/// 
/// ERROR HANDLING:
/// ===============
/// All methods return boolean success indicators and log detailed error messages.
/// Check Unity Console for detailed operation logs and error information.
/// 
/// ARCHITECTURE NOTES:
/// • Static Methods: All functionality accessible without instantiation
/// • Editor-Only: #if UNITY_EDITOR compilation prevents inclusion in builds
/// • Unity Integration: Uses Unity's native APIs for maximum compatibility  
/// • Error Resilient: Comprehensive validation and error handling
/// • Logging: Detailed console output for operation tracking
/// • Extensible: Easy to add new CLI commands and functionality
/// </summary>
#endregion

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;

namespace VibeUnity.Editor
{

    /// <summary>
    /// Command Line Interface tools for Unity development workflow automation
    /// Provides scene creation, canvas management, and project analysis capabilities
    /// </summary>
    public static class CLI
    {
        #region CLI Entry Points
        
        /// <summary>
        /// Creates a new Unity scene with specified name, path, and setup type
        /// </summary>
        public static bool CreateScene(string sceneName, string scenePath, string sceneSetup = "Empty", bool addToBuildSettings = false)
        {
            return VibeUnityScenes.CreateScene(sceneName, scenePath, sceneSetup, addToBuildSettings);
        }
        
        /// <summary>
        /// Lists all available scene types for the current Unity installation
        /// </summary>
        public static void ListSceneTypes()
        {
            var availableTypes = VibeUnityScenes.GetAvailableSceneTypes();
            Debug.Log($"[VibeUnityCLI] Available scene types: {string.Join(", ", availableTypes)}");
            
            // Also log detailed descriptions
            Debug.Log("[VibeUnityCLI] Scene Type Descriptions:");
            Debug.Log("  Empty - Completely empty scene");
            Debug.Log("  DefaultGameObjects - Scene with Main Camera and Directional Light");
            Debug.Log("  2D - 2D optimized scene setup");
            Debug.Log("  3D - 3D optimized scene setup with skybox");
            
            // Check for render pipeline specific types
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
            {
                string rpName = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.GetType().Name;
                if (rpName.Contains("Universal"))
                {
                    Debug.Log("  URP - Universal Render Pipeline optimized scene");
                }
                else if (rpName.Contains("HDRP") || rpName.Contains("HighDefinition"))
                {
                    Debug.Log("  HDRP - High Definition Render Pipeline optimized scene");
                }
            }
        }
        
        #endregion
        
        #region Canvas Creation Methods
        
        /// <summary>
        /// Adds a canvas to the specified or currently active scene with specified parameters
        /// </summary>
        /// <param name="canvasName">Name for the canvas GameObject</param>
        /// <param name="sceneName">Name of scene to target (optional)</param>
        /// <param name="renderMode">Canvas render mode (ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace)</param>
        /// <param name="referenceWidth">Reference resolution width</param>
        /// <param name="referenceHeight">Reference resolution height</param>
        /// <param name="scaleMode">UI scale mode (ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize)</param>
        /// <param name="sortingOrder">Canvas sorting order</param>
        /// <param name="worldPosition">Position for WorldSpace canvas</param>
        /// <returns>True if canvas was created successfully</returns>
        public static bool AddCanvas(
            string canvasName,
            string sceneName = null,
            string renderMode = "ScreenSpaceOverlay", 
            int referenceWidth = 1920, 
            int referenceHeight = 1080,
            string scaleMode = "ScaleWithScreenSize",
            int sortingOrder = 0,
            Vector3? worldPosition = null)
        {
            return VibeUnityUI.AddCanvas(canvasName, sceneName, renderMode, referenceWidth, referenceHeight, scaleMode, sortingOrder, worldPosition);
        }
        
        #endregion
        
        #region UI Element Creation Methods
        
        /// <summary>
        /// Creates a UI panel with optional parent GameObject
        /// </summary>
        /// <param name="panelName">Name for the panel GameObject</param>
        /// <param name="parentName">Name of parent GameObject (canvas or other UI element)</param>
        /// <param name="sceneName">Name of scene to target (optional)</param>
        /// <param name="width">Panel width (default: 200)</param>
        /// <param name="height">Panel height (default: 200)</param>
        /// <param name="anchorPreset">Anchor preset (default: MiddleCenter)</param>
        /// <returns>True if panel was created successfully</returns>
        public static bool AddPanel(
            string panelName,
            string parentName = null,
            string sceneName = null,
            float width = 200f,
            float height = 200f,
            string anchorPreset = "MiddleCenter")
        {
            return VibeUnityUI.AddPanel(panelName, parentName, sceneName, width, height, anchorPreset);
        }
        
        /// <summary>
        /// Creates a UI button with optional parent GameObject
        /// </summary>
        /// <param name="buttonName">Name for the button GameObject</param>
        /// <param name="parentName">Name of parent GameObject</param>
        /// <param name="sceneName">Name of scene to target (optional)</param>
        /// <param name="buttonText">Text to display on button</param>
        /// <param name="width">Button width (default: 160)</param>
        /// <param name="height">Button height (default: 30)</param>
        /// <param name="anchorPreset">Anchor preset (default: MiddleCenter)</param>
        /// <returns>True if button was created successfully</returns>
        public static bool AddButton(
            string buttonName,
            string parentName = null,
            string sceneName = null,
            string buttonText = "Button",
            float width = 160f,
            float height = 30f,
            string anchorPreset = "MiddleCenter")
        {
            return VibeUnityUI.AddButton(buttonName, parentName, sceneName, buttonText, width, height, anchorPreset);
        }
        
        /// <summary>
        /// Creates a UI text element with optional parent GameObject
        /// </summary>
        /// <param name="textName">Name for the text GameObject</param>
        /// <param name="parentName">Name of parent GameObject</param>
        /// <param name="sceneName">Name of scene to target (optional)</param>
        /// <param name="textContent">Text content to display</param>
        /// <param name="fontSize">Font size (default: 14)</param>
        /// <param name="width">Text width (default: 200)</param>
        /// <param name="height">Text height (default: 50)</param>
        /// <param name="anchorPreset">Anchor preset (default: MiddleCenter)</param>
        /// <returns>True if text was created successfully</returns>
        public static bool AddText(
            string textName,
            string parentName = null,
            string sceneName = null,
            string textContent = "New Text",
            int fontSize = 14,
            float width = 200f,
            float height = 50f,
            string anchorPreset = "MiddleCenter")
        {
            return VibeUnityUI.AddText(textName, parentName, sceneName, textContent, fontSize, width, height, anchorPreset);
        }
        
        /// <summary>
        /// Adds a ScrollView UI element to a scene
        /// </summary>
        /// <param name="scrollViewName">Name for the ScrollView GameObject</param>
        /// <param name="parentName">Parent GameObject name (optional)</param>
        /// <param name="sceneName">Target scene name (optional)</param>
        /// <param name="width">ScrollView width in pixels</param>
        /// <param name="height">ScrollView height in pixels</param>
        /// <param name="anchorPreset">Anchor preset name</param>
        /// <param name="horizontal">Enable horizontal scrolling</param>
        /// <param name="vertical">Enable vertical scrolling</param>
        /// <param name="scrollbarVisibility">Scrollbar visibility mode</param>
        /// <param name="scrollSensitivity">Scroll sensitivity value</param>
        /// <returns>True if ScrollView was created successfully</returns>
        public static bool AddScrollView(
            string scrollViewName,
            string parentName = null,
            string sceneName = null,
            float width = 300f,
            float height = 200f,
            string anchorPreset = "MiddleCenter",
            bool horizontal = true,
            bool vertical = true,
            string scrollbarVisibility = "AutoHideAndExpandViewport",
            float scrollSensitivity = 1.0f)
        {
            return VibeUnityUI.AddScrollView(scrollViewName, parentName, sceneName, width, height, horizontal, vertical, scrollbarVisibility, scrollSensitivity, anchorPreset);
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Parses string scene setup type to Unity enum
        /// </summary>
        private static NewSceneSetup ParseSceneSetup(string sceneSetup)
        {
            switch (sceneSetup.ToLower())
            {
                case "empty":
                    return NewSceneSetup.EmptyScene;
                case "defaultgameobjects":
                case "default":
                case "3d":
                    return NewSceneSetup.DefaultGameObjects;
                case "2d":
                    // For 2D, we'll use empty and set up 2D specific settings
                    return NewSceneSetup.EmptyScene;
                default:
                    Debug.LogWarning($"[VibeUnityCLI] Unknown scene setup '{sceneSetup}', using Empty");
                    return NewSceneSetup.EmptyScene;
            }
        }
        
        /// <summary>
        /// Recursively searches for a GameObject by name
        /// </summary>
        private static GameObject FindGameObjectRecursive(GameObject parent, string targetName)
        {
            return VibeUnityGameObjects.FindRecursive(parent, targetName);
        }
        
        /// <summary>
        /// Gets the full hierarchy path of a GameObject
        /// </summary>
        private static string GetGameObjectPath(GameObject gameObject)
        {
            return VibeUnityGameObjects.GetPath(gameObject);
        }
        
        /// <summary>
        /// Lists available GameObjects in the scene for error reporting
        /// </summary>
        private static string ListAvailableGameObjects()
        {
            return VibeUnityGameObjects.ListAvailable();
        }
        
        
        /// <summary>
        /// Loads the target scene if specified, otherwise uses current scene or smart detection
        /// </summary>
        private static bool LoadTargetScene(string sceneName)
        {
            return VibeUnityScenes.LoadTargetScene(sceneName);
        }
        
        /// <summary>
        /// Finds scene asset path by name
        /// </summary>
        private static string FindSceneAsset(string sceneName)
        {
            return VibeUnityScenes.FindSceneAsset(sceneName);
        }
        
        /// <summary>
        /// Lists available scenes for error reporting
        /// </summary>
        private static string ListAvailableScenes()
        {
            return VibeUnityScenes.ListAvailableScenes();
        }
        
        /// <summary>
        /// Gets the most recently modified scene in the project (for smart scene detection)
        /// </summary>
        private static string GetMostRecentScene()
        {
            return VibeUnitySystem.GetMostRecentScene();
        }
        
        /// <summary>
        /// Gets list of available scene types
        /// </summary>
        private static List<string> GetAvailableSceneTypes()
        {
            var types = new List<string>
            {
                "Empty",
                "DefaultGameObjects",
                "2D",
                "3D"
            };
            
            // Check for render pipeline specific types
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
            {
                string rpName = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.GetType().Name;
                if (rpName.Contains("Universal"))
                {
                    types.Add("URP");
                }
                else if (rpName.Contains("HDRP") || rpName.Contains("HighDefinition"))
                {
                    types.Add("HDRP");
                }
            }
            
            // Check for VR/AR support (simplified check)
            #if UNITY_XR_MANAGEMENT
            types.Add("VR");
            #endif
            
            #if UNITY_AR_FOUNDATION
            types.Add("AR");
            #endif
            
            return types;
        }
        
        /// <summary>
        /// Validates scene creation parameters
        /// </summary>
        private static bool ValidateSceneCreation(string sceneName, string scenePath)
        {
            return VibeUnitySystem.ValidateSceneCreation(sceneName, scenePath);
        }
        
        /// <summary>
        /// Adds scene to build settings
        /// </summary>
        private static void AddSceneToBuildSettings(string scenePath)
        {
            VibeUnityScenes.AddSceneToBuildSettings(scenePath);
        }
        
        #endregion
        
        #region Command Line Interface Entry Points
        
        /// <summary>
        /// Command line entry point for scene creation.
        /// Usage: Unity -batchmode -quit -executeMethod VibeUnity.Editor.CLI.CreateSceneFromCommandLine -projectPath "path/to/project" scene_name scene_path [scene_type] [add_to_build]
        /// </summary>
        public static void CreateSceneFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int executeMethodIndex = System.Array.FindIndex(args, arg => arg == "-executeMethod");
            if (executeMethodIndex == -1 || executeMethodIndex + 3 >= args.Length)
            {
                Debug.LogError("[VibeUnityCLI] Invalid arguments. Usage: scene_name scene_path [scene_type] [add_to_build]");
                Debug.LogError("[VibeUnityCLI] Available scene types: " + string.Join(", ", GetAvailableSceneTypes()));
                EditorApplication.Exit(1);
                return;
            }

            string sceneName = args[executeMethodIndex + 2];
            string scenePath = args[executeMethodIndex + 3];
            string sceneType = args.Length > executeMethodIndex + 4 ? args[executeMethodIndex + 4] : "DefaultGameObjects";
            bool addToBuild = args.Length > executeMethodIndex + 5 && bool.Parse(args[executeMethodIndex + 5]);

            Debug.Log($"[VibeUnityCLI] Creating scene: {sceneName} at {scenePath} (type: {sceneType}, addToBuild: {addToBuild})");

            bool success = CreateScene(sceneName, scenePath, sceneType, addToBuild);
            if (success)
            {
                Debug.Log($"[VibeUnityCLI] ✅ Successfully created scene: {scenePath}/{sceneName}.unity");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[VibeUnityCLI] ❌ Failed to create scene");
                EditorApplication.Exit(1);
            }
        }
        
        /// <summary>
        /// Command line entry point for canvas creation.
        /// Usage: Unity -batchmode -quit -executeMethod VibeUnity.Editor.CLI.AddCanvasFromCommandLine canvas_name [scene_name] [render_mode] [width] [height] [scale_mode]
        /// </summary>
        public static void AddCanvasFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int executeMethodIndex = System.Array.FindIndex(args, arg => arg == "-executeMethod");
            if (executeMethodIndex == -1 || executeMethodIndex + 2 >= args.Length)
            {
                Debug.LogError("[VibeUnityCLI] Invalid arguments. Usage: canvas_name [scene_name] [render_mode] [width] [height] [scale_mode]");
                Debug.LogError("[VibeUnityCLI] Render modes: ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace");
                EditorApplication.Exit(1);
                return;
            }

            string canvasName = args[executeMethodIndex + 2];
            string sceneName = args.Length > executeMethodIndex + 3 ? args[executeMethodIndex + 3] : null;
            string renderMode = args.Length > executeMethodIndex + 4 ? args[executeMethodIndex + 4] : "ScreenSpaceOverlay";
            int width = args.Length > executeMethodIndex + 5 ? int.Parse(args[executeMethodIndex + 5]) : 1920;
            int height = args.Length > executeMethodIndex + 6 ? int.Parse(args[executeMethodIndex + 6]) : 1080;
            string scaleMode = args.Length > executeMethodIndex + 7 ? args[executeMethodIndex + 7] : "ScaleWithScreenSize";
            if (string.IsNullOrEmpty(sceneName)) sceneName = null;

            Debug.Log($"[VibeUnityCLI] Adding canvas: {canvasName} in scene {sceneName ?? "current"} ({renderMode}, {width}x{height})");

            bool success = AddCanvas(canvasName, sceneName, renderMode, width, height, scaleMode);
            if (success)
            {
                Debug.Log($"[VibeUnityCLI] ✅ Successfully added canvas: {canvasName}");
                EditorSceneManager.SaveOpenScenes();
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[VibeUnityCLI] ❌ Failed to add canvas");
                EditorApplication.Exit(1);
            }
        }
        
        /// <summary>
        /// Command line entry point for listing scene types.
        /// Usage: Unity -batchmode -quit -executeMethod VibeUnity.Editor.CLI.ListSceneTypesFromCommandLine
        /// </summary>
        public static void ListSceneTypesFromCommandLine()
        {
            Debug.Log("[VibeUnityCLI] === Available Scene Types ===");
            ListSceneTypes();
            Debug.Log("[VibeUnityCLI] ===========================");
            EditorApplication.Exit(0);
        }
        
        /// <summary>
        /// Command line entry point for adding UI panels.
        /// Usage: Unity -batchmode -quit -executeMethod VibeUnity.Editor.CLI.AddPanelFromCommandLine panel_name [parent_name] [scene_name] [width] [height] [anchor_preset]
        /// </summary>
        public static void AddPanelFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int idx = System.Array.FindIndex(args, arg => arg == "-executeMethod");
            if (idx == -1 || idx + 2 >= args.Length)
            {
                Debug.LogError("[VibeUnityCLI] Invalid arguments. Usage: panel_name [parent_name] [scene_name] [width] [height] [anchor_preset]");
                EditorApplication.Exit(1);
                return;
            }

            string panelName  = args[idx + 2];
            string parentName = args.Length > idx + 3 ? args[idx + 3] : null;
            string sceneName  = args.Length > idx + 4 ? args[idx + 4] : null;
            float  width      = args.Length > idx + 5 ? float.Parse(args[idx + 5]) : 200f;
            float  height     = args.Length > idx + 6 ? float.Parse(args[idx + 6]) : 200f;
            string anchor     = args.Length > idx + 7 ? args[idx + 7] : "MiddleCenter";
            if (string.IsNullOrEmpty(parentName)) parentName = null;
            if (string.IsNullOrEmpty(sceneName))  sceneName  = null;

            Debug.Log($"[VibeUnityCLI] Adding panel: {panelName} (parent: {parentName ?? "auto"}, scene: {sceneName ?? "current"})");

            bool success = VibeUnityUI.AddPanel(panelName, parentName, sceneName, width, height, anchor);
            if (success)
            {
                Debug.Log($"[VibeUnityCLI] ✅ Successfully added panel: {panelName}");
                EditorSceneManager.SaveOpenScenes();
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[VibeUnityCLI] ❌ Failed to add panel");
                EditorApplication.Exit(1);
            }
        }
        
        /// <summary>
        /// Command line entry point for adding UI buttons.
        /// Usage: Unity -batchmode -quit -executeMethod VibeUnity.Editor.CLI.AddButtonFromCommandLine button_name [parent_name] [button_text] [width] [height] [anchor_preset]
        /// </summary>
        public static void AddButtonFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int idx = System.Array.FindIndex(args, arg => arg == "-executeMethod");
            if (idx == -1 || idx + 2 >= args.Length)
            {
                Debug.LogError("[VibeUnityCLI] Invalid arguments. Usage: button_name [parent_name] [button_text] [width] [height] [anchor_preset]");
                EditorApplication.Exit(1);
                return;
            }

            string buttonName = args[idx + 2];
            string parentName = args.Length > idx + 3 ? args[idx + 3] : null;
            string buttonText = args.Length > idx + 4 ? args[idx + 4] : "Button";
            float  width      = args.Length > idx + 5 ? float.Parse(args[idx + 5]) : 160f;
            float  height     = args.Length > idx + 6 ? float.Parse(args[idx + 6]) : 30f;
            string anchor     = args.Length > idx + 7 ? args[idx + 7] : "MiddleCenter";
            if (string.IsNullOrEmpty(parentName)) parentName = null;

            Debug.Log($"[VibeUnityCLI] Adding button: {buttonName} (parent: {parentName ?? "auto"}, text: \"{buttonText}\")");

            bool success = VibeUnityUI.AddButton(buttonName, parentName, null, buttonText, width, height, anchor);
            if (success)
            {
                Debug.Log($"[VibeUnityCLI] ✅ Successfully added button: {buttonName}");
                EditorSceneManager.SaveOpenScenes();
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[VibeUnityCLI] ❌ Failed to add button");
                EditorApplication.Exit(1);
            }
        }
        
        /// <summary>
        /// Command line entry point for adding UI text.
        /// Usage: Unity -batchmode -quit -executeMethod VibeUnity.Editor.CLI.AddTextFromCommandLine text_name [parent_name] [text_content] [font_size] [width] [height] [anchor_preset]
        /// </summary>
        public static void AddTextFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int idx = System.Array.FindIndex(args, arg => arg == "-executeMethod");
            if (idx == -1 || idx + 2 >= args.Length)
            {
                Debug.LogError("[VibeUnityCLI] Invalid arguments. Usage: text_name [parent_name] [text_content] [font_size] [width] [height] [anchor_preset]");
                EditorApplication.Exit(1);
                return;
            }

            string textName    = args[idx + 2];
            string parentName  = args.Length > idx + 3 ? args[idx + 3] : null;
            string textContent = args.Length > idx + 4 ? args[idx + 4] : "New Text";
            int    fontSize    = args.Length > idx + 5 ? int.Parse(args[idx + 5]) : 14;
            float  width       = args.Length > idx + 6 ? float.Parse(args[idx + 6]) : 200f;
            float  height      = args.Length > idx + 7 ? float.Parse(args[idx + 7]) : 50f;
            string anchor      = args.Length > idx + 8 ? args[idx + 8] : "MiddleCenter";
            if (string.IsNullOrEmpty(parentName)) parentName = null;

            Debug.Log($"[VibeUnityCLI] Adding text: {textName} (parent: {parentName ?? "auto"}, content: \"{textContent}\")");

            bool success = VibeUnityUI.AddText(textName, parentName, null, textContent, fontSize, width, height, anchor);
            if (success)
            {
                Debug.Log($"[VibeUnityCLI] ✅ Successfully added text: {textName}");
                EditorSceneManager.SaveOpenScenes();
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[VibeUnityCLI] ❌ Failed to add text");
                EditorApplication.Exit(1);
            }
        }
        
        /// <summary>
        /// Command line entry point for batch file execution.
        /// Usage: Unity -batchmode -quit -executeMethod VibeUnity.Editor.CLI.ExecuteBatchFromCommandLine json_file_path
        /// </summary>
        public static void ExecuteBatchFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int idx = System.Array.FindIndex(args, arg => arg == "-executeMethod");
            if (idx == -1 || idx + 2 >= args.Length)
            {
                Debug.LogError("[VibeUnityCLI] Invalid arguments. Usage: json_file_path");
                EditorApplication.Exit(1);
                return;
            }

            string jsonFilePath = args[idx + 2];
            Debug.Log($"[VibeUnityCLI] Executing batch file: {jsonFilePath}");

            bool success = VibeUnityJSONProcessor.ProcessBatchFileWithLogging(jsonFilePath);
            if (success)
            {
                Debug.Log("[VibeUnityCLI] ✅ Batch file executed successfully");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[VibeUnityCLI] ❌ Batch file execution failed");
                EditorApplication.Exit(1);
            }
        }
        
        /// <summary>
        /// Executes commands from a JSON batch file
        /// </summary>
        /// <param name="jsonFilePath">Path to the JSON batch file</param>
        /// <returns>True if all commands executed successfully</returns>
        public static bool ExecuteBatchFile(string jsonFilePath)
        {
            return VibeUnityJSONProcessor.ProcessBatchFileWithLogging(jsonFilePath);
        }
        
        /// <summary>
        /// Executes commands from a JSON batch file with detailed logging
        /// </summary>
        /// <param name="jsonFilePath">Path to the JSON batch file</param>
        /// <param name="logCapture">StringBuilder to capture log output</param>
        /// <returns>True if all commands executed successfully</returns>
        private static bool ExecuteBatchFileWithLogging(string jsonFilePath, System.Text.StringBuilder logCapture)
        {
            return VibeUnityJSONProcessor.ProcessBatchFileWithLogging(jsonFilePath);
        }
        
        
        
        
        
        
        
        
        
        
        
        /// <summary>
        /// Finds a GameObject by name in the active scene
        /// </summary>
        private static GameObject FindGameObjectInActiveScene(string name)
        {
            return VibeUnityGameObjects.FindInActiveScene(name);
        }
        
        /// <summary>
        /// Command line entry point for help
        /// Usage: Unity -batchmode -quit -executeMethod VibeUnity.Editor.CLI.ShowHelpFromCommandLine
        /// </summary>
        public static void ShowHelpFromCommandLine()
        {
            VibeUnitySystem.ShowHelp();
        }
        
        #endregion
        
        
        #region File Watcher System
        
        /// <summary>
        /// Enable the file watcher
        /// </summary>
        public static void EnableFileWatcher()
        {
            VibeUnitySystem.EnableFileWatcher();
        }
        
        /// <summary>
        /// Disable the file watcher
        /// </summary>
        public static void DisableFileWatcher()
        {
            VibeUnitySystem.DisableFileWatcher();
        }
        
        #endregion
        
        #region Scene Management Helper Methods
        
        /// <summary>
        /// Saves the currently active scene and refreshes the asset database
        /// </summary>
        /// <param name="suppressLog">Whether to suppress the success log message (useful for batch operations)</param>
        /// <param name="forceSave">Whether to force save even during batch processing</param>
        private static void SaveActiveScene(bool suppressLog = false, bool forceSave = false)
        {
            VibeUnityScenes.SaveActiveScene(suppressLog, forceSave);
        }
        
        /// <summary>
        /// Marks the currently active scene as dirty to ensure changes are saved
        /// </summary>
        private static void MarkActiveSceneDirty()
        {
            VibeUnityScenes.MarkActiveSceneDirty();
        }
        
        /// <summary>
        /// Force saves the currently active scene immediately
        /// </summary>
        private static void ForceSaveActiveScene()
        {
            VibeUnityScenes.ForceSaveActiveScene();
        }
        
        #endregion
        
        #region Component Management Helper Methods
        
        /// <summary>
        /// Adds a component to a GameObject by type name
        /// </summary>
        private static Component AddComponentToGameObject(GameObject target, string componentTypeName)
        {
            return VibeUnityGameObjects.AddComponent(target, componentTypeName);
        }
        
        
        
        /// <summary>
        /// Sets parameters on a component using reflection
        /// </summary>
        private static bool SetComponentParameters(Component component, VibeUnityGameObjects.ComponentParameter[] parameters, System.Text.StringBuilder logCapture = null)
        {
            return VibeUnityGameObjects.SetComponentParameters(component, parameters, logCapture);
        }
        
        
        
        #endregion       
    }
}
#endif