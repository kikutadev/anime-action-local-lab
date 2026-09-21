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
        string url = Application.absoluteURL;
        bool attackDemo = url.Contains("demo=1") || url.Contains("demo=attack");
        bool dodgeDemo = url.Contains("demo=dodge");
        if (!attackDemo && !dodgeDemo)
        {
            yield break;
        }

        // Deterministic browser acceptance mode; normal gameplay is unaffected.
        yield return new WaitForSeconds(1.0f);
        if (dodgeDemo)
        {
            motor.TriggerDodge();
        }
        else
        {
            motor.TriggerAttack();
        }
    }
}
