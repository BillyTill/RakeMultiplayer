using UnityEngine;

[RequireComponent(typeof(Animator))]
public class RootMotionScript : MonoBehaviour
{
	private void OnAnimatorMove()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		Animator component = ((Component)this).GetComponent<Animator>();
		if (Object.op_Implicit((Object)(object)component))
		{
			Vector3 position = ((Component)this).transform.position;
			position.z += component.GetFloat("Runspeed") * Time.deltaTime;
			((Component)this).transform.position = position;
		}
	}
}
