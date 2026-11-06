using UnityEngine;

public class Flashlight_trigger : Trigger
{
	public bool SetLightState;

	public override void Action(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			((Component)info).GetComponent<FPSController>().SetLightState(SetLightState);
		}
	}
}
