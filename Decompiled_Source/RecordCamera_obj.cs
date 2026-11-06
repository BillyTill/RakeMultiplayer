using UnityEngine;
using UnityEngine.UI;

public class RecordCamera_obj : RECCameras_obj
{
	public enum Camera_state
	{
		Unsetup,
		Setup
	}

	public bool isRecord;

	public Text text;

	public GameObject setupCamera;

	public GameObject cameraPoint;

	public GameObject cameraRender;

	public Camera_state cameraState;

	public Transform backlight;

	public Transform currentTarget;

	public Transform camRec;

	public AudioClip setupSound;

	public int cameraID;

	private void Start()
	{
		name = "Press 'E' to setup camera";
	}

	private void AddCameraItem()
	{
		if (WeaponManager.instance.CheckWeapon(2))
		{
			if (WeaponManager.instance.bFullAmmo(2))
			{
				InformationUI.instance.DrawHint("Full count of cameras");
				return;
			}
			WeaponManager.instance.AddAmmo(2, 1);
			InformationUI.instance.PickupInfo("+ Add camera 1");
			SetUnsetupState();
		}
		else
		{
			WeaponManager.instance.AddWeapon(2, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add camera 1");
			SetUnsetupState();
		}
	}

	public void SetSetupState()
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		cameraState = Camera_state.Setup;
		setupCamera.SetActive(true);
		cameraPoint.SetActive(false);
		cameraRender.SetActive(false);
		CameraManager.instance.AddCamera(setupCamera.transform);
		AudioSource.PlayClipAtPoint(setupSound, ((Component)this).transform.position);
		name = "Press 'E' to pickup camera ";
	}

	public void SetUnsetupState()
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		cameraState = Camera_state.Unsetup;
		setupCamera.SetActive(false);
		cameraPoint.SetActive(true);
		cameraRender.SetActive(false);
		CameraManager.instance.RemoveCamera(setupCamera.transform);
		AudioSource.PlayClipAtPoint(setupSound, ((Component)this).transform.position);
		name = "Press 'E' to setup camera ";
	}

	public override void SetupCamera()
	{
		if (cameraState == Camera_state.Unsetup)
		{
			SetSetupState();
		}
	}

	public override void Action()
	{
		if (cameraState == Camera_state.Unsetup && WeaponManager.instance.CurrentWeapon() != 2)
		{
			if (WeaponManager.instance.CheckWeapon(2))
			{
				InformationUI.instance.DrawHint("Please select camera");
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have camera");
			}
		}
		if (cameraState == Camera_state.Setup && WeaponManager.instance.CurrentWeapon() != 2)
		{
			Debug.Log((object)"addCAmeraitem");
			AddCameraItem();
		}
	}

	private void Update()
	{
		if (cameraState == Camera_state.Unsetup)
		{
			UpdateColor();
		}
		if (cameraState == Camera_state.Setup && !((Object)(object)currentTarget != (Object)null))
		{
		}
	}

	private void UpdateColor()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		Renderer component = ((Component)backlight).GetComponent<Renderer>();
		Color val = default(Color);
		((Color)(ref val))._002Ector(Color.yellow.r, Color.yellow.g, Color.yellow.b, Mathf.PingPong(Time.time * 0.1f, 0.1f));
		component.material.SetColor("_TintColor", val);
	}

	public override bool CheckSetup()
	{
		if (cameraState == Camera_state.Unsetup)
		{
			if (CameraManager.instance.cameraCount >= 5)
			{
				InformationUI.instance.DrawHint("You cannot setup no more 5 cameras");
				return false;
			}
			return true;
		}
		AddCameraItem();
		return false;
	}
}
