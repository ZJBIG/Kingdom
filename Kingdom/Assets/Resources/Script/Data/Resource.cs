using UnityEngine;

[CreateAssetMenu(fileName = "创建资源", menuName = "数据/资源", order = 0)]
public class Resource : GameDefinition
{
    public string Label;
    public string Description;
    public Sprite Sprite;
    public Color Color = Color.white;
}
