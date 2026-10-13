using Api;
using Api.Endpoints;
using Api.Infrastructure;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();

// Observability & HealthChecks
builder.AddCustomOpenTelemetry();
builder.AddDefaultHealthChecks();

// Order event polymorphism
builder.AddOrderHistoryJson();

var app = builder.Build();

// Mongo indexes (idempotent: no-op if they already exist)
await app.Services.InitializeInfrastructureAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.UseExceptionHandler();

// Health check
app.MapHealthChecks();

// custom endpoints
app.MapTestEndpointsInDevelopment();
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapVoucherEndpoints();

app.Run();