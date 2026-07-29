using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchDisplayer : MonoBehaviour
{

    public static class DisplayerFrame
    {
        public static readonly Color OutlineSel = new Color(0f, 0.6901961f, 0.7647059f, 1f);
        public static readonly Color OutlineCompleted = new Color(1f, 0.6588235f, 0.09803922f, 0.92f);
        public static readonly Color OutlineUnsel = new Color(0f, 0f, 0f, 0f);
        public static Dictionary<TechLevel, Pair<Sprite, Sprite>> FrameAndProgress = new(){
        {TechLevel.Animal,new Pair<Sprite,Sprite>(Resources.Load<Sprite>($"Texture/UI/ResearchUI/Animal"),Resources.Load<Sprite>($"Texture/UI/ResearchUI/ProgressAnimal")) },
        {TechLevel.Neolithic,new Pair<Sprite,Sprite>(Resources.Load<Sprite>($"Texture/UI/ResearchUI/Neolithic"),Resources.Load<Sprite>($"Texture/UI/ResearchUI/ProgressNeolithic"))  },
        {TechLevel.Medieval,new Pair<Sprite,Sprite>(Resources.Load<Sprite>($"Texture/UI/ResearchUI/Medieval"),Resources.Load<Sprite>($"Texture/UI/ResearchUI/ProgressMedieval"))  },
        {TechLevel.Industrial,new Pair<Sprite,Sprite>(Resources.Load<Sprite>($"Texture/UI/ResearchUI/Industrial"),Resources.Load<Sprite>($"Texture/UI/ResearchUI/ProgressIndustrial"))  },
        {TechLevel.Spacer,new Pair<Sprite,Sprite>(Resources.Load<Sprite>($"Texture/UI/ResearchUI/Spacer"),Resources.Load<Sprite>($"Texture/UI/ResearchUI/ProgressSpacer"))  },
        {TechLevel.Ultra,new Pair<Sprite,Sprite>(Resources.Load<Sprite>($"Texture/UI/ResearchUI/Ultra"),Resources.Load<Sprite>($"Texture/UI/ResearchUI/ProgressUltra"))  },
        {TechLevel.Archotech,new Pair<Sprite,Sprite>(Resources.Load<Sprite>($"Texture/UI/ResearchUI/Archotech"),Resources.Load<Sprite>($"Texture/UI/ResearchUI/ProgressArchotech"))  },};
    }

    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Point;
    [SerializeField] private Image Outline;
    [SerializeField] private Slider Frame;
    [SerializeField] private Image FillImage;

    private ResearchState state;
    private ResearchViewer viewer;
    private bool selected;
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
        this.selected = selected;
        RefreshOutline();
    }

    public void Refresh()
    {
        if (state == null)
            return;
        if (Frame != null)
            Frame.value = (float)ProgressPercent;
        RefreshOutline();
    }

    public static Color GetOutlineColor(bool selected, ResearchStatus status)
    {
        if (selected)
            return DisplayerFrame.OutlineSel;
        return status == ResearchStatus.Completed
            ? DisplayerFrame.OutlineCompleted
            : DisplayerFrame.OutlineUnsel;
    }

    private void RefreshOutline()
    {
        if (Outline != null && state != null)
            Outline.color = GetOutlineColor(selected, state.Status);
    }

    private void RefreshStatic()
    {
        if (state == null)
            return;
        if (Label != null)
            Label.text = Research.Label;
        if (Point != null)
            Point.text = state.BaseCost.ToGameString();
        if (Frame != null)
        {
            if (!DisplayerFrame.FrameAndProgress.TryGetValue(
                    Research.TechLevel,
                    out Pair<Sprite, Sprite> frameAndProgress))
            {
                frameAndProgress = DisplayerFrame.FrameAndProgress[TechLevel.Animal];
            }

            Frame.GetComponent<Image>().sprite = frameAndProgress.First;
            if (FillImage != null)
                FillImage.sprite = frameAndProgress.Second;
        }
    }
}
