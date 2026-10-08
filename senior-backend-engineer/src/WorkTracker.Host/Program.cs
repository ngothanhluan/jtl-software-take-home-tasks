using FastEndpoints;
using FastEndpoints.Swagger;
using WorkTracker.Host;
using WorkTracker.Users;
using WorkTracker.WorkItems;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddProblemDetails()
    .AddExceptionHandler<ProblemDetailsExceptionHandler>()
    .AddUsersModule()
    .AddWorkItemsModule()
    .AddFastEndpoints(o => o.Assemblies = [typeof(UsersModule).Assembly, typeof(WorkItemsModule).Assembly])
    .SwaggerDocument();

var app = builder.Build();

app.UseExceptionHandler();
app.UseFastEndpoints(c => c.Errors.UseProblemDetails());
app.UseSwaggerGen();

app.Run();

// Lets WorkTracker.Api.Tests start the app with WebApplicationFactory<Program>.
public partial class Program { }
