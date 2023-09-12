using Microsoft.Extensions.Logging;

namespace Models;

#pragma warning disable CA1812
public class KernelSettings
{
    public string ServiceType { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string ChatCompletionDeploymentOrModelId { get; set; } = string.Empty;
    public string TextEmbeddingGenerationDeploymentOrModelId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
    public LogLevel? LogLevel { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}
