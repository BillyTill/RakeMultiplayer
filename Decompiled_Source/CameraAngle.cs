using UnityEngine;

public class CameraAngle : MonoBehaviour
{
	public Transform camera;

	public Transform target;

	private bool isView;

	public Transform cam;

	public int id;

	public void OnTriggerEnter(Collider info)
	{
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<Unit_obj>()))
		{
			target = ((Component)info).gameObject.transform;
		}
	}

	public void OnTriggerExit(Collider info)
	{
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<Unit_obj>()) && (Object)(object)target == (Object)(object)((Component)info).gameObject.transform)
		{
			isView = false;
			target = null;
			CameraManager.instance.Event_disableCamera(id);
		}
	}

	private void Update()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)target != (Object)null)
		{
			Vector3 val = ((Component)target).transform.position - ((Component)this).transform.position;
			if (Vector3.Angle(((Component)camera).transform.position, val) < 60f && !isView)
			{
				isView = true;
				id = CameraManager.instance.FindCameraID(cam);
				CameraManager.instance.Event_activeCamera(id);
			}
		}
	}
}
