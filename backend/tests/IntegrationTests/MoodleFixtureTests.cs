extern alias MoodleApi;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduMonitorAI.IntegrationTests;

public sealed class MoodleFixtureTests
{
    [Fact]
    public async Task CourseActivities_MapMoodleResponseToFixture()
    {
        const string moodleResponse = """
            [
              {"section":1,"name":"Unidad 1","modules":[{"id":9001,"name":"Examen Parcial 1","modname":"quiz","visible":1,"uservisible":true}]},
              {"section":2,"name":"Unidad 2","modules":[{"id":9002,"name":"Tarea Autónoma 1","modname":"assign","visible":true,"uservisible":1}]}
            ]
            """;
        var handler = new FixtureHandler(_ => moodleResponse);
        var client = CreateClient(handler);

        var sections = await client.GetCourseContentsAsync(50, CancellationToken.None);
        var actual = sections.SelectMany(section => MoodleApi::MoodleResponseMapper.Map(section)).ToArray();
        using var fixture = await ReadFixtureAsync("moodle_activities_mock.json");
        var expected = fixture.RootElement.GetProperty("activities").EnumerateArray().ToArray();

        Assert.Equal("core_course_get_contents", handler.Functions.Single());
        Assert.Equal(2, actual.Length);
        for (var index = 0; index < actual.Length; index++)
        {
            Assert.Equal(expected[index].GetProperty("id").GetInt32(), actual[index].Id);
            Assert.Equal(expected[index].GetProperty("section").GetInt32(), actual[index].Section);
            Assert.Equal(expected[index].GetProperty("sectionname").GetString(), actual[index].SectionName);
            Assert.Equal(expected[index].GetProperty("name").GetString(), actual[index].Name);
            Assert.Equal(expected[index].GetProperty("module").GetString(), actual[index].Module);
            Assert.Equal(expected[index].GetProperty("visible").GetBoolean(), actual[index].Visible);
            Assert.Equal(expected[index].GetProperty("uservisible").GetBoolean(), actual[index].UserVisible);
        }
    }

    [Fact]
    public async Task GradeItems_PreserveFixtureValuesIncludingZeroAndMissingGrades()
    {
        const string moodleResponse = """
            {"usergrades":[
              {"userid":201,"userfullname":"Estudiante Prueba 1","gradeitems":[
                {"id":7001,"itemname":"Examen Parcial 1","itemtype":"mod","itemmodule":"quiz","graderaw":8.5,"gradeformatted":"8.50","gradedatesubmitted":0,"gradedategraded":1767225600},
                {"id":7002,"itemname":"Tarea Autónoma 1","itemtype":"mod","itemmodule":"assign","graderaw":null,"gradeformatted":"-","gradedatesubmitted":1767312000,"gradedategraded":0}]},
              {"userid":202,"userfullname":"Estudiante Prueba 2","gradeitems":[
                {"id":7001,"itemname":"Examen Parcial 1","itemtype":"mod","itemmodule":"quiz","graderaw":null,"gradeformatted":"-","gradedatesubmitted":0,"gradedategraded":0}]},
              {"userid":203,"userfullname":"Estudiante Prueba 3","gradeitems":[
                {"id":7001,"itemname":"Examen Parcial 1","itemtype":"mod","itemmodule":"quiz","graderaw":0.0,"gradeformatted":"0.00","gradedatesubmitted":0,"gradedategraded":1767225600}]}
            ]}
            """;
        var handler = new FixtureHandler(_ => moodleResponse);
        var client = CreateClient(handler);

        var report = await client.GetGradeReportAsync(50, CancellationToken.None);
        var actual = report.UserGrades!.Select(MoodleApi::MoodleResponseMapper.Map).ToArray();
        using var fixture = await ReadFixtureAsync("moodle_grades_mock.json");
        var expectedStudents = fixture.RootElement.GetProperty("students").EnumerateArray().ToArray();

        Assert.Equal("gradereport_user_get_grade_items", handler.Functions.Single());
        Assert.Equal(expectedStudents.Length, actual.Length);
        for (var studentIndex = 0; studentIndex < actual.Length; studentIndex++)
        {
            var expectedStudent = expectedStudents[studentIndex];
            Assert.Equal(expectedStudent.GetProperty("userid").GetInt32(), actual[studentIndex].UserId);
            var expectedGrades = expectedStudent.GetProperty("grades").EnumerateArray().ToArray();
            Assert.Equal(expectedGrades.Length, actual[studentIndex].Grades.Count);
            for (var gradeIndex = 0; gradeIndex < actual[studentIndex].Grades.Count; gradeIndex++)
            {
                var expectedGrade = expectedGrades[gradeIndex];
                var actualGrade = actual[studentIndex].Grades[gradeIndex];
                var expectedRaw = expectedGrade.GetProperty("graderaw");
                if (expectedRaw.ValueKind == JsonValueKind.Null)
                {
                    Assert.Null(actualGrade.GradeRaw);
                }
                else
                {
                    Assert.Equal(expectedRaw.GetDouble(), actualGrade.GradeRaw);
                }

                Assert.Equal(expectedGrade.GetProperty("itemname").GetString(), actualGrade.ItemName);
                Assert.Equal(expectedGrade.GetProperty("status").GetString(), actualGrade.Status);
            }
        }

        Assert.Equal("evaluado", actual[0].Grades[0].Status);
        Assert.Equal("pendiente", actual[0].Grades[1].Status);
        Assert.Equal(0d, actual[2].Grades[0].GradeRaw);
        Assert.Equal("evaluado", actual[2].Grades[0].Status);
    }

    private static MoodleApi::MoodleWebServiceClient CreateClient(FixtureHandler handler) =>
        new(new HttpClient(handler), Options.Create(new MoodleApi::MoodleOptions
        {
            BaseUrl = "https://moodle.example.test",
            Token = "fixture-token"
        }));

    private static async Task<JsonDocument> ReadFixtureAsync(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        await using var stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream);
    }

    private sealed class FixtureHandler(Func<string, string> responseFactory) : HttpMessageHandler
    {
        public List<string> Functions { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            var functionField = form.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(field => field.Split('=', 2))
                .First(field => Uri.UnescapeDataString(field[0]) == "wsfunction");
            var function = Uri.UnescapeDataString(functionField[1]);
            Functions.Add(function);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseFactory(function), Encoding.UTF8, "application/json")
            };
        }
    }
}
