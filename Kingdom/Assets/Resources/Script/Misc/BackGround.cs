using UnityEngine;

public class BackGround : MonoBehaviour
{
    public float RotateSpeed;
    void Update()
    {
        transform.Rotate(new Vector3(0, 0, RotateSpeed) * Time.timeScale);
    }
}
