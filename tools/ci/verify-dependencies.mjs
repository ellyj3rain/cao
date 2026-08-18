import { writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { spawnSync } from "node:child_process";
import { auditNuGetReport } from "./assert-nuget-audit.mjs";

const dotnet = process.env.DOTNET_HOST_PATH || "dotnet";
const result = spawnSync(
  dotnet,
  [
    "list",
    "Source/ColonistAwareness.csproj",
    "package",
    "--vulnerable",
    "--include-transitive",
    "--format",
    "json",
  ],
  { encoding: "utf8", stdio: "pipe" },
);

if (result.stderr) process.stderr.write(result.stderr);
if (result.status !== 0) {
  throw new Error(`dotnet dependency report failed with exit code ${result.status}`);
}

const firstBrace = result.stdout.indexOf("{");
const lastBrace = result.stdout.lastIndexOf("}");
if (firstBrace < 0 || lastBrace < firstBrace) {
  throw new Error("dotnet dependency report did not contain JSON");
}

const reportText = result.stdout.slice(firstBrace, lastBrace + 1);
const report = JSON.parse(reportText);
auditNuGetReport(report);

const outputPath = resolve(process.env.CAO_NUGET_REPORT || "nuget-vulnerabilities.json");
await writeFile(outputPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");
console.log(`NuGet evidence written to ${outputPath}`);
