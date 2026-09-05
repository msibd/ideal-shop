# Phase 2 — Authentication / Authorization (RBAC)

Status: **Complete** — build passes, migration applied, admin seeded and verified
Date: 2026-09-04 · *corrected 2026-09-04 — see §6*

---

## 1. Scope

Implemented:

- ApplicationUser
- Identity configuration
- Login / Logout
- Authentication cookie
- Basic authorization (role based)
- Roles seed + Admin user seed
- Login Razor view
- AccountController

Not implemented (deliberately out of scope):

- permission table / claims-permission engine
- multi-branch or multi-tenant authorization
- JWT, refresh tokens
- external login, email confirmation, password reset
- user-management UI (create/edit staff)

---

## 2. Design decisions

| Decision | Choice | Reason |
|---|---|---|
| RBAC model | ASP.NET Core Identity **roles only** (`Admin`, `Cashier`) | Phase forbids "complex permissions". A permission table serves no current requirement. |
| Identity key type | Framework default `string` | Zero friction with Identity. Business entities will use `int` from Phase 3. |
| Login credential | **UserName** (not email) | Shop staff accounts (`admin`); no mail infrastructure required. |
| Login command/handler | **None** — controller calls `IIdentityService` directly | A handler that only forwards to Identity is a pass-through. CLAUDE.md §6: do not blindly create all three files. |
| `IApplicationDbContext` | **Not created yet** | No business entities exist to query. Added in Phase 3 with Category/Brand. |
| Authorization default | Global fallback `[Authorize]` | Secure-by-default: a future controller cannot be forgotten. Opt out with `[AllowAnonymous]`. |
| Anti-forgery | Global `AutoValidateAntiforgeryTokenAttribute` filter | Every POST protected without per-action attributes. |

---

## 3. Layer-by-layer changes

### 3.1 Optical.Domain

**No changes.** Domain has zero references and zero files in this phase.
Roles are a security concern, not a domain concept, so `AppRoles` lives in Application.

---

### 3.2 Optical.Application

Created 4 files. Application still has **no** dependency on ASP.NET Core or Identity.

```
Optical.Application/
├── Common/
│   └── AppRoles.cs
└── Abstractions/
    └── Identity/
        ├── IIdentityService.cs
        ├── ICurrentUserService.cs
        └── LoginResult.cs
```

**`Common/AppRoles.cs`** — role-name constants, so views, controllers and the seeder never use magic strings.

```csharp
public const string Admin   = "Admin";
public const string Cashier = "Cashier";
public static readonly string[] All = [Admin, Cashier];
```

**`Abstractions/Identity/IIdentityService.cs`** — the authentication contract. Implemented by Infrastructure so the Web layer never touches Identity or EF Core types.

```csharp
Task<LoginResult> LoginAsync(string userName, string password, bool rememberMe);
Task LogoutAsync();
```

**`Abstractions/Identity/LoginResult.cs`** — `Success | InvalidCredentials | LockedOut | Disabled`.
An enum, not an exception: a wrong password is an expected outcome, not an exceptional one.

**`Abstractions/Identity/ICurrentUserService.cs`** — `UserId`, `UserName`, `IsAuthenticated` for the current request. Prescribed by CLAUDE.md §7; will be consumed for auditing from Phase 3.

**csproj:** unchanged in this phase (already references `Optical.Domain` + `Microsoft.EntityFrameworkCore` from Phase 1).

---

### 3.3 Optical.Infrastructure

Created 6 files + migration. This is the only layer that knows about Identity, EF Core and PostgreSQL.

```
Optical.Infrastructure/
├── DependencyInjection.cs
├── Identity/
│   ├── ApplicationUser.cs
│   ├── IdentityService.cs
│   └── CurrentUserService.cs
└── Persistence/
    ├── ApplicationDbContext.cs
    ├── Migrations/
    │   ├── 20260904141647_InitialIdentity.cs
    │   ├── 20260904141647_InitialIdentity.Designer.cs
    │   └── ApplicationDbContextModelSnapshot.cs
    └── Seed/
        └── DatabaseSeeder.cs
```

**`Identity/ApplicationUser.cs`** — extends `IdentityUser` with two fields only:

| Field | Purpose |
|---|---|
| `FullName` | display name for staff |
| `IsActive` | disabled staff keep their history but can no longer sign in |

`IsActive` is **enforced at login** (see `IdentityService`), so it is not a dead flag.

**`Identity/IdentityService.cs`** — implements `IIdentityService` using `SignInManager` + `UserManager`.
Order of checks: user exists → `IsActive` → `PasswordSignInAsync(lockoutOnFailure: true)` → lockout → success.

**`Identity/CurrentUserService.cs`** — implements `ICurrentUserService` from `IHttpContextAccessor`.

**`Persistence/ApplicationDbContext.cs`** — `IdentityDbContext<ApplicationUser>` with `ApplyConfigurationsFromAssembly` in `OnModelCreating`, ready for the Phase 3 entity configurations.

**`Persistence/Seed/DatabaseSeeder.cs`**

1. Creates any missing role from `AppRoles.All`.
2. Reads `Seed:AdminUserName` / `Seed:AdminPassword` / `Seed:AdminEmail` from configuration.
3. If username or password is missing → logs a warning and skips (never crashes production).
4. If the user already exists → no-op (idempotent).
5. Otherwise creates the user and assigns the `Admin` role.

The password is never logged and never appears in source.

**`DependencyInjection.cs`** — single `AddInfrastructure(IServiceCollection, IConfiguration)` entry point:

- throws if `ConnectionStrings:DefaultConnection` is missing (fail fast)
- `AddDbContext<ApplicationDbContext>` → `UseNpgsql`
- `AddIdentity<ApplicationUser, IdentityRole>` with:
  - `Password.RequiredLength = 8`, `RequireNonAlphanumeric = false`
  - `User.RequireUniqueEmail = false` (email is optional for shop staff)
  - `Lockout.MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 5 min`
- `ConfigureApplicationCookie`:
  - name `Optical.Auth`, `HttpOnly`, `SameSite=Lax`, `SecurePolicy=Always`
  - `LoginPath=/Account/Login`, `AccessDeniedPath=/Account/AccessDenied`
  - `ExpireTimeSpan = 8h`, `SlidingExpiration = true`
- registers `IIdentityService`, `ICurrentUserService`, `DatabaseSeeder`, `IHttpContextAccessor` (all scoped)

`AddDefaultTokenProviders()` was intentionally **not** added — token providers only serve password reset / 2FA, which are out of scope.

**csproj changes**

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.11" />
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

The framework reference is what makes `SignInManager` and `IHttpContextAccessor` available inside a class library.

**Migration `20260904141647_InitialIdentity`**

Creates the 7 standard Identity tables — `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens` — with the two extra columns on `AspNetUsers`:

```csharp
FullName = table.Column<string>(type: "text", nullable: false),
IsActive = table.Column<bool>(type: "boolean", nullable: false),
```

---

### 3.4 Optical.Web

```
Optical.Web/
├── Program.cs                       (rewritten)
├── appsettings.json                 (Seed section added)
├── Controllers/
│   ├── AccountController.cs         (new)
│   └── HomeController.cs            (edited)
├── ViewModels/
│   └── LoginViewModel.cs            (new)
└── Views/
    ├── Account/
    │   ├── Login.cshtml             (new)
    │   └── AccessDenied.cshtml      (new)
    └── Shared/
        └── _Layout.cshtml           (edited)
```

**`ViewModels/LoginViewModel.cs`** — `UserName` (required, max 50), `Password` (required, `DataType.Password`), `RememberMe`, `ReturnUrl`.

**`Controllers/AccountController.cs`** — thin by design; no EF, no Identity types, no business rules.

| Action | Method | Auth |
|---|---|---|
| `Login` | GET | `[AllowAnonymous]` — redirects to Home if already signed in |
| `Login` | POST | `[AllowAnonymous]` + global anti-forgery |
| `Logout` | POST | requires authentication (global fallback policy) |
| `AccessDenied` | GET | `[AllowAnonymous]` |

`RedirectToLocal` uses `Url.IsLocalUrl(returnUrl)` — open-redirect protection.
`LoginResult` is mapped to a user-facing message with a `switch` expression; no technical detail leaks to the user.

> Fixed during this phase: a class-level `[AllowAnonymous]` was silently overriding the action-level `[Authorize]` on `Logout` (compiler warning **ASP0026**). `[AllowAnonymous]` is now applied per action.

**`Views/Account/Login.cshtml`** — standalone page (`Layout = null`) so the signed-out screen carries no app chrome. Bootstrap 5 centred card, validation summary, client-side validation scripts.

**`Views/Account/AccessDenied.cshtml`** — simple message + link home.

**`Views/Shared/_Layout.cshtml`** — branding renamed `Optical.Web` → `Optical Shop`; Privacy nav link removed; added, for authenticated users, the username and a POST **Sign out** form. (The full dashboard shell arrives in Phase 9.)

**`Controllers/HomeController.cs`** — added `[AllowAnonymous]` to `Error` only, so the error page still renders when signed out. `Index` and `Privacy` now require authentication via the fallback policy.

**`Program.cs`**

```
AddInfrastructure(builder.Configuration)
AddControllersWithViews(+ AutoValidateAntiforgeryTokenAttribute filter)
AddAuthorization(FallbackPolicy = RequireAuthenticatedUser)
...
UseAuthentication()      <-- added, before UseAuthorization
UseAuthorization()
...
startup scope:
  Development only -> Database.MigrateAsync()
  all environments -> DatabaseSeeder.SeedAsync()
```

**`appsettings.json`** — added, with empty values (real values live in user-secrets):

```json
"Seed": {
  "AdminUserName": "admin",
  "AdminEmail": "",
  "AdminPassword": ""
}
```

---

## 4. Authentication flow

```
GET /Account/Login
  -> AccountController.Login  (AllowAnonymous)

POST /Account/Login          [anti-forgery enforced globally]
  -> IIdentityService.LoginAsync
       UserManager.FindByNameAsync  -> not found        -> InvalidCredentials
       user.IsActive == false                           -> Disabled
       SignInManager.PasswordSignInAsync(lockout: true)
            IsLockedOut                                 -> LockedOut
            Succeeded  -> cookie "Optical.Auth" issued  -> Success
  -> Success : Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : Home/Index
  -> Failure : ModelState error, redisplay form

Unauthenticated request      -> 302 /Account/Login?ReturnUrl=...
Authenticated, wrong role    -> 302 /Account/AccessDenied
POST /Account/Logout         -> SignOutAsync -> /Account/Login
```

---

## 5. Security checklist

| Requirement (CLAUDE.md §11) | Status |
|---|---|
| ASP.NET Core Identity | Yes |
| Password hashing via Identity | Yes (`UserManager.CreateAsync` / `PasswordSignInAsync`) |
| Authentication | Cookie, `UseAuthentication()` in pipeline |
| Authorization | Global fallback `[Authorize]`, roles available via `AppRoles` |
| Anti-forgery | Global `AutoValidateAntiforgeryTokenAttribute` |
| Server-side validation | `ModelState` + DataAnnotations |
| HTTPS in production | `UseHttpsRedirection` + `UseHsts` |
| Secrets outside source | Connection string and admin password in user-secrets |
| Secure cookies | `HttpOnly`, `SameSite=Lax`, `SecurePolicy=Always` |
| No passwords/tokens in logs | Verified — seeder logs username only |

Additional hardening: brute-force lockout (5 attempts / 5 min), open-redirect guard, disabled-account check, generic "Invalid username or password" message (no user enumeration at the password step).

---

## 6. Verification

```
dotnet build Optical.slnx
  -> Build succeeded. 0 Warning(s), 0 Error(s)

dotnet ef migrations add InitialIdentity
      --project Optical.Infrastructure --startup-project Optical.Web
      --output-dir Persistence/Migrations
  -> OK. 7 Identity tables; AspNetUsers includes FullName + IsActive.
  -> Warning "No IEntityTypeConfiguration found" is expected (no business entities yet).

dotnet ef database update
  -> OK. Applied 20260904141647_InitialIdentity
```

### Correction to the original text of this section

This section first reported `database update` as **FAILED — cannot connect to 127.0.0.1:5432**, and
concluded "PostgreSQL is not installed/running on the development machine." **That conclusion was
wrong.** The check only looked for a Windows service (`Get-Service postgres*`) and for `psql` on
`PATH`. PostgreSQL was running the whole time, in Docker:

```
PostgreSQL_Server   postgres:18   0.0.0.0:5434->5432/tcp   (healthy)
```

The real fault was the guessed connection string, which used port **5432** while the container
publishes **5434**. Once the port was corrected and the `opticalshop` database created, the migration
applied cleanly and the seeder ran. Verified in the live database:

```
 UserName | IsActive | role  | has_password        Name
----------+----------+-------+--------------      ---------
 admin    | t        | Admin | t                   Admin
                                                   Cashier
```

The login flow was later exercised end to end against the running application, including the
lockout-free happy path, the anti-forgery token, and the `Url.IsLocalUrl` redirect.

---

## 7. How to run

1. Start the PostgreSQL container and make sure the connection string matches it. The working value
   (already stored in user-secrets) is:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5434;Database=opticalshop;Username=postgres;Password=postgres" --project Optical.Web
   ```

   Note the port: the container publishes **5434**, not the default 5432.

2. Apply the migration:

   ```powershell
   dotnet ef database update --project Optical.Infrastructure --startup-project Optical.Web
   ```

3. Run using the **https** profile (`https://localhost:7001`). The auth cookie is `Secure`-only, so the http profile will not hold a session.

4. Sign in with `admin` / `Admin#12345` (stored in user-secrets as `Seed:AdminPassword`). **Change this before any real use.**

---

## 8. Configuration keys

| Key | Location | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | user-secrets | empty placeholder in appsettings.json; live value uses port **5434** |
| `Seed:AdminUserName` | appsettings.json | `admin` — not a secret |
| `Seed:AdminEmail` | appsettings.json | optional |
| `Seed:AdminPassword` | user-secrets | required for seeding; seeding is skipped with a warning if absent |

---

## 9. Architecture compliance

| Rule | Result |
|---|---|
| Domain has no Infrastructure/Web dependency | Domain untouched, zero references |
| Application has no Web dependency | Only abstractions + constants; no ASP.NET types |
| Infrastructure implements Application abstractions | `IdentityService`, `CurrentUserService` |
| Controllers stay thin | `AccountController` binds, calls, redirects — nothing else |
| No DB access from controllers | No EF Core type anywhere in Web |
| No banned abstractions (§18) | No MediatR, AutoMapper, Repository, UnitOfWork, CQRS framework |

---

## 9a. Defect found later in this phase's work

The global fallback authorization policy added here also applied to the endpoints created by
`app.MapStaticAssets()`, so `GET /css/site.css` returned `302 -> /Account/Login` for signed-out
visitors — the login page rendered with **no CSS at all**. Fixed later with
`app.MapStaticAssets().AllowAnonymous();`. Full write-up in `docs/Platform-UI-And-Database.md` §3.

---

## 10. Next phase

Phase 3 — Branch / Category / Brand. That phase introduces the first business entities and therefore `IApplicationDbContext`, the first EF `IEntityTypeConfiguration` classes, and the first role-gated controllers.
