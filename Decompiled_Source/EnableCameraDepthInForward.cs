using UnityEngine;

[RequireComponent(typeof(Camera))]
public class EnableCameraDepthInForward : MonoBehaviour
{
	private void Start()
	{
		Set();
	}

	private void Set()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((Component)this).GetComponent<Camera>().depthTextureMode == 0)
		{
			((Component)this).GetComponent<Camera>().depthTextureMode = (DepthTextureMode)1;
		}
	}
}
