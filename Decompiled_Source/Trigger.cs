using UnityEngine;

public class Trigger : MonoBehaviour
{
	public void OnTriggerEnter(Collider info)
	{
		Action(info);
	}

	public virtual void Action(Collider info)
	{
	}
}
