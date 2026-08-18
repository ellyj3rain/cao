# B15 Technological Knowledge Execution Receipt

Generated: 2026-08-18 13:47:54 UTC / 2026-08-18 06:47:54 -07:00

Assembly: `Assemblies/ColonistAwareness.dll`
SHA-256: `EF56D9BADAAF82DB5E8269A3996F2F56A565BC8E564DB9A970C97F5B72C94342`

| Check | Result |
|---|---|
| Canonical owner | PASS - `CAFactionState` owns Technological Knowledge beside Culture and Political Order |
| Society initialization | PASS - 22/22 built-in Society presets atomically apply and match all three faction components |
| Failed validation | PASS - an invalid technological component leaves all three targets byte-equivalent at the data level |
| Saved Society | PASS - Culture, Political Order, Technological Knowledge ranks, and exact research evidence survive Scribe serialization/readback and participate in matching; pawn custody is not copied |
| Standard mode | PASS - canonical faction rank 3 remains effective independently of pawn custody |
| Mode boundary | PASS - standard mode does not transfer canonical ranks through dormant pawn custody |
| Non-carrier loss | PASS - losing a pawn with no active custody records does not create a false knowledge revision |
| Initial realization | PASS - settlement form, wealth, construction era, morphology, and apertures use canonical faction knowledge before residents spawn |
| Distributed mode | PASS - availability is projected from the same canonical domains through living pawn, institution, and record custody |
| Local availability | PASS - a carrier contributes on its supplied map/settlement boundary and does not unlock another map |
| Redundancy | PASS - two carriers survive one loss |
| Isolated carrier | PASS - loss removes availability and later queries do not regenerate custody |
| Incapacity/recovery | PASS - the carrier-availability predicate removes and restores the same knowledge without rewriting it |
| Persistent retention | PASS - institutional and recorded custody survive Scribe readback |
| Placement versus realization | PASS - settlement placement checks whether the faction can establish required functions; confirmation still rejects absent programs and material functions |
| Base consumers | PASS - rank-zero knowledge cannot build, maintain, manufacture or use weapons, operate recipes, sow plants, or provide medical care; matching rank-one knowledge can |
| Supported migration | PASS - B14 faction owner, founding owner, regional plan, nested founding plan, and settlement receipt execute their B15 migration and validate |
| Current authored fixture | PASS - current regional/founding schemas Scribe-load with 3 factions, 4 settlements, all relations, and all four staged knowledge compositions intact |
| Settlement realization | PASS - settlements persist a faction knowledge identity/revision/tier receipt rather than a second knowledge owner |

Three-component fingerprints: `353EC12DCB8922C4:BE154D8C4AA172BF:F67C29B157E99988`, `E188CE62AF9FAE24:58E94AEC1CCB954C:549FD0520BBA7724`, `CAA6B4CCD418EA04:81E84A5ECA54D2EF:E6F01429F72E6AAB`, `8C232B6986E1D8CB:1A57E2DADD22A768:AA4380ED4E72B9F6`, `97DB3D639D0562CA:BE154D8C4AA172BF:562370EA5F88702C`, `D010604D50242282:303C91B636F3627C:63FAAAE5FCE4F155`, `A8E4855F70F0EAED:81E84A5ECA54D2EF:26541CB92380A6A6`, `3BE78BF1BDF2B14E:81E84A5ECA54D2EF:A7FD7D0806A7F96D`, `01144034DC6F4732:303C91B636F3627C:8AA37170EE981DD7`, `AB75105ECF96E956:5CD633E32EA5C773:0E31F1A409A8F2D9`, `DF30B6046565BAB7:4BAE4B466D2D0596:BA2EFCF48B46868F`, `DCBF5DBA1FAFF0C0:B818B374D2739294:2BC561DE2F75A793`, `751244CA48E5B055:5C2CA8CAD6EB8F29:CCD617B9559DB7E2`, `53CB600E47A1AF57:36F562EFB03DD3C4:11A891DECE2DBAF7`, `9762F44D7CCE12AA:4DAEFE0E8FF9CA30:3C0BF3B4BE0D4A8F`, `FDE997C27E821691:3242701C130176D5:04E8CE5E62B0FB2C`, `7B3B0DC041D002D2:5C1599B6ABF20DE4:09C17DA10CDED817`, `1556F82B62F72C92:7521AC75E48DC881:F4AF105F2223627B`, `C88C7C43E729A0EF:096F9DC8856B5C41:E4079A49F6CE1E90`, `AF93BFA6E23F2B7F:BD34104DED6727DF:082B331A5EEEFEF7`, `506ED965192918DD:0DCFE2E1477955B2:B7C8A25EC4143CFE`, `6FCD58C7C13F9907:FF264EBD18728B2C:9923D84AACDA33D4`

This executable receipt verifies causal data behavior. It does not claim operator visual or gameplay acceptance.
