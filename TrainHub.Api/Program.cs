using TrainHub.Application;
using TrainHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Service Registration (Clean Architecture layers) ────────────
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// ── API Services ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// ── HTTP Pipeline ───────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();