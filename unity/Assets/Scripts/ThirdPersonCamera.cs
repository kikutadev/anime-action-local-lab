using UnityEngine;

public sealed class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new(0f, 2.35f, -4.8f);
    public float followSharpness = 10f;
    public float lookHeight = 1.15f;

    private float yaw = 20f;
    private float pitch = 10f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * 3.5f;
            pitch -= Input.GetAxis("Mouse Y") * 2.6f;
            pitch = Mathf.Clamp(pitch, -15f, 42f);
        }

        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = target.position + orbit * offset;
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        Vector3 lookAt = target.position + Vector3.up * lookHeight;
        transform.rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
    }
}
