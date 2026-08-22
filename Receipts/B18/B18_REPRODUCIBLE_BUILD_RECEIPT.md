# B18 Reproducible Build Receipt

Timestamp: 2026-08-19 19:05 UTC / 12:05 PDT

| Fact | Value |
|---|---|
| SDK | .NET SDK 8.0.423, pinned by `global.json` (`rollForward: disable`), resolved with the shell working directory inside this worktree |
| Configuration | `Release`, portable SDK-style project (`Source/ColonistAwareness.csproj`), Krafs.Rimworld.Ref `[1.6.4871]`, Lib.Harmony `[2.4.2]` |
| Build A | `.tmp/b18-final2-a/ColonistAwareness.dll` |
| Build B | `.tmp/b18-final2-b/ColonistAwareness.dll` |
| Warnings / errors | 0 / 0 on both clean builds |
| Byte identity | Both builds are byte-identical |
| SHA-256 | `0F8B16C2C862372211BD52F4D62D477923D9263A570566619C96836BA365E50D` |
| Size | 4,525,056 bytes |

SDK resolution note: `global.json` discovery walks up from the shell's
working directory, not the project file; a build started from a working
directory outside this repository silently uses a different SDK and emits a
byte-different assembly. Every governed build in this batch was executed
with the working directory inside the worktree and the resolved SDK verified
as 8.0.423.
