using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchDisplayer : MonoBehaviour
{
    public static class OutlineColor
    {
        public static readonly Color Sel = new Color(0f, 0.6901961f, 0.7647059f, 1f);
        public static readonly Color UnSel = new Color(0f, 0f, 0f, 0f);
    }

    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Point;
    [SerializeField] private Image Outline;
    [SerializeField] private Slider Fill;

    private ResearchState state;
    private ResearchViewer viewer;
    public Research Research => state?.Definition;
    public ResearchState BoundState => state;
    public double ProgressPercent => state.ProgressRatio.ToDouble();

    public void Bind(ResearchState newState)
    {
        state = newState ?? throw new System.ArgumentNullException(nameof(newState));
        RefreshStatic();
        Refresh();
    }

    private void Awake()
    {
        viewer = GetComponentInParent<ResearchViewer>(true);
    }

    public void StartResearch() => ResearchManager.Instance.StartResearch(Research);

    public void SetSelect() => viewer?.SelectResearch(Research);

    public void SetSelectedVisual(bool selected)
    {
        if (Outline != null)
            Outline.color = selected ? OutlineColor.Sel : OutlineColor.UnSel;
    }

    public void Refresh()
    {
        if (state == null || Fill == null)
            return;
        Fill.value = (float)ProgressPercent;
    }

    private void RefreshStatic()
    {
        if (state == null)
            return;
        if (Label != null)
            Label.text = Research.Label;
        if (Point != null)
            Point.text = state.BaseCost.ToGameString();
    }
}
