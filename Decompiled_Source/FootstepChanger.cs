using UnityEngine;

public class FootstepChanger : MonoBehaviour
{
	public int stepID;

	private void OnTriggerEnter(Collider player)
	{
		if (((Component)player).tag == "Player")
		{
			((Component)player).GetComponent<PlayerMovement>().SetFootstepSound(stepID);
		}
	}

	private void OnTriggerExit(Collider player)
	{
		if (((Component)player).tag == "Player")
		{
			((Component)player).GetComponent<PlayerMovement>().SetFootstepSound(0);
		}
	}
}
