using UnityEngine;

// Runtime input behavior only; all visual hierarchy remains authored in prefabs.
[DisallowMultipleComponent]
public sealed class OnboardingControlFilter : MonoBehaviour, ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
        OnboardingInputGate.Allows(transform);
}
