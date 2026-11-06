using UnityEngine;

public class showcasecam : MonoBehaviour
{
	public Transform rotationPoint;

	private void Start()
	{
	}

	private void Update()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		((Component)this).transform.RotateAround(rotationPoint.position, Vector3.up, 20f * Time.deltaTime);
	}
}
