using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Camera_weapon : Weapon
{
	public Animator animator;

	public Animator UI;

	public GameObject UI_System;

	public bool bSetup;

	public Text ammoDisplay;

	private void OnEnable()
	{
		UI_System.SetActive(true);
		bSetup = false;
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
	}

	private void Update()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		ammoDisplay.text = ammo.ToString();
		Ray val = default(Ray);
		((Ray)(ref val))._002Ector(((Component)this).transform.position, ((Component)this).transform.forward);
		RaycastHit val2 = default(RaycastHit);
		if (Physics.Raycast(val, ref val2, 2.5f) && Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<RECCameras_obj>()))
		{
			RECCameras_obj component = ((Component)((RaycastHit)(ref val2)).collider).GetComponent<RECCameras_obj>();
			if (Input.GetButtonDown("Use") && WeaponManager.instance.weaponsAmmo[weaponID] > 0 && !bSetup)
			{
				((MonoBehaviour)this).StartCoroutine(Setup(component));
			}
		}
	}

	private IEnumerator Setup(RECCameras_obj cam)
	{
		if (cam.CheckSetup())
		{
			bSetup = true;
			List<int> weaponsAmmo;
			List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
			int index2;
			int index = (index2 = weaponID);
			index2 = weaponsAmmo[index2];
			list[index] = index2 - 1;
			animator.SetBool("Setup", true);
			yield return (object)new WaitForSeconds(0.1f);
			cam.SetupCamera();
			yield return (object)new WaitForSeconds(0.2f);
			bSetup = false;
			yield return (object)new WaitForSeconds(0.5f);
			if (WeaponManager.instance.weaponsAmmo[weaponID] > 0)
			{
				animator.SetBool("Setup", false);
				yield break;
			}
			WeaponManager.instance.RemoveWeapon(weaponID);
			UI_System.SetActive(false);
			((Component)this).gameObject.SetActive(false);
		}
	}
}
