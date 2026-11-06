using System.Collections;
using UnityEngine;

public class CameraComputer : DynamicObject
{
	public Animator effects;

	public bool processing;

	public bool bWatching;

	public GameObject cameraManager_UI;

	public float rotationSpeed;

	public float smoothTime;

	private Quaternion cameraTargetRot = Quaternion.identity;

	private float rotationY;

	public AudioSource audioSource;

	public AudioSource motion;

	public static CameraComputer instance;

	public AudioClip startSound;

	public override void Action()
	{
		if (!processing)
		{
			if (CameraManager.instance.GetCameraCount() > 0)
			{
				((MonoBehaviour)this).StartCoroutine(changeType(type: false));
			}
			else
			{
				InformationUI.instance.DrawHint("You did not setup cameras");
			}
		}
	}

	private void Start()
	{
		instance = this;
	}

	private void Update()
	{
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		if (bWatching)
		{
			if (Input.GetKeyDown((KeyCode)101))
			{
				((MonoBehaviour)this).StartCoroutine(changeType(type: true));
			}
			motion.volume = Mathf.Abs(Input.GetAxis("Horizontal"));
			rotationY += Input.GetAxis("Horizontal") * rotationSpeed;
			rotationY = Mathf.Clamp(rotationY, -45f, 45f);
			if ((Object)(object)CameraManager.instance.currentCamera != (Object)null)
			{
				CameraManager.instance.currentCamera.localEulerAngles = new Vector3(((Component)this).transform.localEulerAngles.x, rotationY, 0f);
			}
		}
	}

	public void Exit()
	{
		((MonoBehaviour)this).StartCoroutine(changeType(type: true));
	}

	public void ResetCameraPosition()
	{
		rotationY = 0f;
		((MonoBehaviour)this).StartCoroutine(switchEffect());
	}

	private IEnumerator switchEffect()
	{
		effects.SetBool("Switch", true);
		yield return (object)new WaitForSeconds(0.2f);
		effects.SetBool("Switch", false);
	}

	private IEnumerator changeType(bool type)
	{
		if (!type)
		{
			audioSource.PlayOneShot(startSound);
			effects.SetBool("Fade", true);
			bWatching = false;
			processing = true;
			yield return (object)new WaitForSeconds(1f);
			FPSController.instance.DisablePlayer();
			effects.SetBool("Fade", false);
			CameraManager.instance.ActiveCamera(0);
			yield return (object)new WaitForSeconds(1f);
			cameraManager_UI.SetActive(true);
			bWatching = true;
			Cursor.lockState = (CursorLockMode)0;
			Cursor.visible = true;
			motion.mute = false;
			processing = false;
			yield break;
		}
		audioSource.PlayOneShot(startSound);
		effects.SetBool("Fade", true);
		bWatching = false;
		motion.mute = true;
		processing = true;
		yield return (object)new WaitForSeconds(1f);
		FPSController.instance.EnablePlayer();
		effects.SetBool("Fade", false);
		CameraManager.instance.DisableAllCameras();
		cameraManager_UI.SetActive(false);
		yield return (object)new WaitForSeconds(1f);
		if ((Object)(object)RainManager.instance != (Object)null)
		{
			RainManager.instance.currentPlayer = RainManager.instance.defaultPlayer;
		}
		bWatching = false;
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		processing = false;
	}
}
