using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

#pragma warning disable CS0649
public class FoodVarietyDisplayer : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("isActive")] private Image activeImage;
    [SerializeField, FormerlySerializedAs("Active")] private Sprite activeSprite;
    [SerializeField, FormerlySerializedAs("NotActive")] private Sprite inactiveSprite;
    [SerializeField] private bool selected;

    private static readonly Color Available = Color.white;
    private static readonly Color Unavailable = Color.black;

    public bool Selected => selected;

    public void SetSelect() => SetSelected(!selected);

    public void SetSelected(bool value)
    {
        selected = value;
        RefreshVisual();
    }

    private void OnValidate() => RefreshVisual();

    private void RefreshVisual()
    {
        if (activeImage == null)
            return;

        activeImage.sprite = selected ? activeSprite : inactiveSprite;
        activeImage.color = selected ? Available : Unavailable;
    }

    [Serializable]
    public class FoodVarietyDisplayerData
    {
        public string FoodName;
        public bool isActive;
    }
}
#pragma warning restore CS0649
