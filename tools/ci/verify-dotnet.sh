#!/usr/bin/env bash
set -euo pipefail

REPOSITORY_ROOT="$(git rev-parse --show-toplevel)"
DOTNET="${DOTNET_HOST_PATH:-dotnet}"
WORK_DIRECTORY="$(mktemp -d -t cao-ci.XXXXXX)"

cleanup() {
  case "$(basename "$WORK_DIRECTORY")" in
    cao-ci.*) rm -rf -- "$WORK_DIRECTORY" ;;
    *) echo "Refusing to remove unexpected CI directory: $WORK_DIRECTORY" >&2 ;;
  esac
}
trap cleanup EXIT

cd "$REPOSITORY_ROOT"

SOURCE_PROJECT="Source/ColonistAwareness.csproj"
FIRST_OUTPUT="$WORK_DIRECTORY/build-a"
SECOND_OUTPUT="$WORK_DIRECTORY/build-b"
TOOL_OUTPUT="$WORK_DIRECTORY/tools-bin"
mkdir -p "$FIRST_OUTPUT" "$SECOND_OUTPUT" "$TOOL_OUTPUT"

"$DOTNET" restore "$SOURCE_PROJECT" --locked-mode --nologo

build_production() {
  local output="$1"
  "$DOTNET" build "$SOURCE_PROJECT" \
    --configuration Release \
    --no-restore \
    --nologo \
    --target:Rebuild \
    -p:OutputPath="$output/" \
    -p:ContinuousIntegrationBuild=true \
    -p:TreatWarningsAsErrors=true \
    -p:DebugType=None \
    -p:DebugSymbols=false
}

build_production "$FIRST_OUTPUT"
build_production "$SECOND_OUTPUT"

FIRST_DLL="$FIRST_OUTPUT/ColonistAwareness.dll"
SECOND_DLL="$SECOND_OUTPUT/ColonistAwareness.dll"
TRACKED_DLL="Assemblies/ColonistAwareness.dll"

cmp --silent "$FIRST_DLL" "$SECOND_DLL" || {
  echo "Clean production builds are not byte-identical." >&2
  exit 1
}

cmp --silent "$SECOND_DLL" "$TRACKED_DLL" || {
  echo "Tracked Assemblies/ColonistAwareness.dll does not match a clean production build." >&2
  exit 1
}

mapfile -t TOOL_PROJECTS < <(sed -e 's/\r$//' -e '/^[[:space:]]*$/d' -e '/^[[:space:]]*#/d' tools/ci/projects.txt)
for project in "${TOOL_PROJECTS[@]}"; do
  [[ -f "$project" ]] || {
    echo "Declared CI project does not exist: $project" >&2
    exit 1
  }
  slug="${project//[^A-Za-z0-9]/_}"
  "$DOTNET" build "$project" \
    --configuration Release \
    --nologo \
    -p:ContinuousIntegrationBuild=true \
    -p:TreatWarningsAsErrors=true \
    -p:OutputPath="$TOOL_OUTPUT/$slug/"
done

"$DOTNET" "$TOOL_OUTPUT/tools_B10SyntheticStateAudit_B10SyntheticStateAudit_csproj/B10SyntheticStateAudit.dll" \
  "$REPOSITORY_ROOT"
"$DOTNET" "$TOOL_OUTPUT/tools_B11AcceptanceReceipts_B11AcceptanceReceipts_csproj/B11AcceptanceReceipts.dll" \
  "$REPOSITORY_ROOT" --census-only
git diff --exit-code -- B10_SYNTHETIC_STATE_SWEEP.md PERSISTENCE_CENSUS.md

if command -v sha256sum >/dev/null 2>&1; then
  BUILD_SHA="$(sha256sum "$SECOND_DLL" | awk '{print toupper($1)}')"
else
  BUILD_SHA="$(shasum -a 256 "$SECOND_DLL" | awk '{print toupper($1)}')"
fi
BUILD_BYTES="$(wc -c < "$SECOND_DLL" | tr -d '[:space:]')"

SUMMARY="$WORK_DIRECTORY/ci-build-summary.txt"
cat > "$SUMMARY" <<EOF
ColonistAwareness.dll
bytes=$BUILD_BYTES
sha256=$BUILD_SHA
clean_builds_byte_identical=true
tracked_assembly_byte_identical=true
tool_projects_compiled=${#TOOL_PROJECTS[@]}
portable_receipt_checks=2
EOF

cat "$SUMMARY"

if [[ -n "${CAO_CI_ARTIFACT_DIR:-}" ]]; then
  mkdir -p "$CAO_CI_ARTIFACT_DIR"
  cp -- "$SECOND_DLL" "$CAO_CI_ARTIFACT_DIR/ColonistAwareness.dll"
  cp -- "$SUMMARY" "$CAO_CI_ARTIFACT_DIR/ci-build-summary.txt"
fi
