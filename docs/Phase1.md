# Phase 1 — Solution Setup

Status: **Complete** — restore and build pass, dependency direction verified
Date: 2026-09-04 · *written retrospectively 2026-09-04; verification figures are from the original run*

---

## 1. Scope

Wire the four-project Clean Architecture solution and its packages. No business features, no
authentication, no database schema.

Implemented:

- project references in the required direction
- EF Core 10 + PostgreSQL provider packages
- `dotnet ef` design-time package on the startup project
- connection-string configuration held outside source

Not implemented: authentication, entities, `DbContext`, migrations, any feature.

---

## 2. Starting point

The four projects and `Optical.slnx` already existed as plain `dotnet new` output, all targeting
`net10.0` with nullable reference types and implicit usings already enabled. Nothing was wired
together — no project references, no packages.

```
Optical.slnx
Optical.Domain/          (empty class library)
Optical.Application/     (empty class library)
Optical.Infrastructure/  (empty class library)
Optical.Web/             (MVC scaffold: HomeController, Views, Bootstrap, jQuery)
```

SDK in use: **.NET 10.0.400**.

---

## 3. Changes by layer

### 3.1 Optical.Domain

**Reference added: none — deliberately.** Domain has no project reference and no NuGet package. That
is the whole point of the layer, and it is still true today.

### 3.2 Optical.Application

| Change | Value |
|---|---|
| Project reference | `Optical.Domain` |
| Package | `Microsoft.EntityFrameworkCore` 10.0.11 |

EF Core is referenced here because `IApplicationDbContext` (added in Phase 3) exposes `DbSet<T>`. The
package is the ORM abstraction only — no provider, so no PostgreSQL dependency leaks into Application.

### 3.3 Optical.Infrastructure

| Change | Value |
|---|---|
| Project reference | `Optical.Application` |
| Packages | `Microsoft.EntityFrameworkCore` 10.0.11, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 |

### 3.4 Optical.Web

| Change | Value |
|---|---|
| Project references | `Optical.Application`, `Optical.Infrastructure` |
| Package | `Microsoft.EntityFrameworkCore.Design` 10.0.11 (`PrivateAssets=all`) |
| Config | `ConnectionStrings:DefaultConnection` key added to `appsettings.json`, left empty |
| Config | `UserSecretsId` initialised; the real connection string stored there |

---

## 4. The one deviation from CLAUDE.md §4

CLAUDE.md draws the dependency graph as `Web → Application → Domain` with Infrastructure on its own
branch. In practice **Web must also reference Infrastructure**, because `Program.cs` is the
composition root and has to register the concrete `ApplicationDbContext` in DI.

This does not break the architecture rule. The rule that matters is that **Domain and Application
never depend outward**, and that still holds exactly. Only `Program.cs` touches an Infrastructure
type; no controller, view, or Application file does. The alternative — a plugin-loading indirection
so Web never names Infrastructure — is precisely the kind of over-engineering CLAUDE.md §18 bans.

Final graph:

```
Domain          -> (nothing)
Application     -> Domain
Infrastructure  -> Application -> Domain
Web             -> Application, Infrastructure
```

---

## 5. Secrets

`appsettings.json` carries the key with an empty value, so the shape is discoverable in source; the
value itself lives in user-secrets and never enters the repository. This satisfies CLAUDE.md §11
("secrets outside source code") from the first commit rather than as a later cleanup.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5434;Database=opticalshop;Username=postgres;Password=postgres" --project Optical.Web
```

> The value shown is the **corrected** one. The connection string first written in this phase guessed
> port 5432; the PostgreSQL container actually publishes **5434**. That mistake was not discovered
> until later and caused wrong "PostgreSQL is not installed" conclusions in Phases 2 and 3 — both now
> corrected in their own documents.

---

## 6. Deliberately not created

- **No `ApplicationDbContext`.** There were no entities to model. An empty context plus an empty
  migration is dead code.
- **No `IApplicationDbContext`.** Nothing to query yet; it arrives in Phase 3.
- **No Directory.Packages.props / central package management.** Four projects with a handful of
  packages do not need it.
- **No folder skeleton.** Empty `Features/`, `Entities/`, `Configurations/` folders were not
  pre-created; each appears in the phase that first needs it.

---

## 7. Verification

```
dotnet restore Optical.slnx   -> OK
dotnet build   Optical.slnx   -> Build succeeded. 0 Warning(s), 0 Error(s)

dotnet list reference (per project):
  Optical.Domain          -> (none)
  Optical.Application     -> Optical.Domain
  Optical.Infrastructure  -> Optical.Application
  Optical.Web             -> Optical.Application, Optical.Infrastructure
```

Domain confirmed to have zero external or infrastructure dependencies.

---

## 8. Architecture compliance

| Rule | Result |
|---|---|
| Domain has no Infrastructure dependency | zero references of any kind |
| Domain has no Web dependency | same |
| Application has no Web dependency | references Domain + EF Core abstractions only |
| Infrastructure implements Application abstractions | reference in place; implementations from Phase 2 |
| Web contains HTTP/MVC concerns | MVC scaffold untouched in this phase |
| No banned abstractions (§18) | none introduced |

---

## 9. Next phase

Phase 2 — Authentication / Authorization (RBAC). It adds ASP.NET Core Identity, `ApplicationUser`,
the first `DbContext`, and the first migration.
