using System.Text;
using System.Text.Json;
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
    public async Task<string> AnswerAsync(string userMessage, bool evidenceOnly = false, CancellationToken cancellationToken = default)
    {
        var context = BuildLocalContext(userMessage);

        if (evidenceOnly || chatClient is null)
        {
            return context;
        }

        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System,
                        "You are the Harris County Oracle demo assistant. Use ONLY the evidence block. " +
                        "If radius/miles is asked and evidence mentions no coordinates, say radius cannot be applied. " +
                        "Cite request_identifier and roof_age_years from evidence; do not invent addresses."),
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
        sb.AppendLine($"County: {CountyConstants.Slug}");
        sb.AppendLine();

        if (lower.Contains("mile") || lower.Contains("radius") || lower.Contains("gps"))
        {
            sb.AppendLine("⚠ Radius not applied: no latitude/longitude in the current HCAD PDATA load (GIS layer pending).");
            sb.AppendLine();
        }

        if (lower.Contains("pipeline") || lower.Contains("run"))
        {
            sb.AppendLine("Recent pipeline runs:");
            foreach (var run in queryRepository.ListPipelineRuns(5))
            {
                sb.AppendLine($"  • {run.RunId} ({run.Status}) — {run.CountsJson}");
            }

            sb.AppendLine();
        }

        if (lower.Contains("roof") && (lower.Contains("15") || lower.Contains("older") || lower.Contains("age")))
        {
            var rows = roofingRepository.SearchByRoofAge(CountyConstants.DefaultRoofAgeThresholdYears, 15);
            sb.AppendLine($"Properties with roof_age_years >= {CountyConstants.DefaultRoofAgeThresholdYears} (top 15, source: v_property_leads):");
            sb.AppendLine(FormatRows(rows));
            sb.AppendLine();
        }

        if (lower.Contains("permit"))
        {
            var rows = roofingRepository.SearchLongOpenRoofingPermits(365, 15);
            sb.AppendLine("Long-open roofing-related permits (HCAD, top 15):");
            sb.AppendLine(FormatRows(rows));
            sb.AppendLine();
        }

        if (lower.Contains("schema") || lower.Contains("duckdb"))
        {
            sb.AppendLine("v_property_leads columns:");
            foreach (var col in queryRepository.GetPropertyQuerySchema())
            {
                sb.AppendLine($"  {col}");
            }

            sb.AppendLine();
        }

        if (sb.Length < 80)
        {
            sb.AppendLine("Ask about pipeline runs, roof age (15+ years), permits, or DuckDB schema.");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatRows(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0)
        {
            return "  (no rows)";
        }

        var slim = rows.Select(r => new
        {
            request_identifier = r.GetValueOrDefault("request_identifier"),
            roof_age_years = r.GetValueOrDefault("roof_age_years"),
            owner_name = r.GetValueOrDefault("owner_name"),
            days_open = r.GetValueOrDefault("days_open"),
            contractor_name = r.GetValueOrDefault("contractor_name"),
            bbb_rating = r.GetValueOrDefault("bbb_rating"),
        });

        return JsonSerializer.Serialize(slim, new JsonSerializerOptions { WriteIndented = true });
    }
}