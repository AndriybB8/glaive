using StudyPlanner.Api.Services;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.Notes;
using StudyPlanner.Infrastructure.Notes;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton(TimeProvider.System);
// TODO: po wdrożeniu logowania (JWT) podmienić na implementację czytającą użytkownika z tokenu.
builder.Services.AddScoped<ICurrentUser, DevCurrentUser>();
builder.Services.AddScoped<NoteService>();
// TODO: po podłączeniu bazy podmienić na repozytorium EF Core (AddScoped).
builder.Services.AddSingleton<INoteRepository, InMemoryNoteRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
