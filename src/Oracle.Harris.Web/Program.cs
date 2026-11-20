using Microsoft.Extensions.AI;
using OpenAI;
using Oracle.Harris.Ingest;
using Oracle.Harris.Publish;
using Oracle.Harris.Storage;
using Oracle.Harris.Storage.Schema;
using Oracle.Harris.Web.Components;
using Oracle.Harris.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var duckDbPath = HarrisPaths.ResolveFromRepositoryRoot(
    builder.Configuration["DUCKDB_PATH"]
    ?? Environment.GetEnvironmentVariable("DUCKDB_PATH")
    ?? "data/harris-tx/harris-tx.duckdb");
builder.Services.AddOracleHarrisStorage(o => o.DatabasePath = duckDbPath);
builder.Services.AddOracleHarrisIngest(o =>
{
    o.TempDataRoot = HarrisPaths.ResolveFromRepositoryRoot(
        Environment.GetEnvironmentVariable("TEMPDATA_ROOT") ?? "tempdata");
});
builder.Services.AddOracleHarrisPublish();
builder.Services.AddSingleton<DemoChatService>();

var openAiKey = builder.Configuration["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = builder.Configuration["OPENAI_CHAT_MODEL"] ?? "gpt-4o-mini";
if (!string.IsNullOrWhiteSpace(openAiKey))
{
    builder.Services.AddChatClient(sp =>
    {
        var client = new OpenAIClient(openAiKey);
        return client.GetChatClient(model).AsIChatClient();
    });
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DuckDbSchemaMigrator>().Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/api/chat", async (ChatRequest request, DemoChatService chat, CancellationToken ct) =>
{
    var answer = await chat.AnswerAsync(request.Message, request.EvidenceOnly, ct);
    return Results.Ok(new ChatResponse(answer));
});

app.Run();

file sealed record ChatRequest(string Message, bool EvidenceOnly = true);

file sealed record ChatResponse(string Answer);