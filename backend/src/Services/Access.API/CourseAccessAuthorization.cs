using Microsoft.AspNetCore.Authorization;

namespace EduMonitorAI.Access.Authorization;

public sealed class CourseAccessRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "CourseAccess";
}

public sealed class CourseAccessHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<CourseAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CourseAccessRequirement requirement)
    {
        var courseId = httpContextAccessor.HttpContext?.Request.RouteValues["courseId"]?.ToString();
        if (string.IsNullOrWhiteSpace(courseId))
        {
            return Task.CompletedTask;
        }

        var hasGrant = context.User.FindAll("course_access")
            .Any(claim => string.Equals(claim.Value, courseId, StringComparison.Ordinal));

        if (hasGrant)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
