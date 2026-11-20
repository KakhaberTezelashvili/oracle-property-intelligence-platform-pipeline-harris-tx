using System.Text;
using Microsoft.Extensions.AI;
using Oracle.Harris.Domain;
using Oracle.Harris.Storage.Repositories;

namespace Oracle.Harris.Web.Services;

public sealed class DemoChatService(
    IChatClient? chatClient,
    DuckDbQueryRepository queryRepository,
    RoofingLeadQueryRepository roofingRepository,
    ILogger<DemoChatService> logger)
{
    public async Task<string> AnswerAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        var context = BuildLocalContext(userMessage);

        if (chatClient is null)
        {
            return context;
        }

        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, "You are the Harris County Oracle demo assistant. Use only the provided evidence."),
                    new ChatMessage(ChatRole.User, $"{userMessage}\n\nEvidence:\n{context}"),
                ],
                cancellationToken: cancellationToken);
            return response.Text ?? context;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM chat failed; returning local evidence");
            return context;
        }
    }

    private string BuildLocalContext(string message)
    {
        var lower = message.ToLowerInvariant();
        var sb = new StringBuilder();
        sb.AppendLine($"County slug: {CountyConstants.Slug}");

        if (lower.Contains("pipeline") || lower.Contains("run"))
        {
            sb.AppendLine("Recent runs:");
            foreach (var run in queryRepository.ListPipelineRuns(5))
            {
                sb.AppendLine($"- {run.RunId} status={run.Status} counts={run.CountsJson}");
            }
        }

        if (lower.Contains("roof") && (lower.Contains("15") || lower.Contains("older")))
        {
            var rows = roofingRepository.SearchByRoofAge(CountyConstants.DefaultRoofAgeThresholdYears, 10);
            sb.AppendLine(queryRepository.SerializeRows(rows));
        }

        if (lower.Contains("permit"))
        {
            var rows = roofingRepository.SearchLongOpenRoofingPermits(365, 10);
            sb.AppendLine(queryRepository.SerializeRows(rows));
        }

        if (lower.Contains("schema") || lower.Contains("duckdb"))
        {
            sb.AppendLine(string.Join('\n', queryRepository.GetPropertyQuerySchema()));
        }

        if (sb.Length == CountyConstants.Slug.Length + 20)
        {
            sb.AppendLine("Ask about pipeline runs, roof age, permits, or DuckDB schema.");
        }

        return sb.ToString();
    }
}