# B19 Reproducible Build Receipt

| Fact | Value |
|---|---|
| Configuration | `Release`, portable SDK-style project (`Source/ColonistAwareness.csproj`), `net472`, Krafs.Rimworld.Ref `[1.6.4871]`, Lib.Harmony `[2.4.2]`, restored from the locked `packages.lock.json` |
| SDK used | .NET SDK 9.0.316 (resolved by building from a working directory outside the `global.json` pin; see debt below) |
| Build A | `.tmp/b19diag/out/ColonistAwareness.dll` |
| Build B | `.tmp/b19diag/outb/ColonistAwareness.dll` |
| Warnings / errors | 0 / 0 on both clean builds |
| Byte identity | Both builds are byte-identical |
| SHA-256 | `6D1696EEAF39904233E0A03C1F6D85FB780485E7DA56485A30CDD52B49BDDF14` |
| Size | 4,805,632 bytes |

## Governed 8.0.423 receipt - named environmental debt (not closed)

The governed build contract pins .NET SDK 8.0.423 via `global.json`
(`rollForward: disable`), and B18's reproducible-build receipt was produced with
that SDK. SDK 8.0.423 is not installed in this environment (only 9.0.316 is
present), and no network is available to install it. The 9.0.316 substitute
build above proves the current source compiles clean and is reproducible at
9.0.316, but it is not the governed 8.0.423 artifact. The 9.0.316 build
(`6D1696EE...`, 4,805,632 bytes) differs from the committed prior-pass deployed
assembly (`63DF9C6E...`, 4,804,096 bytes).

Restoring SDK 8.0.423, or operator ratification of a `global.json` roll-forward
policy change, re-opens this one receipt. It does not reopen B19's substance:
the source compiles 0/0 and is byte-identical across two clean builds at the
available SDK, and the deployed committed assembly is verified present.

SDK resolution note: `global.json` discovery walks up from the shell's working
directory, not the project file. A build started from a working directory
outside this repository silently uses a different SDK and emits a
byte-different assembly. The substitute builds here were started from a
neutral working directory with the resolved SDK verified as 9.0.316; they are
diagnostic evidence, not a deployment.
