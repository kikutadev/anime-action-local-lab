using System.Collections;
using UnityEngine;

public sealed class DemoAutoPlay : MonoBehaviour
{
    private AnimeFighterMotor motor;

    private void Awake()
    {
        motor = GetComponent<AnimeFighterMotor>();
    }

    private IEnumerator Start()
    {
        if (!Application.absoluteURL.Contains("demo=1"))
        {
            yield break;
        }

        // Deterministic browser acceptance mode: capture a generated attack
        // without changing normal interactive gameplay.
        yield return new WaitForSeconds(1.0f);
        motor.TriggerAttack();
    }
}
