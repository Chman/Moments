using UnityEngine;

[AddComponentMenu("")]
public class CubeKiller : MonoBehaviour
{
	void OnTriggerEnter(Collider other)
	{
		Destroy(other.gameObject);
	}
}
