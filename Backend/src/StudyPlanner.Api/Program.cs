using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using StudyPlanner.Api.Services;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.NoteGroups;
using StudyPlanner.Application.Notes;
using StudyPlanner.Application.NoteTasks;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Application.Tasks;
using StudyPlanner.Application.Users;
using StudyPlanner.Infrastructure.NoteGroups;
using StudyPlanner.Infrastructure.Notes;
using StudyPlanner.Infrastructure.NoteTasks;
using StudyPlanner.Infrastructure.Persistence;
using StudyPlanner.Infrastructure.Schedules;
using StudyPlanner.Infrastructure.Tasks;
using StudyPlanner.Infrastructure.Users;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Enumy w JSON jako tekst w camelCase (np. "inProgress"), a nie liczby.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
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
builder.Services.AddScoped<INoteRepository, EfNoteRepository>();
builder.Services.AddScoped<NoteGroupService>();
builder.Services.AddScoped<INoteGroupRepository, EfNoteGroupRepository>();
builder.Services.AddScoped<NoteTaskService>();
builder.Services.AddScoped<INoteTaskRepository, EfNoteTaskRepository>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<IScheduleRepository, EfScheduleRepository>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<ITaskRepository, EfTaskRepository>();

builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IUserRepository, EfUserRepository>();

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
