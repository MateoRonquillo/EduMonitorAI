using Microsoft.AspNetCore.Authorization;

public sealed class CourseAccessRequirement : IAuthorizationRequirement
{
}

public sealed class CourseAccessHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<CourseAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CourseAccessRequirement requirement)
    {
        var courseId = httpContextAccessor.HttpContext?.Request.RouteValues["courseId"]?.ToString();
        var hasGrant = !string.IsNullOrWhiteSpace(courseId) && context.User.FindAll("course_access")
            .Any(claim => string.Equals(claim.Value, courseId, StringComparison.Ordinal));

        if (hasGrant)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
