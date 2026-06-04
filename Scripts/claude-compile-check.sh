#!/bin/bash

# claude-compile-check.sh - Unity compilation validator for claude-code integration
#
# Purpose: Validates Unity C# script compilation after claude-code makes changes
# Returns structured output with error/warning details and precise file locations
#
# Exit Codes:
#   0 = Success (no compilation errors)
#   1 = Compilation errors found
#   2 = Indeterminate: Unity not reachable, log not found, or no compilation
#       evidence observed within the timeout. NOT treated as success.
#   3 = Script execution error (bad usage)
#
# Usage: ./claude-compile-check.sh [--include-warnings] [--log-path PATH]
#
# Environment:
#   VIBE_UNITY_LOG  Override path to Unity's Editor.log (highest precedence).

set -uo pipefail

INCLUDE_WARNINGS=false
SCRIPT_VERSION="2.1.0"
PROJECT_NAME=""
RETRY_COUNT=0
MAX_RETRIES=2
LOG_PATH_OVERRIDE=""
UNITY_LOG_PATH=""

# Internal sentinel returned by monitor_compilation to request a retry.
# Kept distinct from the documented exit codes (0/1/2/3) so a genuine script
# error can never be mistaken for "retry needed".
readonly RETRY_SENTINEL=10

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --include-warnings)
            INCLUDE_WARNINGS=true
            shift
            ;;
        --log-path)
            LOG_PATH_OVERRIDE="${2:-}"
            if [[ -z "$LOG_PATH_OVERRIDE" ]]; then
                echo "ERROR: --log-path requires a value" >&2
                exit 3
            fi
            shift 2
            ;;
        --help|-h)
            echo "Usage: $0 [--include-warnings] [--log-path PATH]"
            echo "Validates Unity compilation for claude-code integration"
            echo ""
            echo "Options:"
            echo "  --include-warnings  Include warning details in output"
            echo "  --log-path PATH     Path to Unity Editor.log (overrides auto-detect)"
            echo "  --help, -h          Show this help message"
            echo ""
            echo "Environment:"
            echo "  VIBE_UNITY_LOG      Path to Unity Editor.log (highest precedence)"
            echo ""
            echo "Exit codes:"
            echo "  0 = Success (no errors)"
            echo "  1 = Compilation errors found"
            echo "  2 = Indeterminate (Unity not reachable / no evidence observed)"
            echo "  3 = Script execution error"
            exit 0
            ;;
        *)
            echo "ERROR: Unknown option $1" >&2
            echo "Use --help for usage information" >&2
            exit 3
            ;;
    esac
done

# Portable byte size of a file (replaces GNU-specific `stat -c%s`).
file_size() {
    wc -c < "$1" 2>/dev/null | tr -d ' '
}

# Resolve the Unity Editor.log path across WSL and git-bash, with overrides.
# Precedence: VIBE_UNITY_LOG > --log-path > auto-detect > legacy fallback.
resolve_unity_log() {
    if [[ -n "${VIBE_UNITY_LOG:-}" ]]; then
        UNITY_LOG_PATH="$VIBE_UNITY_LOG"
        return
    fi
    if [[ -n "$LOG_PATH_OVERRIDE" ]]; then
        UNITY_LOG_PATH="$LOG_PATH_OVERRIDE"
        return
    fi

    local localappdata=""
    local uname_s
    uname_s="$(uname -s 2>/dev/null || echo unknown)"

    if grep -qiE 'microsoft|wsl' /proc/version 2>/dev/null; then
        # WSL: ask Windows for %LOCALAPPDATA%, translate to a /mnt path.
        local win
        win="$(cmd.exe /c "echo %LOCALAPPDATA%" 2>/dev/null | tr -d '\r')"
        if [[ -n "$win" && "$win" != "%LOCALAPPDATA%" ]] && command -v wslpath >/dev/null 2>&1; then
            localappdata="$(wslpath -u "$win" 2>/dev/null)"
        fi
    elif [[ "$uname_s" == MINGW* || "$uname_s" == MSYS* || "$uname_s" == CYGWIN* ]]; then
        # git-bash / MSYS / Cygwin: %LOCALAPPDATA% is exported as a Windows path.
        if [[ -n "${LOCALAPPDATA:-}" ]]; then
            if command -v cygpath >/dev/null 2>&1; then
                localappdata="$(cygpath -u "$LOCALAPPDATA" 2>/dev/null)"
            else
                # Best-effort: C:\Users\x -> /c/Users/x
                localappdata="$(echo "$LOCALAPPDATA" | sed -e 's|\\|/|g' -e 's|^\([A-Za-z]\):|/\L\1|')"
            fi
        fi
    fi

    if [[ -n "$localappdata" ]]; then
        UNITY_LOG_PATH="$localappdata/Unity/Editor/Editor.log"
    else
        # Last-resort legacy fallback; warn loudly since this is user-specific.
        UNITY_LOG_PATH="/mnt/c/Users/$(whoami)/AppData/Local/Unity/Editor/Editor.log"
        echo "WARNING: could not auto-detect Unity log path; falling back to $UNITY_LOG_PATH" >&2
        echo "         Set VIBE_UNITY_LOG or pass --log-path if this is wrong." >&2
    fi
}

# Function to output structured results
output_result() {
    local status="$1"
    local error_count="$2"
    local warning_count="$3"
    local details="$4"

    echo "STATUS: $status"
    echo "ERRORS: $error_count"
    echo "WARNINGS: $warning_count"
    if [[ -n "$details" ]]; then
        echo "DETAILS:"
        # Use %b so the accumulated literal "\n" separators render as newlines.
        printf '%b\n' "$details"
    fi
    echo "SCRIPT_VERSION: $SCRIPT_VERSION"
}

# Function to detect project name from current directory
detect_project_name() {
    PROJECT_NAME="$(basename "$(pwd)")"
    echo "Detected project name: $PROJECT_NAME" >&2
}

# Function to focus Unity window for recompilation
focus_unity() {
    local project_pattern="$PROJECT_NAME"
    if [[ -z "$project_pattern" ]]; then
        project_pattern="Unity"
    fi

    # Note: stderr is intentionally preserved so "No Unity process found" and the
    # window-mismatch warning reach the caller's log instead of being discarded.
    powershell.exe -NoProfile -Command "
    \$unityProcesses = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue | Where-Object { \$_.MainWindowTitle -ne '' };
    \$targetUnity = \$null;
    if ('$project_pattern' -ne 'Unity') {
        \$targetUnity = \$unityProcesses | Where-Object { \$_.MainWindowTitle -like '*$project_pattern*' } | Select-Object -First 1;
    }
    if (-not \$targetUnity -and \$unityProcesses) {
        \$targetUnity = \$unityProcesses | Select-Object -First 1;
        Write-Host \"Warning: Could not find Unity window for project '$project_pattern', using first Unity window: \$((\$targetUnity).MainWindowTitle)\";
    }
    if (\$targetUnity) {
        Add-Type -AssemblyName Microsoft.VisualBasic;
        [Microsoft.VisualBasic.Interaction]::AppActivate(\$targetUnity.Id);
        Write-Host \"Unity focused for compilation check: \$((\$targetUnity).MainWindowTitle)\";
        exit 0
    } else {
        Write-Host 'No Unity process found';
        exit 1
    }"
    return $?
}

# Function to focus back to the calling terminal (best-effort; non-fatal).
focus_wsl() {
    powershell.exe -NoProfile -Command "
    \$names = @('WindowsTerminal','cmd','Code','alacritty','ConEmu64','ConEmu');
    foreach (\$n in \$names) {
        \$p = Get-Process -Name \$n -ErrorAction SilentlyContinue | Select-Object -First 1;
        if (\$p) {
            Add-Type -AssemblyName Microsoft.VisualBasic;
            [Microsoft.VisualBasic.Interaction]::AppActivate(\$p.Id);
            break
        }
    }" 2>/dev/null || true
}

# Function to check Unity compilation status via status file
check_unity_compilation_status() {
    local status_file=".vibe-unity/status/compilation.json"
    if [[ ! -f "$status_file" ]]; then
        echo "NOT_FOUND"
        return
    fi
    local file_content=""
    if file_content=$(cat "$status_file" 2>/dev/null); then
        if echo "$file_content" | grep -q '"status":"compiling"'; then
            echo "COMPILING"
        elif echo "$file_content" | grep -q '"status":"complete"'; then
            echo "COMPLETE"
        else
            echo "UNKNOWN"
        fi
    else
        echo "LOCKED"
    fi
}

# Function to parse compilation errors and warnings from Unity log
parse_compilation_results() {
    local log_content="$1"
    local errors=""
    local warnings=""
    local error_count=0
    local warning_count=0

    while IFS= read -r line; do
        if [[ "$line" =~ ^(.+)\(([0-9]+),([0-9]+)\):\ error\ (.+):\ (.+)$ ]]; then
            local file="${BASH_REMATCH[1]}"
            local line_num="${BASH_REMATCH[2]}"
            local error_msg="${BASH_REMATCH[5]}"
            file=$(echo "$file" | sed 's|.*[/\\]Packages[/\\]|./Packages/|' | sed 's|.*[/\\]Assets[/\\]|./Assets/|')
            errors="${errors}  [$file:$line_num] $error_msg\n"
            ((++error_count))
        elif [[ "$line" =~ error\ CS[0-9]+: ]]; then
            local error_msg=$(echo "$line" | sed 's/.*error CS[0-9]*: //')
            errors="${errors}  [Unknown] $error_msg\n"
            ((++error_count))
        fi
    done <<< "$log_content"

    while IFS= read -r line; do
        if [[ "$line" =~ ^(.+)\(([0-9]+),([0-9]+)\):\ warning\ (CS[0-9]+):\ (.+)$ ]]; then
            ((++warning_count))
            if [[ "$INCLUDE_WARNINGS" == "true" ]]; then
                local file="${BASH_REMATCH[1]}"
                local line_num="${BASH_REMATCH[2]}"
                local warning_code="${BASH_REMATCH[4]}"
                local warning_msg="${BASH_REMATCH[5]}"
                file=$(echo "$file" | sed 's|\\|/|g' | sed 's|.*[/]Packages[/]|./Packages/|' | sed 's|.*[/]Assets[/]|./Assets/|')
                warnings="${warnings}  [$file:$line_num] WARNING $warning_code: $warning_msg\n"
            fi
        elif [[ "$line" =~ warning\ (CS[0-9]+):\ (.+)$ ]]; then
            ((++warning_count))
            if [[ "$INCLUDE_WARNINGS" == "true" ]]; then
                local warning_code="${BASH_REMATCH[1]}"
                local warning_msg="${BASH_REMATCH[2]}"
                warnings="${warnings}  [Unknown] WARNING $warning_code: $warning_msg\n"
            fi
        fi
    done <<< "$log_content"

    local details=""
    [[ -n "$errors" ]] && details="${details}${errors}"
    [[ -n "$warnings" ]] && details="${details}${warnings}"

    echo "$error_count|$warning_count|$details"
}

# Function to check if compilation logs exist
check_compilation_logs() {
    local log_content="$1"
    if echo "$log_content" | grep -q "CompileScripts\|Reloading assemblies\|Compilation\|Nothing to compile"; then
        return 0
    fi
    return 1
}

# Function to monitor Unity compilation with timeout
monitor_compilation() {
    local timeout=45
    local elapsed=0

    if [[ ! -f "$UNITY_LOG_PATH" ]]; then
        output_result "ERROR" "0" "0" "Unity Editor log not found at: $UNITY_LOG_PATH"
        return 2
    fi

    local initial_size
    initial_size=$(file_size "$UNITY_LOG_PATH")
    local current_size="$initial_size"
    local compilation_started=false
    local complete_checks=0

    while [[ $elapsed -lt $timeout ]]; do
        current_size=$(file_size "$UNITY_LOG_PATH")
        local unity_status
        unity_status=$(check_unity_compilation_status)

        if [[ "$unity_status" == "LOCKED" || "$unity_status" == "COMPILING" ]]; then
            compilation_started=true
            echo "Unity compilation detected via status file - compilation in progress..." >&2
        elif [[ "$unity_status" == "COMPLETE" ]]; then
            if [[ "$compilation_started" == "false" && $current_size -eq $initial_size ]]; then
                echo "Unity status complete and no new log activity; nothing to compile" >&2
                output_result "SUCCESS" "0" "0" "No compilation required - Unity is up to date"
                return 0
            fi
            if [[ "$compilation_started" == "true" ]]; then
                ((++complete_checks))
                if [[ $complete_checks -ge 3 ]]; then
                    echo "Unity shows complete status - treating as success" >&2
                    output_result "SUCCESS" "0" "0" "Compilation completed successfully"
                    return 0
                fi
            fi
        fi

        if [[ $current_size -gt $initial_size ]]; then
            local new_entries
            new_entries=$(tail -c +$((initial_size + 1)) "$UNITY_LOG_PATH")

            if echo "$new_entries" | grep -q "Nothing to compile\|All compiler tasks finished\|Compilation succeeded"; then
                output_result "SUCCESS" "0" "0" "No compilation needed - all scripts up to date"
                return 0
            fi

            if [[ "$compilation_started" == "false" ]] && echo "$new_entries" | grep -q "Reloading assemblies\|CompileScripts\|Start importing"; then
                compilation_started=true
            fi

            if echo "$new_entries" | grep -q "Reloading assemblies after forced synchronous recompile\|Finished compiling graph\|CompileScripts:.*ms\|Hotreload:.*ms\|PostProcessAllAssets:.*ms"; then
                local parse_result error_count warning_count details
                parse_result=$(parse_compilation_results "$new_entries")
                error_count=$(echo "$parse_result" | cut -d'|' -f1)
                warning_count=$(echo "$parse_result" | cut -d'|' -f2)
                details=$(echo "$parse_result" | cut -d'|' -f3-)
                if [[ $error_count -eq 0 ]]; then
                    output_result "SUCCESS" "$error_count" "$warning_count" "$details"
                    return 0
                else
                    output_result "ERRORS" "$error_count" "$warning_count" "$details"
                    return 1
                fi
            fi

            if echo "$new_entries" | grep -q "error CS[0-9]*:"; then
                sleep 2
                local final_entries parse_result error_count warning_count details
                final_entries=$(tail -c +$((initial_size + 1)) "$UNITY_LOG_PATH")
                parse_result=$(parse_compilation_results "$final_entries")
                error_count=$(echo "$parse_result" | cut -d'|' -f1)
                warning_count=$(echo "$parse_result" | cut -d'|' -f2)
                details=$(echo "$parse_result" | cut -d'|' -f3-)
                output_result "ERRORS" "$error_count" "$warning_count" "$details"
                return 1
            fi
        fi

        sleep 1
        ((++elapsed))

        if [[ "$unity_status" == "LOCKED" || "$unity_status" == "COMPILING" ]] && [[ $elapsed -gt $((timeout - 10)) ]]; then
            echo "Unity still compiling near timeout, extending wait..." >&2
            timeout=$((timeout + 15))
        fi
    done

    # Timeout reached - re-read state explicitly (do not rely on loop-local size).
    local final_unity_status final_size final_entries=""
    final_unity_status=$(check_unity_compilation_status)
    final_size=$(file_size "$UNITY_LOG_PATH")
    if [[ ${final_size:-0} -gt ${initial_size:-0} ]]; then
        final_entries=$(tail -c +$((initial_size + 1)) "$UNITY_LOG_PATH")
    fi

    if [[ "$final_unity_status" == "LOCKED" || "$final_unity_status" == "COMPILING" ]]; then
        echo "Unity still compiling at timeout (status: $final_unity_status)" >&2
        if [[ $RETRY_COUNT -lt $MAX_RETRIES ]]; then
            ((++RETRY_COUNT))
            echo "Retrying (attempt $((RETRY_COUNT + 1))/$((MAX_RETRIES + 1)))..." >&2
            return $RETRY_SENTINEL
        fi
    fi

    if check_compilation_logs "$final_entries"; then
        local parse_result error_count warning_count details
        parse_result=$(parse_compilation_results "$final_entries")
        error_count=$(echo "$parse_result" | cut -d'|' -f1)
        warning_count=$(echo "$parse_result" | cut -d'|' -f2)
        details=$(echo "$parse_result" | cut -d'|' -f3-)
        if [[ $error_count -eq 0 ]]; then
            output_result "SUCCESS" "$error_count" "$warning_count" "Compilation completed (found in logs after timeout)"
            return 0
        else
            output_result "ERRORS" "$error_count" "$warning_count" "$details"
            return 1
        fi
    fi

    if [[ $RETRY_COUNT -lt $MAX_RETRIES ]]; then
        ((++RETRY_COUNT))
        echo "No compilation evidence yet. Retrying (attempt $((RETRY_COUNT + 1))/$((MAX_RETRIES + 1)))..." >&2
        return $RETRY_SENTINEL
    else
        # IMPORTANT: never report SUCCESS when we observed no compilation evidence.
        # The whole point of this tool is to *confirm* compilation; "couldn't tell"
        # is reported as indeterminate (exit 2), not a clean pass.
        output_result "UNKNOWN" "0" "0" "No compilation evidence observed. Unity may not be running, auto-refresh may be disabled, or the trigger failed. Treat as UNVERIFIED."
        return 2
    fi
}

# Main execution function
main() {
    detect_project_name
    resolve_unity_log
    echo "Using Unity log: $UNITY_LOG_PATH" >&2

    local result=$RETRY_SENTINEL

    while [[ $result -eq $RETRY_SENTINEL ]] && [[ $RETRY_COUNT -le $MAX_RETRIES ]]; do
        if ! focus_unity; then
            output_result "ERROR" "0" "0" "Failed to focus Unity window. Ensure Unity is running."
            return 2
        fi
        sleep 2
        focus_wsl
        sleep 1
        monitor_compilation
        result=$?
        if [[ $result -eq $RETRY_SENTINEL ]] && [[ $RETRY_COUNT -lt $MAX_RETRIES ]]; then
            sleep 2
        fi
    done

    # If we exhausted retries still holding the sentinel, that's indeterminate.
    if [[ $result -eq $RETRY_SENTINEL ]]; then
        output_result "UNKNOWN" "0" "0" "Compilation could not be verified after retries. Treat as UNVERIFIED."
        return 2
    fi

    return $result
}

# Execute main function if script is run directly
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi
