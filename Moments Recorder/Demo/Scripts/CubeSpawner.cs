using UnityEngine;
using System.Collections;

[AddComponentMenu("")]
public class CubeSpawner : MonoBehaviour
{
	public Transform CubePrefab;
	public float Interval = 0.5f;

	void Start()
	{
		if (CubePrefab == null)
			return;

		StartCoroutine(SpawnCube());
	}

	IEnumerator SpawnCube()
	{
		float timer = 0f;

		while (true)
		{
			timer += Time.deltaTime;

			if (timer > Interval)
			{
				timer = 0f;
				Instantiate(CubePrefab, transform.position, Random.rotationUniform);
			}

			yield return null;
		}
	}
}
