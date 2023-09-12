using System.Collections.ObjectModel;

namespace Models;

public record AzureAdvisorScore(string Category, DateTime LastRefreshed, decimal Score, decimal PotentialScoreIncrease, int ImpactedResourceCount)
{
    public static ReadOnlyCollection<string> ValidCategories => new(new List<string>
    {
        "Security",
        "OperationalExcellence",
        "Cost",
        "HighAvailability",
        "Performance",
        "Advisor"
    });
};
