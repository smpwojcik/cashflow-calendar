using System.IO;
using System.Net;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

using CashFlowCalendar.Components;
using CashFlowCalendar.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Blazor / Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// AuthZ (recommended when using [Authorize])
builder.Services.AddAuthorization();

// Persist DataProtection keys (important for OIDC correlation/nonce cookies across restarts)
builder.Services
    .AddDataProtection()
    .SetApplicationName("CashFlowCalendar")
    .PersistKeysToFileSystem(new DirectoryInfo("/src/.aspnet/DataProtection-Keys")); // bind-mounted => persists

// AuthN (Cookie + OIDC)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = builder.Configuration["Authentik:Authority"];
    options.ClientId = builder.Configuration["Authentik:ClientId"];
    options.ClientSecret = builder.Configuration["Authentik:ClientSecret"];
    options.ResponseType = builder.Configuration["Authentik:ResponseType"] ?? "code";

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");

    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;

    // Callbacks
    options.CallbackPath = "/signin-oidc";
    options.SignedOutCallbackPath = "/signout-callback-oidc";

    // Helps avoid cookie issues during redirects (especially when TLS is terminated at NPM)
    options.NonceCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

// Trust reverse proxy headers (NPM) so Request.Scheme becomes https
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                     | ForwardedHeaders.XForwardedProto
                     | ForwardedHeaders.XForwardedHost,
    ForwardLimit = 1,
    KnownProxies =
    {
        // Put your NPM IP here. Keeping both is fine if you’re mid-migration.
        IPAddress.Parse("10.0.0.233"),
        IPAddress.Parse("10.0.0.223"),
    }
});

// Debug endpoint (keep for now; remove later)
app.MapGet("/__debug/headers", (HttpContext ctx) => Results.Json(new
{
    scheme = ctx.Request.Scheme,
    host = ctx.Request.Host.Value,
    remote_ip = ctx.Connection.RemoteIpAddress?.ToString(),
    x_forwarded_proto = ctx.Request.Headers["X-Forwarded-Proto"].ToString(),
    x_forwarded_host = ctx.Request.Headers["X-Forwarded-Host"].ToString(),
    x_forwarded_for = ctx.Request.Headers["X-Forwarded-For"].ToString(),
}));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// NOTE: TLS is terminated at NPM, so keep this off for now.
// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Must be after authN/authZ for InteractiveServer
app.UseAntiforgery();

// Login/logout helpers
app.MapGet("/login", (HttpContext ctx) =>
    Results.Challenge(new AuthenticationProperties { RedirectUri = "/" },
        new[] { OpenIdConnectDefaults.AuthenticationScheme }));

app.MapGet("/logout", (HttpContext ctx) =>
    Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
        new[]
        {
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme
        }));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
