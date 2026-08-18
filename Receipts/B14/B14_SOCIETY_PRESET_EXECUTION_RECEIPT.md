# B14 Society Preset Execution Receipt

Generated: 2026-08-18 13:47:52 UTC / 2026-08-18 06:47:52 -07:00

Assembly: `Assemblies/ColonistAwareness.dll`

This executable reflection receipt loads the built assembly against RimWorld's managed references. It does not claim operator visual or gameplay acceptance.

| Check | Result |
|---|---|
| Catalog validation | PASS |
| Independent catalog identity | PASS - Society keys are distinct from Culture keys; a second Society identity can reuse one Culture reference |
| Complete owned composition | PASS - 22 presets each own and apply 48 Culture values and a frozen 26-question Political Order snapshot |
| Deep-copy match | PASS - every independently copied pair matches its applied preset |
| Mutation-negative match | PASS - changing a causal inherited-Culture field ends the derived Society match |
| Saved Society snapshot | PASS - the schema-1 profile survives Scribe serialization/readback and its copied Culture and Political Order apply together through the same atomic path |
| Component isolation | PASS - Culture-only and Political-Order-only substitutions leave the sibling component unchanged |
| Atomic rejection | PASS - `missing-culture-preset is not a Culture preset`; before/after state fingerprints agree |
| Replacement | PASS - applying a second Society preset replaced all three components |
| Runtime ownership | PASS - canonical Culture and Political Order carry no Society-preset ownership field |

## Built-in catalog crosswalk

| Society key | Society name | Type | Region | Period | Culture reference | Applied Culture name | Political base | State hash |
|---|---|---|---|---|---|---|---|---|
| `society-mobile-kin` | Mobile kin society | Social forms |  |  | `mobile-kin-band` | Mobile kin culture | `communal-assembly` | `E4E4D126D39D8B9B:BE154D8C4AA172BF:9D9099DFA9088776` |
| `society-ranked-agrarian` | Ranked agrarian society | Social forms |  |  | `ranked-agrarian-households` | Ranked agrarian culture | `customary-landed` | `7C4FB71C0FFC750C:58E94AEC1CCB954C:F9903A6CE747EE5C` |
| `society-civic-market-town` | Civic market town | Social forms |  |  | `civic-market-town` | Civic market-town culture | `civic-market` | `75234844C9253B81:81E84A5ECA54D2EF:B54B2D485DDBA226` |
| `society-central-court` | Central court society | Social forms |  |  | `central-court-society` | Central court culture | `developmental-executive` | `DB3B05F8FFFA0B04:1A57E2DADD22A768:1913A89D1E1498D0` |
| `society-frontier-mutual-aid` | Frontier mutual-aid settlement | Social forms |  |  | `frontier-mutual-aid` | Frontier mutual-aid culture | `communal-assembly` | `F9F3BCAD352D5273:BE154D8C4AA172BF:46D66057C5305FFB` |
| `society-industrial-civic` | Industrial civic association | Social forms |  |  | `industrial-civic-association` | Industrial civic culture | `progressive-civic-mix` | `9D189A182D2E8E48:303C91B636F3627C:E3905A1C6735428E` |
| `society-us-postwar` | Postwar United States | Historical societies | Americas | 1946-1964 | `united-states-postwar-mid-century` | Postwar American culture | `civic-market` | `5C65FC28E2298E09:81E84A5ECA54D2EF:F47E5F6C23404690` |
| `society-us-millennium` | Turn-of-the-millennium United States | Historical societies | Americas | 1995-2005 | `united-states-turn-millennium` | Turn-of-the-millennium American culture | `civic-market` | `6D873075FECD1A23:81E84A5ECA54D2EF:D4AA5D8454DF598B` |
| `society-us-contemporary` | Contemporary United States | Historical societies | Americas | 2017-2024 | `united-states-contemporary` | Contemporary American culture | `progressive-civic-mix` | `DAF25F5189085E04:303C91B636F3627C:B049D4F43145B76D` |
| `society-english-colonies` | English North American colonies | Historical societies | Americas | 1607-1700 | `english-north-america-early-colonial` | Early English colonial culture | `customary-landed` | `C8995F0DD4AB25A4:5CD633E32EA5C773:F51712B55228666E` |
| `society-civil-war-union` | Civil War Union | Historical societies | Americas | 1861-1865 | `united-states-civil-war-union` | Civil War Union culture | `civic-market` | `0A8196C9EF9AAEBD:4BAE4B466D2D0596:9841EE24F5A26C80` |
| `society-confederate-states` | Confederate States | Historical societies | Americas | 1861-1865 | `confederate-slaveholding-dominant-culture` | Confederate slaveholding culture | `customary-landed` | `39F6CE4CA82A37F4:B818B374D2739294:53C68CA36D975ABA` |
| `society-freedpeople-emancipation` | Freedpeople during emancipation | Historical societies | Americas | 1863-1877 | `freedpeople-emancipation-communities` | Freedpeople emancipation culture | `progressive-civic-mix` | `AEA902D980CC1801:5C2CA8CAD6EB8F29:CCA0477D6C26DC42` |
| `society-first-french-empire` | First French Empire | Historical societies | Europe | 1804-1815 | `france-napoleonic-empire` | Napoleonic French culture | `developmental-executive` | `A3E212F826A485EC:36F562EFB03DD3C4:C36363C53739C79F` |
| `society-second-french-empire` | Second French Empire | Historical societies | Europe | 1852-1870 | `france-second-empire` | Second-Empire French culture | `developmental-executive` | `2D64A7412E94BA44:4DAEFE0E8FF9CA30:9DE8E57B59BD90A7` |
| `society-weimar-republic` | Weimar Republic | Historical societies | Europe | 1919-1933 | `germany-weimar-republic` | Weimar German culture | `progressive-civic-mix` | `09A8B1D1D7E200E9:3242701C130176D5:47C18306F901D35D` |
| `society-nazi-germany` | Nazi Germany | Historical societies | Europe | 1933-1945 | `germany-national-socialist-dictatorship` | Nazi regime culture | `central-party` | `7D48CD9D50C3F32B:5C1599B6ABF20DE4:D963E42F0A447643` |
| `society-late-tokugawa-japan` | Late Tokugawa Japan | Historical societies | Asia | 1800-1867 | `japan-late-tokugawa` | Late Tokugawa Japanese culture | `customary-landed` | `B0D0BC07ECA3940C:7521AC75E48DC881:317B3EB48C5A7DED` |
| `society-meiji-japan` | Meiji Japan | Historical societies | Asia | 1868-1912 | `japan-meiji-transformation` | Meiji Japanese culture | `developmental-executive` | `4CB1A144A24FB17C:096F9DC8856B5C41:F9AC1C8AA29F76D3` |
| `society-late-qing-china` | Late Qing China | Historical societies | Asia | 1800-1911 | `qing-china-late-imperial` | Late Qing Chinese culture | `customary-landed` | `51E0700EFD488ECE:BD34104DED6727DF:C2655CC7C9875E73` |
| `society-tanzimat-ottoman-empire` | Tanzimat Ottoman Empire | Historical societies | Asia | 1839-1876 | `ottoman-empire-tanzimat` | Tanzimat Ottoman culture | `developmental-executive` | `6333301CA42FBC87:0DCFE2E1477955B2:E60D3A6871E017E5` |
| `society-mughal-empire` | Mughal Empire | Historical societies | Asia | 1556-1605 | `mughal-india-akbar` | Akbar-era Mughal culture | `customary-landed` | `C01D9F64B298E8D1:FF264EBD18728B2C:19B5D25CEA3BF747` |
