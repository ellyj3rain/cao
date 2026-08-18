# B14 Society Preset Execution Receipt

Generated: 2026-08-17 23:44:37 UTC / 2026-08-17 16:44:37 -07:00

Assembly: `C:\Users\jleyv\Peanut Butter\AI Assisted Software Engineering Mass Repository\Projects\colonist-awareness\.claude\worktrees\rimworld-regional-multithreading-47e9ec\Assemblies\ColonistAwareness.dll`

This executable reflection receipt loads the built assembly against RimWorld's managed references. It does not claim operator visual or gameplay acceptance.

| Check | Result |
|---|---|
| Catalog validation | PASS |
| Independent catalog identity | PASS - Society keys are distinct from Culture keys; a second Society identity can reuse one Culture reference |
| Complete owned composition | PASS - 22 presets each own and apply 24 Culture values and a frozen 26-question Political Order snapshot |
| Deep-copy match | PASS - every independently copied pair matches its applied preset |
| Mutation-negative match | PASS - changing a causal inherited-Culture field ends the derived Society match |
| Saved Society snapshot | PASS - the schema-1 profile survives Scribe serialization/readback and its copied Culture and Political Order apply together through the same atomic path |
| Component isolation | PASS - Culture-only and Political-Order-only substitutions leave the sibling component unchanged |
| Atomic rejection | PASS - `missing-culture-preset is not a Culture preset`; before/after state fingerprints agree |
| Replacement | PASS - applying a second Society preset replaced both components |
| Runtime ownership | PASS - canonical Culture and Political Order carry no Society-preset ownership field |

## Built-in catalog crosswalk

| Society key | Society name | Type | Region | Period | Culture reference | Applied Culture name | Political base | State hash |
|---|---|---|---|---|---|---|---|---|
| `society-mobile-kin` | Mobile kin society | Social forms |  |  | `mobile-kin-band` | Mobile kin culture | `communal-assembly` | `ACAA4226B065D684:BE154D8C4AA172BF` |
| `society-ranked-agrarian` | Ranked agrarian society | Social forms |  |  | `ranked-agrarian-households` | Ranked agrarian culture | `customary-landed` | `13835447B8EC06E9:58E94AEC1CCB954C` |
| `society-civic-market-town` | Civic market town | Social forms |  |  | `civic-market-town` | Civic market-town culture | `civic-market` | `B7DDA921055622EB:81E84A5ECA54D2EF` |
| `society-central-court` | Central court society | Social forms |  |  | `central-court-society` | Central court culture | `developmental-executive` | `040EAE21871CE8F8:1A57E2DADD22A768` |
| `society-frontier-mutual-aid` | Frontier mutual-aid settlement | Social forms |  |  | `frontier-mutual-aid` | Frontier mutual-aid culture | `communal-assembly` | `338E61A2752DB9B9:BE154D8C4AA172BF` |
| `society-industrial-civic` | Industrial civic association | Social forms |  |  | `industrial-civic-association` | Industrial civic culture | `progressive-civic-mix` | `4E4DB507E256E6CA:303C91B636F3627C` |
| `society-us-postwar` | Postwar United States | Historical societies | Americas | 1946-1964 | `united-states-postwar-mid-century` | Postwar American culture | `civic-market` | `2D016CF64FC04C1D:81E84A5ECA54D2EF` |
| `society-us-millennium` | Turn-of-the-millennium United States | Historical societies | Americas | 1995-2005 | `united-states-turn-millennium` | Turn-of-the-millennium American culture | `civic-market` | `3DAF6094BE274A4D:81E84A5ECA54D2EF` |
| `society-us-contemporary` | Contemporary United States | Historical societies | Americas | 2017-2024 | `united-states-contemporary` | Contemporary American culture | `progressive-civic-mix` | `B002C5EE553C61DA:303C91B636F3627C` |
| `society-english-colonies` | English North American colonies | Historical societies | Americas | 1607-1700 | `english-north-america-early-colonial` | Early English colonial culture | `customary-landed` | `081CC91C34A73D1B:5CD633E32EA5C773` |
| `society-civil-war-union` | Civil War Union | Historical societies | Americas | 1861-1865 | `united-states-civil-war-union` | Civil War Union culture | `civic-market` | `59E85D57D1114467:4BAE4B466D2D0596` |
| `society-confederate-states` | Confederate States | Historical societies | Americas | 1861-1865 | `confederate-slaveholding-dominant-culture` | Confederate slaveholding culture | `customary-landed` | `49C491A920E0A299:B818B374D2739294` |
| `society-freedpeople-emancipation` | Freedpeople during emancipation | Historical societies | Americas | 1863-1877 | `freedpeople-emancipation-communities` | Freedpeople emancipation culture | `progressive-civic-mix` | `780D41F84385EBFA:5C2CA8CAD6EB8F29` |
| `society-first-french-empire` | First French Empire | Historical societies | Europe | 1804-1815 | `france-napoleonic-empire` | Napoleonic French culture | `developmental-executive` | `EAE8ABFE3E43AC36:36F562EFB03DD3C4` |
| `society-second-french-empire` | Second French Empire | Historical societies | Europe | 1852-1870 | `france-second-empire` | Second-Empire French culture | `developmental-executive` | `711AD6A4C55844EA:4DAEFE0E8FF9CA30` |
| `society-weimar-republic` | Weimar Republic | Historical societies | Europe | 1919-1933 | `germany-weimar-republic` | Weimar German culture | `progressive-civic-mix` | `D9102B638EB34835:3242701C130176D5` |
| `society-nazi-germany` | Nazi Germany | Historical societies | Europe | 1933-1945 | `germany-national-socialist-dictatorship` | Nazi regime culture | `central-party` | `1E5A3EB09C5E3BA1:5C1599B6ABF20DE4` |
| `society-late-tokugawa-japan` | Late Tokugawa Japan | Historical societies | Asia | 1800-1867 | `japan-late-tokugawa` | Late Tokugawa Japanese culture | `customary-landed` | `C90E8C437D008E8B:7521AC75E48DC881` |
| `society-meiji-japan` | Meiji Japan | Historical societies | Asia | 1868-1912 | `japan-meiji-transformation` | Meiji Japanese culture | `developmental-executive` | `0A822909A0C878E2:096F9DC8856B5C41` |
| `society-late-qing-china` | Late Qing China | Historical societies | Asia | 1800-1911 | `qing-china-late-imperial` | Late Qing Chinese culture | `customary-landed` | `56A7A22E1B3678B1:BD34104DED6727DF` |
| `society-tanzimat-ottoman-empire` | Tanzimat Ottoman Empire | Historical societies | Asia | 1839-1876 | `ottoman-empire-tanzimat` | Tanzimat Ottoman culture | `developmental-executive` | `5A05C3FE760B5A49:0DCFE2E1477955B2` |
| `society-mughal-empire` | Mughal Empire | Historical societies | Asia | 1556-1605 | `mughal-india-akbar` | Akbar-era Mughal culture | `customary-landed` | `8775ABCED643EF51:FF264EBD18728B2C` |
