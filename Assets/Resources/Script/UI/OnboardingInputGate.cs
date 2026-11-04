using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The root filter closes every pointer route except the authored step target and controls.
[DisallowMultipleComponent]
public sealed class OnboardingInputGate : MonoBehaviour, ICanvasRaycastFilter
{
    private static OnboardingInputGate activeGate;
    private RectTransform target;
    private RectTransform overlay;
    private Button next;
    private Button skip;
    private EventSystem navigationOwner;
    private bool previousNavigationEvents;
    private RectMask2D[] targetMasks;
    private bool controlsPrepared;

    public bool IsActive { get; private set; }
    public static bool BlocksNavigation => activeGate != null && activeGate.IsActive;

    public void Configure(RectTransform stepTarget, RectTransform overlayRoot,
        Button nextButton, Button skipButton)
    {
        if (stepTarget == null || !stepTarget.gameObject.activeInHierarchy ||
            overlayRoot == null || !overlayRoot.gameObject.activeInHierarchy)
        {
            Clear();
            return;
        }

        if (target != stepTarget) targetMasks = stepTarget.GetComponentsInParent<RectMask2D>();
        target = stepTarget;
        overlay = overlayRoot;
        next = nextButton;
        skip = skipButton;
        IsActive = true;
        activeGate = this;
        if (!controlsPrepared)
        {
            PrepareControls(gameObject);
            controlsPrepared = true;
        }
        CaptureNavigation();
    }

    public static void PrepareControls(GameObject root)
    {
        // Include inactive controls so their first activation is already gated.
        foreach (Selectable control in root.GetComponentsInChildren<Selectable>(true))
            if (!control.TryGetComponent<OnboardingControlFilter>(out _))
                control.gameObject.AddComponent<OnboardingControlFilter>();
    }

    public void Clear()
    {
        IsActive = false;
        if (activeGate == this)
            activeGate = null;
        if (navigationOwner != null)
            navigationOwner.sendNavigationEvents = previousNavigationEvents;
        navigationOwner = null;
        target = null;
        targetMasks = null;
        overlay = null;
        next = null;
        skip = null;
    }

    public static bool Allows(Transform control)
    {
        return !BlocksNavigation || activeGate.AllowsControl(control);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!IsActive)
            return true;
        return ContainsTarget(screenPoint, eventCamera) ||
            ContainsButton(next, screenPoint, eventCamera) ||
            ContainsButton(skip, screenPoint, eventCamera);
    }

    private bool ContainsTarget(Vector2 point, Camera camera)
    {
        if (!Contains(target, point, camera)) return false;
        for (int i = 0; targetMasks != null && i < targetMasks.Length; i++)
            if (targetMasks[i] != null && targetMasks[i].isActiveAndEnabled &&
                !targetMasks[i].IsRaycastLocationValid(point, camera)) return false;
        return true;
    }

    private bool AllowsControl(Transform control)
    {
        return control != null &&
            ((target != null && target.IsChildOf(control)) ||
             (next != null && next.gameObject.activeInHierarchy && control.IsChildOf(next.transform)) ||
             (skip != null && skip.gameObject.activeInHierarchy && control.IsChildOf(skip.transform)));
    }

    private void CaptureNavigation()
    {
        EventSystem current = EventSystem.current;
        if (navigationOwner != current)
        {
            if (navigationOwner != null)
                navigationOwner.sendNavigationEvents = previousNavigationEvents;
            navigationOwner = current;
            if (navigationOwner != null)
                previousNavigationEvents = navigationOwner.sendNavigationEvents;
        }
        if (navigationOwner == null)
            return;
        navigationOwner.sendNavigationEvents = false;
        GameObject selected = navigationOwner.currentSelectedGameObject;
        if (selected != null && !AllowsControl(selected.transform))
            navigationOwner.SetSelectedGameObject(null);
    }

    private static bool Contains(RectTransform rect, Vector2 point, Camera camera)
    {
        return rect != null && rect.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(rect, point, camera);
    }

    private static bool ContainsButton(Button button, Vector2 point, Camera camera)
    {
        return button != null && Contains(button.transform as RectTransform, point, camera);
    }

    private void LateUpdate()
    {
        if (!IsActive)
            return;
        if (target == null || !target.gameObject.activeInHierarchy ||
            overlay == null || !overlay.gameObject.activeInHierarchy)
        {
            Clear();
            return;
        }
        CaptureNavigation();
    }

    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
