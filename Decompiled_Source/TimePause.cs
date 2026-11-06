using UnityEngine;

public class TimePause : MonoBehaviour, IPausable
{
	public float pauseDelay = 0.3f;

	private float timeScale;

	public void OnUnPause()
	{
		Debug.Log((object)"TestPause.OnUnPause");
		Time.timeScale = timeScale;
	}

	public void OnPause()
	{
		Debug.Log((object)"TestPause.OnPause");
		timeScale = Time.timeScale;
		((MonoBehaviour)this).Invoke("StopTime", pauseDelay);
	}

	private void StopTime()
	{
		Time.timeScale = 0f;
	}
}
