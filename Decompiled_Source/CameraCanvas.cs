using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraCanvas : MonoBehaviour
{
	public List<Image> points = new List<Image>();

	private void OnEnable()
	{
		GetCamerasState();
	}

	private void GetCamerasState()
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		for (int i = 0; i < CameraManager.instance.allCameras.Count; i++)
		{
			if (((Component)CameraManager.instance.allCameras[i]).GetComponent<RecordCamera_obj>().cameraState == RecordCamera_obj.Camera_state.Setup)
			{
				((Graphic)points[i]).color = Color.green;
			}
			else
			{
				((Graphic)points[i]).color = Color.red;
			}
		}
	}
}
