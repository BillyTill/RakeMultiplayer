using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
	public float TD;

	private float timer;

	private void Update()
	{
		timer += Time.deltaTime;
		if (timer > TD)
		{
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
