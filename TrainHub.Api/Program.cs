using Microsoft.EntityFrameworkCore;
using TrainHub.Infrastructure.Data; // تأكد من مطابقة مسار الـ DbContext في مشروعك

var builder = WebApplication.CreateBuilder(args);

// 1. Add DbContext with Connection String from appsettings.json
builder.Services.AddDbContext<TrainHubDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Add Controllers & OpenAPI/Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// 3. Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// 4. Map Controller routes
app.MapControllers();

app.Run();