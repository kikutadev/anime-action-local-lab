using System.Collections;
using UnityEngine;

public sealed class TrainingDummy : MonoBehaviour
{
    private Vector3 home;
    private Renderer targetRenderer;
    private Color baseColor;
    private bool reacting;

    private void Awake()
    {
        home = transform.position;
        targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null) baseColor = targetRenderer.material.color;
    }

    public void Hit(Vector3 direction)
    {
        if (!reacting) StartCoroutine(React(direction));
    }

    private IEnumerator React(Vector3 direction)
    {
        reacting = true;
        if (targetRenderer != null) targetRenderer.material.color = Color.white;

        Vector3 start = transform.position;
        Vector3 pushed = start + direction.normalized * 0.32f;
        float t = 0f;
        while (t < 0.11f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, pushed, t / 0.11f);
            yield return null;
        }

        yield return new WaitForSeconds(0.08f);

        if (targetRenderer != null) targetRenderer.material.color = baseColor;
        t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(pushed, home, t / 0.2f);
            yield return null;
        }

        transform.position = home;
        reacting = false;
    }
}
