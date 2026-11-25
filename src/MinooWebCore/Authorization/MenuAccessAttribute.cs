using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using MinooWebCore.Infrastructure.Services.Contract;

namespace MinooWebCore.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class MenuAccessAttribute : TypeFilterAttribute
{
    public MenuAccessAttribute(string objectName)
        : base(typeof(MenuAccessFilter))
    {
        Arguments = new object[] { objectName };
    }
}

public sealed class MenuAccessFilter : IAsyncAuthorizationFilter
{
    private readonly string _objectName;
    private readonly IMenuAccessService _menuAccess;
    private readonly ILogger<MenuAccessFilter> _logger;

    public MenuAccessFilter(
        string objectName,
        IMenuAccessService menuAccess,
        ILogger<MenuAccessFilter> logger)
    {
        _objectName = objectName;
        _menuAccess = menuAccess;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var userCode = principal.FindFirstValue("UserCode");
        if (string.IsNullOrWhiteSpace(userCode))
        {
            _logger.LogWarning(
                "Menu access denied because the authenticated principal has no valid UserCode. Menu: {ObjectName}.",
                _objectName);

            context.Result = new ForbidResult();
            return;
        }

        var hasAccess = await _menuAccess.HasAccessAsync(
            userCode,
            _objectName,
            context.HttpContext.RequestAborted);

        if (!hasAccess)
            context.Result = new ForbidResult();
    }
}
