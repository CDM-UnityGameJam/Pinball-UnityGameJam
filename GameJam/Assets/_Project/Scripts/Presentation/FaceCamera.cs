using UnityEngine;

namespace Pinball.Presentation
{
    // Billboard: keeps a flat sprite (e.g. an enemy's monster art) turned toward the main camera.
    public sealed class FaceCamera : MonoBehaviour
    {
        Camera target;

        void LateUpdate()
        {
            if (!target) target = Camera.main;
            if (target) transform.rotation = target.transform.rotation;
        }
    }
}
