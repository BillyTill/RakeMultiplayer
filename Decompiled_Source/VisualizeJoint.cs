using UnityEngine;

[ExecuteInEditMode]
public class VisualizeJoint : MonoBehaviour
{
	private void Start()
	{
	}

	private void Update()
	{
	}

	private void OnDrawGizmos()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		Gizmos.color = Color.red;
		Gizmos.DrawSphere(((Component)this).transform.position, 0.015f);
		foreach (Transform item in ((Component)this).transform)
		{
			Transform val = item;
			Gizmos.DrawLine(((Component)this).transform.position, val.position);
		}
	}
}
