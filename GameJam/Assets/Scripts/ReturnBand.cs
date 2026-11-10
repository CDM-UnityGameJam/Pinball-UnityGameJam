using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ReturnBand : MonoBehaviour
{
    [Min(0)] public int returnsPerLaunch = 2;
    [Min(0f)] public float returnSpeed = 22f;
    [Min(0f)] public float cooldown = .4f;
    [Range(0f, .5f)] public float inwardBias = .25f;

    void OnTriggerEnter(Collider other) => TryReturn(other);
    void OnTriggerStay(Collider other) => TryReturn(other);

    public bool TryReturn(Collider other)
    {
        var gm = GameManager.Instance;
        var ball = other.attachedRigidbody;
        if (!gm || !gm.IsPlaying || !ball || ball.isKinematic || !other.CompareTag("Player")) return false;
        // Ascending launches pass freely through; only descending balls consume a charge.
        if (Vector3.Dot(ball.linearVelocity, gm.board.forward) >= -.1f) return false;
        var state = ball.GetComponent<BallReturnState>();
        if (!state || !state.TrySpend(Time.time, cooldown)) return false;
        float x = gm.board.InverseTransformPoint(ball.position).x;
        var direction = (gm.board.forward - gm.board.right * Mathf.Clamp(x * .08f, -inwardBias, inwardBias)).normalized;
        ball.linearVelocity = direction * returnSpeed;
        return true; // No combat hit, energy grant, score or budget reset.
    }
}
