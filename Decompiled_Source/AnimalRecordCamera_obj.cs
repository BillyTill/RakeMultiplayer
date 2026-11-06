using UnityEngine;

public class AnimalRecordCamera_obj : RECCameras_obj
{
	public GameObject setupCamera;

	public Transform parent;

	public Transform collar;

	public Transform backlight;

	public override void Action()
	{
		if (WeaponManager.instance.CurrentWeapon() != 2)
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
	}

	private void Update()
	{
		UpdateColor();
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

	public override void SetupCamera()
	{
		((Component)backlight).gameObject.SetActive(false);
		Debug.Log((object)"FF");
		setupCamera.gameObject.SetActive(true);
		((Component)collar).gameObject.SetActive(true);
		CameraManager.instance.AddCamera(setupCamera.transform);
		Object.Destroy((Object)(object)((Component)this).gameObject);
	}
}
