using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallReturnState : MonoBehaviour
{
    public int Remaining { get; private set; }
    public int Capacity { get; private set; } = 2;
    float lastReturn = float.NegativeInfinity;

    public void BeginLaunch(int capacity)
    {
        Capacity = Mathf.Max(0, capacity);
        Remaining = Capacity;
        lastReturn = float.NegativeInfinity;
    }

    public bool TrySpend(float now, float cooldown)
    {
        if (Remaining <= 0 || now - lastReturn < cooldown) return false;
        Remaining--;
        lastReturn = now;
        return true;
    }

    void OnCollisionEnter(Collision collision) => CheckManualHit(collision);
    void OnCollisionStay(Collision collision) => CheckManualHit(collision);

    void CheckManualHit(Collision collision)
    {
        var gm = GameManager.Instance;
        var flipper = collision.collider.GetComponentInParent<Flipper>();
        if (!gm || !gm.IsPlaying || !flipper || !flipper.IsPressed) return;
        // Resting on a bat or touching an unpressed bat cannot replenish the budget.
        if (Vector3.Dot(GetComponent<Rigidbody>().linearVelocity, gm.board.forward) > 1f)
            Remaining = Capacity;
    }
}
