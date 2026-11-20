using Oracle.Harris.Mcp.Tools;
using Oracle.Harris.Storage;
using Oracle.Harris.Storage.Options;

var builder = WebApplication.CreateBuilder(args);

var duckDbPath = builder.Configuration["DUCKDB_PATH"] ?? "data/harris-tx/harris-tx.duckdb";
builder.Services.AddOracleHarrisStorage(o => o.DatabasePath = duckDbPath);

builder.Services.AddSingleton<HarrisMcpTools>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<HarrisMcpTools>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<Oracle.Harris.Storage.Schema.DuckDbSchemaMigrator>();
    migrator.Migrate();
}

app.MapMcp("/mcp");
app.MapGet("/", () => Results.Ok(new { county = "harris-tx", mcp = "/mcp" }));

app.Run();