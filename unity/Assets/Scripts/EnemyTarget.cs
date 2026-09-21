using System.Collections;
using UnityEngine;

public sealed class EnemyTarget : MonoBehaviour
{
    private Vector3 home;
    private Renderer[] renderers;
    private Color[] baseColors;
    private bool reacting;

    private void Awake()
    {
        home = transform.position;
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            baseColors[i] = renderers[i].material.color;
        }
    }

    public void Hit(Vector3 direction)
    {
        if (!reacting)
        {
            StartCoroutine(HitRoutine(direction));
        }
    }

    private IEnumerator HitRoutine(Vector3 direction)
    {
        reacting = true;
        float elapsed = 0f;
        Vector3 start = transform.position;
        Vector3 end = start + direction.normalized * 0.35f;
        foreach (Renderer r in renderers)
        {
            r.material.color = Color.white;
        }

        while (elapsed < 0.12f)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, elapsed / 0.12f);
            yield return null;
        }

        yield return new WaitForSeconds(0.08f);
        foreach (int i in System.Linq.Enumerable.Range(0, renderers.Length))
        {
            renderers[i].material.color = baseColors[i];
        }

        elapsed = 0f;
        start = transform.position;
        while (elapsed < 0.22f)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, home, elapsed / 0.22f);
            yield return null;
        }

        transform.position = home;
        reacting = false;
    }
}
