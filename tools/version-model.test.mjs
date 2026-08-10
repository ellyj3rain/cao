import test from "node:test";
import assert from "node:assert/strict";

import {
  CURRENT_VERSION,
  DEFAULT_REPO_ROOT,
  VERSION_UNITS,
  computeNextVersion,
  computeVersionReplay,
  expandBatchSpan,
  validateRepository,
} from "./version-model.mjs";

test("semantic replay derives the current CAO version", () => {
  const replay = computeVersionReplay();
  assert.equal(replay.currentVersion, "0.12.3.0-alpha");
  assert.equal(CURRENT_VERSION, "0.12.3.0-alpha");
  assert.equal(replay.trace.length, 27);
  assert.equal(replay.trace[0].version, "0.1.0.0-pre-alpha");
  assert.equal(replay.trace[1].version, "0.2.0.0-pre-alpha");
  assert.equal(replay.trace[2].version, "0.2.0.1-pre-alpha");
  assert.equal(replay.trace[8].version, "0.5.0.0-alpha");
});

test("version units partition A1-A102 exactly once", () => {
  const covered = VERSION_UNITS.flatMap(expandBatchSpan);
  const expected = Array.from({ length: 102 }, (_, index) => `A${index + 1}`);
  assert.deepEqual(covered, expected);
});

test("Neo odometer caps roll mechanically", () => {
  assert.equal(computeNextVersion("0.12.3.0-alpha", "minor"), "1.0.0.0-alpha");
  assert.equal(computeNextVersion("0.12.16.0-alpha", "kohai"), "1.0.0.0-alpha");
  assert.equal(computeNextVersion("0.11.16.24-alpha", "patch"), "0.12.0.0-alpha");
  assert.equal(computeNextVersion("0.12.16.24-alpha", "hotfix"), "1.0.0.0-alpha");
});

test("repository projections and identifiers match the replay", () => {
  const result = validateRepository(DEFAULT_REPO_ROOT);
  assert.equal(result.ok, true, result.errors.join("\n"));
  assert.equal(result.closedBatchCount, 102);
  assert.equal(result.nextBatch, "B1");
});
