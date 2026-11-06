using UnityEngine;

public class Log_trigger : Trigger
{
	public string text;

	public override void Action(Collider info)
	{
		if (((Component)info).gameObject.tag == "Player")
		{
			InformationUI.instance.DrawHint(text);
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
