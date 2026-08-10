import {
  existsSync,
  readFileSync,
  readdirSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

// CAO's source-owned adaptation of Neo's governed odometer and semantic replay.
// Reference mechanism: Neo DR-058/DR-069/DR-120 and
// core/governance/{assess,version-replay}.js. CAO's unit grain differs
// deliberately: one tiered version unit may contain several contiguous atomic
// batches, while the arithmetic and maturity ladder remain the same.

export const VERSION_MODEL_SCHEMA = "cao.version-model/1";
export const MINOR_HARD_CAP = 12;
export const KOHAI_HARD_CAP = 16;
export const PATCH_HARD_CAP = 24;
export const MATURITY_LADDER = Object.freeze(["pre-alpha", "alpha", "beta", "rc"]);
export const ROOT_REPLAY_START_VERSION = "0.1.0.0-pre-alpha";
export const CLOSED_BATCH_TIP = "B2";
export const NEXT_BATCH = "B3";

const here = dirname(fileURLToPath(import.meta.url));
export const DEFAULT_REPO_ROOT = resolve(here, "..");

export const VERSION_UNITS = Object.freeze([
  {
    id: "VU-001",
    first: 1,
    last: 2,
    dates: "2026-07-22",
    tier: "initial",
    name: "Repository and imported tactical baseline",
    threads: ["T-001", "T-002", "T-003", "T-004", "T-007", "T-008"],
    rationale: "The former A1 source entry established the repository and imported the complete pre-git baseline under VERSION 0.1.0; the later batch recatalog split that one historical version boundary into A1 and A2.",
  },
  {
    id: "VU-002",
    first: 3,
    last: 3,
    dates: "2026-07-22",
    tier: "minor",
    name: "First pillar slice",
    threads: ["T-003", "T-004", "T-005"],
    rationale: "A3 joins the former A2 and A2.1 implementation and hardening records. It established the first public Disposition, Knowledge, and Authority slice and carried the historical VERSION 0.2.0 boundary.",
  },
  {
    id: "VU-003",
    first: 4,
    last: 4,
    dates: "2026-07-23",
    tier: "patch",
    name: "Half-speed control correction",
    threads: ["T-002", "T-029", "T-030"],
    rationale: "A4 contains the half-speed control and its immediate load correction. The historical 0.2.1 movement is an in-place correction and therefore maps to the fourth-coordinate patch tier.",
  },
  {
    id: "VU-004",
    first: 5,
    last: 7,
    dates: "2026-07-24",
    tier: "minor",
    name: "Shared awareness and communications",
    threads: ["T-004", "T-005", "T-008", "T-010"],
    rationale: "A5-A7 form one continuous expansion from shared contact evidence through communication media to the corrected autonomy floor. The former A2.3-A2.3.2 run carried the historical 0.2.2 boundary; semantic replay classifies the public capability growth as minor.",
  },
  {
    id: "VU-005",
    first: 8,
    last: 8,
    dates: "2026-07-24",
    tier: "kohai",
    name: "Independent pointer bridge",
    threads: ["T-011", "T-029"],
    rationale: "A8 added event-local developer input infrastructure without establishing a new gameplay capability line.",
  },
  {
    id: "VU-006",
    first: 9,
    last: 14,
    dates: "2026-07-24 to 2026-07-25",
    tier: "minor",
    name: "Individual awareness and welfare judgment",
    threads: ["T-003", "T-004", "T-006", "T-008", "T-010", "T-012"],
    rationale: "A9-A14 continuously apply the awareness substrate to lost contacts, animal response, equipment access, immediate combat judgment, casualty triage, and private welfare accountability. Together they establish the first complete individual decision-and-care line.",
  },
  {
    id: "VU-007",
    first: 15,
    last: 15,
    dates: "2026-07-25",
    tier: "kohai",
    name: "Pointer bridge across program states",
    threads: ["T-011", "T-029"],
    rationale: "A15 extends the existing pointer bridge through menus and dialogs; it matures the tool boundary rather than adding a separate operator capability.",
  },
  {
    id: "VU-008",
    first: 16,
    last: 21,
    dates: "2026-07-25 to 2026-07-26",
    tier: "minor",
    name: "Native emergency execution",
    threads: ["T-006", "T-007", "T-008", "T-010", "T-012", "T-016"],
    rationale: "A16-A21 move awareness decisions into persistent native work: fighting withdrawal, synchronized hauling, cover halts, fire response, threat correction, and downed-animal feeding. The intervening corrections were discovered through the same live native-execution run.",
  },
  {
    id: "VU-009",
    first: 22,
    last: 22,
    dates: "2026-07-26",
    tier: "maturity-alpha",
    name: "Reproducible alpha assembly",
    threads: ["T-002", "T-030"],
    rationale: "A22 made the proven native-execution line reproducibly assemblable. It changes maturity from pre-alpha to alpha without moving a numeric coordinate.",
  },
  {
    id: "VU-010",
    first: 23,
    last: 31,
    dates: "2026-07-27 to 2026-07-28",
    tier: "minor",
    name: "Authored homes and space programs",
    threads: ["T-010", "T-013", "T-014", "T-015", "T-016", "T-025"],
    rationale: "A23-A31 progress continuously from autonomous home planning through authored room programs and residents to native construction prerequisites and the storage policy those prerequisites exposed.",
  },
  {
    id: "VU-011",
    first: 32,
    last: 32,
    dates: "2026-07-28",
    tier: "kohai",
    name: "Regional living-world architecture",
    threads: ["T-001", "T-019", "T-020", "T-021"],
    rationale: "A32 ratified the regional architecture without shipping an independent runtime surface. It is structural preparation for the later regional line.",
  },
  {
    id: "VU-012",
    first: 33,
    last: 43,
    dates: "2026-07-28",
    tier: "minor",
    name: "Native storage and contextual facilities",
    threads: ["T-013", "T-014", "T-015", "T-016", "T-019", "T-023"],
    rationale: "A33-A43 build one public spatial-planning capability from native construction storage and durable inventory through settlement context, contextual placement, furnishing evidence, facility comparison, and authored requirements.",
  },
  {
    id: "VU-013",
    first: 44,
    last: 49,
    dates: "2026-07-28",
    tier: "kohai",
    name: "Waste, stockpiles, and spatial initiative",
    threads: ["T-010", "T-015", "T-016", "T-017"],
    rationale: "A44-A49 extend the existing spatial-planning line through toxic-waste lifecycle handling, return storage authority to native stockpiles, and add shared spatial initiative and shelf construction.",
  },
  {
    id: "VU-014",
    first: 50,
    last: 55,
    dates: "2026-07-29",
    tier: "kohai",
    name: "Contextual furnishing and society evidence",
    threads: ["T-004", "T-006", "T-012", "T-014", "T-015", "T-023"],
    rationale: "A50-A55 mature contextual facilities with room-authority transitions, stable welfare evidence, animal infrastructure, society-specific interpretation, and authored Bedroom comparison.",
  },
  {
    id: "VU-015",
    first: 56,
    last: 56,
    dates: "2026-07-29 to 2026-07-30",
    tier: "kohai",
    name: "Developer item relocation",
    threads: ["T-002", "T-011", "T-029", "T-030"],
    rationale: "A56 adds and proves a developer-only item-relocation and native MouseMux tool path; it matures runtime tooling without adding simulation behavior.",
  },
  {
    id: "VU-016",
    first: 57,
    last: 57,
    dates: "2026-07-30",
    tier: "patch",
    name: "Bedroom construction cause proof",
    threads: ["T-013", "T-014", "T-016", "T-030"],
    rationale: "A57 verifies and tightens the existing authored Bedroom material-and-construction chain rather than establishing another capability.",
  },
  {
    id: "VU-017",
    first: 58,
    last: 61,
    dates: "2026-07-30",
    tier: "minor",
    name: "Combat execution and battlefield evidence",
    threads: ["T-004", "T-008", "T-009"],
    rationale: "A58-A61 form a continuous combat release: execution restoration, initiative separation, pawn-proximal topology, stable battlefield reference, and continuous after-action proof.",
  },
  {
    id: "VU-018",
    first: 62,
    last: 64,
    dates: "2026-08-05",
    tier: "minor",
    name: "Political beliefs and first deployment",
    threads: ["T-006", "T-022", "T-023"],
    rationale: "A62-A64 establish standing conventions, persistent conviction and issue judgment, and the first deployed political simulation line.",
  },
  {
    id: "VU-019",
    first: 65,
    last: 69,
    dates: "2026-08-05",
    tier: "minor",
    name: "Settlement setup and institutional economy",
    threads: ["T-018", "T-021", "T-022", "T-023", "T-024", "T-025", "T-028"],
    rationale: "A65-A69 rebuild setup around owned scopes, restore regional population and settlement authoring, separate settlement axes, and add explicit institutional transactions and credit terms.",
  },
  {
    id: "VU-020",
    first: 70,
    last: 71,
    dates: "2026-08-06",
    tier: "kohai",
    name: "Repository and governance convergence",
    threads: ["T-001", "T-002", "T-030"],
    rationale: "A70-A71 preserve the pre-cleanup repository and adopt the canonical governance pack. This is structural repository maturation with no gameplay capability movement.",
  },
  {
    id: "VU-021",
    first: 72,
    last: 77,
    dates: "2026-08-06 to 2026-08-07",
    tier: "minor",
    name: "Regional spatial generation",
    threads: ["T-019", "T-020", "T-021", "T-025", "T-026"],
    rationale: "A72-A77 establish the public regional execution line: carrier-owned geography, constituent-local spatial work, candidate persistence, compatibility gates, per-cell allocation, extent, and authoritative projection.",
  },
  {
    id: "VU-022",
    first: 78,
    last: 84,
    dates: "2026-08-07",
    tier: "kohai",
    name: "World-generation onboarding and projection",
    threads: ["T-019", "T-020", "T-021", "T-024", "T-025", "T-026", "T-027", "T-028"],
    rationale: "A78-A84 integrate and close the existing regional capability through onboarding structure, landing separation, engine-root boundaries, fixture corrections, the projection kernel, and Screen 2 semantics.",
  },
  {
    id: "VU-023",
    first: 85,
    last: 90,
    dates: "2026-08-07 to 2026-08-08",
    tier: "minor",
    name: "Political, cultural, and territorial composition",
    threads: ["T-014", "T-016", "T-019", "T-020", "T-021", "T-022", "T-023", "T-025", "T-026", "T-028"],
    rationale: "A85-A90 form one new simulation and authoring contract from political architecture and canonical relations through Ideoligion, settlement population and provisions, and territorial realization.",
  },
  {
    id: "VU-024",
    first: 91,
    last: 91,
    dates: "2026-08-08",
    tier: "kohai",
    name: "Runtime exercise harness",
    threads: ["T-002", "T-027", "T-029", "T-030"],
    rationale: "A91 hardens the test and launch boundary and parameterizes map scale; it is runtime tooling for the existing world-generation capability.",
  },
  {
    id: "VU-025",
    first: 92,
    last: 98,
    dates: "2026-08-08",
    tier: "kohai",
    name: "Creator mechanics and product convergence",
    threads: ["T-008", "T-015", "T-016", "T-018", "T-021", "T-022", "T-023", "T-024", "T-025", "T-026", "T-028"],
    rationale: "A92-A98 mature the existing creator line: composition-first authoring, stratified axes, concrete consumers, corrected ontology, operational machinery, and agreement between declarations and generated worlds.",
  },
  {
    id: "VU-026",
    first: 99,
    last: 99,
    dates: "2026-08-08 to 2026-08-09",
    tier: "patch",
    name: "Map-size selector correction",
    threads: ["T-024", "T-027", "T-029", "T-030"],
    rationale: "A99 diagnoses and fixes one production map-size field without changing the surrounding creator contract.",
  },
  {
    id: "VU-027",
    first: 100,
    last: 102,
    dates: "2026-08-09 to 2026-08-10",
    tier: "kohai",
    name: "World language and ontology convergence",
    threads: ["T-014", "T-016", "T-021", "T-022", "T-023", "T-024", "T-025", "T-026", "T-028", "T-030"],
    rationale: "A100-A102 converge world tendencies, creator grammar, evidence capture, factions, settlements, population, provisions, persistence, generation, and player-facing language onto the already established creator capability.",
  },
  {
    id: "VU-028",
    series: "B",
    first: 1,
    last: 1,
    dates: "2026-08-10",
    tier: "kohai",
    name: "Causal world authoring",
    threads: ["T-002", "T-019", "T-021", "T-024", "T-025", "T-026", "T-028", "T-030"],
    rationale: "B1 converges the existing World tendencies surface into one causal policy, realization, persistence, and generation contract. It matures the A100-A102 creator line with fixed-seed isolation receipts, current-schema fixture repair, and a verified deployment rather than opening a separate gameplay capability.",
  },
  {
    id: "VU-029",
    series: "B",
    first: 2,
    last: 2,
    dates: "2026-08-10",
    tier: "minor",
    name: "Player founding authoring",
    threads: ["T-022", "T-023", "T-024", "T-025", "T-028", "T-030"],
    rationale: "B2 restores a missing player-visible authoring and runtime contract: Culture, native Ideoligion, Political Beliefs, and the adopted Founding Arrangement now share the faction ontology while preserving the temporal difference between an established society and a new colony. The world-owned draft and one-shot arrangement receipt cover regional and non-regional starts without fabricating mature player institutions. This is a new setup capability rather than an in-place correction, so it carries the minor tier.",
  },
]);

function parseVersion(version) {
  const raw = String(version ?? "").trim();
  const match = /^(\d+)\.(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z-]+))?$/.exec(raw);
  if (!match) {
    throw new Error(`malformed version "${raw}"; expected major.minor.kohai.patch-maturity`);
  }
  const maturity = match[5] ?? null;
  if (maturity && !MATURITY_LADDER.includes(maturity)) {
    throw new Error(`unknown maturity "${maturity}"`);
  }
  const parsed = {
    major: Number(match[1]),
    minor: Number(match[2]),
    kohai: Number(match[3]),
    patch: Number(match[4]),
    maturity,
  };
  if (parsed.minor > MINOR_HARD_CAP) throw new Error(`MINOR exceeds hard cap ${MINOR_HARD_CAP}`);
  if (parsed.kohai > KOHAI_HARD_CAP) throw new Error(`KOHAI exceeds hard cap ${KOHAI_HARD_CAP}`);
  if (parsed.patch > PATCH_HARD_CAP) throw new Error(`PATCH exceeds hard cap ${PATCH_HARD_CAP}`);
  return parsed;
}

function formatVersion({ major, minor, kohai, patch, maturity }) {
  return `${major}.${minor}.${kohai}.${patch}${maturity ? `-${maturity}` : ""}`;
}

export function computeNextVersion(current, tier) {
  const state = parseVersion(current);
  const next = { ...state };
  const normalized = String(tier ?? "").trim().toLowerCase();

  if (normalized === "major") {
    next.major += 1;
    next.minor = 0;
    next.kohai = 0;
    next.patch = 0;
  } else if (normalized === "minor") {
    if (next.minor === MINOR_HARD_CAP) {
      next.major += 1;
      next.minor = 0;
    } else {
      next.minor += 1;
    }
    next.kohai = 0;
    next.patch = 0;
  } else if (normalized === "kohai") {
    if (next.kohai === KOHAI_HARD_CAP) {
      if (next.minor === MINOR_HARD_CAP) {
        next.major += 1;
        next.minor = 0;
      } else {
        next.minor += 1;
      }
      next.kohai = 0;
    } else {
      next.kohai += 1;
    }
    next.patch = 0;
  } else if (normalized === "patch" || normalized === "hotfix") {
    if (next.patch === PATCH_HARD_CAP) {
      next.patch = 0;
      if (next.kohai === KOHAI_HARD_CAP) {
        next.kohai = 0;
        if (next.minor === MINOR_HARD_CAP) {
          next.major += 1;
          next.minor = 0;
        } else {
          next.minor += 1;
        }
      } else {
        next.kohai += 1;
      }
    } else {
      next.patch += 1;
    }
  } else {
    throw new Error(`unknown version tier "${tier}"`);
  }

  return formatVersion(next);
}

function applyUnit(version, unit) {
  if (unit.tier === "initial") return version;
  if (unit.tier === "maturity-alpha") {
    const state = parseVersion(version);
    state.maturity = "alpha";
    return formatVersion(state);
  }
  return computeNextVersion(version, unit.tier);
}

export function computeVersionReplay(units = VERSION_UNITS) {
  let version = ROOT_REPLAY_START_VERSION;
  const trace = [];
  for (const unit of units) {
    version = applyUnit(version, unit);
    trace.push({ ...unit, version });
  }
  return {
    schema: VERSION_MODEL_SCHEMA,
    startVersion: ROOT_REPLAY_START_VERSION,
    currentVersion: version,
    trace,
  };
}

export const CURRENT_VERSION = computeVersionReplay().currentVersion;

export function expandBatchSpan(unit) {
  const series = unit.series ?? "A";
  return Array.from({ length: unit.last - unit.first + 1 }, (_, index) => `${series}${unit.first + index}`);
}

function batchSpan(unit) {
  const series = unit.series ?? "A";
  return unit.first === unit.last ? `${series}${unit.first}`
    : `${series}${unit.first}-${series}${unit.last}`;
}

function renderThreadRefs(threads) {
  return threads.map((id) => `\`${id}\``).join(", ");
}

export function renderVersionMap() {
  const replay = computeVersionReplay();
  const rows = replay.trace.map((unit) =>
    `| ${unit.id} | ${batchSpan(unit)} | ${unit.dates} | ${unit.tier} | \`${unit.version}\` | ${unit.name} | ${renderThreadRefs(unit.threads)} | ${unit.rationale} |`,
  );
  const nextMinor = computeNextVersion(replay.currentVersion, "minor");
  const nextKohai = computeNextVersion(replay.currentVersion, "kohai");
  const nextPatch = computeNextVersion(replay.currentVersion, "patch");

  return `# Version map

This is the regulatory version replay for Colonist Awareness. It partitions the closed batch chronology into contiguous capability units without rewriting the batch records. Threads classify work across time; version units partition time; batches remain the atomic historical record.

| Field | Current state |
|---|---|
| Schema | \`${VERSION_MODEL_SCHEMA}\` |
| Form | \`major.minor.kohai.patch-maturity\` |
| Hard caps | minor ${MINOR_HARD_CAP}; kohai ${KOHAI_HARD_CAP}; patch ${PATCH_HARD_CAP} |
| Replay start | \`${ROOT_REPLAY_START_VERSION}\` |
| Current version | \`${replay.currentVersion}\` |
| Closed chronology | \`A1-B2\` |
| Next batch | \`${NEXT_BATCH}\` |
| Executable source | [\`tools/version-model.mjs\`](tools/version-model.mjs) |

## Tier meanings

| Tier | Meaning in CAO |
|---|---|
| major | Formal release, project-identity, or supported-compatibility boundary. No A-series unit requires it. |
| minor | A new player-visible simulation capability or a new authoring/runtime contract. |
| kohai | A coherent extension, integration, or structural maturation of an existing capability. |
| patch | An in-place correction, verification closure, or repair that does not change the capability boundary. |
| hotfix | Urgent patch movement. It shares patch arithmetic and is not used by the A-series replay. |
| maturity | \`pre-alpha -> alpha -> beta -> rc -> GA\`; maturity can change without moving a numeric coordinate. |

## Chronological replay

| Unit | Batches | Date or range | Tier | Resulting version | Descriptive name | Thread evidence | Boundary rationale |
|---|---|---|---|---|---|---|---|
${rows.join("\n")}

## Historical version evidence

The original file moved from \`0.1.0\` to \`0.2.0\`, \`0.2.1\`, and \`0.2.2\`. Those declarations establish the first four historical boundaries. The three-coordinate values are replayed through the four-coordinate hierarchy: the former patch becomes the fourth coordinate, while substantive public capability growth is classified by the current tier rubric. After A5 the old file remained hand-frozen at \`0.2.2\`; it is evidence of the former state, not a version assignment for later batches.

A22 is the maturity boundary: reproducible assembly followed an end-to-end runtime line with live execution evidence, so the replay changes from \`pre-alpha\` to \`alpha\` there without a numeric bump.

## Next movement

\`${NEXT_BATCH}\` is the next ordinary batch. Its content determines its tier after it exists:

| If ${NEXT_BATCH} is | Result |
|---|---|
| patch or hotfix | \`${nextPatch}\` |
| kohai | \`${nextKohai}\` |
| minor | \`${nextMinor}\` (the minor hard cap rolls the numeric major; maturity remains alpha) |

The thematic catalog is series-neutral in [\`Batches/THREADS.md\`](Batches/THREADS.md). Temporary \`AT-*\` and \`ATF-*\` identifiers resolve through [\`Batches/THREAD_ID_CROSSWALK.md\`](Batches/THREAD_ID_CROSSWALK.md).
`;
}

const STAMP_MARKER = "cao:generated:version";
const STAMP_TARGETS = Object.freeze({
  "README.md": () => `Current version: \`${CURRENT_VERSION}\`. Implementation is complete through batch \`${CLOSED_BATCH_TIP}\`; \`${NEXT_BATCH}\` is the next development batch. The verified assembly is deployed for operator runtime testing. Static verification does not substitute for how the game looks and plays.`,
  "CORE.md": () => `| Version | \`${CURRENT_VERSION}\` · closed batch tip \`${CLOSED_BATCH_TIP}\` · next \`${NEXT_BATCH}\` |`,
  "GOVERNANCE.md": () => `| Version | \`${CURRENT_VERSION}\` · closed batch tip \`${CLOSED_BATCH_TIP}\` · next \`${NEXT_BATCH}\` |`,
  "MEMORY.md": () => `| Version | \`${CURRENT_VERSION}\` · closed batch tip \`${CLOSED_BATCH_TIP}\` · next \`${NEXT_BATCH}\` |`,
  "SESSION_STATE.md": () => `| Version | \`${CURRENT_VERSION}\` · closed batch tip \`${CLOSED_BATCH_TIP}\` · next \`${NEXT_BATCH}\` |`,
});

function generatedRegion(body) {
  return `<!-- ${STAMP_MARKER} BEGIN -->\n${body}\n<!-- ${STAMP_MARKER} END -->`;
}

function replaceGeneratedRegion(text, body, file) {
  const pattern = new RegExp(`<!-- ${STAMP_MARKER} BEGIN -->[\\s\\S]*?<!-- ${STAMP_MARKER} END -->`);
  if (!pattern.test(text)) throw new Error(`${file} has no ${STAMP_MARKER} region`);
  return text.replace(pattern, generatedRegion(body));
}

function markdownFilesForLegacyIdScan(repoRoot) {
  const files = [
    "BATCH_LOG.md",
    "ROADMAP.md",
    "README.md",
    "CORE.md",
    "GOVERNANCE.md",
    "MEMORY.md",
    "SESSION_STATE.md",
    "VERSION_MAP.md",
  ];
  const batches = readdirSync(join(repoRoot, "Batches"))
    .filter((name) => name.endsWith(".md") && name !== "THREAD_ID_CROSSWALK.md")
    .map((name) => join("Batches", name));
  const source = readdirSync(join(repoRoot, "Source"))
    .filter((name) => name.endsWith(".cs"))
    .map((name) => join("Source", name));
  return [...files, ...batches, ...source];
}

export function validateRepository(repoRoot = DEFAULT_REPO_ROOT) {
  const errors = [];
  const replay = computeVersionReplay();

  const covered = VERSION_UNITS.flatMap(expandBatchSpan);
  const expected = [
    ...Array.from({ length: 102 }, (_, index) => `A${index + 1}`),
    "B1",
    "B2",
  ];
  if (JSON.stringify(covered) !== JSON.stringify(expected)) {
    errors.push("version units must cover A1-B2 exactly once, contiguously, and in order");
  }
  VERSION_UNITS.forEach((unit, index) => {
    const expectedId = `VU-${String(index + 1).padStart(3, "0")}`;
    if (unit.id !== expectedId) errors.push(`expected version unit ${expectedId}, found ${unit.id}`);
  });

  const batchFiles = readdirSync(join(repoRoot, "Batches"));
  const closedIds = batchFiles
    .map((name) => /^([A-Z])(\d{3})-.*\.md$/.exec(name))
    .filter(Boolean)
    .map((match) => `${match[1]}${Number(match[2])}`)
    .sort((left, right) => left[0].localeCompare(right[0])
      || Number(left.slice(1)) - Number(right.slice(1)));
  if (JSON.stringify(closedIds) !== JSON.stringify(expected)) {
    errors.push("Batches/ must contain exactly the closed A001-A102 and B001-B002 record set");
  }
  if (batchFiles.some((name) => /^B0*3-.*\.md$/.test(name))) {
    errors.push("B3 must remain unconsumed until the next development batch");
  }

  const batchLog = readFileSync(join(repoRoot, "BATCH_LOG.md"), "utf8");
  const logIds = [...batchLog.matchAll(/^\| \[([A-Z])(\d+)\]/gm)]
    .map((match) => `${match[1]}${Number(match[2])}`);
  if (JSON.stringify(logIds) !== JSON.stringify(closedIds)) {
    errors.push("BATCH_LOG.md must index A1-B2 exactly once and in order");
  }
  if (/^\| \[B3\]/m.test(batchLog)) errors.push("BATCH_LOG.md must not contain a B3 record yet");

  const threads = readFileSync(join(repoRoot, "Batches", "THREADS.md"), "utf8");
  const declaredThreads = new Set([...threads.matchAll(/<a id="t-(\d{3})"><\/a>T-(\d{3})/g)].map((match) => `T-${match[1]}`));
  const declaredFamilies = new Set([...threads.matchAll(/^## TF-(\d{2}) /gm)].map((match) => `TF-${match[1]}`));
  if (declaredThreads.size !== 30) errors.push(`expected 30 series-neutral threads, found ${declaredThreads.size}`);
  if (declaredFamilies.size !== 12) errors.push(`expected 12 series-neutral thread families, found ${declaredFamilies.size}`);
  for (const unit of VERSION_UNITS) {
    for (const thread of unit.threads) {
      if (!declaredThreads.has(thread)) errors.push(`${unit.id} references unknown thread ${thread}`);
    }
  }
  const crosswalk = readFileSync(join(repoRoot, "Batches", "THREAD_ID_CROSSWALK.md"), "utf8");
  for (let index = 1; index <= 12; index += 1) {
    const suffix = String(index).padStart(2, "0");
    if (!crosswalk.includes(`| \`ATF-${suffix}\` | \`TF-${suffix}\` |`)) {
      errors.push(`thematic crosswalk is missing ATF-${suffix} -> TF-${suffix}`);
    }
  }
  for (let index = 1; index <= 30; index += 1) {
    const suffix = String(index).padStart(3, "0");
    if (!crosswalk.includes(`| \`AT-${suffix}\` | \`T-${suffix}\` |`)) {
      errors.push(`thematic crosswalk is missing AT-${suffix} -> T-${suffix}`);
    }
  }

  for (const relative of markdownFilesForLegacyIdScan(repoRoot)) {
    const text = readFileSync(join(repoRoot, relative), "utf8");
    if (/\bATF?-\d+\b/.test(text) || /#at-\d+/.test(text) || /id="at-\d+/.test(text)) {
      errors.push(`${relative} still uses temporary A-series thematic identifiers`);
    }
  }

  const versionFile = readFileSync(join(repoRoot, "VERSION"), "utf8").trim();
  if (versionFile !== replay.currentVersion) {
    errors.push(`VERSION is ${versionFile}; replay derives ${replay.currentVersion}`);
  }
  const versionMapPath = join(repoRoot, "VERSION_MAP.md");
  if (!existsSync(versionMapPath) || readFileSync(versionMapPath, "utf8") !== renderVersionMap()) {
    errors.push("VERSION_MAP.md is stale; run node tools/version-model.mjs --write");
  }
  for (const [relative, render] of Object.entries(STAMP_TARGETS)) {
    const text = readFileSync(join(repoRoot, relative), "utf8");
    try {
      const expectedText = replaceGeneratedRegion(text, render(), relative);
      if (text !== expectedText) errors.push(`${relative} has a stale generated version stamp`);
    } catch (error) {
      errors.push(error.message);
    }
  }

  return {
    ok: errors.length === 0,
    schema: VERSION_MODEL_SCHEMA,
    currentVersion: replay.currentVersion,
    unitCount: VERSION_UNITS.length,
    closedBatchCount: closedIds.length,
    nextBatch: NEXT_BATCH,
    errors,
  };
}

export function writeGenerated(repoRoot = DEFAULT_REPO_ROOT) {
  writeFileSync(join(repoRoot, "VERSION"), `${CURRENT_VERSION}\n`, "utf8");
  writeFileSync(join(repoRoot, "VERSION_MAP.md"), renderVersionMap(), "utf8");
  for (const [relative, render] of Object.entries(STAMP_TARGETS)) {
    const path = join(repoRoot, relative);
    const current = readFileSync(path, "utf8");
    writeFileSync(path, replaceGeneratedRegion(current, render(), relative), "utf8");
  }
}

function printUsage() {
  console.log("Usage: node tools/version-model.mjs [--check|--write|--json]");
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = new Set(process.argv.slice(2));
  if (args.has("--help")) {
    printUsage();
    process.exit(0);
  }
  if (args.has("--write")) writeGenerated(DEFAULT_REPO_ROOT);
  const result = validateRepository(DEFAULT_REPO_ROOT);
  if (args.has("--json")) console.log(JSON.stringify(result, null, 2));
  else if (result.ok) console.log(`version-model: OK — ${result.currentVersion}; ${result.unitCount} units cover A1-B2; ${result.nextBatch} remains next`);
  else result.errors.forEach((error) => console.error(`version-model: ${error}`));
  process.exit(result.ok ? 0 : 1);
}
