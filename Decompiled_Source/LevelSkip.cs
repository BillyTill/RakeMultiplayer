using System.Collections;
using UnityEngine;

public class LevelSkip : MonoBehaviour
{
	public GameObject HUD;

	public int levelToLoad;

	public CameraMouseLook cml;

	private void OnTriggerEnter(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			cml = ((Component)info).GetComponent<CameraMouseLook>();
			cml.bControl = false;
			HUD.SetActive(true);
			Cursor.lockState = (CursorLockMode)0;
			Cursor.visible = true;
			WeaponManager.instance.HideCurrentWeapon();
		}
	}

	private void OnTriggerExit(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			Exit();
		}
	}

	public void Load()
	{
	}

	public void Exit()
	{
		cml.bControl = true;
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		HUD.SetActive(false);
	}

	private IEnumerator wait()
	{
		SaveGameSystem.instance.SaveGame();
		FPSController.instance.effectAnimator.SetBool("Fade", true);
		HUD.SetActive(false);
		yield return (object)new WaitForSeconds(1f);
		Application.LoadLevel(levelToLoad);
	}

	public void SkipLevel(bool state)
	{
		if (state)
		{
			((MonoBehaviour)this).StartCoroutine(wait());
		}
		else
		{
			Exit();
		}
	}
}
