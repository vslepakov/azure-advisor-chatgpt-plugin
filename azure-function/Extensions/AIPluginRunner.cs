// Copyright (c) Microsoft. All rights reserved.

using System.Net;
using System.Threading.Tasks;
using Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Models;

namespace AIPlugins.AzureFunctions.Extensions;

public class AIPluginRunner : IAIPluginRunner
{
    private readonly ILogger<AIPluginRunner> _logger;
    private readonly IKernel _kernel;

    public AIPluginRunner(IKernel kernel, ILoggerFactory loggerFactory)
    {
        this._kernel = kernel;
        this._logger = loggerFactory.CreateLogger<AIPluginRunner>();
    }


    /// <summary>
    /// Runs a semantic function using the operationID and returns back an HTTP response.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="operationId"></param>
    /// <param name="contextVariables"></param>
    public async Task<HttpResponseData> RunAIPluginOperationAsync(HttpRequestData req, string operationId, ContextVariables contextVariables)
    {
        var appSettings = AppSettings.LoadSettings();

        if (!_kernel.Skills.TryGetFunction(
            skillName: appSettings.AIPlugin.NameForModel,
            functionName: operationId,
            out ISKFunction? function))
        {
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await errorResponse.WriteStringAsync($"Function {operationId} not found").ConfigureAwait(false);
            return errorResponse;
        }

        var result = await _kernel.RunAsync(contextVariables, function).ConfigureAwait(false);
        if (result.ErrorOccurred)
        {
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            string? message = result?.LastException?.Message;
            if (message != null)
            {
                await errorResponse.WriteStringAsync(message).ConfigureAwait(false);
            }
            return errorResponse;
        }

        return await req.CreateTextResponseAsync(result.Result).ConfigureAwait(false);
    }
}
