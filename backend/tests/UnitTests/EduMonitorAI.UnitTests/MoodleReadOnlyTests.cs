extern alias MoodleApi;

using System.Reflection;
using Xunit;

namespace EduMonitorAI.UnitTests;

public class MoodleReadOnlyTests
{
    [Fact]
    public void Cp510_MoodleClient_OnlyExposesReadOperations()
    {
        var operations = typeof(MoodleApi::MoodleWebServiceClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        Assert.NotEmpty(operations);
        Assert.All(operations, name => Assert.StartsWith("Get", name));
    }

    [Fact]
    public void Cp510_AcademicData_HasNoReferenceToTheMoodleClient()
    {
        var references = typeof(EduMonitorAI.AcademicData.Sync.GradeNormalizer).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name);

        Assert.DoesNotContain("MoodleIntegration.API", references);
    }
}
