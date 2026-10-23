using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
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
    // MediatR 12 skips pre-processors during assembly scanning unless asked, which left CreatePersonPreProcessor unregistered.
    // AutoRegisterRequestProcessors registers every pre-processor, but not the behavior that runs them, so add that too.
    cfg.AutoRegisterRequestProcessors = true;
    cfg.AddOpenBehavior(typeof(RequestPreProcessorBehavior<,>));
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly);
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// .NET 8's UseExceptionHandler() won't start without a ProblemDetails service, even with an IExceptionHandler registered.
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();


public partial class Program { }
