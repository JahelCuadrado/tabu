using Microsoft.Extensions.DependencyInjection;
using Tabu.Domain.Interfaces;
using Tabu.Infrastructure.Win32;

namespace Tabu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IWindowDetector, WindowDetector>();
        return services;
    }
}
