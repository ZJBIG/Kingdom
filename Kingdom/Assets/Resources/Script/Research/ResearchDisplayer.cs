using TMPro;
using UnityEngine;

public class ResearchDisplayer : MonoBehaviour
{
    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Description;
    private LineRenderer lineRenderer;
    public Research Research;
    public int Scale = 1;
    private Vector2 Position => new Vector2(Research.x, Research.y) * Scale;
    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }
    void Update()
    {

    }
    void DrawLine()
    {
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.positionCount = 2;
        foreach (var req in Research.Prequisites)
        {
            //lineRenderer.SetPosition(0, transform.position);
            //lineRenderer.SetPosition(1, req.position);
        }
    }
}
