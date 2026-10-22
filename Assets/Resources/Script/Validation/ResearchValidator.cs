using System.Collections.Generic;
using System.Text;

public static class ResearchValidator
{
    public static bool ValidateNoCycles(IEnumerable<ResearchDef> researches, out string error)
    {
        if (researches == null)
        {
            error = "研究验证失败：定义集合为空。";
            return false;
        }

        var visiting = new HashSet<ResearchDef>();
        var visited = new HashSet<ResearchDef>();
        var path = new List<ResearchDef>();

        foreach (ResearchDef research in researches)
        {
            if (!Visit(research, visiting, visited, path, out error))
                return false;
        }

        error = null;
        return true;
    }

    private static bool Visit(
        ResearchDef research,
        HashSet<ResearchDef> visiting,
        HashSet<ResearchDef> visited,
        List<ResearchDef> path,
        out string error)
    {
        if (research == null)
        {
            error = "研究验证失败：定义集合包含空引用。";
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
        var uniquePrerequisites = new HashSet<ResearchDef>();
        if (research.Prerequisites != null)
        {
            foreach (ResearchDef prerequisite in research.Prerequisites)
            {
                if (prerequisite == null)
                {
                    error = $"研究验证失败：“{research.name}”包含空的前置研究。";
                    return false;
                }

                if (prerequisite == research)
                {
                    string label = DisplayName(research);
                    error = $"研究依赖循环：{label} -> {label}";
                    return false;
                }

                if (!uniquePrerequisites.Add(prerequisite))
                {
                    error = $"研究验证失败：“{DisplayName(research)}”重复引用前置研究“{DisplayName(prerequisite)}”。";
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

    private static string BuildCycleError(List<ResearchDef> path, ResearchDef repeated)
    {
        int start = path.IndexOf(repeated);
        if (start < 0)
            start = 0;

        var builder = new StringBuilder("研究依赖循环：");
        for (int i = start; i < path.Count; i++)
        {
            if (i > start)
                builder.Append(" -> ");
            builder.Append(DisplayName(path[i]));
        }

        builder.Append(" -> ");
        builder.Append(DisplayName(repeated));
        return builder.ToString();
    }

    private static string DisplayName(ResearchDef research)
    {
        if (research == null)
            return "<null>";
        if (!string.IsNullOrWhiteSpace(research.Id))
            return research.Id;
        if (!string.IsNullOrWhiteSpace(research.name))
            return research.name;
        return "<unnamed>";
    }
}
