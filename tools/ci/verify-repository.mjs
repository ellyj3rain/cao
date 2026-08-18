import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, "../..");

function run(command, args, { echo = true } = {}) {
  const result = spawnSync(command, args, {
    cwd: repositoryRoot,
    encoding: "utf8",
    stdio: "pipe",
  });

  if (echo && result.stdout) process.stdout.write(result.stdout);
  if (result.stderr) process.stderr.write(result.stderr);
  if (result.status !== 0) {
    throw new Error(`${command} ${args.join(" ")} failed with exit code ${result.status}`);
  }

  return result.stdout.trim();
}

function fail(message) {
  throw new Error(`repository verification failed: ${message}`);
}

run(process.execPath, ["tools/version-model.mjs", "--check"]);
run(process.execPath, ["--test", "tools/version-model.test.mjs"]);

const trackedFiles = run("git", ["ls-files"], { echo: false })
  .split(/\r?\n/u)
  .filter(Boolean);
const trackedFileSet = new Set(trackedFiles);
const publicCorpusFiles = new Set([
  "Corpus/PlayerBaseLayouts/README.md",
  "Corpus/PlayerBaseLayouts/external/real-ruins-broad-profile.json",
]);
const trackedCorpusFiles = trackedFiles.filter((file) =>
  file.startsWith("Corpus/PlayerBaseLayouts/"),
);

for (const file of trackedCorpusFiles) {
  if (!publicCorpusFiles.has(file)) {
    fail(`${file} is substantive or private corpus state and must remain local`);
  }
}

for (const file of trackedFiles) {
  if (/\.(?:rws|bp|layout\.json)$/iu.test(file)) {
    fail(`${file} is a tracked source or normalized layout payload`);
  }
}

for (const requiredGovernanceFile of [
  "DATASET_GOVERNANCE.md",
  "PLAYER_BASE_PATTERN_CORPUS.md",
  ...publicCorpusFiles,
]) {
  if (!trackedFileSet.has(requiredGovernanceFile)) {
    fail(`${requiredGovernanceFile} is required dataset-governance evidence`);
  }
}

for (const ignoredProbe of [
  "Corpus/PlayerBaseLayouts/.cache/probe.bp",
  "Corpus/PlayerBaseLayouts/operator/probe.layout.json",
  "Corpus/PlayerBaseLayouts/external/probe-clean-manifest.json",
  "Corpus/PlayerBaseLayouts/local/acquisitions/probe.json",
  "Corpus/PlayerBaseLayouts/training/probe.json",
  "Corpus/PlayerBaseLayouts/validation/probe.json",
  "Corpus/PlayerBaseLayouts/test/probe.json",
  "Corpus/PlayerBaseLayouts/governance-private/probe.json",
]) {
  const ignored = spawnSync(
    "git",
    ["check-ignore", "--no-index", "--quiet", "--", ignoredProbe],
    { cwd: repositoryRoot, stdio: "ignore" },
  );
  if (ignored.status !== 0) {
    fail(`${ignoredProbe} is not ignored by the corpus deny-by-default policy`);
  }
}

const aggregateProfilePath = resolve(
  repositoryRoot,
  "Corpus/PlayerBaseLayouts/external/real-ruins-broad-profile.json",
);
const aggregateProfile = JSON.parse(await readFile(aggregateProfilePath, "utf8"));
if (aggregateProfile.entries || aggregateProfile.records || aggregateProfile.layouts) {
  fail("the public Real Ruins profile contains per-record corpus payload");
}

const packagingSurfaces = trackedFiles.filter((file) =>
  file === ".gitlab-ci.yml"
  || /^\.github\/workflows\/.*\.ya?ml$/u.test(file)
  || /\.(?:csproj|props|targets)$/u.test(file)
  || /^tools\/ci\/.*\.(?:sh|ps1|cmd|bat)$/u.test(file),
);
for (const packagingSurface of packagingSurfaces) {
  const contents = await readFile(resolve(repositoryRoot, packagingSurface), "utf8");
  if (/Corpus[\\/]/u.test(contents)) {
    fail(`${packagingSurface} references the local corpus in build or release automation`);
  }
}

const trackedProjects = run("git", ["ls-files", "*.csproj"], { echo: false })
  .split(/\r?\n/u)
  .filter(Boolean);

if (trackedProjects.length === 0) {
  fail("no tracked C# projects were found");
}

const declaredProjects = (await readFile(resolve(repositoryRoot, "tools/ci/projects.txt"), "utf8"))
  .split(/\r?\n/u)
  .map((line) => line.trim())
  .filter((line) => line && !line.startsWith("#"));
const trackedProjectSet = new Set(trackedProjects);
if (new Set(declaredProjects).size !== declaredProjects.length) {
  fail("tools/ci/projects.txt contains duplicate projects");
}
for (const project of declaredProjects) {
  if (!trackedProjectSet.has(project)) {
    fail(`${project} is declared for CI but is not a tracked C# project`);
  }
}

for (const project of trackedProjects) {
  const contents = await readFile(resolve(repositoryRoot, project), "utf8");
  const wildcardPackage = /<PackageReference\b[^>]*\bVersion\s*=\s*["'][^"']*\*[^"']*["']/iu;
  const absoluteHintPath = /<HintPath>\s*(?:[A-Za-z]:[\\/]|\/(?:Users|home)\/)/iu;

  if (wildcardPackage.test(contents)) {
    fail(`${project} contains a floating package version`);
  }
  if (absoluteHintPath.test(contents)) {
    fail(`${project} contains a machine-specific assembly path`);
  }
}

const lockPath = resolve(repositoryRoot, "Source/packages.lock.json");
const lock = JSON.parse(await readFile(lockPath, "utf8"));
const framework = lock.dependencies?.[".NETFramework,Version=v4.7.2"];
if (!framework) {
  fail("Source/packages.lock.json does not contain the net472 dependency graph");
}

const expectedDirectPackages = new Map([
  ["Krafs.Rimworld.Ref", "1.6.4871"],
  ["Lib.Harmony", "2.4.2"],
]);

for (const [packageName, expectedVersion] of expectedDirectPackages) {
  const entry = framework[packageName];
  if (!entry || entry.type !== "Direct" || entry.resolved !== expectedVersion) {
    fail(`${packageName} must resolve exactly to ${expectedVersion}`);
  }
}

console.log(
  `repository verification passed: version replay, corpus boundary, ${trackedProjects.length} tracked C# projects, ${declaredProjects.length} executable support projects, and locked production dependencies`,
);
