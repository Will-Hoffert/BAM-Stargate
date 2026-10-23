using MediatR.Pipeline;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;
using StargateAPI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<StargateContext>(options => 
    options.UseSqlite(builder.Configuration.GetConnectionString("StarbaseApiDatabase")));

builder.Services.AddMediatR(cfg =>
{
    // Behaviors run in the order they are added. Logging comes first so it also records requests the pre-processors reject.
    cfg.AddOpenBehavior(typeof(ProcessLoggingBehavior<,>));
    // MediatR 12 skips pre-processors during assembly scanning unless asked, which left CreatePersonPreProcessor unregistered.
    // AutoRegisterRequestProcessors registers every pre-processor, but not the behavior that runs them, so add that too.
    cfg.AutoRegisterRequestProcessors = true;
    cfg.AddOpenBehavior(typeof(RequestPreProcessorBehavior<,>));
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly);
});

builder.Services.AddCors();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// .NET 8's UseExceptionHandler() won't start without a ProblemDetails service, even with an IExceptionHandler registered.
builder.Services.AddProblemDetails();

// [ApiController] answers a body it can't bind with its own ProblemDetails 400 before the action runs.
// Send that 400 in the BaseResponse envelope like every other error, using ASP.NET's messages.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = string.Join(" ", context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m)));

        return new BadRequestObjectResult(new BaseResponse
        {
            Success = false,
            Message = message.Length > 0 ? message : "The request is invalid.",
            ResponseCode = StatusCodes.Status400BadRequest
        });
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Lets a browser UI on another origin call the API. Only origins listed in Cors:AllowedOrigins are allowed: none in
// appsettings.json, the Angular dev server in Development. Methods and headers are only what the API uses.
app.UseCors(policy => policy
    .WithOrigins(app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
    .WithMethods("GET", "POST", "PUT")
    .WithHeaders("Content-Type"));

app.UseAuthorization();

app.MapControllers();

app.Run();


public partial class Program { }
