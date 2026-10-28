# StudyPlanner
Aplikacja internetowa wspomagająca planowanie nauki oraz monitorowanie postępów edukacyjnych.
Harmonogramy, listy ToDo, notatki, Pomodoro oraz fiszki generowane z plików PDF.

## Struktura
```
/
Backend/            ASP.NET Core Web API (Clean Architecture)
Frontend/           React + TypeScript (Vite, Tailwind CSS)
docs/               dokumentacja
docker-compose.yml  lokalna baza danych PostgreSQL
```

## Wymagania

- **[.NET SDK](https://dotnet.microsoft.com/download) 10.0** (sprawdzenie `dotnet --version`)
- **[Node.js](https://nodejs.org/) v24 LTS**, **npm**
- **[Docker Compose](https://docs.docker.com/compose/install)** (sprawdzenie `docker compose version`)
- **[Git](https://git-scm.com/install/)** (sprawdzenie `git -v`)

## Szybki start

### 1. Baza danych

W katalogu głównym repozytorium wywołać:
1. `cp .env.example .env`
2. `docker compose up -d`

Baza PostgreSQL działa na `localhost:5432` (dane z pliku `.env`).
Wartości w `.env.example` służą wyłącznie do pracy lokalnej.

**Zatrzymanie:**        `docker compose stop`<br>
**Czyszczenie bazy:**   `docker compose down -v`

### 2. Backend

W katalogu głównym repozytorium wywołać:

Ustawić sekrety lokalne (raz, dla swojego środowiska – nigdy nie trafiają do repozytorium):
1. `dotnet user-secrets init --project src/StudyPlanner.Api`
2. `dotnet user-secrets set “ConnectionStrings:Default” “Host=localhost;Port=<POSTGRES_PORT>;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>” --project src/StudyPlanner.Api`
3. `dotnet user-secrets set “Jwt:Key” “<losowy string, min. 32 znaki – np. z openssl rand -base64 32>” --project src/StudyPlanner.Api`

Wartości `<POSTGRES_*>` biorą się z pliku `.env` z kroku 1 ("Baza danych").

Uruchomić:
1. `cd Backend`
2. `dotnet build`
3. `dotnet run --project src/StudyPlanner.Api`



Testy: `dotnet test`

### 3. Frontend

W katalogu głównym repozytorium wywołać:
1. `cd Frontend`
2. `npm install`
3. `npm run dev`

Aplikacja działa na `http://localhost:5173`.
Zapytania na `/api` są przekierowywane do backendu (`http://localhost:5196`), więc backend musi być uruchomiony.

**Build:**  `npm run build`<br>
**Lint:**   `npm run lint`

## Zasady pracy
- Nie pracujemy bezpośrednio na `main`. Zmiany wchodzą przez PR (minimum jedno review).
- Jeden branch na zadanie, tworzony od aktualnego `main`. Przykładowo: `<prefiks>/krotki-opis`. Prefiks musi być taki jak w backlogu:
![Przykład prefiksu brancha w backlogu](docs/images/branch-prefix-example.png)
- Nazywamy PR `<prefiks> Krótki opis co wykonano`
- Merge przez **Squash and merge**
- Do repozytorium nie trafiają sekrety ani `.env`. Wzorzec zmiennych wpisujemy w `.env.example`
- Daty w bazie i API przechowujemy w **UTC**

## Technologie

| Obszar         | Technologia                           |
|----------------|---------------------------------------|
| Backend        | ASP.NET Core, Entity Framework Core   |
| Baza danych    | PostgreSQL                            |
| Frontend       | React, TypeScript, Vite, Tailwind CSS |
| Konteneryzacja | Docker Compose                        |

