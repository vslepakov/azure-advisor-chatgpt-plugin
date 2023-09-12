using System.Net;
using AIPlugins.AzureFunctions.Extensions;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Advisor;
using Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Skills.Core;
using Microsoft.SemanticKernel.Text;

namespace AzureAdvisorPlugin;

public class QueryRecommendations
{
    private readonly ILogger _logger;
    private readonly ArmClient _client;
    private readonly IKernel _kernel;
    private readonly IAIPluginRunner _pluginRunner;

    private const int MaxTokens = 1024;
    private const int MaxFileSize = 2048;

    public QueryRecommendations(IKernel kernel, IAIPluginRunner pluginRunner, ArmClient client)
    {
        _kernel = kernel;
        _pluginRunner = pluginRunner;
        _logger = _kernel.LoggerFactory.CreateLogger<QueryRecommendations>();
        _client = client;
    }

    [Function("QueryRecommendations")]
    [OpenApiOperation(operationId: "QueryRecommendations", tags: new[] { "ExecuteFunction" }, Description = "Queries and summarizes recommendations.")]
    [OpenApiParameter(name: "subscriptionId", Description = "Azure Subscription Id", Required = true, In = ParameterLocation.Query)]
    [OpenApiRequestBody(contentType: "text/plain", bodyType: typeof(string), Description = "A detailed question about Azure Advisor recommendations; provide all the context the user provided to get the best results.", Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns a summary of Azure Advisor recommendations.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        var prompt = await req.ReadAsStringAsync();
        var subscriptionId = req.Query["subscriptionId"];

        if (string.IsNullOrEmpty(prompt))
        {
            _logger.LogError($"No user input provided in the request!");
            return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Please pass your user input in the body of the request");
        }

        if (string.IsNullOrEmpty(subscriptionId))
        {
            _logger.LogError($"No subscription id provided in the request! The prompt is {prompt}");
            return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Please pass your subscriptionId in the query string");
        }

        _logger.LogInformation($"Processing request for subscription: {subscriptionId}");

        if(await EmbeddingsCacheEmptyAsync(subscriptionId).ConfigureAwait(false))
        {
            _logger.LogInformation($"Embeddings cache empty for subscription: {subscriptionId}");

            var recommendations = await DownloadRecommendationsAsync(subscriptionId).ConfigureAwait(false);
            await SummarizeRecommendationsForSubscriptionAsync(subscriptionId, recommendations).ConfigureAwait(false);
        }

        var context = new ContextVariables(prompt)
        {
            [TextMemorySkill.CollectionParam] = subscriptionId,
            [TextMemorySkill.RelevanceParam] = "0.7",
            [TextMemorySkill.LimitParam] = "20",
            ["input"] = prompt,
        };

        return await _pluginRunner.RunAIPluginOperationAsync(req, "MemoryQuery", context).ConfigureAwait(false);
    }

    private async Task<List<ResourceRecommendationBaseResource>> DownloadRecommendationsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var scopeId = new ResourceIdentifier($"/subscriptions/{subscriptionId}");
        var collection = _client.GetResourceRecommendationBases(scopeId);

        _logger.LogInformation($"Downloading Azure Advisor recommendation for subscription: {subscriptionId}");

        return await collection.GetAllAsync(cancellationToken: cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SummarizeRecommendationsForSubscriptionAsync(string subscriptionId, IEnumerable<ResourceRecommendationBaseResource> recommendations,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Building embeddings cache from Azure Advisor recommendations for subscription: {subscriptionId}");

        var text = ConvertToPlainText(recommendations);

        if (text.Length > MaxFileSize)
        {
            var lines = TextChunker.SplitPlainTextLines(text, MaxTokens);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, MaxTokens);

            for (int i = 0; i < paragraphs.Count; i++)
            {
                await _kernel.Memory.SaveInformationAsync(
                    $"{subscriptionId}",
                    text: $"{paragraphs[i]}",
                    id: $"{subscriptionId}_{i}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await _kernel.Memory.SaveInformationAsync(
                $"{subscriptionId}",
                text: $"{text}",
                id: $"{subscriptionId}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private string ConvertToPlainText(IEnumerable<ResourceRecommendationBaseResource> recommendations)
    {
        var lines = new List<string>();

        foreach (var recommendation in recommendations)
        {
            var data = recommendation.Data;
            var recommendationMessage = data.ExtendedProperties.SingleOrDefault(kv => kv.Key == "recommendationMessage");
            var annualSavings = data.ExtendedProperties.SingleOrDefault(kv => kv.Key == "annualSavingsAmount");

            lines.Add($"Affected Resource: {data.ImpactedValue}, " +
                $"Resource Type: {data.ImpactedField}, " +
                $"Problem: {data.ShortDescription.Problem}, " +
                $"Solution: {data.ShortDescription.Solution}, " +
                $"Impact: {data.Impact}, " +
                $"Category: {data.Category}, " +
                $"Last Updated: {data.LastUpdated}, " +
                $"Recommendation Message: {recommendationMessage}, " +
                $"{Environment.NewLine}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task<bool> EmbeddingsCacheEmptyAsync(string subscriptionId)
    {
        // TODO: Create a more sophisticated/efficient cache (in)validation strategy. Consider using timestamps etc.
        var collectionNames = await _kernel.Memory.GetCollectionsAsync().ConfigureAwait(false);
        return !collectionNames.Any(c => c.StartsWith(subscriptionId));
    }
}