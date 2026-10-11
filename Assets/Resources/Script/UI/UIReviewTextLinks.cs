using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class UIReviewTextLinks : MonoBehaviour, IPointerClickHandler
{
    private TMP_Text text;
    private Action<string> navigate;
    public void Bind(TMP_Text value, Action<string> action) { text = value; navigate = action; }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging || text == null) return;
        int index = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
        if (index >= 0) navigate?.Invoke(text.textInfo.linkInfo[index].GetLinkID());
    }
}
