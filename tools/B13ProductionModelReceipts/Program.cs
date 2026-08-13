using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ColonistAwareness;

internal static class Program
{
    private static readonly string Managed =
        @"C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed";

    private static int Main()
    {
        ResolveEventHandler resolver = (_, args) =>
        {
            string name = new AssemblyName(args.Name).Name;
            string path = name == "ColonistAwareness"
                ? Path.Combine(Environment.CurrentDirectory, "Assemblies",
                    "ColonistAwareness.dll")
                : name == "0Harmony"
                    ? Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile), ".nuget",
                    "packages", "lib.harmony", "2.4.2", "lib", "net472",
                    "0Harmony.dll")
                    : Path.Combine(Managed, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            return Run();
        }
        catch (Exception exception)
        {
            Exception cause = exception is TargetInvocationException target
                    && target.InnerException != null
                ? target.InnerException : exception;
            while (cause.InnerException != null)
                cause = cause.InnerException;
            Console.WriteLine(cause.GetType().Name + ": " + cause.Message
                + Environment.NewLine + cause.StackTrace);
            return 2;
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }
    }

    private static int Run()
    {
        var culture = new CACulture
        {
            schemaVersion = 0,
            questionRegistryVersion = 0,
            withinGroupSpread = 99
        };
        var valid = new CACultureQuestionDistribution
        {
            questionKey = CACultureQuestionRegistry.SameSexAcceptance,
            populationScope = null,
            spread = 0.99f
        };
        culture.inheritedQuestions.Add(valid);
        culture.inheritedQuestions.Add(new CACultureQuestionDistribution
        {
            questionKey = "receipt.invalid-question"
        });
        Type model = typeof(CACulture).Assembly.GetType(
            "ColonistAwareness.CACultureModel", true);
        model.GetMethod("Normalize", BindingFlags.Static
                | BindingFlags.NonPublic)
            .Invoke(null, new object[] { culture });
        bool normalized = culture.schemaVersion
                == CACulture.CurrentSchemaVersion
            && culture.questionRegistryVersion
                == CACultureQuestionRegistry.CurrentVersion
            && culture.withinGroupSpread == 4
            && culture.inheritedQuestions.Count == 1
            && culture.inheritedQuestions.Single() == valid
            && valid.populationScope == "*"
            && Math.Abs(valid.spread - 0.60f) < 0.0001f;

        string[] unmappedSubjects = CASocialSubjectRegistry.Authorable()
            .Where(subject => CACulturalCognitionPureKernel
                .ExpertiseDomainsForSocialSubject(subject.Key).Count == 0)
            .Select(subject => subject.Key).ToArray();
        float gatheringExpertise = CACulturalCognitionPureKernel
            .DemonstratedExpertise(CASocialSubjectRegistry.PublicGathering,
                domain => domain
                        == CACulturalCognitionPureKernel.ExpertiseSocial
                    ? 0.85f : 0.05f);
        float orderExpertise = CACulturalCognitionPureKernel
            .DemonstratedExpertise(CASocialSubjectRegistry.EnforcedOrder,
                domain => domain
                        == CACulturalCognitionPureKernel.ExpertiseMelee
                    ? 0.90f : 0.10f);
        float unknownExpertise = CACulturalCognitionPureKernel
            .DemonstratedExpertise("integration.unregistered",
                _ => 1f);
        bool expertise = unmappedSubjects.Length == 0
            && Math.Abs(gatheringExpertise - 0.85f) < 0.0001f
            && Math.Abs(orderExpertise - 0.90f) < 0.0001f
            && Math.Abs(unknownExpertise - 0.50f) < 0.0001f;

        CARepresentedSourceAppraisal directSource =
            CACulturalCognitionPureKernel
                .RepresentedQuestionSourceAppraisal(0.15f, 0.80f, 6);
        CARepresentedMoralExperience directMoral =
            CACulturalCognitionPureKernel
                .RepresentedQuestionMoralExperience(-0.75f, 0.15f,
                    0.80f, 6);
        bool stableReceipt = CACulturalCognitionPureKernel
            .ReceivesRepresentedQuestionEvidence(42,
                "receipt:question-history", 0.55f)
            == CACulturalCognitionPureKernel
                .ReceivesRepresentedQuestionEvidence(42,
                    "receipt:question-history", 0.55f);
        bool directEvidence = directSource.Trust > 0.75f
            && directSource.Weight == 0.80f
            && directMoral.ApprovalMagnitude == 0.75f
            && directMoral.Salience == 0.80f && stableReceipt;

        bool passed = normalized && expertise && directEvidence;
        Console.WriteLine(passed
            ? "production DLL normalized Culture; mapped every built-in social subject to demonstrated expertise; and executed stable direct-question source and moral appraisals"
            : "production DLL receipt failed: normalize=" + normalized
                + "; expertise=" + expertise + "; directEvidence="
                + directEvidence + "; unmapped="
                + string.Join(",", unmappedSubjects));
        return passed ? 0 : 3;
    }
}
