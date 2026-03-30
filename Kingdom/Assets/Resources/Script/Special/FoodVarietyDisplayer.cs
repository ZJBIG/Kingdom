using System;
using UnityEngine;
using UnityEngine.UI;

public class FoodVarietyDisplayer : MonoBehaviour
{
    public bool isActive;
    public Image ActiveImage;
    public Sprite Active, NotActive;

    public readonly Color Available = Color.white;
    public readonly Color Unavailable = Color.black;

    public void SetSelect()
    {

    }





    [Serializable]
    public class FoodVarietyDisplayerData
    {
        public string FoodName;
        public bool isActive;
    }
}
