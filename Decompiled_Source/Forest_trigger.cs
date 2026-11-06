using UnityEngine;

public class Forest_trigger : Trigger
{
	public Objective obj;

	public override void Action(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			obj.ObjectiveAction();
		}
	}
}
