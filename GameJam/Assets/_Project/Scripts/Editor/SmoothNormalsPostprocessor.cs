using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pinball.Editor
{
    // Bakes position-averaged normals into UV channel 7 of every model under Art/Models, so the
    // Pinball/Toon outline pass can push hard-edged meshes out without splitting at the corners.
    sealed class SmoothNormalsPostprocessor : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Art/Models/";
        const int Channel = 7;

        public override uint GetVersion() => 2;

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Folder)) return;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh) Bake(filter.sharedMesh);
            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skinned.sharedMesh) Bake(skinned.sharedMesh);
        }

        static void Bake(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (normals.Length != vertices.Length) return;
            var sums = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                sums.TryGetValue(vertices[i], out var sum);
                sums[vertices[i]] = sum + normals[i];
            }
            var smooth = new List<Vector3>(vertices.Length);
            // Opposite normals at one position (double-sided/zero-thickness parts) cancel out: keep the vertex's own.
            for (int i = 0; i < vertices.Length; i++)
            {
                var sum = sums[vertices[i]];
                smooth.Add(sum.sqrMagnitude > 1e-8f ? sum.normalized : normals[i]);
            }
            mesh.SetUVs(Channel, smooth);
        }
    }
}
