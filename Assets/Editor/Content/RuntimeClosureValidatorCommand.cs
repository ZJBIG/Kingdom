using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RuntimeClosureValidatorCommand
{
    public static void RunFromCommandLine()
    {
        try
        {
            IReadOnlyList<ResourceDef> resources = DataBase<ResourceDef>.All;
            IReadOnlyList<BuildingDef> buildings = DataBase<BuildingDef>.All;
            IReadOnlyList<ResearchDef> researches = DataBase<ResearchDef>.All;
            IReadOnlyList<WorkshopUpgradeDef> upgrades = DataBase<WorkshopUpgradeDef>.All;
            Debug.Log($"Runtime closure input: resources={resources.Count}, buildings={buildings.Count}, research={researches.Count}, workshops={upgrades.Count}");
            if (!EconomyDependencyValidator.Validate(resources, buildings, researches, upgrades, out string error))
                throw new InvalidOperationException(error);
            Debug.Log("RUNTIME_CLOSURE_VALIDATION_PASS");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(2);
            return;
        }
        EditorApplication.Exit(0);
    }
}
