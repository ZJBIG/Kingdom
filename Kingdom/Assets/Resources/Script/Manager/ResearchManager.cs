using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResearchManager : Singleton<ResearchManager>
{
    [SerializeField] private Transform Content;


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(Vector2.zero, Vector2.one   );
    }
    private void OnGUI()
    {
    }
}
