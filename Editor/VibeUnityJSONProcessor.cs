using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;

namespace VibeUnity.Editor
{
    /// <summary>
    /// JSON batch processing functionality for Vibe Unity
    /// </summary>
    public static class VibeUnityJSONProcessor
    {
        /// <summary>
        /// Executes commands from a JSON batch file with detailed logging
        /// </summary>
        public static bool ProcessBatchFileWithLogging(string jsonFilePath)
        {
            var logCapture = new StringBuilder();
            bool overallSuccess = false;
            
            try
            {
                if (!File.Exists(jsonFilePath))
                {
                    string error = $"Batch file not found: {jsonFilePath}";
                    logCapture.AppendLine($"ERROR: {error}");
                    Debug.LogError($"[VibeUnity] {error}");
                    return false;
                }
                
                // Read and parse JSON
                string jsonContent = File.ReadAllText(jsonFilePath);
                logCapture.AppendLine($"=== Processing {Path.GetFileName(jsonFilePath)} ===");
                logCapture.AppendLine($"Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logCapture.AppendLine($"File Path: {jsonFilePath}");
                logCapture.AppendLine();
                logCapture.AppendLine("JSON Content:");
                logCapture.AppendLine(jsonContent);
                logCapture.AppendLine();
                
                BatchCommandFile batchFile;
                try
                {
                    batchFile = JsonUtility.FromJson<BatchCommandFile>(jsonContent);
                    logCapture.AppendLine("✅ JSON parsing successful");
                }
                catch (System.Exception e)
                {
                    string error = $"Failed to parse JSON: {e.Message}";
                    logCapture.AppendLine($"❌ ERROR: {error}");
                    Debug.LogError($"[VibeUnity] {error}");
                    return false;
                }
                
                if (batchFile.commands == null || batchFile.commands.Length == 0)
                {
                    string error = "No commands found in batch file";
                    logCapture.AppendLine($"❌ ERROR: {error}");
                    Debug.LogError($"[VibeUnity] {error}");
                    return false;
                }
                
                logCapture.AppendLine($"✅ Batch file loaded: {batchFile.commands.Length} commands");
                logCapture.AppendLine($"Description: {batchFile.description}");
                logCapture.AppendLine();
                
                // Handle scene configuration if present (JsonUtility never returns null for
                // object fields, so check name is non-empty to detect an actual scene block)
                if (batchFile.scene != null && !string.IsNullOrEmpty(batchFile.scene.name))
                {
                    if (!EnsureSceneLoadedWithSceneConfig(batchFile.scene, logCapture))
                    {
                        return false;
                    }
                }
                
                // Execute commands sequentially
                logCapture.AppendLine($"=== Command Execution ({batchFile.commands.Length} commands) ===");
                for (int i = 0; i < batchFile.commands.Length; i++)
                {
                    var command = batchFile.commands[i];
                    logCapture.AppendLine($"--- Command {i + 1}/{batchFile.commands.Length}: {command.action} ---");
                    logCapture.AppendLine($"Command details: {JsonUtility.ToJson(command, true)}");
                    
                    bool commandSuccess = ExecuteBatchCommandWithLogging(command, logCapture);
                    if (!commandSuccess)
                    {
                        logCapture.AppendLine($"❌ Command {i + 1} failed: {command.action}");
                        Debug.LogError($"[VibeUnity] Command {i + 1} failed: {command.action}");
                        return false;
                    }
                    else
                    {
                        logCapture.AppendLine($"✅ Command {i + 1} succeeded: {command.action}");
                    }
                    logCapture.AppendLine();
                }
                
                // Save all scenes after batch execution
                logCapture.AppendLine("Saving scenes and refreshing asset database...");
                // Force save the active scene specifically
                VibeUnityScenes.ForceSaveActiveScene();
                // Also save any other open scenes
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.Refresh();
                logCapture.AppendLine("✅ Scenes saved and asset database refreshed");
                
                // Generate scene state artifact after batch processing
                logCapture.AppendLine("Generating scene state artifact...");
                try
                {
                    if (VibeUnitySceneExporter.ExportActiveSceneState())
                    {
                        logCapture.AppendLine("✅ Scene state artifact generated successfully");
                        logCapture.AppendLine("   └─ State file saved alongside scene file");
                        logCapture.AppendLine("   └─ Coverage analysis report generated");
                    }
                    else
                    {
                        logCapture.AppendLine("⚠️ Warning: Scene state artifact generation failed");
                        logCapture.AppendLine("   └─ Check console for export error details");
                    }
                }
                catch (System.Exception e)
                {
                    logCapture.AppendLine($"⚠️ Warning: Exception during scene state export: {e.Message}");
                    logCapture.AppendLine("   └─ Batch processing completed successfully despite export issue");
                }
                
                overallSuccess = true;
                return true;
            }
            catch (System.Exception e)
            {
                string error = $"Exception executing batch file: {e.Message}";
                logCapture.AppendLine($"❌ FATAL ERROR: {error}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                Debug.LogError($"[VibeUnity] {error}");
                overallSuccess = false;
                return false;
            }
            finally
            {
                // Save the log file
                SaveLogFile(jsonFilePath, logCapture, overallSuccess);
            }
        }
        
        /// <summary>
        /// Ensures a scene is loaded based on scene configuration
        /// </summary>
        private static bool EnsureSceneLoadedWithSceneConfig(SceneConfig sceneConfig, StringBuilder logCapture)
        {
            try
            {
                logCapture.AppendLine("=== Scene Configuration Validation ===");
                logCapture.AppendLine($"✅ Scene Name: '{sceneConfig.name}'");
                
                if (sceneConfig.create)
                {
                    logCapture.AppendLine($"   └─ Create if missing: True");
                    logCapture.AppendLine($"   └─ Creation Path: {sceneConfig.path}");
                    logCapture.AppendLine($"   └─ Scene Type: {sceneConfig.type}");
                    logCapture.AppendLine($"   └─ Add to Build: {sceneConfig.addToBuild}");
                }
                else
                {
                    logCapture.AppendLine($"   └─ Create if missing: False");
                }
                
                logCapture.AppendLine();
                
                logCapture.AppendLine("=== Scene Processing ===");
                logCapture.AppendLine($"Processing scene: '{sceneConfig.name}'");
                
                // Check if scene exists
                string sceneAssetPath = VibeUnityScenes.FindSceneAsset(sceneConfig.name);
                
                if (!string.IsNullOrEmpty(sceneAssetPath))
                {
                    // Scene exists, load it
                    logCapture.AppendLine($"✅ Scene file found: {sceneAssetPath}");
                    
                    try
                    {
                        var targetScene = EditorSceneManager.OpenScene(sceneAssetPath);
                        if (targetScene.IsValid())
                        {
                            logCapture.AppendLine($"✅ Scene loaded successfully: {sceneConfig.name}");
                            logCapture.AppendLine($"   └─ Root GameObjects: {targetScene.rootCount}");
                            logCapture.AppendLine($"   └─ Scene Path: {sceneAssetPath}");
                            return true;
                        }
                        else
                        {
                            logCapture.AppendLine($"❌ Failed to load scene: {sceneConfig.name}");
                            return false;
                        }
                    }
                    catch (System.Exception e)
                    {
                        logCapture.AppendLine($"❌ Failed to load scene: {e.Message}");
                        return false;
                    }
                }
                else
                {
                    // Scene doesn't exist
                    if (sceneConfig.create)
                    {
                        // Create the scene as requested
                        logCapture.AppendLine($"⚠️ Scene not found, creating as requested: {sceneConfig.name}");
                        logCapture.AppendLine($"   └─ Path: {sceneConfig.path}");
                        logCapture.AppendLine($"   └─ Type: {sceneConfig.type}");
                        logCapture.AppendLine($"   └─ Add to Build: {sceneConfig.addToBuild}");
                        
                        // Create the scene with specified parameters
                        bool sceneCreated = VibeUnityScenes.CreateScene(sceneConfig.name, sceneConfig.path, sceneConfig.type, sceneConfig.addToBuild);
                        
                        if (sceneCreated)
                        {
                            logCapture.AppendLine($"✅ Scene created and loaded: {sceneConfig.name}");
                            logCapture.AppendLine($"   └─ Full Path: {sceneConfig.path}/{sceneConfig.name}.unity");
                            return true;
                        }
                        else
                        {
                            logCapture.AppendLine($"❌ Failed to create scene: {sceneConfig.name}");
                            return false;
                        }
                    }
                    else
                    {
                        // Scene doesn't exist and creation not requested
                        logCapture.AppendLine($"❌ Scene '{sceneConfig.name}' not found and 'create' is false");
                        logCapture.AppendLine($"   └─ Set scene.create = true to auto-create missing scenes");
                        return false;
                    }
                }
            }
            catch (System.Exception e)
            {
                logCapture.AppendLine($"❌ Exception in EnsureSceneLoadedWithSceneConfig: {e.Message}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Executes a single batch command with logging
        /// </summary>
        private static bool ExecuteBatchCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            switch (command.action.ToLower())
            {
                case "create-scene":
                    return ExecuteCreateSceneCommandWithLogging(command, logCapture);
                case "add-canvas":
                    return ExecuteAddCanvasCommandWithLogging(command, logCapture);
                case "add-panel":
                    return ExecuteAddPanelCommandWithLogging(command, logCapture);
                case "add-button":
                    return ExecuteAddButtonCommandWithLogging(command, logCapture);
                case "add-text":
                    return ExecuteAddTextCommandWithLogging(command, logCapture);
                case "add-scrollview":
                    return ExecuteAddScrollViewCommandWithLogging(command, logCapture);
                case "add-cube":
                    return ExecuteAddCubeCommandWithLogging(command, logCapture);
                case "add-sphere":
                    return ExecuteAddSphereCommandWithLogging(command, logCapture);
                case "add-plane":
                    return ExecuteAddPlaneCommandWithLogging(command, logCapture);
                case "add-cylinder":
                    return ExecuteAddCylinderCommandWithLogging(command, logCapture);
                case "add-capsule":
                    return ExecuteAddCapsuleCommandWithLogging(command, logCapture);
                case "add-component":
                    return ExecuteAddComponentCommandWithLogging(command, logCapture);
                case "add-gameobject":
                    return ExecuteAddGameObjectCommandWithLogging(command, logCapture);
                default:
                    logCapture.AppendLine($"❌ ERROR: Unknown batch command: {command.action}");
                    return false;
            }
        }
        
        #region Command Execution Methods
        
        private static bool ExecuteCreateSceneCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            try
            {
                // Support both "sceneName" and "name" fields for backwards compatibility
                string sceneName = !string.IsNullOrEmpty(command.sceneName) ? command.sceneName : command.name;
                string scenePath = !string.IsNullOrEmpty(command.path) ? command.path : "Assets/Scenes";
                string sceneType = !string.IsNullOrEmpty(command.type) ? command.type : "DefaultGameObjects";
                bool addToBuild = command.addToBuild;
                
                logCapture.AppendLine($"Executing create-scene command...");
                logCapture.AppendLine($"Scene Name: {sceneName}");
                logCapture.AppendLine($"Scene Path: {scenePath}");
                logCapture.AppendLine($"Scene Type: {sceneType}");
                logCapture.AppendLine($"Add to Build: {addToBuild}");
                
                bool result = VibeUnityScenes.CreateScene(sceneName, scenePath, sceneType, addToBuild);
                
                if (result)
                {
                    logCapture.AppendLine($"✅ Scene created successfully: {scenePath}/{sceneName}.unity");
                }
                else
                {
                    logCapture.AppendLine($"❌ Failed to create scene: {scenePath}/{sceneName}.unity");
                }
                
                return result;
            }
            catch (System.Exception e)
            {
                logCapture.AppendLine($"❌ Exception in create-scene: {e.Message}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                return false;
            }
        }
        
        private static bool ExecuteAddCanvasCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            try
            {
                string canvasName = command.name;
                string renderMode = !string.IsNullOrEmpty(command.renderMode) ? command.renderMode : "ScreenSpaceOverlay";
                int width = command.referenceWidth > 0 ? command.referenceWidth : 1920;
                int height = command.referenceHeight > 0 ? command.referenceHeight : 1080;
                string scaleMode = !string.IsNullOrEmpty(command.scaleMode) ? command.scaleMode : "ScaleWithScreenSize";
                int sortingOrder = command.sortingOrder;
                
                Vector3? worldPosition = null;
                if (command.worldPosition != null && command.worldPosition.Length == 3)
                {
                    worldPosition = new Vector3(command.worldPosition[0], command.worldPosition[1], command.worldPosition[2]);
                }
                
                logCapture.AppendLine($"Executing add-canvas command...");
                logCapture.AppendLine($"Canvas Name: {canvasName}");
                logCapture.AppendLine($"Render Mode: {renderMode}");
                logCapture.AppendLine($"Resolution: {width}x{height}");
                logCapture.AppendLine($"Scale Mode: {scaleMode}");
                logCapture.AppendLine($"Sorting Order: {sortingOrder}");
                if (worldPosition.HasValue)
                    logCapture.AppendLine($"World Position: {worldPosition.Value}");
                
                // Scene is already loaded at batch level, pass null to use current scene
                bool result = VibeUnityUI.AddCanvas(canvasName, null, renderMode, width, height, scaleMode, sortingOrder, worldPosition);
                
                if (result)
                {
                    logCapture.AppendLine($"✅ Canvas created successfully: {canvasName}");
                }
                else
                {
                    logCapture.AppendLine($"❌ Failed to create canvas: {canvasName}");
                }
                
                return result;
            }
            catch (System.Exception e)
            {
                logCapture.AppendLine($"❌ Exception in add-canvas: {e.Message}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                return false;
            }
        }
        
        private static bool ExecuteAddPanelCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            try
            {
                string panelName = command.name;
                string parentName = command.parent;
                float width = command.width > 0 ? command.width : 200f;
                float height = command.height > 0 ? command.height : 200f;
                string anchor = !string.IsNullOrEmpty(command.anchor) ? command.anchor : "MiddleCenter";
                
                logCapture.AppendLine($"Executing add-panel command...");
                logCapture.AppendLine($"Panel Name: {panelName}");
                logCapture.AppendLine($"Parent: {parentName ?? "auto-detect"}");
                logCapture.AppendLine($"Size: {width}x{height}");
                logCapture.AppendLine($"Anchor: {anchor}");
                
                bool result = VibeUnityUI.AddPanel(panelName, parentName, null, width, height, anchor);
                
                // Apply position offset if specified
                if (result && command.position != null && command.position.Length >= 2)
                {
                    GameObject panelGO = VibeUnityGameObjects.FindInActiveScene(panelName);
                    if (panelGO != null)
                    {
                        RectTransform rectTransform = panelGO.GetComponent<RectTransform>();
                        if (rectTransform != null)
                        {
                            Vector2 offset = new Vector2(command.position[0], command.position[1]);
                            rectTransform.anchoredPosition = offset;
                            logCapture.AppendLine($"Applied position offset: ({offset.x:F2}, {offset.y:F2})");
                        }
                    }
                }
                
                if (result)
                {
                    logCapture.AppendLine($"✅ Panel created successfully: {panelName}");
                }
                else
                {
                    logCapture.AppendLine($"❌ Failed to create panel: {panelName}");
                }
                
                return result;
            }
            catch (System.Exception e)
            {
                logCapture.AppendLine($"❌ Exception in add-panel: {e.Message}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                return false;
            }
        }
        
        private static bool ExecuteAddButtonCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            try
            {
                string buttonName = command.name;
                string parentName = command.parent;
                string buttonText = !string.IsNullOrEmpty(command.text) ? command.text : "Button";
                float width = command.width > 0 ? command.width : 160f;
                float height = command.height > 0 ? command.height : 30f;
                string anchor = !string.IsNullOrEmpty(command.anchor) ? command.anchor : "MiddleCenter";
                
                logCapture.AppendLine($"Executing add-button command...");
                logCapture.AppendLine($"Button Name: {buttonName}");
                logCapture.AppendLine($"Parent: {parentName ?? "auto-detect"}");
                logCapture.AppendLine($"Text: {buttonText}");
                logCapture.AppendLine($"Size: {width}x{height}");
                logCapture.AppendLine($"Anchor: {anchor}");
                
                bool result = VibeUnityUI.AddButton(buttonName, parentName, null, buttonText, width, height, anchor);
                
                // Apply position offset if specified
                if (result && command.position != null && command.position.Length >= 2)
                {
                    GameObject buttonGO = VibeUnityGameObjects.FindInActiveScene(buttonName);
                    if (buttonGO != null)
                    {
                        RectTransform rectTransform = buttonGO.GetComponent<RectTransform>();
                        if (rectTransform != null)
                        {
                            Vector2 offset = new Vector2(command.position[0], command.position[1]);
                            rectTransform.anchoredPosition = offset;
                            logCapture.AppendLine($"Applied position offset: ({offset.x:F2}, {offset.y:F2})");
                        }
                    }
                }
                
                if (result)
                {
                    logCapture.AppendLine($"✅ Button created successfully: {buttonName}");
                }
                else
                {
                    logCapture.AppendLine($"❌ Failed to create button: {buttonName}");
                }
                
                return result;
            }
            catch (System.Exception e)
            {
                logCapture.AppendLine($"❌ Exception in add-button: {e.Message}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                return false;
            }
        }
        
        private static bool ExecuteAddTextCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            try
            {
                string textName = command.name;
                string parentName = command.parent;
                string textContent = !string.IsNullOrEmpty(command.text) ? command.text : "New Text";
                int fontSize = command.fontSize > 0 ? command.fontSize : 14;
                float width = command.width > 0 ? command.width : 200f;
                float height = command.height > 0 ? command.height : 50f;
                string anchor = !string.IsNullOrEmpty(command.anchor) ? command.anchor : "MiddleCenter";
                
                logCapture.AppendLine($"Executing add-text command...");
                logCapture.AppendLine($"Text Name: {textName}");
                logCapture.AppendLine($"Parent: {parentName ?? "auto-detect"}");
                logCapture.AppendLine($"Content: {textContent}");
                logCapture.AppendLine($"Font Size: {fontSize}");
                logCapture.AppendLine($"Size: {width}x{height}");
                logCapture.AppendLine($"Anchor: {anchor}");
                
                bool result = VibeUnityUI.AddText(textName, parentName, null, textContent, fontSize, width, height, anchor);
                
                // Apply position offset and color if specified
                if (result && (command.position != null || !string.IsNullOrEmpty(command.color)))
                {
                    GameObject textGO = VibeUnityGameObjects.FindInActiveScene(textName);
                    if (textGO != null)
                    {
                        RectTransform rectTransform = textGO.GetComponent<RectTransform>();
                        if (rectTransform != null && command.position != null && command.position.Length >= 2)
                        {
                            Vector2 offset = new Vector2(command.position[0], command.position[1]);
                            rectTransform.anchoredPosition = offset;
                            logCapture.AppendLine($"Applied position offset: ({offset.x:F2}, {offset.y:F2})");
                        }
                        
                        // Apply color if specified
                        if (!string.IsNullOrEmpty(command.color))
                        {
                            TMPro.TextMeshProUGUI textComponent = textGO.GetComponent<TMPro.TextMeshProUGUI>();
                            if (textComponent != null && ColorUtility.TryParseHtmlString(command.color, out Color color))
                            {
                                textComponent.color = color;
                                logCapture.AppendLine($"Applied color: {command.color}");
                            }
                            else
                            {
                                logCapture.AppendLine($"⚠️ Warning: Could not parse color: {command.color}");
                            }
                        }
                    }
                }
                
                if (result)
                {
                    logCapture.AppendLine($"✅ Text created successfully: {textName}");
                }
                else
                {
                    logCapture.AppendLine($"❌ Failed to create text: {textName}");
                }
                
                return result;
            }
            catch (System.Exception e)
            {
                logCapture.AppendLine($"❌ Exception in add-text: {e.Message}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                return false;
            }
        }
        
        private static bool ExecuteAddScrollViewCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            try
            {
                string scrollViewName = command.name;
                string parentName = command.parent;
                float width = command.width > 0 ? command.width : 300f;
                float height = command.height > 0 ? command.height : 200f;
                bool horizontal = command.horizontal;
                bool vertical = command.vertical;
                string scrollbarVisibility = !string.IsNullOrEmpty(command.scrollbarVisibility) ? command.scrollbarVisibility : "AutoHideAndExpandViewport";
                float scrollSensitivity = command.scrollSensitivity > 0 ? command.scrollSensitivity : 1f;
                string anchor = !string.IsNullOrEmpty(command.anchor) ? command.anchor : "MiddleCenter";
                
                logCapture.AppendLine($"Executing add-scrollview command...");
                logCapture.AppendLine($"ScrollView Name: {scrollViewName}");
                logCapture.AppendLine($"Parent: {parentName ?? "auto-detect"}");
                logCapture.AppendLine($"Size: {width}x{height}");
                logCapture.AppendLine($"Anchor: {anchor}");
                logCapture.AppendLine($"Horizontal: {horizontal}");
                logCapture.AppendLine($"Vertical: {vertical}");
                logCapture.AppendLine($"Scrollbar Visibility: {scrollbarVisibility}");
                logCapture.AppendLine($"Scroll Sensitivity: {scrollSensitivity}");
                
                bool result = VibeUnityUI.AddScrollView(scrollViewName, parentName, null, width, height, horizontal, vertical, scrollbarVisibility, scrollSensitivity, anchor);
                
                if (result)
                {
                    logCapture.AppendLine($"✅ ScrollView created successfully: {scrollViewName}");
                }
                else
                {
                    logCapture.AppendLine($"❌ Failed to create ScrollView: {scrollViewName}");
                }
                
                return result;
            }
            catch (System.Exception e)
            {
                logCapture.AppendLine($"❌ Exception in add-scrollview: {e.Message}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                return false;
            }
        }
        
        private static bool ExecuteAddCubeCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            Vector3 position = command.position != null && command.position.Length >= 3 ?
                new Vector3(command.position[0], command.position[1], command.position[2]) : Vector3.zero;
            Vector3 rotation = command.rotation != null && command.rotation.Length >= 3 ?
                new Vector3(command.rotation[0], command.rotation[1], command.rotation[2]) : Vector3.zero;
            Vector3 scale = command.scale != null && command.scale.Length >= 3 ?
                new Vector3(command.scale[0], command.scale[1], command.scale[2]) : Vector3.one;
            return VibeUnityPrimitives.CreatePrimitiveWithLogging(command.name, PrimitiveType.Cube, position, rotation, scale, logCapture, "Cube", command.color);
        }

        private static bool ExecuteAddSphereCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            Vector3 position = command.position != null && command.position.Length >= 3 ?
                new Vector3(command.position[0], command.position[1], command.position[2]) : Vector3.zero;
            Vector3 rotation = command.rotation != null && command.rotation.Length >= 3 ?
                new Vector3(command.rotation[0], command.rotation[1], command.rotation[2]) : Vector3.zero;
            Vector3 scale = command.scale != null && command.scale.Length >= 3 ?
                new Vector3(command.scale[0], command.scale[1], command.scale[2]) : Vector3.one;
            return VibeUnityPrimitives.CreatePrimitiveWithLogging(command.name, PrimitiveType.Sphere, position, rotation, scale, logCapture, "Sphere", command.color);
        }

        private static bool ExecuteAddPlaneCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            Vector3 position = command.position != null && command.position.Length >= 3 ?
                new Vector3(command.position[0], command.position[1], command.position[2]) : Vector3.zero;
            Vector3 rotation = command.rotation != null && command.rotation.Length >= 3 ?
                new Vector3(command.rotation[0], command.rotation[1], command.rotation[2]) : Vector3.zero;
            Vector3 scale = command.scale != null && command.scale.Length >= 3 ?
                new Vector3(command.scale[0], command.scale[1], command.scale[2]) : Vector3.one;
            return VibeUnityPrimitives.CreatePrimitiveWithLogging(command.name, PrimitiveType.Plane, position, rotation, scale, logCapture, "Plane", command.color);
        }

        private static bool ExecuteAddCylinderCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            Vector3 position = command.position != null && command.position.Length >= 3 ?
                new Vector3(command.position[0], command.position[1], command.position[2]) : Vector3.zero;
            Vector3 rotation = command.rotation != null && command.rotation.Length >= 3 ?
                new Vector3(command.rotation[0], command.rotation[1], command.rotation[2]) : Vector3.zero;
            Vector3 scale = command.scale != null && command.scale.Length >= 3 ?
                new Vector3(command.scale[0], command.scale[1], command.scale[2]) : Vector3.one;
            return VibeUnityPrimitives.CreatePrimitiveWithLogging(command.name, PrimitiveType.Cylinder, position, rotation, scale, logCapture, "Cylinder", command.color);
        }

        private static bool ExecuteAddCapsuleCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            Vector3 position = command.position != null && command.position.Length >= 3 ?
                new Vector3(command.position[0], command.position[1], command.position[2]) : Vector3.zero;
            Vector3 rotation = command.rotation != null && command.rotation.Length >= 3 ?
                new Vector3(command.rotation[0], command.rotation[1], command.rotation[2]) : Vector3.zero;
            Vector3 scale = command.scale != null && command.scale.Length >= 3 ?
                new Vector3(command.scale[0], command.scale[1], command.scale[2]) : Vector3.one;
            return VibeUnityPrimitives.CreatePrimitiveWithLogging(command.name, PrimitiveType.Capsule, position, rotation, scale, logCapture, "Capsule", command.color);
        }
        
        private static bool ExecuteAddComponentCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            try
            {
                string targetName = command.name;
                string componentTypeName = command.componentType;
                
                logCapture.AppendLine($"Executing add-component command...");
                logCapture.AppendLine($"Target GameObject: {targetName}");
                logCapture.AppendLine($"Component Type: {componentTypeName}");
                
                GameObject targetObject = VibeUnityGameObjects.FindInActiveScene(targetName);
                if (targetObject == null)
                {
                    logCapture.AppendLine($"❌ GameObject '{targetName}' not found in active scene");
                    return false;
                }
                
                Component addedComponent = VibeUnityGameObjects.AddComponent(targetObject, componentTypeName);
                if (addedComponent == null)
                {
                    logCapture.AppendLine($"❌ Failed to add component '{componentTypeName}' to '{targetName}'");
                    return false;
                }
                
                // Set component parameters if provided. Surface failures instead of
                // discarding the result and reporting unconditional success.
                if (command.parameters != null && command.parameters.Length > 0)
                {
                    bool paramsOk = VibeUnityGameObjects.SetComponentParameters(addedComponent, command.parameters, logCapture);
                    if (!paramsOk)
                    {
                        logCapture.AppendLine($"FAIL: one or more parameters on '{componentTypeName}' could not be applied (see above)");
                        VibeUnityScenes.MarkActiveSceneDirty();
                        return false;
                    }
                }

                logCapture.AppendLine($"SUCCESS: Component '{componentTypeName}' added to '{targetName}'");
                
                // Mark scene as dirty to ensure changes are saved
                VibeUnityScenes.MarkActiveSceneDirty();
                
                return true;
            }
            catch (System.Exception e)
            {
                logCapture.AppendLine($"❌ Exception in add-component: {e.Message}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                return false;
            }
        }
        
        private static bool ExecuteAddGameObjectCommandWithLogging(BatchCommand command, StringBuilder logCapture)
        {
            try
            {
                string objectName = command.name;
                string parentName = command.parent;
                
                logCapture.AppendLine($"Executing add-gameobject command...");
                logCapture.AppendLine($"GameObject Name: {objectName}");
                logCapture.AppendLine($"Parent: {(!string.IsNullOrEmpty(parentName) ? parentName : "Scene Root")}");
                
                GameObject parentObject = null;
                if (!string.IsNullOrEmpty(parentName))
                {
                    parentObject = VibeUnityGameObjects.FindInActiveScene(parentName);
                    if (parentObject == null)
                    {
                        logCapture.AppendLine($"❌ Parent GameObject '{parentName}' not found in active scene");
                        return false;
                    }
                }
                
                GameObject gameObject = VibeUnityGameObjects.CreateEmptyGameObject(objectName, parentObject);
                if (gameObject == null)
                {
                    logCapture.AppendLine($"❌ Failed to create GameObject '{objectName}'");
                    return false;
                }
                
                // Set position if provided
                if (command.position != null && command.position.Length >= 3)
                {
                    Vector3 position = new Vector3(command.position[0], command.position[1], command.position[2]);
                    gameObject.transform.position = position;
                    logCapture.AppendLine($"   └─ Position set to: ({position.x}, {position.y}, {position.z})");
                }
                
                // Set rotation if provided
                if (command.rotation != null && command.rotation.Length >= 3)
                {
                    Vector3 rotation = new Vector3(command.rotation[0], command.rotation[1], command.rotation[2]);
                    gameObject.transform.eulerAngles = rotation;
                    logCapture.AppendLine($"   └─ Rotation set to: ({rotation.x}, {rotation.y}, {rotation.z})");
                }
                
                // Set scale if provided
                if (command.scale != null && command.scale.Length >= 3)
                {
                    Vector3 scale = new Vector3(command.scale[0], command.scale[1], command.scale[2]);
                    gameObject.transform.localScale = scale;
                    logCapture.AppendLine($"   └─ Scale set to: ({scale.x}, {scale.y}, {scale.z})");
                }
                
                logCapture.AppendLine($"✅ GameObject '{objectName}' created successfully");
                if (parentObject != null)
                {
                    logCapture.AppendLine($"   └─ Parented to: {parentName}");
                }
                
                return true;
            }
            catch (System.Exception e)
            {
                logCapture.AppendLine($"❌ Exception in add-gameobject: {e.Message}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                return false;
            }
        }
        
        #endregion
        
        /// <summary>
        /// Saves a log file for the batch processing
        /// </summary>
        private static void SaveLogFile(string jsonFilePath, StringBuilder logCapture, bool success)
        {
            try
            {
                string processedDir = Path.Combine(Path.GetDirectoryName(jsonFilePath), "processed");
                if (!Directory.Exists(processedDir))
                {
                    Directory.CreateDirectory(processedDir);
                }
                
                string fileName = Path.GetFileName(jsonFilePath);
                string timestamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                string logFilePath = Path.Combine(processedDir, $"{timestamp}-{fileNameWithoutExt}.log");
                
                // Save the log file
                logCapture.AppendLine();
                logCapture.AppendLine($"=== Processing Complete ===");
                logCapture.AppendLine($"Result: {(success ? "SUCCESS" : "FAILURE")}");
                logCapture.AppendLine($"End Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                File.WriteAllText(logFilePath, logCapture.ToString());
                Debug.Log($"[VibeUnity] Log saved to: {logFilePath}");
                
                // Also create latest.log in logs directory for easy access
                try
                {
                    string parentDir = Path.GetDirectoryName(processedDir); // Get .vibe-unity/commands
                    string logsDir = Path.Combine(parentDir, "logs");
                    if (!Directory.Exists(logsDir))
                    {
                        Directory.CreateDirectory(logsDir);
                    }
                    string latestLogPath = Path.Combine(logsDir, "latest.log");
                    File.WriteAllText(latestLogPath, logCapture.ToString());
                }
                catch (System.Exception logEx)
                {
                    Debug.LogWarning($"[VibeUnity] Failed to create latest.log: {logEx.Message}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VibeUnity] Failed to save log file: {e.Message}");
            }
        }
    }
    
    #region Data Structures
    
    /// <summary>
    /// Root structure for batch command files
    /// </summary>
    [System.Serializable]
    public class BatchCommandFile
    {
        public string version = "1.0";
        public string description = "";
        public SceneConfig scene;
        public BatchCommand[] commands;
    }
    
    /// <summary>
    /// Scene configuration for batch command files
    /// </summary>
    [System.Serializable]
    public class SceneConfig
    {
        public string name;
        public string path = "Assets/Scenes";
        public string type = "DefaultGameObjects";
        public bool create = false;
        public bool addToBuild = false;
    }
    
    /// <summary>
    /// Individual command structure
    /// </summary>
    [System.Serializable]
    public class BatchCommand
    {
        public string action;
        public string name;
        
        // Scene creation fields
        public string sceneName;  // Alternative field name for scene creation
        public string path;
        public string type;
        public bool addToBuild;
        
        // Canvas fields
        public string scene;
        public string renderMode;
        public int referenceWidth;
        public int referenceHeight;
        public string scaleMode;
        public int sortingOrder;
        public float[] worldPosition;
        
        // UI element fields
        public string parent;
        public float width;
        public float height;
        public string anchor;
        public float[] position;
        public float[] rotation;
        public float[] scale;
        
        // ScrollView specific
        public bool horizontal = true;
        public bool vertical = true;
        public string scrollbarVisibility = "AutoHideAndExpandViewport";
        public float scrollSensitivity = 1f;
        
        // Text specific
        public string text;
        public int fontSize;
        public string color;
        
        // Component specific
        public string componentType;
        public VibeUnityGameObjects.ComponentParameter[] parameters;
    }
    
    #endregion
}