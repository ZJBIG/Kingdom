using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FoodVarietyViewer : MonoBehaviour
{
    [SerializeField] private Image DisplaySprite;
    [SerializeField] private TMP_Text Information;
    [SerializeField] private TMP_Text ButtonText;



    //IEnumerator UpdateUI()
    //{
    //    while (true)
    //    {
    //        if (CurSelect)
    //        {
    //            DoInvestButton.text = ButtonText;
    //            ProgressPercentage.value = (float)SelectDisplayer.ProgressPercent;

    //            string label = CurSelect.Label ?? "未知研究";
    //            string techLevelDesc = CurSelect.TechLevel.GetDescription() ?? "无等级";
    //            double progress = SelectDisplayer.ProgressPercent * 100;
    //            string desc = CurSelect.Description ?? "无描述";

    //            BaseInfo.text = $"{label}\n{techLevelDesc}\n{progress:F2}%\n{desc}";

    //            if (CurSelect != PreSelect)
    //            {
    //                int count = ResourceList.childCount;
    //                for (int i = count - 1; i >= 1; i--)
    //                    Destroy(ResourceList.GetChild(i).gameObject);
    //                foreach (var (r, num) in CurSelect.ResourceRequirement)
    //                {
    //                    GameObject Displayer = Instantiate(ResearchResourceReqPrefab);
    //                    Image image = Displayer.transform.GetChild(0).GetComponent<Image>();
    //                    TMP_Text text = Displayer.transform.GetChild(1).GetComponent<TMP_Text>();
    //                    image.sprite = r.Sprite;
    //                    image.color = r.Color;
    //                    text.text = num.ToString();
    //                    Displayer.transform.SetParent(ResourceList);
    //                }
    //                PreSelect = CurSelect;
    //            }
    //        }
    //        yield return new WaitForSecondsRealtime(0.1f);
    //    }
    //}
}
