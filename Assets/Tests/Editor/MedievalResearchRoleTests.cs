using NUnit.Framework;

public sealed class MedievalResearchRoleTests
{
    [Test]
    public void MedievalResearchCoreNodesKeepDistinctEffectRoles()
    {
        AssertResearchRole("Fortification", ResearchEffectType.GlobalConstructionMultiplier);
        AssertResearchRole("GuildSystem", ResearchEffectType.GlobalResearchMultiplier);
        AssertResearchRole("GunpowderSupplyStandards", ResearchEffectType.GlobalLogisticsMultiplier);
        AssertResearchRole("PermanentLaborOrganization", ResearchEffectType.PopulationProductivityMultiplier);
    }

    private static void AssertResearchRole(string id, ResearchEffectType expectedType)
    {
        ResearchDef research = DataBase<ResearchDef>.Find(id);
        Assert.That(research, Is.Not.Null, id);
        Assert.That(HasEffectType(research, expectedType), Is.True,
            id + " must retain its player-facing effect role.");
    }

    private static bool HasEffectType(ResearchDef research, ResearchEffectType expectedType)
    {
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null && effect.Type == expectedType)
                return true;
        }
        return false;
    }
}
