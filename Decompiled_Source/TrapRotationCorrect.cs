using UnityEngine;

public class TrapRotationCorrect : MonoBehaviour
{
	private void Update()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		Ray val = default(Ray);
		((Ray)(ref val))._002Ector(((Component)this).transform.position, Vector3.down);
		RaycastHit val2 = default(RaycastHit);
		if (Physics.Raycast(val, ref val2, 5f) && Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Terrain>()))
		{
			((Component)this).transform.rotation = Quaternion.LookRotation(((RaycastHit)(ref val2)).normal);
		}
	}
}
