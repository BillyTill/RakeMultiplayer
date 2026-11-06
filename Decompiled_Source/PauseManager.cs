using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class PauseManager : MonoBehaviour
{
	public GameObject pausable;

	public Canvas pauseCanvas;

	private bool isPaused;

	private Animator anim;

	private Component[] pausableInterfaces;

	private Component[] quittableInterfaces;

	private void Start()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		if ((Object)(object)Object.FindObjectOfType<EventSystem>() == (Object)null)
		{
			GameObject val = new GameObject("EventSystem", new Type[1] { typeof(EventSystem) });
			val.AddComponent<StandaloneInputModule>();
		}
		pausableInterfaces = pausable.GetComponents(typeof(IPausable));
		quittableInterfaces = pausable.GetComponents(typeof(IQuittable));
		anim = ((Component)pauseCanvas).GetComponent<Animator>();
		((Behaviour)pauseCanvas).enabled = false;
	}

	private void Update()
	{
		if (Input.GetKeyDown((KeyCode)27))
		{
			if (isPaused)
			{
				OnUnPause();
			}
			else
			{
				OnPause();
			}
		}
		((Behaviour)pauseCanvas).enabled = isPaused;
		anim.SetBool("IsPaused", isPaused);
	}

	public void OnQuit()
	{
		Debug.Log((object)"PauseManager.OnQuit");
		Component[] array = quittableInterfaces;
		foreach (Component val in array)
		{
			((IQuittable)val)?.OnQuit();
		}
	}

	public void OnUnPause()
	{
		Debug.Log((object)"PauseManager.OnUnPause");
		isPaused = false;
		Component[] array = pausableInterfaces;
		foreach (Component val in array)
		{
			((IPausable)val)?.OnUnPause();
		}
	}

	public void OnPause()
	{
		Debug.Log((object)"PauseManager.OnPause");
		isPaused = true;
		Component[] array = pausableInterfaces;
		foreach (Component val in array)
		{
			((IPausable)val)?.OnPause();
		}
	}
}
