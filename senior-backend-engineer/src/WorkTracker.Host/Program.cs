using FastEndpoints;
using FastEndpoints.Swagger;
using WorkTracker.Host;
using WorkTracker.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddProblemDetails()
    .AddExceptionHandler<ProblemDetailsExceptionHandler>()
    .AddUsersModule()
    .AddFastEndpoints(o => o.Assemblies = [typeof(UsersModule).Assembly])
    .SwaggerDocument();

var app = builder.Build();

app.UseExceptionHandler();
app.UseFastEndpoints(c => c.Errors.UseProblemDetails());
app.UseSwaggerGen();

app.Run();

// Lets WorkTracker.Api.Tests start the app with WebApplicationFactory<Program>.
public partial class Program { }
