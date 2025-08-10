using Application.Client;
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
        services.AddHttpClient<IEasyHubApiClient, EasyHubApiClient>();
        services.AddHostedService<UsageReportingJob>();
        services.Configure<EasyhubTemplateModel>(configuration.GetSection("EasyhubTemplateModel"));
        services.AddSingleton<ILocalInstanceStore, LocalInstanceStore>();

        return services;
    }
}