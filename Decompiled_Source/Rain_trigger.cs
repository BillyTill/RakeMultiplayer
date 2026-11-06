using UnityEngine;

public class Rain_trigger : MonoBehaviour
{
	public void OnTriggerEnter(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			RainManager.instance.SetPlayerRainState(state: false);
		}
	}

	public void OnTriggerExit(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			RainManager.instance.SetPlayerRainState(state: true);
		}
	}
}
