<h1 align="center">🔗 ShortLink — URL Shortener with Click Analytics</h1>

<p align="center">
  A full-stack URL shortener built with <b>ASP.NET Core MVC</b>, <b>Entity Framework Core</b>, and <b>SQL Server</b>.<br/>
  Shorten a link, share it, and track every click — with a per-link analytics dashboard.
</p>

<p align="center">
  <a href="https://url-shortener-cc59.onrender.com/"><img src="https://img.shields.io/badge/🔗_Live_Demo-2563eb?style=for-the-badge" alt="Live Demo"/></a>
</p>

<p align="center"><sub>Hosted free on Render — the first request after idle may take ~30s to wake up.</sub></p>

<p align="center">
  <img src="https://img.shields.io/badge/ASP.NET%20Core-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt="ASP.NET Core"/>
  <img src="https://img.shields.io/badge/EF%20Core-512BD4?style=flat-square" alt="EF Core"/>
  <img src="https://img.shields.io/badge/SQL%20Server-CC2927?style=flat-square&logo=microsoftsqlserver&logoColor=white" alt="SQL Server"/>
  <img src="https://img.shields.io/badge/C%23-239120?style=flat-square&logo=csharp&logoColor=white" alt="C#"/>
  <img src="https://img.shields.io/badge/Bootstrap-7952B3?style=flat-square&logo=bootstrap&logoColor=white" alt="Bootstrap"/>
</p>

---

## Features

- **Shorten any URL** with an auto-generated code, or a **custom alias**.
- **Click tracking** — every visit records a timestamp, referrer, and user agent.
- **Analytics dashboard per link** — total clicks, a 14-day clicks chart (Chart.js), recent clicks, and top referrers.
- **Copy-to-clipboard**, delete, and a live list of all your links with click counts.
- **Server-side validation** (valid http/https URLs, alias format and uniqueness) and **anti-forgery protection** on every form.
- **Provider-agnostic data layer**: SQL Server by default, or SQLite for zero-setup local runs.

---

## Screenshots

<p align="center">
  <img src="docs/screenshots/01-home.png" alt="Home — shorten a URL and list your links" width="90%"/>
  <br/><em>Home — shorten a URL and see all your links with click counts</em>
</p>

<p align="center">
  <img src="docs/screenshots/02-stats.png" alt="Per-link analytics dashboard" width="90%"/>
  <br/><em>Analytics — clicks over 14 days, recent clicks, and top referrers</em>
</p>

---

## Tech stack

| Layer     | Technology                                   |
|-----------|----------------------------------------------|
| Framework | ASP.NET Core MVC (.NET 9)                    |
| Data      | Entity Framework Core (SQL Server / SQLite)  |
| Database  | SQL Server (LocalDB) by default              |
| Frontend  | Razor views, Bootstrap 5, Chart.js           |
| Language  | C#                                           |

---

## Getting started

### Prerequisites
- [.NET SDK 9](https://dotnet.microsoft.com/download)
- SQL Server or SQL Server **LocalDB** (bundled with Visual Studio). *Not needed if you run with SQLite — see below.*

### Run with SQL Server (default)
The connection string is in `appsettings.json` (`ConnectionStrings:SqlServer`, defaults to LocalDB). Then:

```bash
dotnet run
```

The database and tables are created automatically on first run. Open the URL shown in the console.

### Run without SQL Server (SQLite)
No database engine required — the app creates a local `urlshortener.db` file:

```bash
dotnet run --UseSqlite=true
```

### Load demo data (optional)
Seed a few links and sample click history for a populated dashboard:

```bash
dotnet run --UseSqlite=true --Seed=true
```

---

## How it works

1. **Create** — `POST /Home/Create` validates the URL, generates a unique 6-character code (or uses your alias), and stores a `ShortUrl` row.
2. **Redirect** — `GET /r/{code}` looks up the code, records a `Click` (timestamp, referrer, user agent), then 302-redirects to the original URL.
3. **Analytics** — `GET /Home/Details/{id}` aggregates the click history into a 14-day series, recent clicks, and top referrers.

### Data model

```
ShortUrl (Id, Code [unique], OriginalUrl, CreatedAt)
    └── Click (Id, ShortUrlId, ClickedAt, Referrer, UserAgent)   // cascade delete
```

---

## Project structure

```
UrlShortener/
├── Controllers/HomeController.cs   # create, redirect, stats, delete
├── Data/AppDbContext.cs            # EF Core context + model config
├── Data/DemoSeeder.cs             # optional demo data (runs only with --Seed=true)
├── Models/                         # ShortUrl, Click, StatsViewModel
├── Views/Home/                     # Index (list + form), Details (analytics)
├── wwwroot/css/site.css            # custom styling
├── appsettings.json                # connection strings + provider switch
└── Program.cs                      # DI, provider selection, routing
```

---

_Built as a portfolio project by Saeed Ramadan._
