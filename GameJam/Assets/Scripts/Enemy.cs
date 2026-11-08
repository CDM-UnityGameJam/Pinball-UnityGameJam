using UnityEngine;

// Board circle: normal enemy (hidden target, 3 HP), special enemy (111/222/333, 5 HP, 30 s life) or bumper.
public class Enemy : MonoBehaviour
{
    public enum Kind { Normal, Special, Bumper }

    // 28-46 inclusive plus 74, 148, 185, 259, 296: 24 targets. 111/222/333 are special-only.
    public static readonly int[] NormalTargets = BuildNormalTargets();
    public static readonly int[] SpecialTargets = { 111, 222, 333 };

    const float HitCooldown = 0.2f, VisibleTime = 20f, SpecialLifetime = 30f;
    const int EnergyPerHit = 25;

    public float kickSpeed = 20f;
    public Kind kind;
    public int target, hp, maxHp;

    float visibleLeft, lifeLeft;
    float lastHit = -1f;
    TextMesh label;

    public bool Visible => kind == Kind.Special || visibleLeft > 0f;
    public int Multiplier => target == 111 ? 10 : target == 222 ? 2 : 3;

    public void Init(Kind k, int targetNumber)
    {
        kind = k;
        target = targetNumber;
        maxHp = hp = k == Kind.Special ? 5 : 3;
        lifeLeft = SpecialLifetime;
        GetComponent<Renderer>().material.color =
            k == Kind.Normal ? new Color(0.55f, 0.15f, 0.2f) :
            k == Kind.Special ? new Color(0.6f, 0.45f, 0.1f) : new Color(0.6f, 0.6f, 0.65f);
        if (k != Kind.Bumper) label = MakeLabel();
        Refresh();
    }

    // Scaled time: visibility and special lifetime freeze during the launch wait and pause.
    void Update()
    {
        if (kind == Kind.Normal && visibleLeft > 0f) visibleLeft = Mathf.Max(0f, visibleLeft - Time.deltaTime);
        if (kind == Kind.Special)
        {
            lifeLeft -= Time.deltaTime;
            if (lifeLeft <= 0f) { Destroy(gameObject); return; } // timeout: no reward
        }
        Refresh();
    }

    void OnCollisionEnter(Collision c)
    {
        if (!c.collider.CompareTag("Player") || Time.time - lastHit < HitCooldown) return;
        lastHit = Time.time;

        if (kind == Kind.Bumper)
        {
            var dir = Vector3.ProjectOnPlane(c.rigidbody.position - transform.position, transform.up).normalized;
            c.rigidbody.linearVelocity = dir * kickSpeed;
            return;
        }

        // Spec order: damage uses visibility from before this hit, then reveal, then kill and energy.
        var gm = GameManager.Instance;
        bool damage = Visible && target == gm.CurrentNumber;
        if (kind == Kind.Normal) visibleLeft = VisibleTime;
        if (damage) hp--;
        if (hp <= 0)
        {
            if (kind == Kind.Normal) gm.OnNormalKill(); else gm.OnSpecialKill(Multiplier);
            Destroy(gameObject);
        }
        gm.AddEnergy(EnergyPerHit);
        Refresh();
    }

    void Refresh()
    {
        if (!label) return;
        label.text = kind == Kind.Special
            ? target + "\nHP " + hp + "/" + maxHp + "\nx" + Multiplier + " " + Mathf.CeilToInt(lifeLeft) + "s"
            : (Visible ? target.ToString() : "?") + "\nHP " + hp + "/" + maxHp + (Visible ? "\n" + Mathf.CeilToInt(visibleLeft) + "s" : "");
    }

    // White text lying flat on the top face, reading up the table.
    TextMesh MakeLabel()
    {
        var go = new GameObject("Label");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.01f, 0f); // primitive cylinder top is at local y = 1
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var t = go.AddComponent<TextMesh>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
        t.fontSize = 64;
        t.characterSize = 0.06f;
        t.anchor = TextAnchor.MiddleCenter;
        t.alignment = TextAlignment.Center;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        return t;
    }

    static int[] BuildNormalTargets()
    {
        var list = new System.Collections.Generic.List<int>();
        for (int n = 28; n <= 46; n++) list.Add(n);
        list.AddRange(new[] { 74, 148, 185, 259, 296 });
        return list.ToArray();
    }
}
