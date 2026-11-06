using UnityEngine;

public class HideZoneTrigger : MonoBehaviour
{
	private void OnTriggerEnter(Collider info)
	{
		if (((Component)info).gameObject.tag == "Player")
		{
			((Component)info).gameObject.GetComponent<FPSController>().bHide = true;
		}
	}

	private void OnTriggerExit(Collider info)
	{
		if (((Component)info).gameObject.tag == "Player")
		{
			((Component)info).gameObject.GetComponent<FPSController>().bHide = false;
		}
	}
}
