using Api;
using Application;
using Docker.DotNet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.ApiServiceProvider(builder.Configuration, builder.Environment);
builder.Services.ApplicationServiceProvider(builder.Configuration);

var app = builder.Build();

builder.Services.AddSingleton<IDockerClient>(sp =>
{
    var dockerUri = System.Runtime.InteropServices.RuntimeInformation
        .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
        ? "npipe://./pipe/docker_engine"
        : "unix:///var/run/docker.sock";

    return new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Node Management API V1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();


app.Run();
