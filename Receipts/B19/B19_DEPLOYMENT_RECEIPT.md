# B19 Deployment Receipt

| Fact | Value |
|---|---|
| Deployed assembly | `Assemblies/ColonistAwareness.dll` |
| SHA-256 | `63DF9C6E0A1C335C087C66D176B792C1C151EEBB765E093B1AEE8C79456AC06D` |
| Size | 4,804,096 bytes |
| Committed at | `ba39c62` (HEAD), tracked in git, working tree clean for this file |
| Built by | the prior pass (assembly written 2026-08-21 16:16 PDT, one minute before the final B19 commit) |
| Installed mod path | NTFS junction to this worktree (Mods/ColonistAwareness) |

The deployed assembly is the committed prior-pass build, present at the
junction-resolved loaded path and verified by SHA-256. It is not re-deployed in
this pass: a 9.0.316 build of current source differs from it, and re-deploying
a non-pinned-SDK build would replace a governed prior-pass assembly. Whether
the prior-pass assembly was built with the pinned 8.0.423 SDK is not
independently verifiable in this environment (8.0.423 absent); the deployment
receipt records the committed artifact's identity and presence, not a fresh
governed rebuild.
