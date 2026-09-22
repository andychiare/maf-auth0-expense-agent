var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerService();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("approve:expenses", policy =>
        policy.RequireClaim("scope", "approve:expenses"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles();
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.RequireAuthorization()
.AddOpenApiOperationTransformer((opperation, context, ct) => { return Task.CompletedTask;});

var statuses = new Dictionary<string, ExpenseStatusUpdate>();

app.MapPost("/expense/{id}/status", (string id, ExpenseStatusUpdate update) =>
{
    statuses[id] = update;
    return Results.NoContent();
})
.RequireAuthorization("approve:expenses");

app.MapGet("/expense/{id}/status", (string id) =>
    statuses.TryGetValue(id, out var update) ? Results.Ok(update) : Results.NotFound());

app.MapFallbackToFile("index.html");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record ExpenseStatusUpdate(string Status, DateTimeOffset Timestamp, string ManagerId);