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
            IReadOnlyList<Resource> resources = DataBase<Resource>.All;
            IReadOnlyList<Building> buildings = DataBase<Building>.All;
            IReadOnlyList<Research> researches = DataBase<Research>.All;
            IReadOnlyList<WorkshopUpgrade> upgrades = DataBase<WorkshopUpgrade>.All;
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
