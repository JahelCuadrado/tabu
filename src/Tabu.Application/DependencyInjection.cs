using Microsoft.Extensions.DependencyInjection;
using Tabu.Application.Services;

namespace Tabu.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<WindowSwitcher>();
        return services;
    }
}
