# Vibe Unity

A powerful Command Line Interface tool for Unity development workflow automation. Streamline your Unity development with CLI-based scene creation, canvas management, and project structure operations.

![Unity Version](https://img.shields.io/badge/Unity-2022.3+-blue.svg)
![License](https://img.shields.io/badge/License-MIT-green.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg)

## 🚀 Features

- **CLI-based Scene Creation**: Create Unity scenes using command line with various templates
- **Canvas Management**: Add and configure UI canvases with customizable parameters
- **Hierarchical UI Creation**: Create complex UI hierarchies with parent-child relationships
- **Scene Context Management**: Target specific scenes with `--scene` parameter
- **Process Management**: Check and kill Unity processes blocking CLI operations
- **WSL Integration**: Native Windows Subsystem for Linux support with bash scripts
- **Scene Template Support**: Works with Unity's built-in scene templates
- **Build Settings Integration**: Automatically add created scenes to build settings
- **Extensible Architecture**: Easy to extend with new CLI commands
- **Cross-Platform**: Works on Windows, macOS, and Linux
- **No External Dependencies**: Uses only Unity's native APIs
- **Detailed Error Reporting**: Machine-readable output with comprehensive diagnostics
- **Claude Code Integration**: Designed for AI-assisted Unity development workflows
- **Automatic File Watcher**: Execute commands even while Unity Editor is running

## 📦 Installation

### Method 1: Unity Package Manager (Recommended)

1. Open Unity Package Manager
2. Click the `+` button and select "Add package from git URL"
3. Enter: `https://github.com/RICoder72/vibe-unity.git`

### Method 2: Manual Installation

1. Download or clone this repository
2. Copy the contents to your Unity project's `Packages` folder
3. Unity will automatically import the package

### Method 3: Unity Package

1. Download the latest `.unitypackage` from [Releases](https://github.com/RICoder72/vibe-unity/releases)
2. Import into your Unity project via `Assets > Import Package > Custom Package`

## 🐧 WSL Setup (Windows Users)

After installing the Unity package, set up the CLI for WSL:

### One-Time Setup Per Project
```bash
# Navigate to your Unity project root
cd /mnt/c/path/to/your/unity-project

# Run the installation script (creates the Scripts directory if needed)
./Scripts/install-vibe-unity

# Restart your terminal or reload shell configuration
source ~/.bashrc
```

### Verify Installation
```bash
# Test the CLI is working
vibe-unity --version
vibe-unity --help

# Test Unity detection
vibe-unity list-types
```

**Requirements:**
- Unity Hub installed in standard Windows location
- WSL with bash or zsh shell
- Unity project with Vibe Unity package installed

## 🛠️ Quick Start

### Method 1: Command Line Interface (Primary - for Claude Code & WSL)

The package automatically sets up CLI scripts in your project root when imported:

```bash
# Create a scene and add a canvas (the main use case)
./vibe-unity create-scene MyScene Assets/Scenes --type DefaultGameObjects
./vibe-unity add-canvas MainCanvas --mode ScreenSpaceOverlay --width 1920 --height 1080

# List available scene types
./vibe-unity list-types

# Add UI elements
./vibe-unity add-panel MenuPanel --parent MainCanvas --width 300 --height 400
./vibe-unity add-button PlayButton --parent MenuPanel --text "Play Game"
./vibe-unity add-text HeaderText --parent MenuPanel --text "Welcome!" --size 24

# Show help
./vibe-unity --help
./vibe-unity help create-scene
```

**Primary use case:** Claude Code integration, WSL development, automation scripts, and CI/CD pipelines.

**Important:** Unity Editor must be closed before running CLI commands (Unity batch mode limitation).

### Method 2: C# API (For Unity Editor scripts)

```csharp
using VibeUnity.Editor;

// Create a new scene
CLI.CreateScene("MyScene", "Assets/Scenes", "DefaultGameObjects", true);

// Add a canvas to current scene
CLI.AddCanvas("MainCanvas", "ScreenSpaceOverlay", 1920, 1080, "ScaleWithScreenSize");

// List available scene types
CLI.ListSceneTypes();
```

**Best for:** Unity developers who want to use these tools in Editor scripts or custom Unity windows.

### Method 3: Unity Menu Integration

Access tools via Unity's menu system:
- **Tools > Vibe Unity > Debug Unity CLI** - Show current configuration
- **Tools > Vibe Unity > Test Unity CLI** - Test CLI functionality

### Method 4: Direct Unity Batch Mode (Advanced)

```bash
# Advanced usage - call Unity directly  
Unity -batchmode -quit -projectPath "path/to/project" -executeMethod VibeUnity.Editor.CLI.CreateSceneFromCommandLine MyScene Assets/Scenes DefaultGameObjects true
```

**Best for:** Advanced automation and build systems that need direct Unity control.

## 📚 Documentation

### Scene Creation

Create Unity scenes with various templates and configurations:

```csharp
// Basic scene creation
CLI.CreateScene("GameScene", "Assets/Scenes/Game");

// Scene with specific template
CLI.CreateScene("UIScene", "Assets/Scenes/UI", "2D");

// Scene added to build settings
CLI.CreateScene("MainMenu", "Assets/Scenes/Menus", "DefaultGameObjects", true);
```

#### Available Scene Types
- `Empty` - Completely empty scene
- `DefaultGameObjects` - Scene with Main Camera and Directional Light
- `2D` - 2D optimized scene setup
- `3D` - 3D optimized scene setup with skybox
- `URP` - Universal Render Pipeline optimized (if URP is installed)
- `HDRP` - High Definition Render Pipeline optimized (if HDRP is installed)

### Canvas Management

Add and configure UI canvases with various render modes:

```csharp
// Basic overlay canvas
CLI.AddCanvas("UICanvas", "ScreenSpaceOverlay");

// Canvas with custom resolution
CLI.AddCanvas("GameUI", "ScreenSpaceOverlay", 1920, 1080, "ScaleWithScreenSize");

// World space canvas
CLI.AddCanvas("WorldUI", "WorldSpace", 100, 100);
```

#### Canvas Parameters
- **Canvas Name**: Name for the canvas GameObject
- **Render Mode**: `ScreenSpaceOverlay`, `ScreenSpaceCamera`, `WorldSpace`
- **Reference Width/Height**: Resolution for scaling (default: 1920x1080)
- **Scale Mode**: `ConstantPixelSize`, `ScaleWithScreenSize`, `ConstantPhysicalSize`
- **Sorting Order**: Canvas sorting order (default: 0)
- **World Position**: Position for WorldSpace canvas (Vector3, default: Vector3.zero)

### CLI Commands

#### vibe-unity create-scene
```bash
vibe-unity create-scene <SCENE_NAME> <SCENE_PATH> [OPTIONS]
```

**Options:**
- `-t, --type <TYPE>` - Scene type (default: DefaultGameObjects)
- `-b, --build` - Add scene to build settings
- `-h, --help` - Show help for this command

**Examples:**
```bash
vibe-unity create-scene MyScene Assets/Scenes
vibe-unity create-scene GameScene Assets/Scenes/Game --type 3D --build
vibe-unity create-scene UIScene Assets/Scenes/UI -t 2D
```

#### vibe-unity add-canvas
```bash
vibe-unity add-canvas <CANVAS_NAME> [OPTIONS]
```

**Options:**
- `-m, --mode <MODE>` - Render mode (default: ScreenSpaceOverlay)
- `-w, --width <WIDTH>` - Reference width (default: 1920)
- `--height <HEIGHT>` - Reference height (default: 1080)
- `-s, --scale <SCALE>` - Scale mode (default: ScaleWithScreenSize)
- `-h, --help` - Show help for this command

**Examples:**
```bash
vibe-unity add-canvas MainCanvas
vibe-unity add-canvas UICanvas --mode ScreenSpaceOverlay --width 1920 --height 1080
vibe-unity add-canvas WorldCanvas -m WorldSpace
```

#### vibe-unity list-types
```bash
vibe-unity list-types
```

Lists all available scene types for your Unity installation.

#### vibe-unity help
```bash
vibe-unity --help                    # General help
vibe-unity help <COMMAND>            # Command-specific help
vibe-unity <COMMAND> --help          # Alternative command help
```

Shows detailed help information for commands.

## 🔧 Advanced Usage

### Batch Scene Creation

```csharp
string[] sceneNames = { "Level1", "Level2", "Level3", "MainMenu", "Settings" };
string basePath = "Assets/Scenes";

foreach (string sceneName in sceneNames)
{
    CLI.CreateScene(sceneName, basePath, "DefaultGameObjects", true);
}
```

### Automated UI Setup

```csharp
// Create UI scene
CLI.CreateScene("MainMenu", "Assets/Scenes/UI", "2D");

// Add main canvas
CLI.AddCanvas("MenuCanvas", "ScreenSpaceOverlay", 1920, 1080, "ScaleWithScreenSize");

// Add overlay canvas for popups
CLI.AddCanvas("PopupCanvas", "ScreenSpaceOverlay", 1920, 1080, "ScaleWithScreenSize");
```

### Integration with Build Tools

```bash
#!/bin/bash
# build-setup.sh - Automated project setup

echo "Setting up Unity project structure..."

# Create scene directories
vibe-unity create-scene MainMenu Assets/Scenes/Menus --type DefaultGameObjects --build
vibe-unity create-scene Level1 Assets/Scenes/Levels --type DefaultGameObjects --build
vibe-unity create-scene Settings Assets/Scenes/UI --type 2D --build

# Add UI canvases
vibe-unity add-canvas MainMenuCanvas --mode ScreenSpaceOverlay --width 1920 --height 1080
vibe-unity add-canvas SettingsCanvas --mode ScreenSpaceOverlay --width 1920 --height 1080

echo "Project setup complete!"
```

## 🎯 Unity Menu Integration

Access Vibe Unity features directly from Unity's menu:

- **Tools > Vibe Unity > Debug Unity CLI** - Show current configuration
- **Tools > Vibe Unity > Test Unity CLI** - Test CLI functionality

## 🔍 Troubleshooting

### HTTP Server Setup (WSL + Unity)

**The Vibe Unity package includes an HTTP server that allows WSL to communicate directly with Unity Editor while it's running:**

✅ **HTTP Server Features:**
- Runs on port 9876 by default
- Allows scene creation and canvas management while Unity is open
- No need to close Unity Editor for CLI commands
- Automatic startup when Unity Editor loads

**WSL-Windows Networking Setup:**
1. **Server Binding**: HTTP server binds to all interfaces (`http://*:9876/`)
2. **Windows Firewall**: Create firewall rule for port 9876
3. **Network Profile**: Ensure correct Windows network profile settings

**Quick Firewall Setup (Administrator PowerShell):**
```powershell
# Create firewall rule for Unity HTTP server
New-NetFirewallRule -DisplayName "Unity Vibe HTTP Server" -Direction Inbound -Protocol TCP -LocalPort 9876 -Action Allow -Profile Domain,Private,Public

# Alternative: Temporary disable for testing (remember to re-enable)
Set-NetFirewallProfile -Profile Public -Enabled False
# Test your connection...
Set-NetFirewallProfile -Profile Public -Enabled True
```

**Testing HTTP Server Connection:**
```bash
# From WSL - test basic connectivity
curl http://172.20.32.1:9876/

# Test scene creation via HTTP
curl -X POST http://172.20.32.1:9876/execute -H "Content-Type: application/json" -d '{"action":"create-scene","parameters":{"name":"TestScene","path":"Assets/Scenes"}}'
```

**From Windows PowerShell:**
```powershell
# Test server is responding
Invoke-WebRequest -Uri "http://localhost:9876/"

# Create scene via HTTP API
$headers = @{"Content-Type" = "application/json"}
$body = '{"action":"create-scene","parameters":{"name":"TestScene","path":"Assets/Scenes"}}'
Invoke-WebRequest -Uri "http://localhost:9876/execute" -Method POST -Headers $headers -Body $body
```

**Unity Menu Controls:**
- `Tools → Vibe Unity → HTTP Server Enabled` - Toggle server on/off
- `Tools → Vibe Unity → Configuration` - View server settings
- Server status shown in Unity Console on startup

**Common Firewall Issues:**

⚠️ **If firewall rules don't work as expected:**

Some Windows configurations (corporate networks, advanced security software) may block WSL→Windows connections even with proper firewall rules. Here are solutions:

1. **Temporary Testing** (Development environments):
   ```powershell
   # Disable Public firewall temporarily
   Set-NetFirewallProfile -Profile Public -Enabled False
   # Test your WSL connection...
   Set-NetFirewallProfile -Profile Public -Enabled True
   ```

2. **Create Development Toggle Script** (`toggle-firewall.bat`):
   ```batch
   @echo off
   if "%1"=="off" (
       echo Disabling Public Firewall for Unity development...
       powershell -Command "Set-NetFirewallProfile -Profile Public -Enabled False"
   ) else if "%1"=="on" (
       echo Re-enabling Public Firewall...
       powershell -Command "Set-NetFirewallProfile -Profile Public -Enabled True"
   ) else (
       echo Usage: toggle-firewall.bat [on^|off]
   )
   ```

3. **Check for Conflicting Rules:**
   ```powershell
   # List all Unity-related firewall rules
   Get-NetFirewallRule -DisplayName "*Unity*" | Select-Object DisplayName, Direction, Action, Enabled
   
   # Disable Unity blocking rules if they conflict
   Get-NetFirewallRule -DisplayName "*Unity*" -Action Block | Disable-NetFirewallRule
   ```

4. **Alternative Port Testing:**
   If port 9876 is blocked, modify the server to use a different port in `VibeUnityHttpServer.cs`.

### WSL (Windows Subsystem for Linux) Configuration

**Important for WSL Users:**
- **HTTP Server Method**: Preferred - allows Unity Editor to stay open
- **Batch Mode Method**: Fallback - requires Unity Editor to be closed
- Scripts automatically convert WSL paths (`/mnt/c/*`) to Windows paths (`C:/*`)
- Unity installation path is auto-detected from common locations
- All bash scripts are executable and ready to use

**Current Working CLI Commands:**
```bash
# Install CLI (run once per project)
./Scripts/install-vibe-unity

# Create a new scene (Unity Editor must be closed)
vibe-unity create-scene MyScene Assets/Scenes/Test --type DefaultGameObjects

# List available scene types  
vibe-unity list-types

# Show help documentation
vibe-unity --help
vibe-unity help create-scene

# Add canvas (requires Unity UI package - currently disabled)
vibe-unity add-canvas MainCanvas --mode ScreenSpaceOverlay --width 1920 --height 1080
```

**Expected Output:**
```bash
$ vibe-unity create-scene MyNewScene Assets/Scenes/Test --type 3D --build
Vibe Unity - Creating Scene
================================
Scene Name: MyNewScene
Scene Path: Assets/Scenes/Test  
Scene Type: 3D
Add to Build: true
Project Path: C:/repos/vibe-unity

✅ Scene created successfully: Assets/Scenes/Test/MyNewScene.unity

$ vibe-unity list-types
Vibe Unity - Available Scene Types
======================================
Project Path: C:/repos/vibe-unity

[UnityCLI] === Available Scene Types ===
[UnityCLI] Available scene types: Empty, DefaultGameObjects, 2D, 3D, URP
[UnityCLI] Scene Type Descriptions:
[UnityCLI]   Empty - Completely empty scene
[UnityCLI]   DefaultGameObjects - Scene with Main Camera and Directional Light
[UnityCLI]   2D - 2D optimized scene setup
[UnityCLI]   3D - 3D optimized scene setup with skybox
[UnityCLI]   URP - Universal Render Pipeline optimized scene
[UnityCLI] ===========================

✅ Scene types listed successfully
```

### Common Issues

**"Multiple Unity instances cannot open the same project"**
- **Solution**: Close Unity Editor completely before running CLI commands
- This is a Unity limitation with batch mode operations
- Scripts work perfectly once Unity Editor is closed

**"Unity Editor not found"**
- Ensure Unity is installed and accessible in PATH
- For WSL: Scripts auto-detect Unity from `/mnt/c/Program Files/Unity/Hub/Editor/*/Editor/Unity.exe`
- If detection fails, manually update `UNITY_PATH` in scripts

**"Couldn't set project path"** 
- This was fixed by implementing WSL path conversion in scripts
- Paths are automatically converted from `/mnt/c/*` to `C:/*` format
- No manual configuration needed

**"No active scene to add canvas to"**
- Open a scene in Unity Editor before adding canvas  
- Create a new scene first using `unity-create-scene`
- Canvas functionality requires Unity UI package (currently commented out)

**"Scene already exists"**
- Check if scene file already exists at the specified path
- Use a different scene name or path
- CLI will warn you with: `[UnityCLI] Scene already exists: path/to/scene.unity`

### Debug Information

Enable detailed logging by running:
```csharp
CLI.DebugUnityCLI(); // Shows configuration and available options
CLI.TestUnityCLI();  // Tests CLI functionality
```

## 🤖 Claude Code Integration

Vibe Unity is designed to work seamlessly with Claude Code and other AI coding assistants. The automatic file watcher system ensures commands work even when Unity Editor is running.

### Why Vibe Unity + Claude Code?

- **No Unity Restart Required**: Commands execute while Unity is running
- **Automatic Adaptation**: CLI detects Unity state and chooses the best execution method
- **AI-Friendly Output**: Clear, parseable responses perfect for AI interpretation
- **Complex UI Generation**: Build entire UI hierarchies with simple commands

### Claude Code Quick Start

```bash
# Works whether Unity is open or closed!
vibe-unity create-scene MenuScene Assets/Scenes --type 2D
vibe-unity add-canvas MainCanvas --mode ScreenSpaceOverlay
vibe-unity add-panel MenuPanel MainCanvas --width 600 --height 400
vibe-unity add-button PlayBtn MenuPanel --text "Play Game"
```

### How It Works with Unity Running

1. Claude Code executes a vibe-unity command
2. CLI detects Unity is running with the project open
3. Command is converted to JSON and placed in `.vibe-commands/`
4. Unity's file watcher immediately processes the command
5. Results are reported back to Claude Code

No manual intervention required!

## 📋 CLAUDE.md Template

**Copy this section into your project's `CLAUDE.md` file to help Claude Code understand how to use Vibe Unity:**

````markdown
# Unity Development with Vibe Unity

This Unity project includes the **Vibe Unity** package for AI-assisted Unity development.

## Available Unity Tools

### HTTP Server API (Preferred - Unity can stay open)

The Unity Editor runs an HTTP server on port 9876 that accepts JSON commands:

```bash
# Test server connectivity
curl http://172.20.32.1:9876/

# Create scene via HTTP API
curl -X POST http://172.20.32.1:9876/execute \
  -H "Content-Type: application/json" \
  -d '{"action":"create-scene","parameters":{"name":"TestScene","path":"Assets/Scenes"}}'

# Add canvas via HTTP API  
curl -X POST http://172.20.32.1:9876/execute \
  -H "Content-Type: application/json" \
  -d '{"action":"add-canvas","parameters":{"name":"MainCanvas"}}'
```

**Available HTTP Actions:**
- `create-scene` - Create Unity scenes with parameters: `name`, `path`, `type`, `addToBuild`
- `add-canvas` - Add UI canvas with parameters: `name`, `renderMode`, `referenceWidth`, `referenceHeight`

### CLI Commands (Fallback - Unity must be closed)

If HTTP server isn't available, use these batch mode commands:

```bash
# Scene creation
vibe-unity create-scene <SCENE_NAME> <SCENE_PATH> [--type TYPE] [--build]

# Canvas management  
vibe-unity add-canvas <CANVAS_NAME> [--mode MODE] [--width WIDTH] [--height HEIGHT]

# UI elements
vibe-unity add-panel <PANEL_NAME> [--parent PARENT] [--width WIDTH] [--height HEIGHT]
vibe-unity add-button <BUTTON_NAME> [--parent PARENT] [--text TEXT]
vibe-unity add-text <TEXT_NAME> [--parent PARENT] [--text CONTENT] [--size SIZE]

# Utilities
vibe-unity list-types     # Show available scene types
vibe-unity --help         # Show all commands
```

## Scene Types Available
- `Empty` - Completely empty scene
- `DefaultGameObjects` - Scene with Main Camera and Directional Light  
- `2D` - 2D optimized scene setup
- `3D` - 3D optimized scene setup with skybox
- `URP` - Universal Render Pipeline optimized (if URP installed)
- `HDRP` - High Definition Render Pipeline optimized (if HDRP installed)

## Canvas Render Modes
- `ScreenSpaceOverlay` - UI renders on top of everything
- `ScreenSpaceCamera` - UI renders with camera perspective
- `WorldSpace` - UI exists in 3D world space

## Usage Examples

```bash
# Create a complete UI setup
vibe-unity create-scene MainMenu Assets/Scenes/UI --type 2D --build
vibe-unity add-canvas MenuCanvas --mode ScreenSpaceOverlay --width 1920 --height 1080
vibe-unity add-panel MenuPanel --parent MenuCanvas --width 600 --height 400
vibe-unity add-button PlayButton --parent MenuPanel --text "Play Game"
vibe-unity add-button SettingsButton --parent MenuPanel --text "Settings"
vibe-unity add-text TitleText --parent MenuPanel --text "Game Title" --size 32

# Create game levels
vibe-unity create-scene Level1 Assets/Scenes/Levels --type 3D --build
vibe-unity create-scene Level2 Assets/Scenes/Levels --type 3D --build
```

## Important Notes

- **HTTP Method**: Preferred method, Unity Editor can stay open
- **CLI Method**: Requires Unity Editor to be closed first
- **WSL Users**: HTTP server handles WSL→Windows communication automatically
- **Firewall**: May need Windows Firewall rule for port 9876 (see Vibe Unity README)
- **Paths**: Use forward slashes in paths (e.g., `Assets/Scenes/UI`)
- **Output**: Commands provide detailed success/error feedback

## Troubleshooting

**HTTP Server Not Responding:**
1. Check Unity Editor console for server startup messages
2. Verify `Tools → Vibe Unity → HTTP Server Enabled` is checked
3. Test Windows firewall: `netstat -an | findstr :9876`
4. For WSL: May need to disable Windows Public firewall temporarily

**CLI Commands Failing:**
1. Ensure Unity Editor is completely closed
2. Verify project path is correct
3. Check Unity installation is accessible

This tool enables rapid Unity scene and UI creation through simple commands!
````

### Additional Resources

- [Claude Code Integration Guide](CLAUDE-CODE-GUIDE.md) - Detailed guide for AI-assisted development
- [Execution Experiments](EXECUTION-EXPERIMENTS.md) - Technical details on how we solved Unity's single-instance limitation

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

### Development Setup

1. Clone the repository
2. Open in Unity 2022.3 or later
3. Make your changes
4. Test with the included test scenes
5. Submit a pull request

### Adding New Commands

1. Add method to `VibeUnity.Editor.CLI` class
2. Create corresponding bash script in `Scripts/` directory
3. Update documentation
4. Add tests

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Unity Technologies for the excellent Unity Editor APIs
- The Unity community for inspiration and feedback
- Contributors who help improve this tool

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/RICoder72/vibe-unity/issues)
- **Discussions**: [GitHub Discussions](https://github.com/RICoder72/vibe-unity/discussions)
- **Email**: contact@vibe-unity.com

---

**Made with ❤️ by the Vibe Unity Team**