using Application.Services.implementations;
using Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class ServiceProvider
{
    public static IServiceCollection ApplicationServiceProvider(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IDockerService, DockerService>();
        services.AddScoped(typeof(INodeService), typeof(NodeService));

        return services;
    }
}