using System.Collections.Generic;
using System.Text;

public static class SectorValidator
{
    public static bool ValidateNoCycles(
        IEnumerable<SectorDefinition> sectors,
        out string error)
    {
        if (sectors == null)
        {
            error = "Sector validation failed: definition collection is null.";
            return false;
        }

        var visiting = new HashSet<SectorDefinition>();
        var visited = new HashSet<SectorDefinition>();
        var path = new List<SectorDefinition>();
        foreach (SectorDefinition sector in sectors)
        {
            if (!Visit(sector, visiting, visited, path, out error))
                return false;
        }

        error = null;
        return true;
    }

    private static bool Visit(
        SectorDefinition sector,
        HashSet<SectorDefinition> visiting,
        HashSet<SectorDefinition> visited,
        List<SectorDefinition> path,
        out string error)
    {
        if (sector == null)
        {
            error = "Sector validation failed: definition collection contains null.";
            return false;
        }
        if (visited.Contains(sector))
        {
            error = null;
            return true;
        }
        if (!visiting.Add(sector))
        {
            error = BuildCycleError(path, sector);
            return false;
        }

        path.Add(sector);
        var uniquePrerequisites = new HashSet<SectorDefinition>();
        if (sector.PrerequisiteSectors != null)
        {
            foreach (SectorDefinition prerequisite in sector.PrerequisiteSectors)
            {
                if (prerequisite == null)
                {
                    error = $"Sector validation failed: '{sector.name}' contains a null prerequisite.";
                    return false;
                }
                if (!uniquePrerequisites.Add(prerequisite))
                {
                    error = $"Sector validation failed: '{sector.name}' contains duplicate prerequisite '{prerequisite.name}'.";
                    return false;
                }
                if (!Visit(prerequisite, visiting, visited, path, out error))
                    return false;
            }
        }

        path.RemoveAt(path.Count - 1);
        visiting.Remove(sector);
        visited.Add(sector);
        error = null;
        return true;
    }

    private static string BuildCycleError(List<SectorDefinition> path, SectorDefinition repeated)
    {
        int start = path.IndexOf(repeated);
        if (start < 0)
            start = 0;
        var builder = new StringBuilder("Sector dependency cycle: ");
        for (int i = start; i < path.Count; i++)
        {
            if (i > start)
                builder.Append(" -> ");
            builder.Append(path[i].name);
        }
        builder.Append(" -> ");
        builder.Append(repeated.name);
        return builder.ToString();
    }
}
