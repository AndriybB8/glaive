using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudyPlanner.Api.Hubs;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.NoteGroups;
using StudyPlanner.Application.Notes;
using StudyPlanner.Application.NoteTasks;
using StudyPlanner.Application.Pomodoro;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Application.Tags;
using StudyPlanner.Application.Tasks;
using StudyPlanner.Application.TaskTags;
using StudyPlanner.Application.TodoLists;
using StudyPlanner.Application.Users;
using StudyPlanner.Infrastructure.NoteGroups;
using StudyPlanner.Infrastructure.Notes;
using StudyPlanner.Infrastructure.NoteTasks;
using StudyPlanner.Infrastructure.Persistence;
using StudyPlanner.Infrastructure.Pomodoro;
using StudyPlanner.Infrastructure.Schedules;
using StudyPlanner.Infrastructure.Tags;
using StudyPlanner.Infrastructure.Tasks;
using StudyPlanner.Infrastructure.TaskTags;
using StudyPlanner.Infrastructure.TodoLists;
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

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Brak Jwt:Key. Sprawdz README.md");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<NoteService>();
builder.Services.AddScoped<INoteRepository, EfNoteRepository>();
builder.Services.AddScoped<NoteGroupService>();
builder.Services.AddScoped<INoteGroupRepository, EfNoteGroupRepository>();
builder.Services.AddScoped<NoteTaskService>();
builder.Services.AddScoped<INoteTaskRepository, EfNoteTaskRepository>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<IScheduleRepository, EfScheduleRepository>();
builder.Services.AddScoped<TodoListService>();
builder.Services.AddScoped<ITodoListRepository, EfTodoListRepository>();
builder.Services.AddScoped<TodoItemService>();
builder.Services.AddScoped<ITodoItemRepository, EfTodoItemRepository>();
builder.Services.AddScoped<TodoListMemberService>();
builder.Services.AddScoped<ITodoListMemberRepository, EfTodoListMemberRepository>();
builder.Services.AddScoped<ITodoListNotifier, SignalRTodoListNotifier>();

// Ustawienia JSON kontrolerów nie obowiązują w SignalR – bez tego enumy szłyby jako liczby.
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<ITaskRepository, EfTaskRepository>();
builder.Services.AddScoped<ITagRepository, EfTagRepository>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<PomodoroSettingsService>();
builder.Services.AddScoped<IPomodoroSettingsRepository, EfPomodoroSettingsRepository>();
builder.Services.AddScoped<TaskTagService>();
builder.Services.AddScoped<ITaskTagRepository, EfTaskTagRepository>();

builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<ScheduleMemberService>();
builder.Services.AddScoped<IScheduleMemberRepository, EfScheduleMemberRepository>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false
        };

        // Przeglądarka nie ustawi nagłówka Authorization dla WebSocketa, więc token przychodzi w adresie.
        // Tylko na ścieżce huba – w pozostałych adresach trafiałby do logów i historii bez potrzeby.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.Request.Path.StartsWithSegments(TodoListHub.Path))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Wyszukanie i zaproszenie zdradzają, czy adres ma konto. Limit po IP, bo UseRateLimiter działa przed uwierzytelnieniem.
    options.AddPolicy("user-lookup", httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

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

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Po wygaśnięciu tokenu połączenie się zamyka – inaczej odbierałoby zmiany dalej, bez ważnego logowania.
app.MapHub<TodoListHub>(TodoListHub.Path, options => options.CloseOnAuthenticationExpiration = true)
    .RequireAuthorization();

app.Run();
