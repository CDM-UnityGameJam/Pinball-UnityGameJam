using UnityEngine;

public class Drain : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) GameManager.Instance.OnDrain();
    }
}
