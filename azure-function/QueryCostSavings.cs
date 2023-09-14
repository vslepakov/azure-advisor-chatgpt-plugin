using Azure.ResourceManager;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using System.Net;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure.ResourceManager.ResourceGraph;
using Extensions;
using Microsoft.Extensions.Caching.Memory;
using Azure.ResourceManager.Resources;

namespace AzureAdvisorPlugin;

public class QueryCostSavings
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<QueryCostSavings> _logger;
    private readonly ArmClient _client;

    private const string CostOptimizationQuery = @"
                AdvisorResources
                | where type == 'microsoft.advisor/recommendations'
                | where properties.category == 'Cost'
                | extend
	                resources = tostring(properties.resourceMetadata.resourceId),
	                savings = todouble(properties.extendedProperties.savingsAmount),
	                solution = tostring(properties.shortDescription.solution),
	                currency = tostring(properties.extendedProperties.savingsCurrency)
                | summarize
	                dcount(resources),
	                bin(sum(savings), 0.01)
	                by solution, currency
                | project solution, dcount_resources, sum_savings, currency
                | order by sum_savings desc";

    public QueryCostSavings(ILoggerFactory loggerFactory, ArmClient client, IMemoryCache memoryCache)
    {
        _logger = loggerFactory.CreateLogger<QueryCostSavings>();
        _client = client;
        _memoryCache = memoryCache;
    }

    [Function("QueryCostSavings")]
    [OpenApiOperation(operationId: "QueryCostSavings", tags: new[] { "ExecuteFunction" }, Description = "Queries and returns information about potential cost savings.")]
    [OpenApiParameter(name: "subscriptionId", Description = "Azure Subscription Id", Required = true, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns information about potential cost saving")]
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

        if (_memoryCache.TryGetValue(subscriptionId, out TenantResource tenant))
        {
            _logger.LogInformation($"Cache hit for subscriptionId {subscriptionId}. Tenant is {tenant.Data.TenantId}");
        }
        else
        {
            var tenants = await _client.GetTenants().ToListAsync();
            var tenantForSubscription = tenants.FirstOrDefault(t =>
            {
                var sub = t.GetSubscription(subscriptionId);
                return sub.Value.HasData && sub.Value.Data.SubscriptionId.Contains(subscriptionId);
            });

            if (tenantForSubscription == null)
            {
                _logger.LogError($"No tenant found for subscription id provided in the request!");
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "No tenant found for subscription id provided in the request!");
            }

            tenant = tenantForSubscription;
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                Size = 1024
            };

            _memoryCache.Set(subscriptionId, tenant, cacheEntryOptions);
        }

        var queryContent = new ResourceQueryContent(CostOptimizationQuery);
        queryContent.Subscriptions.Add(subscriptionId);

        var result = await tenant.GetResourcesAsync(queryContent).ConfigureAwait(false);
        return await req.CreateTextResponseAsync(result.Value.Data.ToString()).ConfigureAwait(false);
    }
}
