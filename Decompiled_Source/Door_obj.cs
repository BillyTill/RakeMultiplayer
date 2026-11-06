using System.Collections;
using UnityEngine;

public class Door_obj : DynamicObject
{
	public enum Door_state
	{
		Open,
		Close
	}

	public Door_state doorState = Door_state.Close;

	public GameObject door;

	private bool isProcessing;

	public AudioClip doorOpenSound;

	public AudioClip doorCloseSound;

	public override void Action()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		if (doorState == Door_state.Close && !isProcessing)
		{
			AudioSource.PlayClipAtPoint(doorOpenSound, ((Component)this).transform.position);
			door.GetComponent<Animation>().Play("Door_open");
			((MonoBehaviour)this).StartCoroutine(coroutine(Door_state.Open));
		}
		if (doorState == Door_state.Open && !isProcessing)
		{
			AudioSource.PlayClipAtPoint(doorCloseSound, ((Component)this).transform.position);
			door.GetComponent<Animation>().Play("Door_close");
			((MonoBehaviour)this).StartCoroutine(coroutine(Door_state.Close));
		}
	}

	private IEnumerator coroutine(Door_state state)
	{
		isProcessing = true;
		yield return (object)new WaitForSeconds(0.5f);
		doorState = state;
		if (state == Door_state.Open)
		{
			name = "Press \"E\" to close door";
		}
		else
		{
			name = "Press \"E\" to open door";
		}
		yield return (object)new WaitForSeconds(0.5f);
		isProcessing = false;
	}
}
