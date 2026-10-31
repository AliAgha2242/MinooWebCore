using Microsoft.Extensions.DependencyInjection;

namespace MinooWebCore.Services.Wcf;

public static class WcfServiceCollectionExtensions
{
    public static IServiceCollection AddWcfClient(this IServiceCollection services)
    {
        services.AddSingleton<IWcfClient, WcfClient>();
        return services;
    }
}
