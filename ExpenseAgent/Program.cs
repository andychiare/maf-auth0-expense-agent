using ExpenseAgent.Client.Pages;
using ExpenseAgent.Components;
using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using ExpenseAgent.Agent;
using ExpenseAgent.Services;
using ExpenseAgent.Models;
//👇new code
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Auth0.MyAccountApi;
//👆new code

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCascadingAuthenticationState();

builder.Services
    .AddAuth0WebAppAuthentication(options => {
      options.Domain = builder.Configuration["Auth0:Domain"]!;
      options.ClientId = builder.Configuration["Auth0:ClientId"]!;
      options.ClientSecret = builder.Configuration["Auth0:ClientSecret"]!;
      options.Scope = "openid profile email offline_access";
    })
    .WithAccessToken(options =>
    {
        options.UseRefreshTokens = true;
    });

builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<ExpenseAgentService>();

//builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(sp =>
{
    var domain = sp.GetRequiredService<IConfiguration>()["Auth0:Domain"]!;
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();

    return new MyAccountClient(new MyAccountClientOptions
    {
        Domain = domain,
        TokenProvider = new DelegateTokenProvider(async cancellationToken =>
        {
            var context = httpContextAccessor.HttpContext;
            if (context == null) return null;

            return await context.GetAccessTokenAsync(new AccessTokenRequest
            {
                Audience = $"https://{domain}/me/",
                Scope = "create:me:connected_accounts"
            });
        })
    });
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization(); 

builder.Services.AddSingleton<ExpenseReportService>();
builder.Services.AddSingleton<FgaService>();

builder.Services.AddScoped<GmailLinkContext>();
builder.Services.AddSingleton<EmailService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var expenseService = scope.ServiceProvider.GetRequiredService<ExpenseReportService>();
    await expenseService.EnsureCollectionAsync();

    await expenseService.IngestAsync(new ExpenseReport
    {
        Id = "exp-001", EmployeeEmail = "alice@example.com",
        Amount = 450.00m, Merchant = "Marriott Hotels",
        Justification = "Client dinner during NYC conference", SubmittedAt = "2026-06-10"
    });
    await expenseService.IngestAsync(new ExpenseReport
    {
        Id = "exp-002", EmployeeEmail = "bob@example.com",
        Amount = 0m, Merchant = "Delta Airlines",
        Justification = "", SubmittedAt = "2026-06-12"
    });
    await expenseService.IngestAsync(new ExpenseReport
    {
        Id = "exp-003", EmployeeEmail = "carol@example.com",
        Amount = 120.50m, Merchant = "",
        Justification = "Team lunch", SubmittedAt = "2026-06-14"
    });
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapGet("/account/login", async (HttpContext httpContext, string returnUrl = "/") =>
{
  var authenticationProperties = new LoginAuthenticationPropertiesBuilder()
          .WithRedirectUri(returnUrl)
          .Build();

  await httpContext.ChallengeAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
});

app.MapGet("/account/logout", async (HttpContext httpContext) =>
{
  var authenticationProperties = new LogoutAuthenticationPropertiesBuilder()
          .WithRedirectUri("/")
          .Build();

  await httpContext.SignOutAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
  await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
});

app.MapGet("/api/internalData", () =>
{
    var data = Enumerable.Range(1, 5).Select(index =>
        Random.Shared.Next(1, 100))
        .ToArray();

    return data;
})
.RequireAuthorization();

app.MapPost("/api/send-missing-data-email", async (
    SendMissingDataEmailRequest request,
    HttpContext httpContext,
    EmailService emailService) =>
{
    var token = await httpContext.GetAccessTokenForConnectionAsync(
        new AccessTokenForConnectionRequest { Connection = "google-oauth2" });

    if (token is null)
    {
        return Results.Conflict(new { needsLink = true });
    }

    await emailService.SendAsync(request.EmployeeEmail, request.Subject, request.Body, token);
    return Results.Ok();
}).RequireAuthorization();

//👇new code
app.MapGet("/Account/ConnectGoogle", async (
    HttpContext ctx,
    IConfiguration config,
    MyAccountClient myAccountClient,
    IDistributedCache cache) =>
{
    // Initiate a connected account flow using the Auth0 My Account client.
    var correlationId = Guid.NewGuid().ToString("N");
    var redirectUri = $"{ctx.Request.Scheme}://{ctx.Request.Host}/Account/ConnectGoogleCallback?cid={correlationId}";

    try
    {
        var response = await myAccountClient.ConnectedAccounts.CreateAsync(
            new CreateConnectedAccountsRequestContent
            {
                Connection = "google-oauth2",
                RedirectUri = redirectUri,
                State = correlationId
            }
        );

        // Cache the minimal pending state so the callback/complete flow can resume.
        await cache.SetStringAsync(
            $"connect:{correlationId}",
            JsonSerializer.Serialize(new { auth_session = response.AuthSession, redirect_uri = redirectUri }),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return Results.Redirect($"{response.ConnectUri}?ticket={response.ConnectParams.Ticket}");
    }
    catch
    {
        return Results.Redirect("/chat");
    }
}).RequireAuthorization();

app.MapGet("/Account/ConnectGoogleCallback", (string cid) => Results.Content($$"""
    <!DOCTYPE html>
    <html>
    <body>
      <script>
        var connectCode = new URLSearchParams(window.location.hash.substring(1)).get('connect_code');
        fetch('/Account/ConnectGoogleComplete', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ cid: '{{cid}}', connectCode: connectCode })
        }).then(() => { window.location.href = '/chat'; });
      </script>
      Finishing Gmail authorization&hellip;
    </body>
    </html>
    """, "text/html")).RequireAuthorization();

app.MapPost("/Account/ConnectGoogleComplete", async (
    HttpRequest req,
    HttpContext ctx,
    IConfiguration config,
    MyAccountClient myAccountClient,
    IDistributedCache cache) =>
{
    // Read JSON payload manually to avoid requiring a dedicated request type.
    using var sr = new StreamReader(req.Body);
    var body = await sr.ReadToEndAsync();
    if (string.IsNullOrEmpty(body)) return Results.BadRequest();

    var doc = JsonSerializer.Deserialize<JsonElement>(body);
    if (!doc.TryGetProperty("cid", out var cidProp) || !doc.TryGetProperty("connectCode", out var codeProp))
    {
        return Results.BadRequest();
    }

    var cid = cidProp.GetString()!;
    var connectCode = codeProp.GetString()!;

    var pendingJson = await cache.GetStringAsync($"connect:{cid}");
    if (pendingJson is null)
    {
        return Results.BadRequest();
    }

    var pending = JsonSerializer.Deserialize<JsonElement>(pendingJson);
    var authSession = pending!.GetProperty("auth_session").GetString();
    var redirectUri = pending.GetProperty("redirect_uri").GetString();

    try
    {
        await myAccountClient.ConnectedAccounts.CompleteAsync(
            new CompleteConnectedAccountsRequestContent
            {
                AuthSession = authSession!,
                ConnectCode = connectCode,
                RedirectUri = redirectUri!
            }
        );

        await cache.RemoveAsync($"connect:{cid}");
        return Results.Ok();
    }
    catch
    {
        return Results.BadRequest();
    }
}).RequireAuthorization();
//👆new 

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ExpenseAgent.Client._Imports).Assembly);

app.Run();

public record SendMissingDataEmailRequest(string EmployeeEmail, string Subject, string Body);
