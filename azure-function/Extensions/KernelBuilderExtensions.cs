using Microsoft.SemanticKernel;
using Models;

internal static class KernelBuilderExtensions
{
    /// <summary>
    /// Adds a chat completion service to the list. It can be either an OpenAI or Azure OpenAI backend service.
    /// </summary>
    /// <param name="kernelBuilder"></param>
    /// <param name="kernelSettings"></param>
    /// <exception cref="ArgumentException"></exception>
    internal static KernelBuilder WithChatCompletionService(this KernelBuilder kernelBuilder, KernelSettings kernelSettings)
    {
        switch (kernelSettings.ServiceType.ToUpperInvariant())
        {
            case ServiceTypes.AzureOpenAI:
                kernelBuilder.WithAzureChatCompletionService(deploymentName: kernelSettings.ChatCompletionDeploymentOrModelId, endpoint: kernelSettings.Endpoint, apiKey: kernelSettings.ApiKey, serviceId: kernelSettings.ServiceId);
                break;

            case ServiceTypes.OpenAI:
                kernelBuilder.WithOpenAIChatCompletionService(modelId: kernelSettings.ChatCompletionDeploymentOrModelId, apiKey: kernelSettings.ApiKey, orgId: kernelSettings.OrgId, serviceId: kernelSettings.ServiceId);
                break;

            default:
                throw new ArgumentException($"Invalid service type value: {kernelSettings.ServiceType}");
        }

        return kernelBuilder;
    }

    internal static KernelBuilder WithTextEmbeddingGenerationService(this KernelBuilder kernelBuilder, KernelSettings kernelSettings)
    {
        switch (kernelSettings.ServiceType.ToUpperInvariant())
        {
            case ServiceTypes.AzureOpenAI:
                kernelBuilder.WithAzureTextEmbeddingGenerationService(deploymentName: kernelSettings.TextEmbeddingGenerationDeploymentOrModelId, endpoint: kernelSettings.Endpoint, apiKey: kernelSettings.ApiKey, serviceId: kernelSettings.ServiceId);
                break;

            case ServiceTypes.OpenAI:
                kernelBuilder.WithOpenAITextEmbeddingGenerationService(modelId: kernelSettings.TextEmbeddingGenerationDeploymentOrModelId, apiKey: kernelSettings.ApiKey, orgId: kernelSettings.OrgId, serviceId: kernelSettings.ServiceId);
                break;

            default:
                throw new ArgumentException($"Invalid service type value: {kernelSettings.ServiceType}");
        }

        return kernelBuilder;
    }
}
