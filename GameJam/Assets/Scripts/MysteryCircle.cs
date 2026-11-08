using UnityEngine;

// Spawns grey; first ball hit reveals enemy (red, takes damage) or obstacle (blue pop bumper).
public class MysteryCircle : MonoBehaviour
{
    public bool isEnemy;
    public int hp = 3;
    public float kickSpeed = 20f;
    const float HitCooldown = 0.2f;

    bool revealed;
    float lastHit = -1f;
    Renderer rend;
    TextMesh label;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        rend.material.color = Color.grey;
    }

    void OnCollisionEnter(Collision c)
    {
        if (!c.collider.CompareTag("Player") || Time.time - lastHit < HitCooldown) return;
        lastHit = Time.time;

        if (!revealed)
        {
            revealed = true;
            rend.material.color = isEnemy ? Color.red : new Color(0.2f, 0.4f, 1f);
        }

        if (isEnemy)
        {
            hp--;
            if (!label) label = MakeLabel();
            label.text = hp.ToString();
            if (hp <= 0) Destroy(gameObject);
        }
        else
        {
            var dir = Vector3.ProjectOnPlane(c.rigidbody.position - transform.position, transform.up).normalized;
            c.rigidbody.linearVelocity = dir * kickSpeed;
        }
    }

    // White HP number lying flat on the top face. One digit at this size stays inside the 1-unit-wide face.
    TextMesh MakeLabel()
    {
        var go = new GameObject("HP");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.01f, 0f); // primitive cylinder top is at local y = 1
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // face up, digit's top points up the table
        var t = go.AddComponent<TextMesh>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
        t.fontSize = 64;
        t.characterSize = 0.11f; // glyph corners ~0.44 from center, face radius 0.5
        t.anchor = TextAnchor.MiddleCenter;
        t.alignment = TextAlignment.Center;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        return t;
    }
}
