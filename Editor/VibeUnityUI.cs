using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace VibeUnity.Editor
{
    /// <summary>
    /// UI element creation functionality for Vibe Unity
    /// </summary>
    public static class VibeUnityUI
    {
        #region Canvas Creation
        
        /// <summary>
        /// Adds a canvas to the specified or currently active scene with specified parameters
        /// </summary>
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
            try
            {
                // Load target scene if specified
                if (!VibeUnityScenes.LoadTargetScene(sceneName))
                {
                    return false;
                }
                
                // Validate active scene
                UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    Debug.LogError($"[VibeUnity] ❌ ERROR: No valid scene available for canvas '{canvasName}'");
                    Debug.LogError($"[VibeUnity]    └─ Target Scene: '{sceneName ?? "current"}'");
                    return false;
                }
                
                // Create canvas GameObject
                GameObject canvasGO = new GameObject(canvasName);
                Canvas canvas = canvasGO.AddComponent<Canvas>();
                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();
                
                // Set render mode
                canvas.renderMode = ParseRenderMode(renderMode);
                canvas.sortingOrder = sortingOrder;
                
                // Set vertex color always in gamma space for UI consistency
                canvas.vertexColorAlwaysGammaSpace = true;
                
                // Configure canvas scaler
                scaler.uiScaleMode = ParseScaleMode(scaleMode);
                scaler.referenceResolution = new Vector2(referenceWidth, referenceHeight);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                
                // Set world position for WorldSpace canvas
                if (canvas.renderMode == RenderMode.WorldSpace && worldPosition.HasValue)
                {
                    canvasGO.transform.position = worldPosition.Value;
                }
                
                // Create EventSystem if it doesn't exist
                if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    CreateEventSystem();
                }
                
                // Log detailed success information
                Debug.Log($"[VibeUnity] ✅ SUCCESS: Created canvas '{canvasName}'");
                Debug.Log($"[VibeUnity]    └─ Render Mode: {renderMode}");
                Debug.Log($"[VibeUnity]    └─ Resolution: {referenceWidth}x{referenceHeight}");
                Debug.Log($"[VibeUnity]    └─ Scale Mode: {scaleMode}");
                Debug.Log($"[VibeUnity]    └─ Sorting Order: {sortingOrder}");
                Debug.Log($"[VibeUnity]    └─ Vertex Color: Always in Gamma Space");
                Debug.Log($"[VibeUnity]    └─ Components: Canvas, CanvasScaler, GraphicRaycaster");
                Debug.Log($"[VibeUnity]    └─ Hierarchy: {VibeUnityGameObjects.GetPath(canvasGO)}");
                
                // Mark scene as dirty before saving
                VibeUnityScenes.MarkActiveSceneDirty();
                
                // Save the scene after successful creation
                VibeUnityScenes.SaveActiveScene(false, true); // Force save even during batch processing
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VibeUnity] Exception creating canvas: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Creates EventSystem if none exists in the scene
        /// </summary>
        private static void CreateEventSystem()
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[VibeUnity] Created EventSystem for UI interaction");
            
            // Mark scene as dirty after creating EventSystem
            VibeUnityScenes.MarkActiveSceneDirty();
        }
        
        #endregion
        
        #region UI Elements
        
        /// <summary>
        /// Creates a UI panel with optional parent GameObject
        /// </summary>
        public static bool AddPanel(
            string panelName,
            string parentName = null,
            string sceneName = null,
            float width = 200f,
            float height = 200f,
            string anchorPreset = "MiddleCenter")
        {
            try
            {
                // Load target scene if specified
                if (!VibeUnityScenes.LoadTargetScene(sceneName))
                {
                    return false;
                }
                
                // Find parent GameObject
                GameObject parent = FindUIParent(parentName);
                if (parent == null)
                {
                    Debug.LogError($"[VibeUnity] ❌ ERROR: Parent lookup failed for panel '{panelName}'");
                    Debug.LogError($"[VibeUnity]    └─ Requested Parent: '{parentName ?? "auto-detect"}'");
                    Debug.LogError($"[VibeUnity]    └─ Target Scene: '{sceneName ?? "current"}'");
                    Debug.LogError($"[VibeUnity]    └─ Available GameObjects: {VibeUnityGameObjects.ListAvailable()}");
                    return false;
                }
                
                // Create panel GameObject
                GameObject panelGO = new GameObject(panelName);
                panelGO.transform.SetParent(parent.transform, false);
                
                // Add UI components
                Image panelImage = panelGO.AddComponent<Image>();
                panelImage.color = new Color(1f, 1f, 1f, 0.392f); // Default panel background
                
                // Setup RectTransform
                RectTransform rectTransform = panelGO.GetComponent<RectTransform>();
                SetupRectTransform(rectTransform, width, height, anchorPreset);
                
                // Log detailed success information
                Debug.Log($"[VibeUnity] ✅ SUCCESS: Created panel '{panelName}'");
                Debug.Log($"[VibeUnity]    └─ Parent: {parent.name} (Type: {parent.GetComponent<Canvas>()?.GetType().Name ?? parent.GetType().Name})");
                Debug.Log($"[VibeUnity]    └─ Components: Image (background)");
                Debug.Log($"[VibeUnity]    └─ Size: {width}x{height}");
                Debug.Log($"[VibeUnity]    └─ Anchor: {anchorPreset}");
                Debug.Log($"[VibeUnity]    └─ Hierarchy: {VibeUnityGameObjects.GetPath(panelGO)}");
                
                // Mark scene as dirty before saving
                VibeUnityScenes.MarkActiveSceneDirty();
                
                // Save the scene after successful creation
                VibeUnityScenes.SaveActiveScene(false, true); // Force save even during batch processing
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VibeUnity] Exception creating panel: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Creates a UI button with optional parent GameObject
        /// </summary>
        public static bool AddButton(
            string buttonName,
            string parentName = null,
            string sceneName = null,
            string buttonText = "Button",
            float width = 160f,
            float height = 30f,
            string anchorPreset = "MiddleCenter")
        {
            try
            {
                // Load target scene if specified
                if (!VibeUnityScenes.LoadTargetScene(sceneName))
                {
                    return false;
                }
                
                // Find parent GameObject
                GameObject parent = FindUIParent(parentName);
                if (parent == null)
                {
                    Debug.LogError($"[VibeUnity] ❌ ERROR: Parent lookup failed for button '{buttonName}'");
                    Debug.LogError($"[VibeUnity]    └─ Requested Parent: '{parentName ?? "auto-detect"}'");
                    Debug.LogError($"[VibeUnity]    └─ Target Scene: '{sceneName ?? "current"}'");
                    Debug.LogError($"[VibeUnity]    └─ Available GameObjects: {VibeUnityGameObjects.ListAvailable()}");
                    return false;
                }
                
                // Create button GameObject
                GameObject buttonGO = new GameObject(buttonName);
                buttonGO.transform.SetParent(parent.transform, false);
                
                // Add button components
                Image buttonImage = buttonGO.AddComponent<Image>();
                Button button = buttonGO.AddComponent<Button>();
                
                // Setup RectTransform
                RectTransform rectTransform = buttonGO.GetComponent<RectTransform>();
                SetupRectTransform(rectTransform, width, height, anchorPreset);
                
                // Create text child
                if (!string.IsNullOrEmpty(buttonText))
                {
                    CreateButtonText(buttonGO, buttonText);
                }
                
                // Log detailed success information
                Debug.Log($"[VibeUnity] ✅ SUCCESS: Created button '{buttonName}'");  
                Debug.Log($"[VibeUnity]    └─ Parent: {parent.name} (Type: {parent.GetComponent<Canvas>()?.GetType().Name ?? parent.GetType().Name})");
                Debug.Log($"[VibeUnity]    └─ Components: Image, Button");
                Debug.Log($"[VibeUnity]    └─ Text: \"{buttonText}\"");
                Debug.Log($"[VibeUnity]    └─ Size: {width}x{height}");
                Debug.Log($"[VibeUnity]    └─ Anchor: {anchorPreset}");
                Debug.Log($"[VibeUnity]    └─ Hierarchy: {VibeUnityGameObjects.GetPath(buttonGO)}");
                
                // Mark scene as dirty before saving
                VibeUnityScenes.MarkActiveSceneDirty();
                
                // Save the scene after successful creation
                VibeUnityScenes.SaveActiveScene(false, true); // Force save even during batch processing
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VibeUnity] Exception creating button: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Creates a UI text element with optional parent GameObject
        /// </summary>
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
            try
            {
                // Load target scene if specified
                if (!VibeUnityScenes.LoadTargetScene(sceneName))
                {
                    return false;
                }
                
                // Find parent GameObject
                GameObject parent = FindUIParent(parentName);
                if (parent == null)
                {
                    Debug.LogError($"[VibeUnity] ❌ ERROR: Parent lookup failed for text '{textName}'");
                    Debug.LogError($"[VibeUnity]    └─ Requested Parent: '{parentName ?? "auto-detect"}'");
                    Debug.LogError($"[VibeUnity]    └─ Target Scene: '{sceneName ?? "current"}'");
                    Debug.LogError($"[VibeUnity]    └─ Available GameObjects: {VibeUnityGameObjects.ListAvailable()}");
                    return false;
                }
                
                // Create text GameObject
                GameObject textGO = new GameObject(textName);
                textGO.transform.SetParent(parent.transform, false);
                
                // Add TextMeshPro text component
                TextMeshProUGUI textComponent = textGO.AddComponent<TextMeshProUGUI>();
                textComponent.text = textContent;
                textComponent.fontSize = fontSize;
                textComponent.alignment = TextAlignmentOptions.Center;
                
                // Setup RectTransform
                RectTransform rectTransform = textGO.GetComponent<RectTransform>();
                SetupRectTransform(rectTransform, width, height, anchorPreset);
                
                // Log detailed success information
                Debug.Log($"[VibeUnity] ✅ SUCCESS: Created text '{textName}'");
                Debug.Log($"[VibeUnity]    └─ Parent: {parent.name} (Type: {parent.GetComponent<Canvas>()?.GetType().Name ?? parent.GetType().Name})");
                Debug.Log($"[VibeUnity]    └─ Components: TextMeshProUGUI");
                Debug.Log($"[VibeUnity]    └─ Content: \"{textContent}\"");
                Debug.Log($"[VibeUnity]    └─ Font Size: {fontSize}px");
                Debug.Log($"[VibeUnity]    └─ Size: {width}x{height}");
                Debug.Log($"[VibeUnity]    └─ Anchor: {anchorPreset}");
                Debug.Log($"[VibeUnity]    └─ Hierarchy: {VibeUnityGameObjects.GetPath(textGO)}");
                
                // Mark scene as dirty before saving
                VibeUnityScenes.MarkActiveSceneDirty();
                
                // Save the scene after successful creation
                VibeUnityScenes.SaveActiveScene(false, true); // Force save even during batch processing
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VibeUnity] Exception creating text: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Creates a UI scroll view with optional parent GameObject
        /// </summary>
        public static bool AddScrollView(
            string scrollViewName,
            string parentName = null,
            string sceneName = null,
            float width = 300f,
            float height = 200f,
            bool horizontal = true,
            bool vertical = true,
            string scrollbarVisibility = "AutoHideAndExpandViewport",
            float scrollSensitivity = 1f,
            string anchorPreset = "MiddleCenter")
        {
            try
            {
                // Load target scene if specified
                if (!VibeUnityScenes.LoadTargetScene(sceneName))
                {
                    return false;
                }
                
                // Find parent GameObject
                GameObject parent = FindUIParent(parentName);
                if (parent == null)
                {
                    Debug.LogError($"[VibeUnity] ❌ ERROR: Parent lookup failed for scroll view '{scrollViewName}'");
                    Debug.LogError($"[VibeUnity]    └─ Requested Parent: '{parentName ?? "auto-detect"}'");
                    Debug.LogError($"[VibeUnity]    └─ Target Scene: '{sceneName ?? "current"}'");
                    Debug.LogError($"[VibeUnity]    └─ Available GameObjects: {VibeUnityGameObjects.ListAvailable()}");
                    return false;
                }
                
                // Create ScrollView GameObject
                GameObject scrollViewGO = new GameObject(scrollViewName);
                scrollViewGO.transform.SetParent(parent.transform, false);
                
                // Add and configure RectTransform
                RectTransform scrollViewRect = scrollViewGO.AddComponent<RectTransform>();
                SetupRectTransform(scrollViewRect, width, height, anchorPreset);
                
                // Add Image component for background
                Image scrollViewImage = scrollViewGO.AddComponent<Image>();
                scrollViewImage.color = new Color(1f, 1f, 1f, 0.392f);
                
                // Add ScrollRect component
                ScrollRect scrollRect = scrollViewGO.AddComponent<ScrollRect>();
                scrollRect.horizontal = horizontal;
                scrollRect.vertical = vertical;
                scrollRect.scrollSensitivity = scrollSensitivity;
                
                // Parse scrollbar visibility
                scrollRect.horizontalScrollbarVisibility = ParseScrollbarVisibility(scrollbarVisibility);
                scrollRect.verticalScrollbarVisibility = ParseScrollbarVisibility(scrollbarVisibility);
                
                // Create Viewport
                GameObject viewport = new GameObject("Viewport");
                viewport.transform.SetParent(scrollViewGO.transform, false);
                
                RectTransform viewportRect = viewport.AddComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.sizeDelta = Vector2.zero;
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;
                
                Image viewportImage = viewport.AddComponent<Image>();
                viewportImage.color = Color.clear;
                
                Mask viewportMask = viewport.AddComponent<Mask>();
                viewportMask.showMaskGraphic = false;
                
                // Create Content
                GameObject content = new GameObject("Content");
                content.transform.SetParent(viewport.transform, false);
                
                RectTransform contentRect = content.AddComponent<RectTransform>();
                contentRect.anchorMin = Vector2.up;
                contentRect.anchorMax = Vector2.one;
                contentRect.sizeDelta = new Vector2(0, 300);
                contentRect.pivot = Vector2.up;
                
                // Assign references to ScrollRect
                scrollRect.viewport = viewportRect;
                scrollRect.content = contentRect;

                // Create and wire actual Scrollbar objects so the visibility modes
                // (e.g. AutoHideAndExpandViewport) function. Previously the visibility
                // was set but no scrollbars existed and the references stayed null.
                if (horizontal)
                {
                    scrollRect.horizontalScrollbar = CreateScrollbar(scrollViewGO, false);
                }
                if (vertical)
                {
                    scrollRect.verticalScrollbar = CreateScrollbar(scrollViewGO, true);
                }

                // Log detailed success information
                Debug.Log($"[VibeUnity] ✅ SUCCESS: Created scroll view '{scrollViewName}'");
                Debug.Log($"[VibeUnity]    └─ Parent: {parent.name} (Type: {parent.GetComponent<Canvas>()?.GetType().Name ?? parent.GetType().Name})");
                Debug.Log($"[VibeUnity]    └─ Components: Image, ScrollRect, Viewport (with Mask), Content");
                Debug.Log($"[VibeUnity]    └─ Size: {width}x{height}");
                Debug.Log($"[VibeUnity]    └─ Horizontal: {horizontal}");
                Debug.Log($"[VibeUnity]    └─ Vertical: {vertical}");
                Debug.Log($"[VibeUnity]    └─ Anchor: {anchorPreset}");
                Debug.Log($"[VibeUnity]    └─ Hierarchy: {VibeUnityGameObjects.GetPath(scrollViewGO)}");
                
                // Mark scene as dirty before saving
                VibeUnityScenes.MarkActiveSceneDirty();
                
                // Save the scene after successful creation
                VibeUnityScenes.SaveActiveScene(false, true); // Force save even during batch processing
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VibeUnity] Exception creating scrollview: {e.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region Helper Methods

        /// <summary>
        /// Creates a functional Scrollbar (background + sliding area + handle) parented
        /// to the scroll view, matching the structure Unity's UI menu produces.
        /// </summary>
        private static Scrollbar CreateScrollbar(GameObject scrollViewGO, bool vertical)
        {
            string name = vertical ? "Scrollbar Vertical" : "Scrollbar Horizontal";
            GameObject sbGO = new GameObject(name);
            sbGO.transform.SetParent(scrollViewGO.transform, false);

            RectTransform sbRect = sbGO.AddComponent<RectTransform>();
            if (vertical)
            {
                sbRect.anchorMin = new Vector2(1, 0);
                sbRect.anchorMax = new Vector2(1, 1);
                sbRect.pivot = new Vector2(1, 1);
                sbRect.sizeDelta = new Vector2(20, 0);
            }
            else
            {
                sbRect.anchorMin = new Vector2(0, 0);
                sbRect.anchorMax = new Vector2(1, 0);
                sbRect.pivot = new Vector2(0, 0);
                sbRect.sizeDelta = new Vector2(0, 20);
            }

            Image sbImage = sbGO.AddComponent<Image>();
            sbImage.color = new Color(0.86f, 0.86f, 0.86f, 1f);

            Scrollbar scrollbar = sbGO.AddComponent<Scrollbar>();
            scrollbar.direction = vertical ? Scrollbar.Direction.BottomToTop : Scrollbar.Direction.LeftToRight;

            GameObject slidingArea = new GameObject("Sliding Area");
            slidingArea.transform.SetParent(sbGO.transform, false);
            RectTransform saRect = slidingArea.AddComponent<RectTransform>();
            saRect.anchorMin = Vector2.zero;
            saRect.anchorMax = Vector2.one;
            saRect.offsetMin = new Vector2(10, 10);
            saRect.offsetMax = new Vector2(-10, -10);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(slidingArea.transform, false);
            RectTransform handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 20);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.66f, 0.66f, 0.66f, 1f);

            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;
            return scrollbar;
        }

        /// <summary>
        /// Parses render mode from string
        /// </summary>
        private static RenderMode ParseRenderMode(string renderMode)
        {
            switch (renderMode.ToLower())
            {
                case "screenspaceoverlay":
                    return RenderMode.ScreenSpaceOverlay;
                case "screenspacecamera":
                    return RenderMode.ScreenSpaceCamera;
                case "worldspace":
                    return RenderMode.WorldSpace;
                default:
                    Debug.LogWarning($"[VibeUnity] Unknown render mode '{renderMode}', using ScreenSpaceOverlay");
                    return RenderMode.ScreenSpaceOverlay;
            }
        }
        
        /// <summary>
        /// Parses scale mode from string
        /// </summary>
        private static CanvasScaler.ScaleMode ParseScaleMode(string scaleMode)
        {
            switch (scaleMode.ToLower())
            {
                case "constantpixelsize":
                    return CanvasScaler.ScaleMode.ConstantPixelSize;
                case "scalewithscreensize":
                    return CanvasScaler.ScaleMode.ScaleWithScreenSize;
                case "constantphysicalsize":
                    return CanvasScaler.ScaleMode.ConstantPhysicalSize;
                default:
                    Debug.LogWarning($"[VibeUnity] Unknown scale mode '{scaleMode}', using ScaleWithScreenSize");
                    return CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }
        }
        
        /// <summary>
        /// Parses scrollbar visibility from string
        /// </summary>
        private static ScrollRect.ScrollbarVisibility ParseScrollbarVisibility(string visibility)
        {
            switch (visibility.ToLower())
            {
                case "permanent":
                    return ScrollRect.ScrollbarVisibility.Permanent;
                case "autohide":
                    return ScrollRect.ScrollbarVisibility.AutoHide;
                case "autohideandexpandviewport":
                    return ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                default:
                    return ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            }
        }
        
        /// <summary>
        /// Finds a suitable UI parent GameObject
        /// </summary>
        private static GameObject FindUIParent(string parentName)
        {
            if (!string.IsNullOrEmpty(parentName))
            {
                GameObject specificParent = VibeUnityGameObjects.FindInActiveScene(parentName);
                if (specificParent != null)
                {
                    return specificParent;
                }
            }
            
            // Auto-detect canvas
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (canvases.Length > 0)
            {
                return canvases[0].gameObject;
            }
            
            return null;
        }
        
        /// <summary>
        /// Sets up a RectTransform with the specified dimensions and anchor
        /// </summary>
        private static void SetupRectTransform(RectTransform rectTransform, float width, float height, string anchorPreset)
        {
            // Set anchor and pivot based on preset
            switch (anchorPreset.ToLower())
            {
                case "topleft":
                    rectTransform.anchorMin = new Vector2(0, 1);
                    rectTransform.anchorMax = new Vector2(0, 1);
                    rectTransform.pivot = new Vector2(0, 1);
                    break;
                case "topcenter":
                    rectTransform.anchorMin = new Vector2(0.5f, 1);
                    rectTransform.anchorMax = new Vector2(0.5f, 1);
                    rectTransform.pivot = new Vector2(0.5f, 1);
                    break;
                case "topright":
                    rectTransform.anchorMin = new Vector2(1, 1);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.pivot = new Vector2(1, 1);
                    break;
                case "middleleft":
                    rectTransform.anchorMin = new Vector2(0, 0.5f);
                    rectTransform.anchorMax = new Vector2(0, 0.5f);
                    rectTransform.pivot = new Vector2(0, 0.5f);
                    break;
                case "middlecenter":
                default:
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    break;
                case "middleright":
                    rectTransform.anchorMin = new Vector2(1, 0.5f);
                    rectTransform.anchorMax = new Vector2(1, 0.5f);
                    rectTransform.pivot = new Vector2(1, 0.5f);
                    break;
                case "bottomleft":
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(0, 0);
                    rectTransform.pivot = new Vector2(0, 0);
                    break;
                case "bottomcenter":
                    rectTransform.anchorMin = new Vector2(0.5f, 0);
                    rectTransform.anchorMax = new Vector2(0.5f, 0);
                    rectTransform.pivot = new Vector2(0.5f, 0);
                    break;
                case "bottomright":
                    rectTransform.anchorMin = new Vector2(1, 0);
                    rectTransform.anchorMax = new Vector2(1, 0);
                    rectTransform.pivot = new Vector2(1, 0);
                    break;
            }
            
            // Set size
            rectTransform.sizeDelta = new Vector2(width, height);
        }
        
        /// <summary>
        /// Creates text for a button
        /// </summary>
        private static void CreateButtonText(GameObject buttonParent, string textContent)
        {
            GameObject textGO = new GameObject("Text (TMP)");
            textGO.transform.SetParent(buttonParent.transform, false);
            
            TextMeshProUGUI textComponent = textGO.AddComponent<TextMeshProUGUI>();
            textComponent.text = textContent;
            textComponent.fontSize = 14;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.color = Color.black;
            
            // Setup RectTransform to fill button
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
        
        #endregion
    }
}