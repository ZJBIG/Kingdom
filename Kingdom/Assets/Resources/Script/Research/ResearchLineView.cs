using UnityEngine;
using UnityEngine.UI;

public sealed class ResearchLineView : MonoBehaviour
{
    public static readonly Color UnselectedColor = new Color(1f, 1f, 1f, 0.1882353f);
    public static readonly Color PrerequisiteColor = new Color(0f, 0.6784314f, 1f, 0.5058824f);
    public static readonly Color NextColor = new Color(0.05882353f, 0.9490196f, 0.1882353f, 0.3764706f);

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

    public void Bind(Research prerequisiteResearch, Research targetResearch)
    {
        Bind(prerequisiteResearch, targetResearch, null, null);
    }

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
        if (lineTransform == null)
            lineTransform = transform as RectTransform;
        if (lineImage == null)
            lineImage = GetComponent<Image>();

        RefreshGeometry();
        SetSelectedResearch(null);
    }

    public void SetSelectedResearch(Research selectedResearch)
    {
        if (lineImage != null)
            lineImage.color = GetColor(selectedResearch, prerequisite, research);
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
        lineTransform.rotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Rad2Deg * Mathf.Atan2(delta.y, delta.x));
    }

    public static Color GetColor(Research selectedResearch, Research prerequisiteResearch, Research targetResearch)
    {
        if (selectedResearch == null)
            return UnselectedColor;
        if (selectedResearch == prerequisiteResearch)
            return NextColor;
        if (selectedResearch == targetResearch)
            return PrerequisiteColor;
        return UnselectedColor;
    }

    private static Vector3 GetNodeCenter(Research researchDefinition, RectTransform node)
    {
        if (node == null)
            return new Vector3(
                100f + 300f * researchDefinition.x,
                -25f - 300f * researchDefinition.y,
                0f);

        return node.TransformPoint(node.rect.center);
    }
}
