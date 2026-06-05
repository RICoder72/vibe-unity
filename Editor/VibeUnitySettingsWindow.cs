#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace VibeUnity.Editor
{
    /// <summary>
    /// Settings window for Vibe Unity package configuration
    /// </summary>
    public class VibeUnitySettingsWindow : EditorWindow
    {
        private VibeUnitySettingsData settings;
        private Vector2 scrollPosition;
        private bool showShortcutHelp = false;
        
        [MenuItem("Tools/Vibe Unity/Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<VibeUnitySettingsWindow>("Vibe Unity Settings");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }
        
        private void OnEnable()
        {
            settings = VibeUnitySettings.GetSettings();
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Vibe Unity Settings", EditorStyles.largeLabel);
            EditorGUILayout.Space();
            
            // Header info
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Version: {settings.version}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Settings file: {Path.GetFileName(VibeUnitySettings.GetSettingsPath())}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.Space();
            
            // General Settings
            GUILayout.Label("General", EditorStyles.boldLabel);
            settings.enableDebugLogs = EditorGUILayout.Toggle("Enable Debug Logs", settings.enableDebugLogs);
            settings.autoSaveSettings = EditorGUILayout.Toggle("Auto Save Settings", settings.autoSaveSettings);
            
            EditorGUILayout.Space();
            
            // Keyboard Shortcuts Section
            GUILayout.Label("Keyboard Shortcuts", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(showShortcutHelp ? "Hide Help" : "Show Help", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    showShortcutHelp = !showShortcutHelp;
                }
                GUILayout.FlexibleSpace();
            }
            
            if (showShortcutHelp)
            {
                EditorGUILayout.HelpBox(
                    "Shortcut Format Guide:\\n" +
                    "% = Ctrl (Windows/Linux) or Cmd (Mac)\\n" +
                    "# = Shift\\n" +
                    "& = Alt\\n\\n" +
                    "Examples:\\n" +
                    "%k = Ctrl+K\\n" +
                    "%#k = Ctrl+Shift+K\\n" +
                    "%&k = Ctrl+Alt+K\\n" +
                    "#&k = Shift+Alt+K\\n\\n" +
                    "Keys: a-z, 0-9, and some symbols",
                    MessageType.Info);
            }
            
            // Shortcut fields
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Compilation Shortcuts", EditorStyles.boldLabel);
                
                DrawShortcutField("Check Status", ref settings.shortcuts.checkStatus, "Display current compilation status");
                DrawShortcutField("Force Recompile", ref settings.shortcuts.forceRecompile, "Force Unity to recompile all scripts");
                DrawShortcutField("Clear Cache", ref settings.shortcuts.clearCache, "Clear compilation cache and messages");
            }
            
            EditorGUILayout.Space();
            
            // Current Shortcuts Display
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Current Shortcuts", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Check Status: {ShortcutSettings.GetShortcutDescription(settings.shortcuts.checkStatus)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Force Recompile: {ShortcutSettings.GetShortcutDescription(settings.shortcuts.forceRecompile)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Clear Cache: {ShortcutSettings.GetShortcutDescription(settings.shortcuts.clearCache)}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.Space();
            
            // Action Buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Settings"))
                {
                    if (VibeUnitySettings.SaveSettings(settings))
                        ShowNotification(new GUIContent("Settings saved!"));
                    else
                        EditorUtility.DisplayDialog("Vibe Unity",
                            "Failed to save settings. See the Console for details.", "OK");
                }
                
                if (GUILayout.Button("Reset to Defaults"))
                {
                    if (EditorUtility.DisplayDialog("Reset Settings", 
                        "Are you sure you want to reset all settings to defaults?", 
                        "Reset", "Cancel"))
                    {
                        VibeUnitySettings.ResetToDefaults();
                        settings = VibeUnitySettings.GetSettings();
                        ShowNotification(new GUIContent("Settings reset to defaults!"));
                    }
                }
                
                if (GUILayout.Button("Open Settings File"))
                {
                    EditorUtility.RevealInFinder(VibeUnitySettings.GetSettingsPath());
                }
            }
            
            EditorGUILayout.Space();
            
            // Information section
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Information", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("• Settings are stored in .vibe-unity/vibe-unity-settings.json", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("• Shortcuts require Unity Editor restart to take effect", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("• Use Tools > Vibe Unity > Compilation menus to access functions", EditorStyles.wordWrappedLabel);
                
                if (GUILayout.Button("Documentation", EditorStyles.linkLabel))
                {
                    Application.OpenURL("https://github.com/your-repo/vibe-unity#shortcuts");
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawShortcutField(string label, ref string shortcutValue, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.Width(120));
                
                string newValue = EditorGUILayout.TextField(shortcutValue);
                if (newValue != shortcutValue)
                {
                    shortcutValue = newValue;
                    if (settings.autoSaveSettings)
                    {
                        VibeUnitySettings.SaveSettings(settings);
                    }
                }
                
                EditorGUILayout.LabelField(ShortcutSettings.GetShortcutDescription(shortcutValue), EditorStyles.miniLabel, GUILayout.Width(100));
            }
        }
        
        private void OnLostFocus()
        {
            if (settings.autoSaveSettings)
            {
                VibeUnitySettings.SaveSettings(settings);
            }
        }
        
        private void OnDestroy()
        {
            if (settings.autoSaveSettings)
            {
                VibeUnitySettings.SaveSettings(settings);
            }
        }
    }
}
#endif