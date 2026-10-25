#!/usr/bin/env bash
# Builds the web player (Runtime Speed with LTO) to Build/GameOfLife and publishes it to gh-pages.
#   ./deploy.sh               build headlessly + publish (Unity editor must be closed)
#   ./deploy.sh --skip-build  publish the existing build only
# With the editor open, use Build > Build and Publish Web instead.
set -euo pipefail
cd "$(dirname "$0")"

windows=false
[[ "$OSTYPE" == msys* || "$OSTYPE" == cygwin* ]] && windows=true

if [[ "${1:-}" != "--skip-build" ]]; then
  # The open editor holds the lockfile: macOS shows it in lsof, Windows refuses to open it
  editor_open=false
  if [[ -e Temp/UnityLockfile ]]; then
    if $windows; then
      { : >> Temp/UnityLockfile; } 2>/dev/null || editor_open=true
    else
      lsof Temp/UnityLockfile >/dev/null 2>&1 && editor_open=true
    fi
  fi
  if $editor_open; then
    echo "The Unity editor has this project open. Close it, or use Build > Build and Publish Web in the editor." >&2
    exit 1
  fi

  version=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')
  if $windows; then
    unity=${UNITY:-/c/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe}
  else
    unity=${UNITY:-/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity}
  fi

  echo "Building web player with Unity $version..."
  "$unity" -batchmode -quit -projectPath "$PWD" -buildTarget WebGL \
    -executeMethod WebDeploy.BuildFromCommandLine -logFile Logs/WebBuild.log \
    || { echo "Build failed, see Logs/WebBuild.log" >&2; exit 1; }
fi

if [[ ! -f Build/GameOfLife/index.html ]]; then
  echo "No build found at Build/GameOfLife" >&2
  exit 1
fi

cd PagesPublisher
[[ -d node_modules ]] || npm ci
node index.js
