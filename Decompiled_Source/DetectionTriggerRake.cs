using UnityEngine;

public class DetectionTriggerRake : MonoBehaviour
{
	public Enemy_AI ai;

	private void OnTriggerEnter(Collider info)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<RecordCamera_obj>()) && ((Component)info).gameObject.GetComponent<RecordCamera_obj>().cameraState == RecordCamera_obj.Camera_state.Setup && CameraComputer.instance.bWatching)
		{
			Vector3 val = ((Component)info).gameObject.transform.TransformPoint(Vector3.forward * 2f);
			NavMeshHit val2 = default(NavMeshHit);
			NavMesh.SamplePosition(val, ref val2, 5f, 1);
			ai.currentBreakPoint = ((NavMeshHit)(ref val2)).position;
			ai.viewRot = ((Component)info).gameObject.transform.position;
			ai.cameraBreak = ((Component)info).gameObject.transform;
			ai.SetBreakState();
		}
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<Animal_AI>()) && !ai.bHaunt && !ai.bPlayerHount && ai.bHungry)
		{
			ai.target = ((Component)info).gameObject.transform;
			ai.bHaunt = true;
		}
		if (((Component)info).gameObject.tag == "Player")
		{
			ai.SetPlayerHountState();
			ai.playerTarget = ((Component)info).gameObject.transform;
		}
	}
}
