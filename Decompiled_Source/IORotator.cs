using UnityEngine;

public class IORotator : MonoBehaviour
{
	public Vector3 rot;

	private void Start()
	{
	}

	private void Update()
	{
		((Component)this).transform.Rotate(rot.x * Time.deltaTime, rot.y * Time.deltaTime, 0f);
	}
}
