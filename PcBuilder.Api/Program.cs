using Microsoft.EntityFrameworkCore;
using PcBuilder.Core.Repositories;
using PcBuilder.Core.Services;
using PcBuilder.Infrastructure.Data;
using PcBuilder.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<PcBuilderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Dependency injection
builder.Services.AddScoped<IPartRepository, EfPartRepository>();
builder.Services.AddScoped<IBuildGenerator, BuildGenerator>();

// CORS — allows the React frontend to call the API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
        policy.WithOrigins("http://localhost:5173",
        "https://pc-part-picker-one.vercel.app")  // React dev server
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReact");  // Move this FIRST
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();