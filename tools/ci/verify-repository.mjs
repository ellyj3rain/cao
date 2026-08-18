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
  `repository verification passed: version replay, ${trackedProjects.length} tracked C# projects, ${declaredProjects.length} executable support projects, and locked production dependencies`,
);
