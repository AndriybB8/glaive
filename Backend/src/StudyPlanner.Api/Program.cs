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

// Baza PostgreSQL z docker-compose. Connection string nie jest w repozytorium:
// lokalnie trzymamy go w User Secrets (dane z własnego .env), na serwerze w zmiennej ConnectionStrings__Default.
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString) && !EF.IsDesignTime)
{
    throw new InvalidOperationException(
        "Brak ConnectionStrings:Default. Lokalnie ustaw go (dane z pliku .env) w katalogu Backend: " +
        "dotnet user-secrets set \"ConnectionStrings:Default\" " +
        "\"Host=localhost;Port=<POSTGRES_PORT>;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>\" " +
        "--project src/StudyPlanner.Api");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

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
