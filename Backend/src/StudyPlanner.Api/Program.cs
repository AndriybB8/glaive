using Microsoft.EntityFrameworkCore;
using StudyPlanner.Api.Services;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.Notes;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Infrastructure.Notes;
using StudyPlanner.Infrastructure.Persistence;
using StudyPlanner.Infrastructure.Schedules;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Baza PostgreSQL z docker-compose. Lokalnie connection string jest w appsettings.Development.json
// (te same dane co w .env.example); na innym środowisku podać zmienną ConnectionStrings__Default.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton(TimeProvider.System);
// TODO: po wdrożeniu logowania (JWT) podmienić na implementację czytającą użytkownika z tokenu.
builder.Services.AddScoped<ICurrentUser, DevCurrentUser>();
builder.Services.AddScoped<NoteService>();
// TODO: po podłączeniu bazy podmienić na repozytorium EF Core (AddScoped).
builder.Services.AddSingleton<INoteRepository, InMemoryNoteRepository>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<IScheduleRepository, EfScheduleRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Lokalnie schemat bazy aktualizuje się sam przy starcie – wystarczy `docker compose up -d`.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
