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

    [Test]
    public void EveryMedievalResearchHasAConcreteEffect()
    {
        string[] ids =
        {
            "Bookmaking",
            "FeudalAdministration",
            "Fortification",
            "GuildSystem",
            "GunpowderSupplyStandards",
            "MechanicalEngineering",
            "PublicHealth",
            "ScholasticInstitutions",
            "PermanentLaborOrganization",
            "Steelmaking",
            "TradeRoutes",
            "UrbanHousing"
        };

        for (int i = 0; i < ids.Length; i++)
        {
            Research research = DataBase<Research>.Find(ids[i]);
            Assert.That(research, Is.Not.Null, ids[i]);
            Assert.That(research.Effects, Is.Not.Null.And.Not.Empty, ids[i]);
        }
    }

    private static void AssertResearchRole(string id, ResearchEffectType expectedType)
    {
        Research research = DataBase<Research>.Find(id);
        Assert.That(research, Is.Not.Null, id);
        Assert.That(HasEffectType(research, expectedType), Is.True,
            id + " must retain its player-facing effect role.");
    }

    private static bool HasEffectType(Research research, ResearchEffectType expectedType)
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
