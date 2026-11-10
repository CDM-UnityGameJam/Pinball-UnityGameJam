using System.Collections.Generic;
using UnityEngine;

// Static physical terrain. The gutter contains no attraction or scripted ball movement.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class OuterRingSurface : MonoBehaviour
{
    const int ArcSegments = 96;
    public bool flatSides = true;
    public float fieldHalfWidth = 6.5f;
    public float arcCenterZ = 7f;
    public float fieldArcHeight = 3.8f;
    public float bottomZ = -4f;
    public float fieldHeight = .32f;
    public float bevelWidth = .45f;
    [Min(.5f), Tooltip("Ramp length from the flipper area to the playfield, independent of the outer side edges.")]
    public float entryRampLength = 3f;
    [Min(0f), Tooltip("Side and top lip height above the playfield. No lip along the bottom launch lane.")]
    public float lipHeight = .08f;
    [Min(.1f), Tooltip("Smooth lip transition width along the playfield edge.")]
    public float lipWidth = .6f;
    [Min(0f), Tooltip("Extra lip height along the top and upper sides; lower sides keep the base height.")]
    public float upperLipExtraHeight = .06f;
    public float upperLipStartZ = 1f;
    public float upperLipFullZ = 5f;
    [Tooltip("Position where side gutters and lips reconnect to the flat top area.")]
    public float topTransitionStartZ = 7f;
    public float topTransitionEndZ = 7.6f;
    public float MaximumHeight => fieldHeight + lipHeight + upperLipExtraHeight;

    public float FeatureWeightAt(float z) => 1f - Mathf.SmoothStep(0, 1,
        Mathf.InverseLerp(topTransitionStartZ, Mathf.Max(topTransitionStartZ + .01f, topTransitionEndZ), z));

    public float LipHeightAt(float z) => (lipHeight + upperLipExtraHeight * Mathf.SmoothStep(0, 1,
        Mathf.InverseLerp(upperLipStartZ, Mathf.Max(upperLipStartZ + .01f, upperLipFullZ), z))) * FeatureWeightAt(z);
    public float wallHalfWidth = 8f;
    public float wallArcHeight = 5f;
    public Material gutterMaterial;
    Mesh terrainMesh, gutterMesh;

    void OnEnable() => Rebuild();

    public float HeightAt(float x, float z)
    {
        if (flatSides) return fieldHeight * (1f - Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((bottomZ - z) / entryRampLength)));
        float distance;
        if (z <= arcCenterZ)
        {
            float t = Mathf.Max((Mathf.Abs(x) - fieldHalfWidth) / bevelWidth, (bottomZ - z) / entryRampLength);
            return fieldHeight * (1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01(t))) + LipAt(Mathf.Abs(x) - fieldHalfWidth, z);
        }
        else
        {
            float theta = Mathf.Atan2((z - arcCenterZ) / fieldArcHeight, x / fieldHalfWidth);
            float q = Mathf.Sqrt(x * x / (fieldHalfWidth * fieldHalfWidth) +
                (z - arcCenterZ) * (z - arcCenterZ) / (fieldArcHeight * fieldArcHeight));
            distance = Vector2.Distance(new Vector2(x, z),
                new Vector2(Mathf.Cos(theta) * fieldHalfWidth, arcCenterZ + Mathf.Sin(theta) * fieldArcHeight)) * (q < 1f ? -1f : 1f);
        }
        float height = fieldHeight * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / bevelWidth)));
        return Mathf.Lerp(fieldHeight, height, FeatureWeightAt(z)) + LipAt(distance, z);
    }

    float LipAt(float signedDistance, float z)
    {
        float t = Mathf.Clamp01((signedDistance + lipWidth) / Mathf.Max(.1f, lipWidth));
        return LipHeightAt(z) * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 2) * Mathf.SmoothStep(0, 1, z - bottomZ);
    }

    List<Vector3> Boundary(float width, float topHeight, float bottom)
    {
        var points = new List<Vector3> { new Vector3(-width, 0, bottom), new Vector3(width, 0, bottom) };
        for (int i = 0; i <= ArcSegments; i++)
        {
            float theta = i * Mathf.PI / ArcSegments;
            points.Add(new Vector3(Mathf.Cos(theta) * width, 0, arcCenterZ + Mathf.Sin(theta) * topHeight));
        }
        return points;
    }

    public void Rebuild()
    {
        Release();
        if (flatSides)
        {
            var boundary = Boundary(wallHalfWidth, wallArcHeight, bottomZ);
            var flatVertices = new List<Vector3> { new Vector3(0, fieldHeight, 3) };
            foreach (var point in boundary) flatVertices.Add(new Vector3(point.x, fieldHeight, point.z));
            var flatTriangles = new List<int>();
            for (int i = 0; i < boundary.Count; i++)
                flatTriangles.AddRange(new[] { 0, 1 + (i + 1) % boundary.Count, 1 + i });
            // Retain only the smooth lower approach from the flipper area.
            int rampStart = flatVertices.Count;
            const int rows = 16;
            for (int row = 0; row <= rows; row++)
            {
                float z = bottomZ - entryRampLength * row / rows;
                flatVertices.Add(new Vector3(-wallHalfWidth, HeightAt(0, z), z));
                flatVertices.Add(new Vector3(wallHalfWidth, HeightAt(0, z), z));
                if (row == rows) continue;
                int a = rampStart + row * 2;
                flatTriangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            terrainMesh = MakeMesh("Continuous flat playfield", flatVertices, flatTriangles);
            GetComponent<MeshFilter>().sharedMesh = terrainMesh;
            GetComponent<MeshCollider>().sharedMesh = terrainMesh;
            var oldGutter = transform.Find("LowGutter");
            if (oldGutter) oldGutter.gameObject.SetActive(false);
            return;
        }
        var inner = Boundary(fieldHalfWidth, fieldArcHeight, bottomZ);
        var inset = Boundary(fieldHalfWidth - lipWidth, fieldArcHeight - lipWidth, bottomZ);
        var outer = Boundary(fieldHalfWidth + bevelWidth, fieldArcHeight + bevelWidth, bottomZ - entryRampLength);
        int count = inner.Count;
        for (int i = 2; i < count; i++)
        {
            float angle = (i - 2) * Mathf.PI / ArcSegments;
            var p = outer[i]; p.y = .14f * Mathf.Pow(Mathf.Sin(angle), 2) * (1f + .2f * Mathf.Cos(angle)); outer[i] = p;
        }
        var vertices = new List<Vector3> { new Vector3(0, fieldHeight, 3) };
        var triangles = new List<int>();
        // The lip lies inside the field, preserving gutter width and the original lower entry ramp.
        const int lipRows = 12, bevelRows = 8;
        for (int row = 0; row <= lipRows + bevelRows; row++)
        {
            bool onLip = row <= lipRows;
            float t = onLip ? row / (float)lipRows : (row - lipRows) / (float)bevelRows;
            for (int i = 0; i < count; i++)
            {
                var p = onLip ? Vector3.Lerp(inset[i], inner[i], t) : Vector3.Lerp(inner[i], outer[i], t);
                p.y = onLip ? fieldHeight + LipHeightAt(p.z) * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 2) * Mathf.SmoothStep(0, 1, p.z - bottomZ)
                    : Mathf.Lerp(fieldHeight, Mathf.Lerp(fieldHeight, outer[i].y, Mathf.SmoothStep(0, 1, t)), FeatureWeightAt(p.z));
                vertices.Add(p);
            }
        }
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            triangles.AddRange(new[] { 0, 1 + next, 1 + i });
            for (int row = 0; row < lipRows + bevelRows; row++)
            {
                int a = 1 + row * count + i, b = 1 + row * count + next;
                int c = a + count, d = b + count;
                triangles.AddRange(new[] { a, b, c, b, d, c });
            }
        }
        terrainMesh = MakeMesh("Raised field with rounded lip", vertices, triangles);
        GetComponent<MeshFilter>().sharedMesh = terrainMesh;
        var collider = GetComponent<MeshCollider>();
        collider.convex = false;
        collider.sharedMesh = terrainMesh;

        // Side gutters rise into plain continuous terrain over the short upper transition.
        var gutterInner = Boundary(fieldHalfWidth + bevelWidth, fieldArcHeight + bevelWidth, bottomZ - bevelWidth);
        var edge = Boundary(wallHalfWidth - .04f, wallArcHeight - .04f, bottomZ - bevelWidth);
        vertices.Clear(); triangles.Clear();
        for (int i = 0; i < count; i++)
        {
            var a = gutterInner[i]; a.y = Mathf.Lerp(fieldHeight, outer[i].y, FeatureWeightAt(a.z)) + .003f;
            var b = edge[i]; b.y = Mathf.Lerp(fieldHeight, outer[i].y, FeatureWeightAt(b.z)) + .003f;
            vertices.Add(a); vertices.Add(b);
        }
        var blue = new List<int>();
        var plain = new List<int>();
        for (int i = 1; i < count; i++)
        {
            int next = (i + 1) % count;
            int a = i * 2, b = next * 2;
            foreach (var tri in new[] { new[] { a, b, a + 1 }, new[] { b, b + 1, a + 1 } })
            {
                var target = Mathf.Min(vertices[tri[0]].z, Mathf.Min(vertices[tri[1]].z, vertices[tri[2]].z)) >= topTransitionEndZ ? plain : blue;
                target.AddRange(tri);
                triangles.AddRange(tri);
            }
        }
        gutterMesh = MakeMesh("Lower horseshoe gutter", vertices, triangles);
        gutterMesh.subMeshCount = 2;
        gutterMesh.SetTriangles(blue, 0);
        gutterMesh.SetTriangles(plain, 1);
        var gutter = transform.Find("LowGutter");
        if (!gutter)
        {
            var go = new GameObject("LowGutter", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            gutter = go.transform; gutter.SetParent(transform, false);
        }
        gutter.GetComponent<MeshFilter>().sharedMesh = gutterMesh;
        gutter.GetComponent<MeshRenderer>().sharedMaterials = new[] { gutterMaterial, GetComponent<MeshRenderer>().sharedMaterial };
        gutter.GetComponent<MeshCollider>().sharedMesh = gutterMesh;
    }

    static Mesh MakeMesh(string name, List<Vector3> vertices, List<int> triangles)
    {
        var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    void OnDisable() => Release();
    void Release()
    {
        if (GetComponent<MeshCollider>()) GetComponent<MeshCollider>().sharedMesh = null;
        if (GetComponent<MeshFilter>()) GetComponent<MeshFilter>().sharedMesh = null;
        var gutter = transform.Find("LowGutter");
        if (gutter) { gutter.GetComponent<MeshFilter>().sharedMesh = null; gutter.GetComponent<MeshCollider>().sharedMesh = null; }
        foreach (var mesh in new[] { terrainMesh, gutterMesh })
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        terrainMesh = gutterMesh = null;
    }
}
