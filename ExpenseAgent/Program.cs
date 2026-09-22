using ExpenseAgent.Client.Pages;
using ExpenseAgent.Components;
using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using ExpenseAgent.Agent;
using ExpenseAgent.Services;
using ExpenseAgent.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCascadingAuthenticationState();

builder.Services
    .AddAuth0WebAppAuthentication(options => {
      options.Domain = builder.Configuration["Auth0:Domain"]!;
      options.ClientId = builder.Configuration["Auth0:ClientId"]!;
    });

builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<ExpenseAgentService>();

builder.Services.AddHttpClient();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization(); 

builder.Services.AddSingleton<ExpenseReportService>();
builder.Services.AddSingleton<FgaService>();

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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ExpenseAgent.Client._Imports).Assembly);

app.Run();
