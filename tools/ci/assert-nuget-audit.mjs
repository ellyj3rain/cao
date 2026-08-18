import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

export function auditNuGetReport(report) {
  const findings = [];

  function visit(value, context = {}) {
  if (Array.isArray(value)) {
    for (const item of value) visit(item, context);
    return;
  }
  if (!value || typeof value !== "object") return;

  const nextContext = {
    project: value.path ?? value.projectPath ?? context.project,
    framework: value.framework ?? context.framework,
    package: value.id ?? value.name ?? context.package,
    version: value.resolvedVersion ?? value.version ?? context.version,
  };

  if (Array.isArray(value.vulnerabilities)) {
    for (const vulnerability of value.vulnerabilities) {
      findings.push({ ...nextContext, ...vulnerability });
    }
  }

  for (const [key, child] of Object.entries(value)) {
    if (key !== "vulnerabilities") visit(child, nextContext);
  }
  }

  visit(report);

  const severe = findings.filter((finding) =>
    ["high", "critical"].includes(String(finding.severity ?? "").toLowerCase()),
  );

  for (const finding of findings) {
    console.log(
      `${finding.severity ?? "unknown"}: ${finding.package ?? "unknown package"} ${finding.version ?? ""} ${finding.advisoryurl ?? finding.advisoryUrl ?? ""}`.trim(),
    );
  }

  if (severe.length > 0) {
    throw new Error(`${severe.length} high or critical NuGet vulnerabilities found`);
  }

  console.log(`NuGet audit passed: ${findings.length} reported vulnerabilities, 0 high or critical`);
  return findings;
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : "";
if (invokedPath === resolve(fileURLToPath(import.meta.url))) {
  const reportPath = process.argv[2];
  if (!reportPath) {
    throw new Error("usage: node tools/ci/assert-nuget-audit.mjs <report.json>");
  }
  auditNuGetReport(JSON.parse(await readFile(reportPath, "utf8")));
}
