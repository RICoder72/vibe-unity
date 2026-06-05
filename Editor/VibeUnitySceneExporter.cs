#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;

namespace VibeUnity.Editor
{
    /// <summary>
    /// Exports Unity scenes to comprehensive JSON state files with gap detection
    /// </summary>
    public static class VibeUnitySceneExporter
    {
        private static readonly HashSet<string> SupportedComponents = new HashSet<string>
        {
            // Transform and basic components
            "Transform", "RectTransform",
            
            // UI Components (currently supported by VibeUnity)
            "Canvas", "CanvasScaler", "GraphicRaycaster",
            "Image", "Text", "Button", "ScrollRect", "Scrollbar",
            "TextMeshPro", "TextMeshProUGUI",
            
            // Basic 3D components
            "MeshRenderer", "MeshFilter",
            
            // Camera and Lighting
            "Camera", "Light",
            
            // Audio
            "AudioSource", "AudioListener"
        };
        
        private static readonly HashSet<string> PartiallySupported = new HashSet<string>
        {
            // Components we support but might be missing some properties
            "Rigidbody", "Collider", "BoxCollider", "SphereCollider"
        };
        
        /// <summary>
        /// Exports the active scene to a JSON state file
        /// </summary>
        /// <param name="scenePath">Optional specific scene path to export</param>
        /// <returns>True if export was successful</returns>
        public static bool ExportActiveSceneState(string scenePath = null)
        {
            try
            {
                Scene activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    Debug.LogError("[VibeUnitySceneExporter] No active scene to export");
                    return false;
                }
                
                string outputPath;
                if (string.IsNullOrEmpty(scenePath))
                {
                    // Generate output path next to scene file
                    string sceneAssetPath = activeScene.path;
                    if (string.IsNullOrEmpty(sceneAssetPath))
                    {
                        Debug.LogError("[VibeUnitySceneExporter] Scene has no saved path. Save the scene first.");
                        return false;
                    }
                    
                    string directory = Path.GetDirectoryName(sceneAssetPath);
                    string sceneName = Path.GetFileNameWithoutExtension(sceneAssetPath);
                    outputPath = Path.Combine(directory, $"{sceneName}.state.json");
                }
                else
                {
                    outputPath = scenePath;
                }
                
                // Build the FULL scene state (every component, transform, UI info,
                // scene settings, asset refs and a coverage report) and serialize with
                // JsonUtility so it round-trips with the importer (FromJson<SceneState>).
                // This replaces the old hand-built JSON that silently dropped all
                // non-script components and produced a schema the importer couldn't read.
                SceneState state = ExportSceneToFullState(activeScene);
                string jsonContent = JsonUtility.ToJson(state, true);
                File.WriteAllText(outputPath, jsonContent);

                var summary = state.coverageReport != null ? state.coverageReport.summary : null;
                if (summary != null)
                {
                    Debug.Log($"[VibeUnitySceneExporter] Scene state exported: {outputPath} " +
                              $"({summary.totalGameObjects} objects, {summary.totalComponents} components, " +
                              $"{summary.coveragePercentage:F0}% coverage, canRebuild={summary.canFullyRebuild})");
                }
                else
                {
                    Debug.Log($"[VibeUnitySceneExporter] Scene state exported: {outputPath}");
                }

                // Refresh asset database to show new file
                AssetDatabase.Refresh();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibeUnitySceneExporter] Failed to export scene state: {e.Message}");
                Debug.LogError($"[VibeUnitySceneExporter] Stack trace: {e.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Builds a complete SceneState (all components, transforms, UI info, scene
        /// settings, asset references and a coverage report) using the full export
        /// helpers. This is the authoritative representation the importer consumes.
        /// </summary>
        private static SceneState ExportSceneToFullState(Scene scene)
        {
            var assetReferences = new HashSet<string>();
            var coverageData = new CoverageAnalysisData();
            var gameObjectsList = new List<GameObjectInfo>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ExportGameObjectRecursive(root, "", gameObjectsList, assetReferences, coverageData);
            }

            var metadata = ExportSceneMetadata(scene);
            metadata.totalGameObjects = gameObjectsList.Count;
            metadata.totalComponents = coverageData.GetTotalComponents();

            return new SceneState
            {
                version = "2.0",
                exportTime = DateTime.Now,
                metadata = metadata,
                gameObjects = gameObjectsList.ToArray(),
                settings = ExportSceneSettings(),
                assetReferences = new List<string>(assetReferences).ToArray(),
                coverageReport = GenerateCoverageReport(coverageData, gameObjectsList.Count)
            };
        }

        /// <summary>
        /// DEPRECATED and UNUSED: hand-built JSON that dropped non-script components
        /// and did not match the importer schema. Retained only for reference; the
        /// live path now uses ExportSceneToFullState. Do not call.
        /// </summary>
        private static string ExportSceneToJsonManually(Scene scene)
        {
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"version\": \"1.0\",");
            json.AppendLine($"  \"sceneName\": \"{EscapeJsonString(scene.name)}\",");
            json.AppendLine($"  \"exportTimestamp\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
            json.AppendLine("  \"gameObjects\": [");
            
            GameObject[] rootObjects = scene.GetRootGameObjects();
            bool isFirst = true;
            
            for (int i = 0; i < rootObjects.Length; i++)
            {
                ExportGameObjectToJsonManually(rootObjects[i], "", json, ref isFirst, 0);
            }
            
            json.AppendLine();
            json.AppendLine("  ]");
            json.Append("}");
            
            return json.ToString();
        }
        
        /// <summary>
        /// Recursively exports GameObject to JSON manually (ENHANCED: all objects + transform + scripts + UI text)
        /// </summary>
        private static void ExportGameObjectToJsonManually(GameObject gameObject, string parentPath, StringBuilder json, ref bool isFirst, int depth)
        {
            string currentPath = string.IsNullOrEmpty(parentPath) ? gameObject.name : $"{parentPath}/{gameObject.name}";
            
            // Get ALL script components (MonoBehaviour derivatives)
            Component[] components = gameObject.GetComponents<Component>();
            var allScripts = new List<string>();
            
            foreach (Component component in components)
            {
                if (component != null && component is MonoBehaviour)
                {
                    allScripts.Add(component.GetType().Name);
                }
            }
            
            // Extract UI text content
            string uiText = ExtractUIText(gameObject);
            
            // Export ALL GameObjects
            if (!isFirst)
            {
                json.AppendLine(",");
            }
            isFirst = false;
            
            string indent = new string(' ', 4); // 2 spaces base + 2 for array
            json.AppendLine($"{indent}{{");
            json.AppendLine($"{indent}  \"name\": \"{EscapeJsonString(gameObject.name)}\",");
            json.AppendLine($"{indent}  \"path\": \"{EscapeJsonString(currentPath)}\",");
            json.AppendLine($"{indent}  \"active\": {gameObject.activeInHierarchy.ToString().ToLower()},");
            
            // Add basic transform data for scene recreation
            Transform transform = gameObject.transform;
            Vector3 pos = transform.localPosition;
            Vector3 rot = transform.localEulerAngles;
            Vector3 scale = transform.localScale;
            
            json.AppendLine($"{indent}  \"position\": [{pos.x:F3}, {pos.y:F3}, {pos.z:F3}],");
            json.AppendLine($"{indent}  \"rotation\": [{rot.x:F3}, {rot.y:F3}, {rot.z:F3}],");
            json.Append($"{indent}  \"scale\": [{scale.x:F3}, {scale.y:F3}, {scale.z:F3}]");
            
            // Add UI text content if present
            if (!string.IsNullOrEmpty(uiText))
            {
                json.AppendLine(",");
                json.Append($"{indent}  \"text\": \"{EscapeJsonString(uiText)}\"");
            }
            
            // Include ALL script components if there are any
            if (allScripts.Count > 0)
            {
                json.AppendLine(",");
                json.AppendLine($"{indent}  \"scripts\": [");
                for (int i = 0; i < allScripts.Count; i++)
                {
                    json.Append($"{indent}    \"{EscapeJsonString(allScripts[i])}\"");
                    if (i < allScripts.Count - 1) json.AppendLine(",");
                    else json.AppendLine();
                }
                json.Append($"{indent}  ]");
            }
            else
            {
                json.AppendLine(); // Close the previous line
            }
            
            json.Append($"{indent}}}");
            
            // Process children
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Transform child = gameObject.transform.GetChild(i);
                ExportGameObjectToJsonManually(child.gameObject, currentPath, json, ref isFirst, depth + 1);
            }
        }
        
        /// <summary>
        /// Extracts UI text content from various UI components
        /// </summary>
        private static string ExtractUIText(GameObject gameObject)
        {
            // Check for TextMeshPro UI components
            var tmpText = gameObject.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
            {
                return tmpText.text;
            }
            
            // Check for legacy Text components
            var legacyText = gameObject.GetComponent<UnityEngine.UI.Text>();
            if (legacyText != null && !string.IsNullOrEmpty(legacyText.text))
            {
                return legacyText.text;
            }
            
            // Check for Button component (which might have text on a child)
            var button = gameObject.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                // Look for text in children
                var childText = gameObject.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (childText != null && !string.IsNullOrEmpty(childText.text))
                {
                    return childText.text;
                }
                
                var childLegacyText = gameObject.GetComponentInChildren<UnityEngine.UI.Text>();
                if (childLegacyText != null && !string.IsNullOrEmpty(childLegacyText.text))
                {
                    return childLegacyText.text;
                }
            }
            
            // Check for InputField
            var inputField = gameObject.GetComponent<TMPro.TMP_InputField>();
            if (inputField != null && !string.IsNullOrEmpty(inputField.text))
            {
                return inputField.text;
            }
            
            var legacyInputField = gameObject.GetComponent<UnityEngine.UI.InputField>();
            if (legacyInputField != null && !string.IsNullOrEmpty(legacyInputField.text))
            {
                return legacyInputField.text;
            }
            
            return null; // No text content found
        }
        
        
        /// <summary>
        /// Escapes strings for JSON
        /// </summary>
        private static string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            
            return str.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     .Replace("\n", "\\n")
                     .Replace("\r", "\\r")
                     .Replace("\t", "\\t");
        }
        
        /// <summary>
        /// DEPRECATED: Old Unity JsonUtility approach
        /// </summary>
        private static SceneState ExportSceneToState(Scene scene)
        {
            // Use ultra-minimal structure and convert it to SceneState for compatibility
            var minimalState = ExportSceneToMinimalState(scene);
            
            // Convert back to SceneState format but with minimal data
            var sceneState = new SceneState();
            sceneState.exportTime = DateTime.Now;
            
            sceneState.metadata = new SceneMetadata
            {
                sceneName = minimalState.sceneName,
                scenePath = scene.path,
                exportTimestamp = minimalState.exportTimestamp,
                exportedBy = "VibeUnity-Minimal"
            };
            
            // Convert minimal GameObjects to full GameObjectInfo structure (but with minimal data)
            var gameObjectsList = new List<GameObjectInfo>();
            foreach (var minimalObj in minimalState.gameObjects)
            {
                var gameObjectInfo = new GameObjectInfo
                {
                    name = minimalObj.name,
                    hierarchyPath = minimalObj.hierarchyPath,
                    isActive = minimalObj.isActive,
                    parentPath = minimalObj.parentPath,
                    childrenPaths = new string[0],
                    siblingIndex = 0,
                    transform = null,
                    uiInfo = null,
                    components = ConvertCustomScriptsToComponents(minimalObj.customScripts)
                };
                gameObjectsList.Add(gameObjectInfo);
            }
            
            sceneState.gameObjects = gameObjectsList.ToArray();
            sceneState.settings = null;
            sceneState.assetReferences = new string[0];
            sceneState.coverageReport = null;
            
            return sceneState;
        }
        
        /// <summary>
        /// Exports scene to ultra-minimal structure
        /// </summary>
        private static MinimalSceneState ExportSceneToMinimalState(Scene scene)
        {
            var minimalState = new MinimalSceneState
            {
                sceneName = scene.name,
                exportTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            var gameObjectsList = new List<MinimalGameObject>();
            
            GameObject[] rootObjects = scene.GetRootGameObjects();
            foreach (GameObject rootObj in rootObjects)
            {
                ExportGameObjectUltraMinimal(rootObj, "", gameObjectsList);
            }
            
            minimalState.gameObjects = gameObjectsList.ToArray();
            return minimalState;
        }
        
        /// <summary>
        /// Converts custom script names to ComponentInfo array
        /// </summary>
        private static ComponentInfo[] ConvertCustomScriptsToComponents(string[] customScripts)
        {
            if (customScripts == null || customScripts.Length == 0)
                return new ComponentInfo[0];
                
            var components = new ComponentInfo[customScripts.Length];
            for (int i = 0; i < customScripts.Length; i++)
            {
                components[i] = new ComponentInfo
                {
                    typeName = customScripts[i],
                    fullTypeName = customScripts[i],
                    enabled = true,
                    properties = new ComponentProperty[0],
                    isSupported = false,
                    missingFeatures = new string[0]
                };
            }
            return components;
        }
        
        /// <summary>
        /// Exports scene metadata
        /// </summary>
        private static SceneMetadata ExportSceneMetadata(Scene scene)
        {
            return new SceneMetadata
            {
                sceneName = scene.name,
                scenePath = scene.path,
                unityVersion = Application.unityVersion,
                exportTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                totalGameObjects = scene.rootCount,
                renderPipelines = GetRenderPipelineInfo()
            };
        }
        
        /// <summary>
        /// Gets render pipeline information
        /// </summary>
        private static string[] GetRenderPipelineInfo()
        {
            var pipelines = new List<string>();
            
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
            {
                string rpName = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.GetType().Name;
                if (rpName.Contains("Universal"))
                    pipelines.Add("URP");
                else if (rpName.Contains("HDRP") || rpName.Contains("HighDefinition"))
                    pipelines.Add("HDRP");
                else
                    pipelines.Add("Custom");
            }
            else
            {
                pipelines.Add("Built-in");
            }
            
            return pipelines.ToArray();
        }
        
        /// <summary>
        /// Exports scene-level settings
        /// </summary>
        private static SceneSettings ExportSceneSettings()
        {
            return new SceneSettings
            {
                lighting = new LightingSettings
                {
                    realtimeGI = Lightmapping.realtimeGI,
                    bakedGI = Lightmapping.bakedGI,
                    ambientColor = ColorToFloatArray(UnityEngine.RenderSettings.ambientLight),
                    ambientMode = UnityEngine.RenderSettings.ambientMode.ToString(),
                    ambientIntensity = UnityEngine.RenderSettings.ambientIntensity
                },
                render = new VibeUnity.Editor.RenderSettings
                {
                    fog = UnityEngine.RenderSettings.fog,
                    fogColor = ColorToFloatArray(UnityEngine.RenderSettings.fogColor),
                    fogMode = UnityEngine.RenderSettings.fogMode.ToString(),
                    fogDensity = UnityEngine.RenderSettings.fogDensity,
                    fogStartDistance = UnityEngine.RenderSettings.fogStartDistance,
                    fogEndDistance = UnityEngine.RenderSettings.fogEndDistance,
                    skyboxMaterial = AssetDatabase.GetAssetPath(UnityEngine.RenderSettings.skybox)
                },
                physics = new PhysicsSettings
                {
                    gravity = Vector3ToFloatArray(Physics.gravity),
                    bounceThreshold = Physics.bounceThreshold,
                    defaultSolverIterations = Physics.defaultSolverIterations,
                    defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations
                },
                audio = new AudioSettings
                {
                    volume = AudioListener.volume,
                    dopplerFactor = 1.0f, // Default Doppler factor
                    speedOfSound = 343.0f // Unity's default speed of sound
                }
            };
        }
        
        /// <summary>
        /// Recursively exports a GameObject with ultra-minimal data
        /// </summary>
        private static void ExportGameObjectUltraMinimal(GameObject gameObject, string parentPath, List<MinimalGameObject> gameObjectsList)
        {
            string currentPath = string.IsNullOrEmpty(parentPath) ? gameObject.name : $"{parentPath}/{gameObject.name}";
            
            // Get only custom script components
            Component[] components = gameObject.GetComponents<Component>();
            var customScripts = new List<string>();
            
            foreach (Component component in components)
            {
                if (component != null && IsCustomScript(component))
                {
                    customScripts.Add(component.GetType().Name);
                }
            }
            
            var minimalGameObject = new MinimalGameObject
            {
                name = gameObject.name,
                hierarchyPath = currentPath,
                isActive = gameObject.activeInHierarchy,
                parentPath = parentPath,
                customScripts = customScripts.ToArray()
            };
            
            gameObjectsList.Add(minimalGameObject);
            
            // Recursively export children
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Transform child = gameObject.transform.GetChild(i);
                ExportGameObjectUltraMinimal(child.gameObject, currentPath, gameObjectsList);
            }
        }
        
        /// <summary>
        /// DEPRECATED: Old minimal export method
        /// </summary>
        private static void ExportGameObjectMinimal(GameObject gameObject, string parentPath, List<GameObjectInfo> gameObjectsList)
        {
            string currentPath = string.IsNullOrEmpty(parentPath) ? gameObject.name : $"{parentPath}/{gameObject.name}";
            
            var gameObjectInfo = new GameObjectInfo
            {
                name = gameObject.name,
                hierarchyPath = currentPath,
                isActive = gameObject.activeInHierarchy,
                parentPath = parentPath,
                childrenPaths = new string[0], // Skip child paths for even more minimal
                components = new ComponentInfo[0], // Start with empty
                transform = null, // No transform data
                uiInfo = null // No UI data
            };
            
            // ONLY export custom script components - nothing else
            Component[] components = gameObject.GetComponents<Component>();
            var scriptComponents = new List<ComponentInfo>();
            
            foreach (Component component in components)
            {
                if (component != null && IsCustomScript(component))
                {
                    scriptComponents.Add(new ComponentInfo
                    {
                        typeName = component.GetType().Name,
                        fullTypeName = component.GetType().FullName,
                        enabled = GetComponentEnabledState(component),
                        properties = new ComponentProperty[0], // No properties - just presence
                        isSupported = false,
                        missingFeatures = new string[0]
                    });
                }
            }
            
            // Only set components if we have custom scripts
            if (scriptComponents.Count > 0)
            {
                gameObjectInfo.components = scriptComponents.ToArray();
            }
            
            gameObjectsList.Add(gameObjectInfo);
            
            // Recursively export children
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Transform child = gameObject.transform.GetChild(i);
                ExportGameObjectMinimal(child.gameObject, currentPath, gameObjectsList);
            }
        }
        
        /// <summary>
        /// DEPRECATED: Old recursive export method - keeping for reference
        /// </summary>
        private static void ExportGameObjectRecursive(GameObject gameObject, string parentPath, 
            List<GameObjectInfo> gameObjectsList, HashSet<string> assetReferences, CoverageAnalysisData coverageData)
        {
            string currentPath = string.IsNullOrEmpty(parentPath) ? gameObject.name : $"{parentPath}/{gameObject.name}";
            
            var gameObjectInfo = new GameObjectInfo
            {
                name = gameObject.name,
                hierarchyPath = currentPath,
                instanceId = gameObject.GetInstanceID(),
                isActive = gameObject.activeInHierarchy,
                tag = gameObject.tag,
                layer = gameObject.layer,
                isStatic = gameObject.isStatic,
                parentPath = parentPath,
                siblingIndex = gameObject.transform.GetSiblingIndex()
            };
            
            // Export transform
            gameObjectInfo.transform = ExportTransform(gameObject.transform);
            
            // Export children paths
            var childrenPaths = new List<string>();
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Transform child = gameObject.transform.GetChild(i);
                childrenPaths.Add($"{currentPath}/{child.name}");
            }
            gameObjectInfo.childrenPaths = childrenPaths.ToArray();
            
            // Export all components
            Component[] components = gameObject.GetComponents<Component>();
            var componentInfos = new List<ComponentInfo>();
            
            foreach (Component component in components)
            {
                if (component != null) // Null check for missing script components
                {
                    var componentInfo = ExportComponent(component, assetReferences, coverageData);
                    componentInfos.Add(componentInfo);
                }
                else
                {
                    // Handle missing script components
                    coverageData.AddGap("Component", "Critical", "MissingScript", 
                        "Missing script component detected", 1, new[] { currentPath },
                        "Restore missing script or remove component reference");
                }
            }
            
            gameObjectInfo.components = componentInfos.ToArray();
            
            // Export UI-specific information if this is a UI element
            if (gameObject.GetComponent<RectTransform>() != null)
            {
                gameObjectInfo.uiInfo = ExportUIElementInfo(gameObject, assetReferences);
            }
            
            gameObjectsList.Add(gameObjectInfo);
            
            // Recursively export children
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Transform child = gameObject.transform.GetChild(i);
                ExportGameObjectRecursive(child.gameObject, currentPath, gameObjectsList, assetReferences, coverageData);
            }
        }
        
        /// <summary>
        /// Exports transform information
        /// </summary>
        private static TransformInfo ExportTransform(Transform transform)
        {
            return new TransformInfo
            {
                localPosition = Vector3ToFloatArray(transform.localPosition),
                localRotation = QuaternionToFloatArray(transform.localRotation),
                localScale = Vector3ToFloatArray(transform.localScale),
                worldPosition = Vector3ToFloatArray(transform.position),
                worldRotation = QuaternionToFloatArray(transform.rotation),
                worldScale = Vector3ToFloatArray(transform.lossyScale)
            };
        }
        
        /// <summary>
        /// Exports a component with coverage analysis (minimalist approach - only scripts)
        /// </summary>
        private static ComponentInfo ExportComponent(Component component, HashSet<string> assetReferences, CoverageAnalysisData coverageData)
        {
            Type componentType = component.GetType();
            string typeName = componentType.Name;
            string fullTypeName = componentType.FullName;
            
            var componentInfo = new ComponentInfo
            {
                typeName = typeName,
                fullTypeName = fullTypeName,
                enabled = GetComponentEnabledState(component)
            };
            
            // Check if component is supported
            bool isSupported = SupportedComponents.Contains(typeName);
            bool isPartiallySupported = PartiallySupported.Contains(typeName);
            
            componentInfo.isSupported = isSupported;
            
            // Track coverage
            coverageData.TrackComponent(typeName, isSupported, isPartiallySupported);
            
            // MINIMALIST APPROACH: Only export properties for script components (MonoBehaviour derivatives)
            var properties = new List<ComponentProperty>();
            var missingFeatures = new List<string>();
            
            bool isCustomScript = IsCustomScript(component);
            if (isCustomScript)
            {
                // Only export detailed properties for custom scripts/MonoBehaviours
                ExportComponentProperties(component, properties, assetReferences, missingFeatures);
            }
            else 
            {
                // For built-in Unity components, only record basic positioning if it's Transform/RectTransform
                if (component is Transform || component is RectTransform)
                {
                    ExportComponentProperties(component, properties, assetReferences, missingFeatures);
                }
                // For all other Unity built-in components, skip detailed property export
            }
            
            componentInfo.properties = properties.ToArray();
            componentInfo.missingFeatures = missingFeatures.ToArray();
            
            // Log gaps for unsupported components
            if (!isSupported && !isPartiallySupported && isCustomScript)
            {
                coverageData.AddGap("Component", "Warning", typeName,
                    $"Custom script '{typeName}' is not supported for scene rebuilding",
                    1, new[] { GetGameObjectPath(component.gameObject) },
                    $"Add support for {typeName} component in VibeUnity package");
            }
            else if (isPartiallySupported && missingFeatures.Count > 0)
            {
                foreach (string feature in missingFeatures)
                {
                    coverageData.AddGap("Property", "Info", typeName,
                        $"Property '{feature}' not fully supported on {typeName}",
                        1, new[] { GetGameObjectPath(component.gameObject) },
                        $"Add property support for {typeName}.{feature}");
                }
            }
            
            return componentInfo;
        }
        
        /// <summary>
        /// Determines if a component is a custom script (MonoBehaviour derivative)
        /// </summary>
        private static bool IsCustomScript(Component component)
        {
            if (component == null) return false;
            
            Type componentType = component.GetType();
            
            // Check if it's a MonoBehaviour derivative
            if (!typeof(MonoBehaviour).IsAssignableFrom(componentType))
                return false;
            
            // Exclude Unity's built-in UI components that derive from MonoBehaviour
            string namespaceName = componentType.Namespace ?? "";
            string typeName = componentType.Name;
            
            // Skip Unity built-in components
            if (namespaceName.StartsWith("UnityEngine") || 
                namespaceName.StartsWith("UnityEditor") ||
                namespaceName.StartsWith("TMPro"))
                return false;
                
            // Skip known Unity UI components
            if (typeName == "Button" || typeName == "Toggle" || typeName == "Slider" ||
                typeName == "ScrollRect" || typeName == "Dropdown" || typeName == "InputField")
                return false;
            
            return true; // This is a custom script
        }
        
        /// <summary>
        /// Gets the enabled state of a component
        /// </summary>
        private static bool GetComponentEnabledState(Component component)
        {
            // Handle different component types that have enabled properties
            if (component is Behaviour behaviour)
                return behaviour.enabled;
            if (component is Collider collider)
                return collider.enabled;
            if (component is Renderer renderer)
                return renderer.enabled;
                
            return true; // Default for components without enabled state
        }
        
        /// <summary>
        /// Exports component properties using reflection
        /// </summary>
        private static void ExportComponentProperties(Component component, List<ComponentProperty> properties, 
            HashSet<string> assetReferences, List<string> missingFeatures)
        {
            Type componentType = component.GetType();
            
            // Get all serializable fields and properties
            FieldInfo[] fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo[] props = componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            // Export fields
            foreach (FieldInfo field in fields)
            {
                if (ShouldExportField(field))
                {
                    try
                    {
                        object value = field.GetValue(component);
                        var property = CreateComponentProperty(field.Name, value, field.FieldType, assetReferences);
                        properties.Add(property);
                    }
                    catch (Exception e)
                    {
                        missingFeatures.Add($"Field {field.Name}: {e.Message}");
                    }
                }
            }
            
            // Export properties (read-only for inspection)
            foreach (PropertyInfo prop in props)
            {
                if (ShouldExportProperty(prop))
                {
                    try
                    {
                        object value = prop.GetValue(component);
                        var property = CreateComponentProperty(prop.Name, value, prop.PropertyType, assetReferences);
                        properties.Add(property);
                    }
                    catch (Exception e)
                    {
                        missingFeatures.Add($"Property {prop.Name}: {e.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Determines if a field should be exported
        /// </summary>
        private static bool ShouldExportField(FieldInfo field)
        {
            // Skip Unity internal fields
            if (field.Name.StartsWith("m_"))
                return false;
                
            // Skip non-serializable fields
            if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                return false;
                
            return true;
        }
        
        /// <summary>
        /// Determines if a property should be exported
        /// </summary>
        private static bool ShouldExportProperty(PropertyInfo property)
        {
            // Only export readable properties
            if (!property.CanRead)
                return false;
                
            // Skip indexer properties
            if (property.GetIndexParameters().Length > 0)
                return false;
                
            // Skip Unity internal properties
            if (property.Name.StartsWith("m_"))
                return false;
                
            return true;
        }
        
        /// <summary>
        /// Creates a ComponentProperty from a field/property value
        /// </summary>
        private static ComponentProperty CreateComponentProperty(string name, object value, Type valueType, HashSet<string> assetReferences)
        {
            var property = new ComponentProperty
            {
                name = name,
                typeName = valueType.Name,
                isSerializable = IsSerializableType(valueType)
            };
            
            if (value == null)
            {
                property.value = "null";
            }
            else if (value is UnityEngine.Object unityObj)
            {
                // Handle Unity object references
                property.isAssetReference = true;
                property.assetPath = AssetDatabase.GetAssetPath(unityObj);
                property.value = unityObj.name;
                
                if (!string.IsNullOrEmpty(property.assetPath))
                {
                    assetReferences.Add(property.assetPath);
                }
            }
            else
            {
                // Convert value to string representation
                property.value = ConvertValueToString(value);
            }
            
            return property;
        }
        
        /// <summary>
        /// Checks if a type is serializable by Unity
        /// </summary>
        private static bool IsSerializableType(Type type)
        {
            if (type.IsPrimitive || type == typeof(string))
                return true;
                
            if (type.IsSubclassOf(typeof(UnityEngine.Object)))
                return true;
                
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) ||
                type == typeof(Quaternion) || type == typeof(Color) || type == typeof(Rect))
                return true;
                
            return type.GetCustomAttribute<SerializableAttribute>() != null;
        }
        
        /// <summary>
        /// Converts various value types to string representations
        /// </summary>
        private static string ConvertValueToString(object value)
        {
            switch (value)
            {
                case Vector2 v2:
                    return $"{v2.x},{v2.y}";
                case Vector3 v3:
                    return $"{v3.x},{v3.y},{v3.z}";
                case Vector4 v4:
                    return $"{v4.x},{v4.y},{v4.z},{v4.w}";
                case Quaternion q:
                    return $"{q.x},{q.y},{q.z},{q.w}";
                case Color c:
                    return $"{c.r},{c.g},{c.b},{c.a}";
                case Rect r:
                    return $"{r.x},{r.y},{r.width},{r.height}";
                default:
                    return value.ToString();
            }
        }
        
        /// <summary>
        /// Exports UI-specific element information
        /// </summary>
        private static UIElementInfo ExportUIElementInfo(GameObject gameObject, HashSet<string> assetReferences)
        {
            var uiInfo = new UIElementInfo();
            
            // Export RectTransform
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                uiInfo.rectTransform = new RectTransformInfo
                {
                    anchoredPosition = Vector2ToFloatArray(rectTransform.anchoredPosition),
                    anchorMin = Vector2ToFloatArray(rectTransform.anchorMin),
                    anchorMax = Vector2ToFloatArray(rectTransform.anchorMax),
                    offsetMin = Vector2ToFloatArray(rectTransform.offsetMin),
                    offsetMax = Vector2ToFloatArray(rectTransform.offsetMax),
                    pivot = Vector2ToFloatArray(rectTransform.pivot),
                    sizeDelta = Vector2ToFloatArray(rectTransform.sizeDelta)
                };
            }
            
            // Export Canvas information
            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas != null)
            {
                uiInfo.canvas = new CanvasInfo
                {
                    renderMode = canvas.renderMode.ToString(),
                    planeDistance = canvas.planeDistance,
                    sortingOrder = canvas.sortingOrder,
                    sortingLayerName = canvas.sortingLayerName,
                    overrideSorting = canvas.overrideSorting
                };
                
                CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    uiInfo.canvas.scaleFactor = scaler.scaleFactor;
                    uiInfo.canvas.referenceResolution = Vector2ToFloatArray(scaler.referenceResolution);
                    uiInfo.canvas.scaleMode = scaler.uiScaleMode.ToString();
                    uiInfo.canvas.matchWidthOrHeight = scaler.matchWidthOrHeight;
                }
            }
            
            // Export Graphic information (Image, Text, etc.)
            Graphic graphic = gameObject.GetComponent<Graphic>();
            if (graphic != null)
            {
                uiInfo.graphic = new GraphicInfo
                {
                    graphicType = graphic.GetType().Name,
                    color = ColorToFloatArray(graphic.color),
                    raycastTarget = graphic.raycastTarget
                };
                
                if (graphic.material != null)
                {
                    uiInfo.graphic.material = AssetDatabase.GetAssetPath(graphic.material);
                    assetReferences.Add(uiInfo.graphic.material);
                }
                
                // Handle specific graphic types
                if (graphic is Image image)
                {
                    uiInfo.graphic.imageType = image.type.ToString();
                    uiInfo.graphic.preserveAspect = image.preserveAspect;
                    if (image.sprite != null)
                    {
                        uiInfo.graphic.sprite = AssetDatabase.GetAssetPath(image.sprite);
                        assetReferences.Add(uiInfo.graphic.sprite);
                    }
                }
                else if (graphic is Text text)
                {
                    uiInfo.graphic.text = text.text;
                    uiInfo.graphic.fontSize = text.fontSize;
                    uiInfo.graphic.fontStyle = text.fontStyle.ToString();
                    uiInfo.graphic.alignment = text.alignment.ToString();
                    uiInfo.graphic.richText = text.supportRichText;
                    if (text.font != null)
                    {
                        uiInfo.graphic.font = AssetDatabase.GetAssetPath(text.font);
                        assetReferences.Add(uiInfo.graphic.font);
                    }
                }
                else if (graphic is TextMeshProUGUI tmpText)
                {
                    uiInfo.graphic.text = tmpText.text;
                    uiInfo.graphic.fontSize = (int)tmpText.fontSize;
                    uiInfo.graphic.fontStyle = tmpText.fontStyle.ToString();
                    uiInfo.graphic.alignment = tmpText.alignment.ToString();
                    uiInfo.graphic.richText = tmpText.richText;
                    if (tmpText.font != null)
                    {
                        uiInfo.graphic.font = AssetDatabase.GetAssetPath(tmpText.font);
                        assetReferences.Add(uiInfo.graphic.font);
                    }
                }
            }
            
            return uiInfo;
        }
        
        /// <summary>
        /// Generates a comprehensive coverage report
        /// </summary>
        private static CoverageReport GenerateCoverageReport(CoverageAnalysisData coverageData, int totalGameObjects)
        {
            var report = new CoverageReport();
            
            // Generate summary
            int totalComponents = coverageData.GetTotalComponents();
            int supportedComponents = coverageData.GetSupportedComponents();
            int partiallySupported = coverageData.GetPartiallySupported();
            
            report.summary = new CoverageSummary
            {
                totalGameObjects = totalGameObjects,
                totalComponents = totalComponents,
                supportedComponents = supportedComponents,
                partiallySupported = partiallySupported,
                unsupportedComponents = totalComponents - supportedComponents - partiallySupported,
                coveragePercentage = totalComponents > 0 ? (float)(supportedComponents + partiallySupported) / totalComponents * 100f : 100f,
                canFullyRebuild = coverageData.GetCriticalGaps() == 0
            };
            
            // Generate component coverage details
            report.componentCoverage = coverageData.GetComponentCoverage();
            
            // Generate gaps list
            report.gaps = coverageData.GetGaps();
            
            // Generate recommendations
            report.recommendations = GenerateRecommendations(coverageData);
            
            return report;
        }
        
        /// <summary>
        /// Generates recommendations based on coverage analysis
        /// </summary>
        private static string[] GenerateRecommendations(CoverageAnalysisData coverageData)
        {
            var recommendations = new List<string>();
            
            var gaps = coverageData.GetGaps();
            var componentGaps = gaps.Where(g => g.category == "Component" && g.severity == "Warning").ToArray();
            
            if (componentGaps.Length > 0)
            {
                var topMissing = componentGaps.OrderByDescending(g => g.affectedCount).Take(3);
                recommendations.Add($"Implement support for most common missing components: {string.Join(", ", topMissing.Select(g => g.componentType))}");
            }
            
            int criticalGaps = gaps.Count(g => g.severity == "Critical");
            if (criticalGaps > 0)
            {
                recommendations.Add($"Fix {criticalGaps} critical issues that prevent scene rebuilding");
            }
            
            float coverage = coverageData.GetCoveragePercentage();
            if (coverage < 80f)
            {
                recommendations.Add($"Current coverage is {coverage:F1}%. Focus on most frequently used components to improve coverage.");
            }
            
            return recommendations.ToArray();
        }
        
        /// <summary>
        /// Generates a detailed gap analysis report file
        /// </summary>
        private static void GenerateGapAnalysisReport(SceneState sceneState, string sceneStateFilePath)
        {
            try
            {
                // Create coverage analysis directory
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string coverageDir = Path.Combine(projectRoot, ".vibe-unity", "commands", "coverage-analysis");
                if (!Directory.Exists(coverageDir))
                {
                    Directory.CreateDirectory(coverageDir);
                }
                
                // Generate report file name
                string sceneName = sceneState.metadata.sceneName;
                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string reportPath = Path.Combine(coverageDir, $"{timestamp}-{sceneName}-gaps.log");
                
                // Generate report content
                var report = new StringBuilder();
                report.AppendLine($"=== Scene Coverage Analysis Report ===");
                report.AppendLine($"Scene: {sceneState.metadata.sceneName}");
                report.AppendLine($"Generated: {sceneState.metadata.exportTimestamp}");
                report.AppendLine($"Unity Version: {sceneState.metadata.unityVersion}");
                report.AppendLine($"State File: {sceneStateFilePath}");
                report.AppendLine();
                
                // Summary
                var summary = sceneState.coverageReport.summary;
                report.AppendLine($"=== COVERAGE SUMMARY ===");
                report.AppendLine($"Total GameObjects: {summary.totalGameObjects}");
                report.AppendLine($"Total Components: {summary.totalComponents}");
                report.AppendLine($"Supported: {summary.supportedComponents} ({(float)summary.supportedComponents/summary.totalComponents*100:F1}%)");
                report.AppendLine($"Partially Supported: {summary.partiallySupported} ({(float)summary.partiallySupported/summary.totalComponents*100:F1}%)");
                report.AppendLine($"Unsupported: {summary.unsupportedComponents} ({(float)summary.unsupportedComponents/summary.totalComponents*100:F1}%)");
                report.AppendLine($"Overall Coverage: {summary.coveragePercentage:F1}%");
                report.AppendLine($"Can Fully Rebuild: {(summary.canFullyRebuild ? "YES" : "NO")}");
                report.AppendLine();
                
                // Component breakdown
                report.AppendLine($"=== COMPONENT BREAKDOWN ===");
                foreach (var componentCov in sceneState.coverageReport.componentCoverage)
                {
                    string status = componentCov.isSupported ? "✅ SUPPORTED" : 
                                   componentCov.supportLevel == "Partial" ? "⚠️ PARTIAL" : "❌ MISSING";
                    report.AppendLine($"{status} {componentCov.componentType} ({componentCov.instanceCount} instances)");
                    
                    if (componentCov.missingProperties?.Length > 0)
                    {
                        report.AppendLine($"   Missing Properties: {string.Join(", ", componentCov.missingProperties)}");
                    }
                    if (componentCov.missingFeatures?.Length > 0)
                    {
                        report.AppendLine($"   Missing Features: {string.Join(", ", componentCov.missingFeatures)}");
                    }
                }
                report.AppendLine();
                
                // Detailed gaps
                report.AppendLine($"=== DETAILED GAPS ===");
                var gapsByCategory = sceneState.coverageReport.gaps.GroupBy(g => g.category);
                foreach (var categoryGroup in gapsByCategory)
                {
                    report.AppendLine($"--- {categoryGroup.Key.ToUpper()} GAPS ---");
                    foreach (var gap in categoryGroup.OrderByDescending(g => g.affectedCount))
                    {
                        string severity = gap.severity == "Critical" ? "🔴 CRITICAL" :
                                         gap.severity == "Warning" ? "🟡 WARNING" : "🔵 INFO";
                        report.AppendLine($"{severity}: {gap.missingItem}");
                        report.AppendLine($"   Description: {gap.description}");
                        report.AppendLine($"   Affected Count: {gap.affectedCount}");
                        report.AppendLine($"   Recommendation: {gap.recommendation}");
                        if (gap.examplePaths?.Length > 0)
                        {
                            report.AppendLine($"   Example Paths: {string.Join(", ", gap.examplePaths.Take(3))}");
                        }
                        report.AppendLine();
                    }
                }
                
                // Recommendations
                if (sceneState.coverageReport.recommendations?.Length > 0)
                {
                    report.AppendLine($"=== RECOMMENDATIONS ===");
                    for (int i = 0; i < sceneState.coverageReport.recommendations.Length; i++)
                    {
                        report.AppendLine($"{i + 1}. {sceneState.coverageReport.recommendations[i]}");
                    }
                    report.AppendLine();
                }
                
                report.AppendLine($"=== END REPORT ===");
                
                // Write report file
                File.WriteAllText(reportPath, report.ToString());
                Debug.Log($"[VibeUnitySceneExporter] Gap analysis report saved: {reportPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibeUnitySceneExporter] Failed to generate gap analysis report: {e.Message}");
            }
        }
        
        #region Helper Methods
        
        private static float[] ColorToFloatArray(Color color)
        {
            return new float[] { color.r, color.g, color.b, color.a };
        }
        
        private static float[] Vector2ToFloatArray(Vector2 vector)
        {
            return new float[] { vector.x, vector.y };
        }
        
        private static float[] Vector3ToFloatArray(Vector3 vector)
        {
            return new float[] { vector.x, vector.y, vector.z };
        }
        
        private static float[] QuaternionToFloatArray(Quaternion quaternion)
        {
            return new float[] { quaternion.x, quaternion.y, quaternion.z, quaternion.w };
        }
        
        private static string GetGameObjectPath(GameObject gameObject)
        {
            return VibeUnityGameObjects.GetPath(gameObject);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Internal class for tracking coverage analysis during export
    /// </summary>
    internal class CoverageAnalysisData
    {
        private Dictionary<string, ComponentCoverageInfo> componentCoverage = new Dictionary<string, ComponentCoverageInfo>();
        private List<Gap> gaps = new List<Gap>();
        
        public void TrackComponent(string componentType, bool isSupported, bool isPartiallySupported)
        {
            if (!componentCoverage.ContainsKey(componentType))
            {
                componentCoverage[componentType] = new ComponentCoverageInfo
                {
                    componentType = componentType,
                    isSupported = isSupported,
                    supportLevel = isSupported ? "Full" : isPartiallySupported ? "Partial" : "None"
                };
            }
            
            componentCoverage[componentType].instanceCount++;
        }
        
        public void AddGap(string category, string severity, string componentType, string description, 
            int affectedCount, string[] examplePaths, string recommendation)
        {
            gaps.Add(new Gap
            {
                category = category,
                severity = severity,
                componentType = componentType,
                missingItem = componentType,
                description = description,
                affectedCount = affectedCount,
                examplePaths = examplePaths,
                recommendation = recommendation
            });
        }
        
        public int GetTotalComponents()
        {
            return componentCoverage.Values.Sum(c => c.instanceCount);
        }
        
        public int GetSupportedComponents()
        {
            return componentCoverage.Values.Where(c => c.supportLevel == "Full").Sum(c => c.instanceCount);
        }
        
        public int GetPartiallySupported()
        {
            return componentCoverage.Values.Where(c => c.supportLevel == "Partial").Sum(c => c.instanceCount);
        }
        
        public int GetCriticalGaps()
        {
            return gaps.Count(g => g.severity == "Critical");
        }
        
        public float GetCoveragePercentage()
        {
            int total = GetTotalComponents();
            if (total == 0) return 100f;
            return (float)(GetSupportedComponents() + GetPartiallySupported()) / total * 100f;
        }
        
        public ComponentCoverage[] GetComponentCoverage()
        {
            return componentCoverage.Values.Select(c => new ComponentCoverage
            {
                componentType = c.componentType,
                instanceCount = c.instanceCount,
                isSupported = c.isSupported,
                supportLevel = c.supportLevel
            }).ToArray();
        }
        
        public Gap[] GetGaps()
        {
            return gaps.ToArray();
        }
    }
    
    internal class ComponentCoverageInfo
    {
        public string componentType;
        public int instanceCount;
        public bool isSupported;
        public string supportLevel;
    }
}
#endif