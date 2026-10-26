public static class ProgressionVisibilityRules
{
    public const string ProductionResearchId = "FactoryOrganization";
    public const string EraResearchId = "CivilizationPlanning";

    public static bool IsResearchUnlocked(GameState state) => state != null && state.HasBuiltHousing;

    public static bool IsProductionUnlocked(ResearchManager research) =>
        research != null && research.IsResearchCompleted(ProductionResearchId);

    public static bool IsEraUnlocked(ResearchManager research) =>
        research != null && research.IsResearchCompleted(EraResearchId);
}
