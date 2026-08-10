using System.Text;
using System.Xml.Linq;

public static class Program
{
    private static int checks;

    private static int Main(string[] args)
    {
        try
        {
            if ((args.Length == 4 || args.Length == 5)
                && args[0] == "--convert")
            {
                ConvertFixture(Path.GetFullPath(args[1]),
                    args.Length == 5 ? Path.GetFullPath(args[2]) : null,
                    Path.GetFullPath(args[args.Length - 2]),
                    Path.GetFullPath(args[args.Length - 1]));
                return 0;
            }
            if (args.Length != 3)
                throw new ArgumentException(
                    "usage: PlayerFoundingReceipts <repo> <mirror> <keyed> "
                    + "or --convert <source> [policy-evidence] <mirror> "
                    + "<keyed>");
            string repository = Path.GetFullPath(args[0]);
            string mirrorPath = Path.GetFullPath(args[1]);
            string keyedPath = Path.GetFullPath(args[2]);
            var report = new StringBuilder();
            report.AppendLine("PLAYER FOUNDING AUTHORING RECEIPT");

            CheckSourceContracts(repository, report);
            XDocument mirror = XDocument.Load(mirrorPath,
                LoadOptions.PreserveWhitespace);
            XDocument keyed = XDocument.Load(keyedPath,
                LoadOptions.PreserveWhitespace);
            CheckFixture("mirror", mirror, report);
            CheckFixture("keyed", keyed, report);

            Require(Fingerprint(mirror) == Fingerprint(keyed),
                "active and keyed fixture plans disagree");
            report.AppendLine("PASS active surfaces -> one composition and "
                + "one founding plan");

            CheckRoundTrip(mirror, report);
            CheckRoundTrip(keyed, report);
            report.AppendLine("result: PASS (" + checks + " assertions)");
            Console.Write(report.ToString());
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("result: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ConvertFixture(string sourcePath,
        string policyEvidencePath, string mirrorPath, string keyedPath)
    {
        XDocument document = XDocument.Load(sourcePath,
            LoadOptions.PreserveWhitespace);
        XElement plan = Plan(document);
        if (policyEvidencePath != null)
        {
            XDocument evidence = XDocument.Load(policyEvidencePath,
                LoadOptions.PreserveWhitespace);
            MergeMissingPolicy(plan, Plan(evidence));
        }
        SetValue(plan, "schemaVersion", "3");
        plan.Element("foundingArrangement")?.Remove();
        plan.Element("foundingArrangementAuthored")?.Remove();
        if (plan.Element("playerFounding") == null)
        {
            var player = new XElement("playerFounding",
                new XElement("culture"),
                new XElement("politicalBeliefs",
                    new XElement("positions")),
                new XElement("arrangement",
                    new XAttribute("IsNull", "True")),
                new XElement("arrangementSource", "0"),
                new XElement("nativeIdeoId", "-1"),
                new XElement("nativeIdeoName"),
                new XElement("nativeIdeoSignature"),
                new XElement("nativeIdeoNotified", "False"),
                new XElement("confirmed", "False"));
            XElement anchor = plan.Element("operatorAuthored");
            if (anchor == null) plan.Add(player);
            else anchor.AddBeforeSelf(player);
        }

        string rendered = (document.Declaration == null ? ""
                : document.Declaration + Environment.NewLine)
            + document.ToString();
        WriteExact(mirrorPath, rendered);
        WriteExact(keyedPath, rendered);
        Console.WriteLine("converted fixture -> schema 3 player founding; "
            + "active surfaces written identically");
    }

    private static void MergeMissingPolicy(XElement targetPlan,
        XElement evidencePlan)
    {
        XElement target = targetPlan.Element("worldPolicy");
        XElement evidence = evidencePlan.Element("worldPolicy");
        if (target == null || evidence == null) return;
        foreach (XElement field in evidence.Elements())
            if (target.Element(field.Name) == null)
                target.Add(new XElement(field));
    }

    private static void SetValue(XElement parent, string name,
        string value)
    {
        XElement element = parent.Element(name);
        if (element == null) parent.AddFirst(new XElement(name, value));
        else element.Value = value;
    }

    private static void WriteExact(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("fixture path has no directory"));
        string temporary = path + ".b2.tmp";
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }

    private static void CheckSourceContracts(string repository,
        StringBuilder report)
    {
        string state = Read(repository, "Source",
            "PlayerFoundingStateModule.cs");
        string page = Read(repository, "Source",
            "PlayerFoundingPageModule.cs");
        string setup = Read(repository, "Source", "RegionalSetupModule.cs");
        string faction = Read(repository, "Source", "FactionStateModule.cs");
        string founding = Read(repository, "Source",
            "FoundingArrangementModule.cs");
        string political = Read(repository, "Source",
            "PoliticalBeliefPracticeModule.cs");
        string organizations = Read(repository, "Source",
            "OrganizationModule.cs");

        Require(state.Contains("class CAPlayerFoundingPlan"),
            "player founding state model is missing");
        Require(state.Contains("Scribe_Deep.Look(ref culture, \"culture\")")
            && state.Contains("Scribe_Deep.Look(ref politicalBeliefs, "
                + "\"politicalBeliefs\")")
            && state.Contains("Scribe_Deep.Look(ref arrangement, "
                + "\"arrangement\")"),
            "founding culture, beliefs, and arrangement are not serialized");
        Require(state.Contains("nativeIdeoId")
            && state.Contains("nativeIdeoName")
            && state.Contains("nativeIdeoSignature")
            && state.Contains("nativeIdeoNotified"),
            "native Ideoligion receipt is not persisted");
        Require(state.Contains("NativeIdeoSignature")
            && state.Contains("currentCacheId")
            && page.Contains("CaptureNativeIdeo(draft, ideo, null)"),
            "native Ideoligion receipt does not follow same-ID editor changes");
        Require(state.Contains("TryValidateNativeIdeo")
            && state.Contains("FirstIncompatiblePreceptPair")
            && state.Contains("FirstRitualMissingTarget")
            && state.Contains("FirstConsumableBuildingMissingRitual"),
            "founding Continue does not enforce native Ideoligion validity");
        Require(state.Contains("class CAPlayerFoundingWorldComponent")
            && state.Contains("CA_playerFoundingAppliedAtTick")
            && state.Contains("internal void MarkApplied"),
            "world-owned one-shot founding receipt is incomplete");
        Require(state.Contains("TotalPawnCount")
            && state.Contains("ScenPart_ConfigPage_ConfigureStartingPawnsBase")
            && state.Contains("configured > 0 ? configured : 3"),
            "suggested arrangements do not read the scenario pawn total");
        Require(page.Contains("nameof(Scenario.GetFirstConfigPage)")
            && page.Contains("current is Page_ChooseIdeoPreset")
            && page.Contains("new Page_CAPlayerFounding"),
            "CA founding page does not replace vanilla preset flow");
        foreach (string label in new[]
        {
            "Culture", "Ideoligion", "Political beliefs",
            "Founding terms"
        })
            Require(page.Contains("\"" + label + "\""),
                label + " is absent from the founding page");
        Require(page.Contains("Page_CAConfigureFoundingIdeo")
            && page.Contains("Page_CAConfigureFoundingFluidIdeo")
            && page.Contains("Dialog_IdeoList_Load"),
            "native fixed, fluid, and load Ideoligion paths are incomplete");
        Require(page.Contains("CAPlayerFoundingSession.Confirm(draft)")
            && page.Contains("ApplyCarriedState(draft")
            && page.Contains("!draft.nativeIdeoNotified"),
            "page completion does not confirm one native-Ideo-aware draft");
        Require(page.Contains("Settlement leadership")
            && page.Contains("Who may give binding orders.")
            && page.Contains("Required work")
            && page.Contains("Whether work and defense may be assigned.")
            && page.Contains("First decisions")
            && page.Contains("Who votes on decisions from the first day.")
            && page.Contains("Starting supplies")
            && page.Contains("Whether provisions are pooled and rationed."),
            "the four founding-term causes are incomplete");
        Require(setup.Contains("CurrentSchemaVersion = 3")
            && setup.Contains("Scribe_Deep.Look(ref playerFounding, "
                + "\"playerFounding\")"),
            "regional plan does not own schema-3 founding state");
        Require(!setup.Contains("CAPlayerFoundingModel.ApplyCarriedState")
            && !setup.Contains("CAPlayerFoundingModel.ApplyToPlayer"),
            "regional resolution still owns or replays player founding");
        Require(faction.Contains("if (!faction.IsPlayer)")
            && faction.Contains("CAFactionStructureModel.GenerateUnset"),
            "player faction is not protected from mature structure generation");
        Require(founding.Contains("foundingOwner?.Applied == true")
            && founding.Contains("ConfirmedForRuntime()")
            && founding.Contains("foundingOwner?.MarkApplied(now)")
            && !founding.Contains("drafted?.foundingArrangement"),
            "runtime founding is not guarded by the durable one-shot owner");
        Require(!state.Contains("SeedStartingOrder")
            && state.Contains("records realized institutions")
            && !founding.Contains("ReconcileCustoms(colony"),
            "the founding arrangement is still promoted into mature order");
        Require(political.Contains("PoliticalStandards")
            && political.Contains("ReconcileCurrentStructure")
            && political.Contains("const string source = \"social order\"")
            && !organizations.Contains("ReconcileCustoms"),
            "belief standards and adopted organizational practice remain "
                + "conflated");
        report.AppendLine("PASS source path -> authoring, persistence, native "
            + "Ideoligion, one-shot materialization, belief/practice "
            + "separation, runtime consumption");
    }

    private static void CheckFixture(string label, XDocument document,
        StringBuilder report)
    {
        XElement plan = Plan(document);
        Require(Value(plan, "schemaVersion") == "3",
            label + " fixture is not schema 3");
        Require(Items(plan, "factions").Count == 3,
            label + " fixture does not contain 3 factions");
        Require(Items(plan, "settlements").Count == 4,
            label + " fixture does not contain 4 settlements");
        int groups = Items(plan, "settlements")
            .Sum(settlement => Items(settlement, "populationGroups").Count);
        Require(groups == 9,
            label + " fixture does not contain 9 population groups");
        XElement player = plan.Element("playerFounding");
        Require(player != null, label + " fixture has no player founding plan");
        Require(player.Element("culture") != null
            && player.Element("politicalBeliefs") != null
            && player.Element("arrangement") != null,
            label + " founding plan is structurally incomplete");
        Require(player.Element("politicalBeliefs")?.Element("positions")
                != null,
            label + " political beliefs have no serialized axis collection");
        // RimWorld's Scribe omits values that equal their declared defaults.
        // Absence of a false boolean, an unset source, or nativeIdeoId -1 is
        // therefore the serialized default rather than missing schema.
        bool confirmed = string.Equals(Value(player, "confirmed"), "True",
            StringComparison.OrdinalIgnoreCase);
        XElement arrangement = player.Element("arrangement");
        bool arrangementNull = string.Equals(
            (string)arrangement?.Attribute("IsNull"), "True",
            StringComparison.OrdinalIgnoreCase);
        if (confirmed)
        {
            Require(!string.IsNullOrEmpty(Value(player.Element("culture"),
                    "id"))
                && !string.IsNullOrEmpty(Value(player.Element("culture"),
                    "gatheringKey")),
                label + " confirmed founding culture is incomplete");
            List<XElement> positions = Items(
                player.Element("politicalBeliefs"), "positions");
            string[] requiredAxes =
            {
                "leadership", "decisions", "participation", "dissent",
                "ownership", "economy", "work", "support", "membership",
                "status", "localOrder", "defense", "warConduct"
            };
            Require(positions.Count == requiredAxes.Length
                && positions.Select(item => Value(item, "axis"))
                    .Distinct(StringComparer.Ordinal).Count()
                    == requiredAxes.Length
                && requiredAxes.All(axis => positions.Any(item =>
                    Value(item, "axis") == axis
                    && !string.IsNullOrEmpty(Value(item, "option"))
                    && Value(item, "source") != "0")),
                label + " confirmed political beliefs do not set every "
                    + "canonical axis");
            Require(!arrangementNull
                && Value(player, "arrangementSource") != "0"
                && !string.IsNullOrEmpty(Value(arrangement, "id")),
                label + " confirmed arrangement is invalid");
        }
        else
        {
            string arrangementSource = Value(player, "arrangementSource");
            string nativeIdeoId = Value(player, "nativeIdeoId");
            bool pristine = arrangementNull
                && (arrangementSource.Length == 0
                    || arrangementSource == "0")
                && (nativeIdeoId.Length == 0 || nativeIdeoId == "-1");
            if (!pristine)
            {
                Require(arrangementNull
                        || (arrangementSource.Length > 0
                            && arrangementSource != "0"
                            && !string.IsNullOrEmpty(
                                Value(arrangement, "id"))),
                    label + " authored founding terms lost provenance");
                if (nativeIdeoId.Length > 0 && nativeIdeoId != "-1")
                    Require(!string.IsNullOrEmpty(
                                Value(player, "nativeIdeoName"))
                            && !string.IsNullOrEmpty(
                                Value(player, "nativeIdeoSignature")),
                        label + " authored Ideoligion lost its receipt");
            }
        }
        Require(plan.Element("foundingArrangement") == null
            && plan.Element("foundingArrangementAuthored") == null,
            label + " fixture retains obsolete root founding fields");
        report.AppendLine("PASS " + label
            + " -> schema 3; 3 factions; 4 settlements; 9 population groups; "
            + (confirmed ? "confirmed founding state"
                : arrangementNull ? "unconfirmed founding authoring state"
                : "unconfirmed authored founding draft"));
    }

    private static void CheckRoundTrip(XDocument original,
        StringBuilder report)
    {
        string before = Fingerprint(original);
        string serialized = original.ToString(SaveOptions.DisableFormatting);
        XDocument readback = XDocument.Parse(serialized,
            LoadOptions.PreserveWhitespace);
        Require(before == Fingerprint(readback),
            "fixture changed across XML serialization and readback");
        report.AppendLine("PASS XML serialization -> composition, relations, "
            + "population assignments, and founding state survive readback");
    }

    private static string Fingerprint(XDocument document)
    {
        XElement plan = Plan(document);
        var fields = new List<string>
        {
            Value(plan, "schemaVersion"), Value(plan, "regionalId"),
            Value(plan, "candidateId"), Value(plan, "startTileId"),
            Value(plan, "mapSize")
        };
        foreach (XElement faction in Items(plan, "factions"))
            fields.Add("F:" + Value(faction, "key") + ":"
                + Value(faction, "existingFactionLoadId") + ":"
                + Value(faction, "customFactionDefName") + ":"
                + Value(faction, "customName"));
        foreach (XElement settlement in Items(plan, "settlements"))
        {
            fields.Add("S:" + Value(settlement, "slot") + ":"
                + Value(settlement, "memberTileId") + ":"
                + Value(settlement, "factionKey") + ":"
                + Value(settlement, "residentPopulation"));
            foreach (XElement group in Items(settlement, "populationGroups"))
                fields.Add("P:" + Value(settlement, "slot") + ":"
                    + Value(group, "key") + ":" + Value(group, "share")
                    + ":" + Value(group, "factionKey") + ":"
                    + Value(group, "kind"));
        }
        foreach (XElement relation in Items(plan, "relations"))
            fields.Add("R:" + Value(relation, "leftFactionKey") + ":"
                + Value(relation, "rightFactionKey") + ":"
                + Value(relation, "relation"));
        XElement player = plan.Element("playerFounding");
        if (player != null)
            fields.Add("PF:" + Canonical(player));
        return string.Join("|", fields);
    }

    private static string Canonical(XElement element)
    {
        return element.Name.LocalName + "["
            + string.Join(";", element.Attributes()
                .OrderBy(attribute => attribute.Name.LocalName)
                .Select(attribute => attribute.Name.LocalName + "="
                    + attribute.Value)) + "]{"
            + string.Join("", element.Elements().Select(Canonical)) + "}="
            + (element.HasElements ? "" : element.Value.Trim());
    }

    private static XElement Plan(XDocument document)
    {
        return document.Root?.Element("plan")
            ?? throw new InvalidDataException("fixture has no plan element");
    }

    private static List<XElement> Items(XElement parent, string collection)
    {
        return parent.Element(collection)?.Elements("li").ToList()
            ?? new List<XElement>();
    }

    private static string Value(XElement parent, string name)
    {
        return (string)parent?.Element(name) ?? "";
    }

    private static string Read(string root, params string[] segments)
    {
        return File.ReadAllText(segments.Aggregate(root, Path.Combine));
    }

    private static void Require(bool condition, string failure)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(failure);
    }
}
