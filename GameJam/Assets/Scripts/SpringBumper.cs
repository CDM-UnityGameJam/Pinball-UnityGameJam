using UnityEngine;

// Fixed scenery: only physical ball contacts receive a kick, with no damage or energy reward.
public class SpringBumper : MonoBehaviour
{
    [Min(0.01f)] public float radius = 0.55f;
    [Min(0f)] public float kickSpeed = 20f;
    [Range(0f, 0.5f)] public float upwardBias = 0.3f;
    public Material coilMaterial;
    public bool guideAlongGutter;
    public Vector3 gutterDirection = new Vector3(.12f, 0f, -1f);

    void Awake() => EnsureVisual();

    public void EnsureVisual()
    {
        if (transform.Find("SpringCoil")) return;
        var source = GetComponent<Renderer>().sharedMaterial;
        var coil = new GameObject("SpringCoil").AddComponent<LineRenderer>();
        coil.transform.SetParent(transform, false);
        coil.useWorldSpace = false;
        coil.sharedMaterial = coilMaterial ? coilMaterial : source;
        coil.startColor = coil.endColor = new Color(0.25f, 0.3f, 0.38f);
        coil.widthMultiplier = 0.045f;
        coil.numCornerVertices = 3;
        coil.numCapVertices = 3;
        coil.positionCount = 161;
        for (int i = 0; i < coil.positionCount; i++)
        {
            float t = i / (float)(coil.positionCount - 1);
            float angle = t * Mathf.PI * 8f;
            float r = Mathf.Lerp(0.06f, 0.43f, t);
            coil.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, 1.05f, Mathf.Sin(angle) * r));
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        var gm = GameManager.Instance;
        var ball = collision.rigidbody;
        if (!gm || !gm.IsPlaying || !ball || ball.isKinematic || !collision.collider.CompareTag("Player")) return;

        var away = Vector3.ProjectOnPlane(ball.position - transform.position, gm.board.up).normalized;
        if (away.sqrMagnitude < 0.01f) away = -gm.board.forward;
        // Keep an outward component even when struck from above; never kick through the pad.
        var direction = (away + gm.board.forward * upwardBias).normalized;
        if (guideAlongGutter)
        {
            direction = gm.board.TransformDirection(gutterDirection).normalized;
            // Preserve separation from the real cylinder, including off-center hits.
            float separation = Vector3.Dot(direction, away);
            if (separation < .15f) direction = (direction + away * (.15f - separation)).normalized;
        }
        ball.linearVelocity = direction * kickSpeed;
    }

    // Test the entire straight descent, not just the birth point. Specials only reserve their position.
    public bool BlocksSpawn(Vector3 position, float enemyRadius, float clearance, bool descending, float dangerZ)
    {
        var center = transform.localPosition;
        float nearestZ = descending
            ? Mathf.Clamp(center.z, Mathf.Min(dangerZ, position.z), Mathf.Max(dangerZ, position.z))
            : position.z;
        var delta = new Vector2(position.x - center.x, nearestZ - center.z);
        float separation = radius + enemyRadius + clearance;
        return delta.sqrMagnitude < separation * separation;
    }
}
