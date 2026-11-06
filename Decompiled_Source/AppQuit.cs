using UnityEngine;

public class AppQuit : MonoBehaviour, IQuittable
{
	public void OnQuit()
	{
		Debug.Log((object)"AppQuit.Quit");
		Application.Quit();
	}
}
