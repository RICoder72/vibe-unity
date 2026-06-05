# Vibe Unity — Code Review & Feature Roadmap

**Date:** 2026-06-04
**Reviewed commit:** `32c3bd1` (origin/master, includes SkyGalerio PRs #4/#5)
**Scope:** All 17 `Editor/*.cs` files (~8,500 LOC) + shell layer (`Scripts/claude-compile-check.sh`, `Runtime/vibe-unity`, `Runtime/install-vibe-unity`, ~2,000 LOC)
**Status:** Review only — no code changed.

> ⚠️ **Note for BiteClub:** the copy installed in `bite-club` is pinned to `e6c8395` (v2.0.0), which is *behind* this review's HEAD. The CLI entry-point fixes (PR #5) and README URL fixes (PR #4) are not yet in the consumed package.

---

## 1. Executive Summary

Vibe Unity works, but it is built on a set of fragile mechanisms that **fail silently and report success anyway**. The single most important architectural problem cuts across the entire codebase:

> **The package communicates almost entirely through the Unity Editor console (`Debug.Log`), which the external agent (claude-code) cannot read. Every "insufficient feedback" finding traces back to this.** The agent only sees files on disk and shell exit codes, yet most outcomes — success, partial failure, dropped data, swallowed exceptions — are written only to the console.

Layered on top of that are three recurring structural problems:

1. **"Optimistic success" everywhere.** Async command-drop paths print "✅ created successfully" before Unity has done (or rejected) anything. The compile checker returns **exit 0 / SUCCESS when it observed *nothing at all*.** Setup reports success when it copied zero files. Add-component reports success when every parameter failed to apply.
2. **Good code orphaned, lossy code live.** A rich `SceneState` export/import pipeline (components, settings, UI info, coverage analysis) exists but is **never called**; the live path is a hand-rolled, lossy JSON builder that drops every non-script component and produces a schema the importer can't even read back.
3. **Hand-rolled where Unity/.NET already provide an API.** Window-focus to trigger compilation, regex/string JSON, `new GameObject` instead of `ObjectFactory`, manual UI construction instead of `DefaultControls`, machine-global `EditorPrefs` for project state, reflection instead of `SerializedObject`/`TypeCache`.

You specifically asked about "we used a HWIN where an API would do" — see **§2.1 (compile trigger)**, which is exactly that pattern and is the highest-value fix in the codebase.

---

## 2. Top Findings (by priority)

### P0 — Silent false success (the agent is actively misled)

#### 2.1 Compilation is triggered by stealing window focus, not an API  — `claude-compile-check.sh:78-111, 398-409`
Compilation is triggered by `AppActivate`-ing the Unity window (relying on Unity's "recompile on focus" preference) and then re-focusing "WindowsTerminal". This is the "HWIN instead of API" case. It breaks when:
- Unity is minimized, on another desktop, or over RDP;
- auto-refresh is disabled;
- the terminal isn't WindowsTerminal/cmd (VS Code terminal, ConEmu, Alacritty…);
- `Microsoft.VisualBasic.Interaction::AppActivate` simply no-ops (it frequently does).

**Fix:** Trigger deterministically. The package already has a file-watcher and a status JSON. Add an editor-side `recompile`/`refresh` command file (same mechanism as scene creation, via `CompilationPipeline.RequestScriptCompilation`) and wait on the status file. Removes the focus hack entirely.

#### 2.2 Compile check returns SUCCESS when it verified nothing  — `claude-compile-check.sh:379-387`
On timeout / "no compilation logs found", the script does `output_result "SUCCESS" … "assuming all scripts are up to date"; return 0`. The whole point of the tool is to *confirm* compilation; the failure-to-observe case is reported as a clean pass. Combined with 2.1, a silently-failed trigger becomes a green light.
**Fix:** Distinguish "verified clean" from "could not determine." Return exit 2 / `STATUS: UNKNOWN` when no compilation evidence was observed — never SUCCESS.

#### 2.3 Async command-drop prints success before Unity acts  — `Runtime/vibe-unity:268-280, 593-604`; `VibeUnitySystem.cs:133-148`
`cmd_create_scene` writes a JSON command file and immediately prints "✅ Scene created successfully", though nothing has happened yet and the result is never read back. On the editor side, `ProcessBatchFileWithLogging` returns a `bool success` that is **captured and never used** — the file is moved to `processed/` whether it succeeded or failed. There is no `failed/` directory and no per-command result file.
**Fix:** After dropping a command file, poll for a per-request result artifact (bounded timeout) and report the real outcome. On the editor side, branch on `success`, write `<requestId>.result.json`, and route failures to a `failed/` folder.

#### 2.4 Setup always reports success & permanently marks complete  — `VibeUnitySetup.cs:25-37, 39-98, 160`
`RunSetup` sets `SETUP_COMPLETE_KEY = true` and shows "installed!" regardless of whether the package path resolved, source files existed, or copying threw (downgraded to a warning). Given 2.7 (wrong package id), this likely copies nothing while telling the user it worked.
**Fix:** Track per-file copy results; mark complete only when the essential script copied; report what was/wasn't installed.

#### 2.5 add-component reports success when parameters silently fail  — `VibeUnityJSONProcessor.cs:682-692`; `VibeUnityGameObjects.cs:341-345`
`SetComponentParameters(...)` returns a `bool` that is discarded; the command logs "✅ added successfully" and returns true even if every parameter failed. Underneath, `ConvertParameterValue` has a bare `catch { return default }`, so `"abc"` for a float silently writes `0` and is logged as "Set X = abc".
**Fix:** Propagate the return; log the failing param name/value/type; fail or warn the command.

### P1 — Silent data loss & broken round-trips

#### 2.6 Scene export & import use incompatible schemas; export drops all non-script components  — `VibeUnitySceneExporter.cs:109-211`; `VibeUnitySceneImporter.cs:44`
The **live** exporter (`ExportSceneToJsonManually`) hand-builds JSON containing only script *type-name strings* + `position/rotation/scale` arrays. The importer deserializes the rich `SceneState` schema (`transform.localPosition`, `components[].typeName`, `hierarchyPath`, …). `JsonUtility` silently ignores the mismatched fields, so **importing a file the exporter just wrote yields empty GameObjects and still reports a high success rate.** Separately, the live export records *only* `MonoBehaviour`s — Camera, Light, MeshRenderer, Collider, Rigidbody, Canvas, Image, AudioSource are all dropped with no record. An agent reading the export to "understand" a scene is misled.
**Fix:** Delete the manual JSON builder. Populate the existing `SceneState` model and serialize with `EditorJsonUtility.ToJson`. This fixes escaping, the schema mismatch, *and* brings the already-written coverage/settings/UI pipeline online in one move. Add a schema `version` and fail loudly when parse yields empty.

#### 2.7 Wrong/duplicated package id breaks install & version detection  — `VibeUnitySetup.cs:140-142` vs `VibeUnityDocumentationUpdater.cs:125`
One file looks for `com.vibe.unity`, the other for `com.ricoder.vibe-unity` (the real id per `package.json`). At most one is right, so script copying or version detection is broken, and the `@1.0.0` PackageCache path is version-pinned.
**Fix:** Use `UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VibeUnitySetup).Assembly)` for both path and `.version`. Removes all hardcoded ids and the package.json regex parse.

#### 2.8 Documentation updater can silently truncate user content  — `VibeUnityDocumentationUpdater.cs:286-289`
If the start marker exists but the end marker is missing (truncated/edited CLAUDE.md), the code does `Substring(0, sectionStart) + newSection`, **deleting everything after the start marker** with no backup or warning.
**Fix:** If the end marker is missing, warn and refuse (or append fresh); write a `.bak` before rewriting.

#### 2.9 Two compilation trackers race on the same file  — `VibeUnitySystem.cs:415-582` vs `VibeUnityCompilationController.cs:85-251`
`VibeUnitySystem` polls `EditorApplication.isCompiling` every frame and writes `compilation.json`; `VibeUnityCompilationController` uses the correct event-driven `CompilationPipeline.compilationStarted/Finished` and *also* writes `compilation.json` — with a different schema. They race.
**Fix:** Delete the polling tracker; keep the event-driven one as the single status writer.

#### 2.10 Settings save failure reported as success in the window  — `VibeUnitySettings.cs:43-60`; `VibeUnitySettingsWindow.cs:108-111, 171-185`
`SaveSettings` swallows write failures (only `Debug.LogError`); the "Save Settings" button unconditionally shows "Settings saved!". A read-only `.vibe-unity` dir => user believes settings persisted.
**Fix:** Return `bool` from `SaveSettings`; show success only on true, error otherwise.

#### 2.11 Project-global EditorPrefs for project-scoped state  — `VibeUnitySetup.cs:14`; `VibeUnityDocumentationUpdater.cs:23-42`
`SETUP_COMPLETE_KEY = "VibeUnity_SetupComplete"` is machine-global and unqualified, so the **first** project to run setup on a machine marks setup "complete" for every other project — new projects silently skip script copying.
**Fix:** Key by project path/hash (a `GetProjectHash()` already exists) or store in the project-local settings JSON.

### P2 — Fragility where a clean API exists

| # | Finding | Location | Fix |
|---|---------|----------|-----|
| 2.12 | JSON parsed/emitted via regex + string interpolation; corrupts on `"` `\` newlines (and compiler errors contain all three) | `VibeUnityHttpServer.cs:133-176`; `VibeUnitySystem.cs:484-542`; `Runtime/vibe-unity:54-141` | Use `JsonUtility`/Newtonsoft (ships as `com.unity.nuget.newtonsoft-json`); JSON-escape shell heredocs or use `jq` |
| 2.13 | `JsonUtility` can't represent the documented schema (no root arrays, no presence detection, unknown fields dropped) — the giant flat `BatchCommand` union is a workaround | `VibeUnityJSONProcessor.cs:45, 829-898` | Move command parsing to Newtonsoft `JObject`; per-verb typed payloads; validate keys |
| 2.14 | No JSON schema validation — misspelled fields (`colour`, `widthPx`) silently dropped, defaults used | `VibeUnityJSONProcessor.cs:45` + executors | Validate keys per `action`; warn on unrecognized keys |
| 2.15 | `new GameObject` / `CreatePrimitive` bypass `ObjectFactory` (no Undo, no auto-dirty, manual scene registration) | `VibeUnityGameObjects.cs:362`; `VibeUnityUI.cs`; `VibeUnityPrimitives.cs` | Use `ObjectFactory.CreateGameObject/CreatePrimitive/AddComponent` + `Undo.RegisterCreatedObjectUndo` |
| 2.16 | UI hand-built; Button has no sprite/transition, ScrollView creates **no scrollbars** but sets visibility on null refs | `VibeUnityUI.cs:213-433` | Use `UnityEngine.UI.DefaultControls.Create*` (+ TMP variants) |
| 2.17 | Reflection (`GetFields`/`GetValue` + string round-trip) for component props; loses precision, misses `[SerializeField] private`, breaks on comma-decimal locales | `VibeUnitySceneExporter.cs:757-801`; `VibeUnitySceneImporter.cs:412-511` | Use `SerializedObject`/`SerializedProperty`; `CultureInfo.InvariantCulture`; store floats as JSON numbers |
| 2.18 | Component type lookup scans every assembly + namespace guessing | `VibeUnityGameObjects.cs:181-209` | `UnityEditor.TypeCache.GetTypesDerivedFrom<Component>()` |
| 2.19 | Positional CLI arg parsing + unguarded `int/float/bool.Parse` (throws out of reflection-invoked entry point, bypassing the `Exit(1)` error path) | `VibeUnityCLI.cs:445-681` | Named-flag dictionary + `TryParse`; or deprecate per-verb CLI in favor of JSON batch |
| 2.20 | `projectHash = Math.Abs(path.GetHashCode())` — not stable across runs; throws on `int.MinValue` | `VibeUnityCompilationController.cs:67` | Stable hash (MD5/SHA over UTF-8 bytes) |
| 2.21 | Hardcoded user log path `…/Users/matth/…` + `/mnt/c` (wrong on git-bash `/c/…` and every other user); `stat -c%s` is GNU-only | `claude-compile-check.sh:16, 249` | Derive via `$LOCALAPPDATA`/`wslpath`/`cygpath`; `wc -c < file` for size |
| 2.22 | WSL→Windows path conversion only rewrites `/mnt/c` (breaks `/mnt/d`, UNC, git-bash) | `Runtime/vibe-unity:30-36` | `wslpath -w` / `cygpath -w` with fallback |
| 2.23 | Render-pipeline detected by `GetType().Name.Contains("Universal")`, duplicated 3× | `VibeUnityCLI.cs:157-163`; `VibeUnityScenes.cs:106-113, 313-320` | One helper; `GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset` |
| 2.24 | `chmod` started fire-and-forget, exit code never checked (×2); also gated on `Platform == Unix`, so never runs under Windows-Unity-in-WSL | `VibeUnitySetup.cs:63-66`; `VibeUnitySystem.cs:746-758` | Check `ExitCode`; reconsider platform gate |
| 2.25 | Broad `*Unity*` process kill also kills Unity Hub / ShaderCompiler / Licensing | `Runtime/vibe-unity:1205-1209` | Scope to exact `Unity` + known children; exclude Hub |

### P3 — Other notable correctness issues
- **Empty/blanket catches** that return null/skip with no log: `VibeUnitySystem.cs:366-369`, `VibeUnitySceneImporter.cs:169-258, 507-510`, `VibeUnityGameObjects.cs:341-345`.
- **Ignored return values:** `FileStream.Read` byte count (`VibeUnitySystem.cs:511`), `SaveScene`/`NewScene`/`OpenScene` bools (`VibeUnitySceneImporter.cs:65-150`).
- **Divide-by-zero / NaN** in coverage & success-rate reports when component/object counts are zero: `VibeUnitySceneExporter.cs:1123-1125`, `VibeUnitySceneImporter.cs:143-153`.
- **`success = !result.StartsWith("Error:")`** — real failures ("Failed to create scene", "Unknown command") don't start with "Error:" and report `success:true`: `VibeUnityHttpServer.cs:157`.
- **Exit code 3 overloaded** — documented "script error" but also returned to mean "retry needed": `claude-compile-check.sh:48, 358, 382`.
- **Literal `\n` in error output** — accumulated with `\n` then `echo`'d without `-e`, so multi-error blocks collapse onto one line: `claude-compile-check.sh:175-201, 60-66`.
- **Emoji as status markers** (`✅❌⚠️`) throughout logs the agent greps — contradicts CLAUDE.md's own "no graphic Unicode" rule and is encoding-fragile; the documented tokens ("STATUS: SUCCESS") aren't consistently emitted.
- **`install-vibe-unity` self-test** runs `vibe-unity --version` (a no-op echo, always exit 0) and prints "working correctly" even when Unity is absent: `install-vibe-unity:63-72`.

---

## 3. Cross-cutting recommendation: a real result protocol

Almost every P0/P1 feedback gap closes with **one** change: for every consumed command, write a machine-readable result the agent can poll, keyed by a `requestId` (the field already exists on `CompileCommand`).

```
.vibe-unity/results/<requestId>.json
{
  "requestId": "…",
  "action": "create-scene",
  "status": "SUCCESS | PARTIAL | FAILURE",
  "message": "…",
  "errors":   [ { "command": 3, "message": "…" } ],
  "warnings": [ … ],
  "produced": [ "Assets/Scenes/Foo.unity" ],
  "durationMs": 1234,
  "schemaVersion": 1
}
```

Pair it with a `--json` mode on `claude-compile-check.sh` emitting the already-parsed `{status, errorCount, warnings, errors:[{file,line,col,code,message}]}` instead of scraped text. This removes the `\n`/`|`/emoji parsing hazards and the optimistic-success paths in one stroke.

---

## 4. Feature Roadmap — capturing what Unity's MCP exposes

Unity's official MCP (the toolset we saw wired into claude-code) is the natural feature target. Below is the **full capability surface**, mapped to vibe-unity's current state, so we can pick what to build. Legend: ✅ have · 🟡 partial/orphaned · ❌ missing.

### 4.1 Editor lifecycle & state — `Unity_ManageEditor`
Play / Pause / Stop, GetState, GetProjectRoot, GetWindows, GetActiveTool, **GetSelection**, GetPrefabStage, Add/Remove/Get **Tags** & **Layers**.
- vibe-unity: ❌ none. No play-mode control (`OnPlayModeChanged` is an empty stub, `VibeUnityHttpServer.cs:97-100`), no selection, no tag/layer management.
- **Build:** `enter-playmode`/`exit-playmode` (`EditorApplication.isPlaying`), report `playModeStateChanged`; `get-selection`/`set-selection`; `add-tag`/`add-layer`. Play-mode control directly serves "play 5 minutes to see if you can beat it" validation.

### 4.2 Console logs — `Unity_GetConsoleLogs` / `Unity_ReadConsole`  ← biggest gap
Read runtime errors/warnings/`Debug.Log` with severity + stack traces.
- vibe-unity: ❌ none. No `Application.logMessageReceivedThreaded` hook anywhere. The agent cannot see *any* runtime output.
- **Build:** subscribe to `Application.logMessageReceivedThreaded`, buffer to a rolling JSON file with `{severity, message, stackTrace, time}`, expose a `get-logs` reader. **This is Unity MCP's signature feature and the highest-value single addition.**

### 4.3 GameObject & component management — `Unity_ManageGameObject`
Create / find / modify / delete GameObjects; add/remove components; **set serialized properties**; reparent; query hierarchy.
- vibe-unity: 🟡 create/add only (Canvas, Panel, Button, Text, ScrollView, primitives, `add-component`). No delete/rename/reparent/move/set-active, no remove-component, no read-back. (The building blocks — `FindInActiveScene`, `GetPath`, `ListAvailable` — exist but aren't exposed as commands.)
- **Build:** mutation verbs (`delete`, `rename`, `reparent`, `set-transform`, `set-active`, `remove-component`, generic `set-property` via `SerializedProperty`); **query verbs** (`get-hierarchy`, `find-object`, `get-component`, `exists`) returning structured JSON so the agent can verify its own work.

### 4.4 Scene management — `Unity_ManageScene`
- vibe-unity: 🟡 create/open/save + lossy export/import (see 2.6). `2D/3D/URP/HDRP` scene types are advertised but silently fall back to Empty/DefaultGameObjects.
- **Build:** round-trip-safe export via `SceneState`; multi-scene/additive support; honor (or stop advertising) the scene-type templates via the `SceneTemplate` API.

### 4.5 Assets — `Unity_ManageAsset`, `Unity_ImportExternalModel`, `Unity_AssetGeneration_*`
Asset CRUD, external model import, AI asset generation + model listing.
- vibe-unity: ❌ none. No prefab instantiation, no material/sprite/texture assignment, no asset creation.
- **Build (high value for scene authoring):** `instantiate-prefab` (`PrefabUtility.InstantiatePrefab` + `AssetDatabase.LoadAssetAtPath`), `assign-material`/`assign-sprite` by asset path, `create-prefab`. Also: capture prefab *connections* on export (currently flattened — 2.6 sibling issue).

### 4.6 Scripts — `Unity_CreateScript` / `ManageScript` / `ApplyTextEdits` / `ValidateScript` / `DeleteScript`
- vibe-unity: ❌ none (claude-code edits scripts directly on disk, so lower priority — but `ValidateScript`-style pre-compile validation could be useful).

### 4.7 Menu items & commands — `Unity_ManageMenuItem` / `Unity_RunCommand`
- vibe-unity: ❌ none. `EditorApplication.ExecuteMenuItem("…")` would expose every menu command (build, lighting bake, etc.) for almost no code.
- **Build:** `execute-menu-item` verb.

### 4.8 Testing & build — (Unity MCP runs via menu/commands; we'd back it directly)
- vibe-unity: ❌ none.
- **Build (maps directly to CLAUDE.md's definition of done):**
  - `run-tests` via `UnityEditor.TestTools.TestRunner.Api.TestRunnerApi` (EditMode/PlayMode), results to JSON — serves "does it pass / is it done."
  - `build` via `BuildPipeline.BuildPlayer` for Android/iOS — the literal "done = published on Play Store / App Store" goal.

### 4.9 Visual capture — `Unity_Camera_Capture` / `SceneView_Capture2DScene` / `CaptureMultiAngleSceneView`
Screenshot game camera & scene view (multi-angle).
- vibe-unity: ❌ none.
- **Build:** `capture-game-view` / `capture-scene-view` via `ScriptableRenderContext`/`Camera.Render` to a PNG the agent can read. Lets the agent *see* a level it built — very high value for "is this level fun/correct."

### 4.10 Profiler — `Unity_Profiler_*` (large suite)
GC allocations (frame/range/overall), time samples (self/top/bottom-up), frame analysis, related-sample trees.
- vibe-unity: ❌ none.
- **Build (v2):** `ProfilerRecorder` API to capture frame timing/GC and write summaries — relevant for mobile perf, but lower priority than logs/tests/capture.

### 4.11 Project introspection — `GetProjectData` / `Grep` / `FindInFile` / `GetSha` / `ListResources` / `ReadResource` / `PackageManager_*`
- vibe-unity: 🟡 has its own project/hierarchy exporters (RICoder tools) and package-version reading (currently broken — 2.7). Grep/find are better served by claude-code's native file tools.

### 4.12 Suggested build order
1. **Console log capture** (4.2) + **result protocol** (§3) — unblocks the agent's blindness.
2. **Query/inspect verbs** (4.3) — lets the agent verify its own work.
3. **Capture game/scene view** (4.9) — lets the agent *see* results.
4. **run-tests + execute-menu-item** (4.7/4.8) — automated validation.
5. **GameObject mutation verbs + prefab/material assignment** (4.3/4.5) — real authoring.
6. **Play-mode control** (4.1), **build automation** (4.8), **profiler** (4.10) — v2.

---

## 5. Appendix — file-by-file finding index

- **VibeUnitySystem.cs** — 2.3, 2.9; empty catch (366), ignored `Read` (511), unchecked chmod (746).
- **VibeUnityHttpServer.cs** — entire server disabled (27-95); regex JSON + string responses (133-176); prefix-sniffed success (157); busy-wait `Thread.Sleep` (152-174).
- **VibeUnityCompilationController.cs** — 2.9; `GetHashCode` project id (67); `UpdateShortcuts` no-op logs success (416-423); `clear-cache` overstates (329-331).
- **VibeUnityCLI.cs** — 2.19; render-pipeline string match (157-163); dead `logCapture` overload (699-702); scene-type list disagreements (141-166 vs 385-419).
- **VibeUnityJSONProcessor.cs** — 2.5, 2.13, 2.14; abort-on-first-failure, no result manifest (80-98); empty-vs-absent field conflation (296+); emoji status markers.
- **VibeUnitySceneExporter.cs** — 2.6; manual JSON builder + weak escaper (109-279); reflection props (757-801); orphaned rich pipeline (284, 553, 1026-1192).
- **VibeUnitySceneImporter.cs** — 2.6; wrong-schema deserialize (44); ignored save/new/open bools (65-150); blanket settings catch (169-258); NaN success rate (143-153).
- **VibeUnitySceneState.cs** — schema is the *correct* target model; fields (`worldCamera`, tag/layer/static/sibling) never populated by live export.
- **VibeUnityScenes.cs** — advertised 2D/URP/HDRP types are no-ops (302-325); render-pipeline string match (106-113, 313-320).
- **VibeUnityGameObjects.cs** — 2.5, 2.15, 2.17, 2.18; silent `ConvertParameterValue` (341-345); missing-param warning lost when `logCapture` null (259); no dirty/save on `CreateEmptyGameObject`/`AddComponent`.
- **VibeUnityPrimitives.cs** — 2.15; no material/layer/tag/static; color handling inconsistent between the two entry points.
- **VibeUnityUI.cs** — 2.16; ScrollView scrollbars never created (377-378); only 5 element types; no layout groups; 9-anchor only (no stretch).
- **VibeUnityMenu.cs** — global EditorPrefs flags (13-23); CLI features advertised that are disabled (486); touch-and-report-initiated (349-378).
- **VibeUnitySettings.cs** — 2.10; corrupt-file not healed on load (65-96).
- **VibeUnitySettingsWindow.cs** — 2.10; shortcuts UI implies live rebind but `[MenuItem]` is compile-time (87-164).
- **VibeUnitySetup.cs** — 2.4, 2.7, 2.11, 2.24; `.gitignore` substring check (114); stale welcome dialog referencing disabled CLI.
- **VibeUnityDocumentationUpdater.cs** — 2.7, 2.8, 2.11; regex package.json parse (130); null version swallowed (121-138).
- **claude-compile-check.sh** — 2.1, 2.2, 2.21; exit-3 overload; literal `\n`; no `set -euo pipefail`; retry off-by-one (355-415); `current_size` scope (348); stale-lock not detected (130-154).
- **Runtime/vibe-unity** — 2.3, 2.12, 2.22, 2.25; `$?` after command-substitution (169-174); errors to stdout not stderr; unused `execute_via_http`.
- **Runtime/install-vibe-unity** — no-op self-test reports success (63-72); naive Unity glob via `command -v` (38-39).

---

## 6. Test Rig Review — `D:\repos\vibe-unity-testrig`

**Reviewed commit:** `43caccc` (origin/main). A real URP Unity project that consumes `com.ricoder.vibe-unity` via the same git URL and exercises it through `run-package-tests.sh` plus helper scripts.

### 6.1 Positive
- **The historical "0 errors even when errors exist" bug is fixed.** `VibeUnityCompilationController.cs:125-196` now counts real `CompilerMessage[]` via `CompilationPipeline.assemblyCompilationFinished` with an `EditorUtility.scriptCompilationFailed` fallback. The old broken reflection approach (documented in `SESSION-NOTES.md`) is gone.
- `run-package-tests.sh` has a genuine pass/fail counter and a meaningful exit code on TEST 1 and TEST 4.

### 6.2 The harness can't reliably fail — `run-package-tests.sh`
- **TEST 3 (scene creation) can never fail the suite.** When Unity isn't running / log missing / status ambiguous, it prints a yellow warning and does **not** increment `TESTS_FAILED` (`run-package-tests.sh:135-149`); the scene-file check likewise only warns. TEST 3 is informational only.
- That leaves **TEST 1 (valid compile) and TEST 4 (cleanup)** as the only hard assertions. **TEST 2 (error script must fail)** is real but hostage to the focus-hack trigger (§2.1): if the trigger silently no-ops, the package's SUCCESS-on-no-evidence path (§2.2) lets a genuine compile error pass undetected.
- Net: the suite can print **"✓ All tests passed! Package is ready for release"** having effectively verified one happy-path compile. False confidence for a release gate.
- **Fix:** make TEST 3 a hard assertion (fail, not warn, when Unity is down or the scene file is absent), or explicitly gate the suite on "requires live Unity."

### 6.3 Script drift & dead code
- **Five compile-check scripts** coexist: `claude-compile-check-simple.sh` (62), `-v2.sh` (339), `-v3.sh` (207), `-v4.sh` (200), and `claude-compile-check.sh` (472). The "current" 472-line copy **diverges from the package's canonical 425-line `Scripts/claude-compile-check.sh`** — a fix in the package won't reach the testrig.
  - **Fix:** delete the four variants; have the testrig invoke the package's script (symlink / copy-at-install) rather than forking it.
- **`test-workflow.sh` is dead.** It targets `/mnt/c/repos/vibe-unity-testrig/.vibe-commands/test-scene-creation.json`, but the repo is at `D:\repos` (not `/mnt/c/repos`), the live dir is `.vibe-unity/commands` (not `.vibe-commands`), and `.vibe-commands/` does not exist. It also re-implements the window-focus hack and the hardcoded `Users/matth` log path. It cannot run.
  - **Fix:** remove it, or rewrite against the real `.vibe-unity/commands` layout + the deterministic trigger from §2.1.

### 6.4 Repo hygiene — committed runtime artifacts + misdirected `.gitignore`
- **Generated/ephemeral state is tracked in git:** `.vibe-unity/compile-commands/compile-request-75AFA5E1-…json`, `.vibe-unity/compile-status/compile-status-error-test-…json`, `compilation/current-status.json`, `compilation/last-errors.json`, `status/compilation.json`, and **`compilation/project-hash.txt`**. The committed project hash is especially harmful given `SESSION-NOTES.md` documents cross-machine hash mismatches (`BBECC349` vs `5E88DFAB`).
- **`.gitignore` excludes `.vibe-commands/`** — a directory that doesn't exist — **but not** the `.vibe-unity/` runtime subdirs that actually hold generated state. The ignore rules predate the current directory layout (same drift as the scripts).
  - **Fix:** `git rm --cached` the runtime artifacts; ignore `.vibe-unity/{compilation,compile-commands,compile-status,status}/` and `project-hash.txt`; keep `vibe-unity-settings.json` if intended as config.
- **`SESSION-NOTES.md`** is stale (dated 2025-08-08, describes since-resolved debugging). Mark as historical to avoid misleading future readers.

### 6.5 Testrig fix priority
1. **6.2** — TEST 3 (and the TEST 2 trigger dependency) so the suite can actually gate a release.
2. **6.3** — collapse the five scripts to one sourced from the package; delete the dead workflow script.
3. **6.4** — untrack runtime artifacts and fix `.gitignore`.
