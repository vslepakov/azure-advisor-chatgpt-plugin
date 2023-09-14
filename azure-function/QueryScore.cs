using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using Extensions;
using Azure.Core;
using Models;
using Newtonsoft.Json;

namespace AzureAdvisorPlugin;

public class QueryScore
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<QueryScore> _logger;
    private readonly HttpClient _client;

    public QueryScore(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
    {
        _logger = loggerFactory.CreateLogger<QueryScore>();
        _client = httpClientFactory.CreateClient(nameof(QueryScore));
        _memoryCache = memoryCache;
    }

    [Function("QueryScore")]
    [OpenApiOperation(operationId: "QueryScore", tags: new[] { "ExecuteFunction" }, Description = "Queries and return the Azure Advisor score for categories: Cost, Security, High Availability, Operational Excellence and Performance")]
    [OpenApiParameter(name: "subscriptionId", Description = "Azure Subscription Id", Required = true, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns information Azure Advisor scores for different categories")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
    {
        var subscriptionId = req.Query["subscriptionId"];

        if (subscriptionId == null)
        {
            _logger.LogError($"No subscription id provided in the request!");
            return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Please pass your subscriptionId in the query string");
        }

        _logger.LogInformation($"Processing request for subscription: {subscriptionId}");

        if (_memoryCache.TryGetValue(subscriptionId, out IList<AzureAdvisorScore> azureAdvisorScores))
        {
            _logger.LogInformation($"Cache hit for subscriptionId {subscriptionId}");
        }
        else
        {
            var result = await _client.GetAsync($"/subscriptions/{subscriptionId}/providers/Microsoft.Advisor/advisorScore?api-version=2023-01-01").ConfigureAwait(false);

            if (result.IsSuccessStatusCode)
            {
                var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                azureAdvisorScores = ConvertToAzureAdvisorScores(content);

                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(10),
                    Size = 1024
                };

                _memoryCache.Set(subscriptionId, azureAdvisorScores, cacheEntryOptions);
            }
        }

        return await req.CreateTextResponseAsync(JsonConvert.SerializeObject(azureAdvisorScores)).ConfigureAwait(false);
    }

    private IList<AzureAdvisorScore> ConvertToAzureAdvisorScores(string content)
    {
        var jsonData = JsonConvert.DeserializeObject<dynamic>(content);
        var advisorScores = new List<AzureAdvisorScore>();

        if(jsonData != null)
        {
            foreach (var data in jsonData.value)
            {
                // Unfortunately the API returns some unknown categories with GUIDs for names
                if (AzureAdvisorScore.ValidCategories.Contains(data.name.ToString()))
                {
                    var lastRefreshedScore = data.properties.lastRefreshedScore;
                    advisorScores.Add(new AzureAdvisorScore(
                        data.name.ToString(),
                        DateTime.Parse(lastRefreshedScore.date.ToString()),
                        (decimal)lastRefreshedScore.score,
                        (decimal)lastRefreshedScore.potentialScoreIncrease,
                        (int)lastRefreshedScore.impactedResourceCount));
                }
            }
        }

        return advisorScores;
    }
}
