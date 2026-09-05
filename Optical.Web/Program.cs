using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Optical.Application;
using Optical.Infrastructure;
using Optical.Infrastructure.Persistence;
using Optical.Infrastructure.Persistence.Seed;
using Optical.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllersWithViews(options =>
{
    // Every POST is anti-forgery protected by default.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Permissions are resolved per request from the database, so an Admin's change to a role
// takes effect on that user's next page rather than at their next sign-in.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    // Everything requires authentication unless it opts out with [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Cheap, dependency-free browser protections: no MIME sniffing, no framing of the
// till or any admin page, and no referrer leaking internal URLs to other sites.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "same-origin";

    await next();
});

// A 404 or 403 re-executes into a friendly page instead of an empty browser error.
app.UseStatusCodePagesWithReExecute("/Home/HttpError", "?code={0}");

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Static files must stay reachable for signed-out users, otherwise the global
// fallback authorization policy would also redirect CSS and JS to the login page.
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await using (var scope = app.Services.CreateAsyncScope())
{
    if (app.Environment.IsDevelopment())
    {
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    }

    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

app.Run();
