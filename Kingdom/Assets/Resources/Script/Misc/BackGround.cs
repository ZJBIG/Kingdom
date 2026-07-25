using UnityEngine;
using UnityEngine.Serialization;

public class BackGround : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("RotateSpeed")] private float rotateSpeed;

    private void Update()
    {
        transform.Rotate(new Vector3(0f, 0f, rotateSpeed) * Time.deltaTime);
    }
}
