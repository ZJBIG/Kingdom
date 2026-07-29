using UnityEngine;
using UnityEngine.UI;

public sealed class ResearchLineView : MonoBehaviour
{
    public static readonly Color UnselectedColor = new Color(1f, 1f, 1f, 0.1882353f);
    public static readonly Color IncompletePrerequisiteColor =
        new Color(1f, 0.2509804f, 0.1843137f, 0.9f);
    public static readonly Color CompletedPrerequisiteColor =
        new Color(0.5607843f, 0.3372549f, 0.9411765f, 0.9f);
    public static readonly Color AvailableSuccessorColor =
        new Color(0.1647059f, 0.8f, 0.3764706f, 0.9f);

    [SerializeField] private Image lineImage;

    private RectTransform lineTransform;
    private Research prerequisite;
    private Research research;
    private RectTransform prerequisiteNode;
    private RectTransform researchNode;

    public Research Prerequisite => prerequisite;
    public Research Research => research;

    private void Awake()
    {
        lineTransform = transform as RectTransform;
        if (lineImage == null)
            lineImage = GetComponent<Image>();
    }

    public void Bind(Research prerequisiteResearch, Research targetResearch)=>Bind(prerequisiteResearch, targetResearch, null, null);

    public void Bind(
        Research prerequisiteResearch,
        Research targetResearch,
        RectTransform prerequisiteResearchNode,
        RectTransform targetResearchNode)
    {
        prerequisite = prerequisiteResearch ?? throw new System.ArgumentNullException(nameof(prerequisiteResearch));
        research = targetResearch ?? throw new System.ArgumentNullException(nameof(targetResearch));
        prerequisiteNode = prerequisiteResearchNode;
        researchNode = targetResearchNode;

            lineTransform = transform as RectTransform;
            lineImage = GetComponent<Image>();

        RefreshGeometry();
        SetSelectedResearch(null);
    }

    public void SetSelectedResearch(Research selectedResearch)
    {
        ResearchStatus prerequisiteStatus = GetStatus(prerequisite);
        lineImage.color = GetColor(
            selectedResearch,
            prerequisite,
            research,
            prerequisiteStatus);
    }

    public void RefreshGeometry()
    {
        if (lineTransform == null || prerequisite == null || research == null)
            return;

        Vector3 beginWorld = GetNodeCenter(prerequisite, prerequisiteNode);
        Vector3 endWorld = GetNodeCenter(research, researchNode);
        Vector3 delta = endWorld - beginWorld;
        lineTransform.sizeDelta = new Vector2(delta.magnitude, 5f);
        lineTransform.position = (beginWorld + endWorld) * 0.5f;
        lineTransform.rotation = Quaternion.Euler(0f,0f,Mathf.Rad2Deg * Mathf.Atan2(delta.y, delta.x));
    }

    public static Color GetColor(
        Research selectedResearch,
        Research prerequisiteResearch,
        Research targetResearch,
        ResearchStatus prerequisiteStatus)
    {
        // Only color the direct edge connected to the selected node.
        // The viewer owns one line per direct prerequisite relationship;
        // do not walk or color any transitive research chain here.
        if (selectedResearch == targetResearch)
        {
            return prerequisiteStatus == ResearchStatus.Completed
                ? CompletedPrerequisiteColor
                : IncompletePrerequisiteColor;
        }
        if (selectedResearch == prerequisiteResearch)
            return AvailableSuccessorColor;
        return UnselectedColor;
    }

    private static ResearchStatus GetStatus(Research definition)
    {
        if (definition != null &&
            ResearchManager.Instance != null &&
            ResearchManager.Instance.States.TryGetValue(
                definition,
                out ResearchState state))
        {
            return state.Status;
        }

        return ResearchStatus.Locked;
    }

    private static Vector3 GetNodeCenter(Research researchDefinition, RectTransform node)
    {
        if (node == null)
            return new Vector3(
                -750f + 50f * researchDefinition.x,
                -25f - 30f * researchDefinition.y);

        return node.TransformPoint(node.rect.center);
    }
}
