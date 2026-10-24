using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

public sealed class MoodleOptions
{
    public string? BaseUrl { get; set; }
    public string? Token { get; set; }
}

public sealed class MoodleWebServiceClient(HttpClient httpClient, IOptions<MoodleOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly MoodleOptions _options = options.Value;

    public Task<IReadOnlyList<MoodleCourse>> GetCoursesAsync(CancellationToken cancellationToken) =>
        CallAsync<IReadOnlyList<MoodleCourse>>("core_course_get_courses", [], cancellationToken);

    public Task<IReadOnlyList<MoodleParticipant>> GetEnrolledUsersAsync(int courseId, CancellationToken cancellationToken) =>
        CallAsync<IReadOnlyList<MoodleParticipant>>(
            "core_enrol_get_enrolled_users",
            [
                new("courseid", courseId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("options[0][name]", "onlyactive"),
                new("options[0][value]", "1"),
                new("options[1][name]", "userfields"),
                new("options[1][value]", "id,firstname,lastname")
            ],
            cancellationToken);

    private async Task<T> CallAsync<T>(string function, IEnumerable<KeyValuePair<string, string>> parameters, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new MoodleServiceException("not_configured", HttpStatusCode.ServiceUnavailable);
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri is null ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && !IsLoopbackHttp(baseUri)))
        {
            throw new MoodleServiceException("invalid_configuration", HttpStatusCode.ServiceUnavailable);
        }

        var endpoint = new Uri($"{_options.BaseUrl.TrimEnd('/')}/webservice/rest/server.php");
        var formFields = new List<KeyValuePair<string, string>>
        {
            new("wstoken", _options.Token),
            new("wsfunction", function),
            new("moodlewsrestformat", "json")
        };
        formFields.AddRange(parameters);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(formFields)
        };
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new MoodleServiceException("http_error", response.StatusCode);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("exception", out _))
            {
                var errorCode = document.RootElement.TryGetProperty("errorcode", out var errorCodeElement)
                    ? errorCodeElement.GetString() ?? "webservice_error"
                    : "webservice_error";
                throw new MoodleServiceException(errorCode, HttpStatusCode.BadGateway);
            }

            return document.RootElement.Deserialize<T>(JsonOptions)
                ?? throw new MoodleServiceException("empty_response", HttpStatusCode.BadGateway);
        }
        catch (JsonException)
        {
            throw new MoodleServiceException("invalid_response", HttpStatusCode.BadGateway);
        }
    }

    private static bool IsLoopbackHttp(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new MoodleBooleanConverter());
        return options;
    }
}

public sealed class MoodleBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Number:
                return reader.GetInt32() != 0;
            case JsonTokenType.String:
                var value = reader.GetString();
                if (bool.TryParse(value, out var boolValue))
                {
                    return boolValue;
                }

                if (int.TryParse(value, out var intValue))
                {
                    return intValue != 0;
                }

                break;
        }

        throw new JsonException("Unsupported Moodle boolean value.");
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
}

public sealed class MoodleServiceException(string errorCode, HttpStatusCode upstreamStatus) : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
    public HttpStatusCode UpstreamStatus { get; } = upstreamStatus;
}

public sealed class MoodleCourse
{
    public int Id { get; init; }
    public string ShortName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public long? StartDate { get; init; }
    public long? EndDate { get; init; }
    public bool Visible { get; init; }
}

public sealed class MoodleParticipant
{
    public int Id { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? FullName { get; init; }
    public List<MoodleRole>? Roles { get; init; } = [];
}

public sealed class MoodleRole
{
    public string? ShortName { get; init; }
    public string? Name { get; init; }
}

public sealed record CourseResponse(
    int Id,
    string ShortName,
    string FullName,
    string? Summary,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? EndAtUtc,
    bool Visible);

public sealed record CourseParticipantResponse(
    int Id,
    string FullName,
    IReadOnlyList<string> Roles);

public static class MoodleResponseMapper
{
    public static CourseResponse Map(MoodleCourse course) => new(
        course.Id,
        course.ShortName,
        course.FullName,
        course.Summary,
        ToDateTimeOffset(course.StartDate),
        ToDateTimeOffset(course.EndDate),
        course.Visible);

    public static CourseParticipantResponse Map(MoodleParticipant participant)
    {
        var fullName = !string.IsNullOrWhiteSpace(participant.FullName)
            ? participant.FullName
            : string.Join(" ", new[] { participant.FirstName, participant.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
        var roles = (participant.Roles ?? [])
            .Select(role => role.ShortName ?? role.Name)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CourseParticipantResponse(participant.Id, fullName, roles);
    }

    private static DateTimeOffset? ToDateTimeOffset(long? timestamp) =>
        timestamp.HasValue && timestamp.Value > 0
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp.Value)
            : null;
}
