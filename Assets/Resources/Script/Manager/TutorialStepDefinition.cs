using UnityEngine;

[CreateAssetMenu(fileName = "TutorialStep", menuName = "Kingdom/Tutorial Step", order = 20)]
public sealed class TutorialStepDefinition : ScriptableObject
{
    public string Id;
    public string Title;
    [TextArea]
    public string Description;
    [TextArea]
    public string NarrativeText;
    public TutorialStepKind Kind;
    public string NextStepId;
    public string RewardId;
    public string TriggerCondition = "game-state";
    public string CompletionCondition;
    public string NavigationPage;

    public TutorialStep ToRuntime()
    {
        return new TutorialStep(
            Id,
            Title,
            Description,
            Kind,
            NextStepId,
            RewardId,
            TriggerCondition,
            CompletionCondition,
            NavigationPage,
            NarrativeText);
    }
}
