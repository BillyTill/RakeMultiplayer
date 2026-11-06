using UnityEngine;

public class GamePauseMenu : MonoBehaviour
{
	public bool isPaused;

	public GameObject menu;

	private void Update()
	{
		if (Input.GetButtonDown("Escape"))
		{
			isPaused = !isPaused;
			if (isPaused)
			{
				menu.SetActive(true);
				FPSController.instance.SetPlayerDisableState("PAUSEMENU");
				Time.timeScale = 0f;
				Cursor.lockState = (CursorLockMode)0;
				Cursor.visible = true;
			}
			else
			{
				menu.SetActive(false);
				FPSController.instance.SetPlayerDisableState("BEFOREPAUSE");
				Time.timeScale = 1f;
				Cursor.lockState = (CursorLockMode)1;
				Cursor.visible = false;
			}
		}
	}

	public void Continue()
	{
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		menu.SetActive(false);
		FPSController.instance.SetPlayerDisableState("BEFOREPAUSE");
		Time.timeScale = 1f;
	}

	public void Exit()
	{
		Application.LoadLevel(0);
	}
}
