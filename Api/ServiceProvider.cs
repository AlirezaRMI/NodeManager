using Microsoft.OpenApi.Models;

namespace Api;

/// <summary>
/// Extension methods to register application services.
/// </summary>
public static class ServiceProvider
{
    /// <summary>
    /// Registers the DbContext with the correct connection string
    /// based on the hosting environment.
    /// </summary>
    public static IServiceCollection ApiServiceProvider(this IServiceCollection services, IConfiguration configuration,
        IWebHostEnvironment env)
    {
     

        // Provides access to HttpContext for IUserContextService
        services.AddHttpContextAccessor();
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Node Management API", Version = "v1" });
        });

        services.AddHttpClient();

        return services;
    }
}