using System.Collections.Generic;
using System.Text;

public static class ResearchValidator
{
    public static bool ValidateNoCycles(IEnumerable<Research> researches, out string error)
    {
        if (researches == null)
        {
            error = "Research validation failed: definition collection is null.";
            return false;
        }

        var visiting = new HashSet<Research>();
        var visited = new HashSet<Research>();
        var path = new List<Research>();

        foreach (Research research in researches)
        {
            if (!Visit(research, visiting, visited, path, out error))
                return false;
        }

        error = null;
        return true;
    }

    private static bool Visit(
        Research research,
        HashSet<Research> visiting,
        HashSet<Research> visited,
        List<Research> path,
        out string error)
    {
        if (research == null)
        {
            error = "Research validation failed: definition collection contains null.";
            return false;
        }

        if (visited.Contains(research))
        {
            error = null;
            return true;
        }

        if (!visiting.Add(research))
        {
            error = BuildCycleError(path, research);
            return false;
        }

        path.Add(research);
        var uniquePrerequisites = new HashSet<Research>();
        if (research.Prerequisites != null)
        {
            foreach (Research prerequisite in research.Prerequisites)
            {
                if (prerequisite == null)
                {
                    error = $"Research validation failed: '{research.name}' contains a null prerequisite.";
                    return false;
                }

                if (prerequisite == research)
                {
                    error = $"Research dependency cycle: {research.name} -> {research.name}";
                    return false;
                }

                if (!uniquePrerequisites.Add(prerequisite))
                {
                    error = $"Research validation failed: '{research.name}' contains duplicate prerequisite '{prerequisite.name}'.";
                    return false;
                }

                if (!Visit(prerequisite, visiting, visited, path, out error))
                    return false;
            }
        }

        path.RemoveAt(path.Count - 1);
        visiting.Remove(research);
        visited.Add(research);
        error = null;
        return true;
    }

    private static string BuildCycleError(List<Research> path, Research repeated)
    {
        int start = path.IndexOf(repeated);
        if (start < 0)
            start = 0;

        var builder = new StringBuilder("Research dependency cycle: ");
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
