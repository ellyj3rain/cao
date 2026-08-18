# B15 Technological Knowledge Execution Receipt

Generated: 2026-08-18 06:20:27 UTC / 2026-08-17 23:20:27 -07:00

Assembly: `C:\Users\jleyv\AppData\Local\Temp\cao-b15-final-builds-native-20260818-0619Z\build-1\ColonistAwareness.dll`
SHA-256: `DE6312FF9CF2F4B052AACDE82E93C113AF231D36494F93667266FB2D415F0764`

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

Three-component fingerprints: `2D7DDF2BCF9F59C3:BE154D8C4AA172BF:F67C29B157E99988`, `B2D30904A011D992:58E94AEC1CCB954C:549FD0520BBA7724`, `922723CA8250BEB7:81E84A5ECA54D2EF:E6F01429F72E6AAB`, `157030D5EF5C3D9E:1A57E2DADD22A768:AA4380ED4E72B9F6`, `A360CED5ED9B4F10:BE154D8C4AA172BF:562370EA5F88702C`, `C837406B01838B1C:303C91B636F3627C:63FAAAE5FCE4F155`, `C3DED6584B20B706:81E84A5ECA54D2EF:26541CB92380A6A6`, `CC27E0DF357A8CC9:81E84A5ECA54D2EF:A7FD7D0806A7F96D`, `5FEA0B0170E80333:303C91B636F3627C:8AA37170EE981DD7`, `B44F470EAD3FE87A:5CD633E32EA5C773:0E31F1A409A8F2D9`, `CCFCF1FB6BD009E6:4BAE4B466D2D0596:BA2EFCF48B46868F`, `73FD4234DBC58D67:B818B374D2739294:2BC561DE2F75A793`, `12AE8D4D9DE20749:5C2CA8CAD6EB8F29:CCD617B9559DB7E2`, `8D93B70197DCD1BF:36F562EFB03DD3C4:11A891DECE2DBAF7`, `F0F25B1834BA7221:4DAEFE0E8FF9CA30:3C0BF3B4BE0D4A8F`, `9E063CB11817FA7D:3242701C130176D5:04E8CE5E62B0FB2C`, `82F7D629C1AB338B:5C1599B6ABF20DE4:09C17DA10CDED817`, `9C15288835979F82:7521AC75E48DC881:F4AF105F2223627B`, `94979DF2D6DDF45B:096F9DC8856B5C41:E4079A49F6CE1E90`, `505BE616A43E9770:BD34104DED6727DF:082B331A5EEEFEF7`, `77F56817F69BCD88:0DCFE2E1477955B2:B7C8A25EC4143CFE`, `8FD33DD178F6F97E:FF264EBD18728B2C:9923D84AACDA33D4`

This executable receipt verifies causal data behavior. It does not claim operator visual or gameplay acceptance.
