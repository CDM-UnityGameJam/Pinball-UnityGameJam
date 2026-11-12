using UnityEngine;

// Lives on every active ball, including runtime multiballs and replacement balls.
[RequireComponent(typeof(Rigidbody))]
public class PinballCollisionAudio : MonoBehaviour
{
    [Min(0f), Tooltip("Ignore gentle contacts and resting contact with the playfield.")]
    public float minimumImpactSpeed = 1f;

    Rigidbody body;

    void Awake() => body = GetComponent<Rigidbody>();

    void OnCollisionEnter(Collision collision)
    {
        var gm = GameManager.Instance;
        if (!gm || !gm.IsPlaying || body.isKinematic) return;
        // Enemy contacts have their own sound at the accepted gameplay hit.
        if (collision.collider.GetComponentInParent<Enemy>()) return;
        // The playfield and glass never produce edge-hit sounds, even on a hard landing.
        var hit = collision.collider;
        if (hit.GetComponentInParent<OuterRingSurface>() ||
            hit.GetComponentInParent<SpringBumper>() ||
            hit.GetComponentInParent<PinballCollisionAudio>()) return;
        if (hit.transform == gm.board.Find("Floor") || hit.transform == gm.board.Find("Glass")) return;
        var flipper = hit.GetComponentInParent<Flipper>();
        // A player-held flipper uses the launch cue; resting flippers retain the normal hit.
        var cue = flipper && flipper.IsPressed ? gm.pinballLaunch : gm.pinballHit;
        if (!cue) return;
        for (int i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            // Board-local up follows the tilted table. Only side-facing contacts count;
            // this also rejects floors, ramps and the tops of rails/flippers.
            if (Mathf.Abs(Vector3.Dot(contact.normal, gm.board.up)) > .5f) continue;
            // Require an impact into the edge, rather than movement along it.
            if (Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal)) < minimumImpactSpeed) continue;
            cue.Play(contact.point);
            break;
        }
    }
}
