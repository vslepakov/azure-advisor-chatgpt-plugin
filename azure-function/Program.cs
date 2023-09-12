// Copyright (c) Microsoft. All rights reserved.

using AIPlugins.AzureFunctions.Extensions;
using Azure.Identity;
using Azure.ResourceManager;
using AzureAdvisorPlugin;
using Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Skills.Core;
using Models;

const string DefaultSemanticFunctionsFolder = "Prompts";
string semanticFunctionsFolder = Environment.GetEnvironmentVariable("SEMANTIC_SKILLS_FOLDER") ?? DefaultSemanticFunctionsFolder;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        _ = services
            .AddSingleton<IMemoryStore, VolatileMemoryStore>()
            .AddScoped<IKernel>((providers) =>
            {
                // This will be called each time a new Kernel is needed

                // Get a logger instance
                var loggerFactory = providers.GetRequiredService<ILoggerFactory>();

                // Register your AI Providers...
                var appSettings = AppSettings.LoadSettings();
                var kernel = new KernelBuilder()
                    .WithChatCompletionService(appSettings.Kernel)
                    .WithTextEmbeddingGenerationService(appSettings.Kernel)
                    .WithLoggerFactory(loggerFactory)
                    .WithMemoryStorage((lf, kc) =>
                    {
                        return providers.GetRequiredService<IMemoryStore>();

                    })
                    .Build();

                // Load your semantic functions...
                kernel.ImportPromptsFromDirectory(appSettings.AIPlugin.NameForModel, semanticFunctionsFolder);
                kernel.ImportSkill(new TextMemorySkill(kernel.Memory));

                return kernel;
            })
            .AddScoped<IAIPluginRunner, AIPluginRunner>()
            .AddScoped(_ => new ArmClient(new DefaultAzureCredential()))
            .AddMemoryCache(o => o.SizeLimit = 10240)
            .AddHttpClient<QueryScore>((serviceProvider, httpClient) =>
            {
                httpClient.BaseAddress = new Uri("https://management.azure.com");
            }).AddHttpMessageHandler(services => new DefaultAzureCredentialsAuthorizationMessageHandler());
    })
    .Build();

host.Run();
