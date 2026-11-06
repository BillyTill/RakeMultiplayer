using UnityEngine;

public class AnimalHead : MonoBehaviour
{
	public Transform target;

	public float fieldOfViewAngle;

	public Animal_AI animal_AI;

	public bool bSeen()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		target = animal_AI.enemy;
		if ((Object)(object)target != (Object)null)
		{
			Vector3 val = ((Component)target).transform.position - ((Component)this).transform.position;
			Vector3 forward = ((Component)this).transform.forward;
			float num = Vector3.Angle(val, forward);
			if (num < fieldOfViewAngle)
			{
				Debug.Log((object)"See");
				return true;
			}
			Debug.Log((object)"Unseen");
			return false;
		}
		return false;
	}
}
