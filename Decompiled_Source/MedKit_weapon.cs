using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MedKit_weapon : Weapon
{
	public Animator animator;

	public AudioSource audioSource;

	public AudioClip useSound;

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
		ammoDisplay.text = ammo.ToString();
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		if (Input.GetButtonDown("Fire1") && WeaponManager.instance.weaponsAmmo[weaponID] > 0 && !bSetup)
		{
			((MonoBehaviour)this).StartCoroutine(Use());
		}
	}

	private IEnumerator Use()
	{
		bSetup = true;
		List<int> weaponsAmmo;
		List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
		int index2;
		int index = (index2 = weaponID);
		index2 = weaponsAmmo[index2];
		list[index] = index2 - 1;
		animator.SetBool("Use", true);
		audioSource.PlayOneShot(useSound);
		yield return (object)new WaitForSeconds(0.1f);
		FPSController.instance.Health += 100;
		if (FPSController.instance.Health > 100)
		{
			FPSController.instance.Health = 100;
		}
		yield return (object)new WaitForSeconds(0.2f);
		bSetup = false;
		yield return (object)new WaitForSeconds(0.5f);
		if (WeaponManager.instance.weaponsAmmo[weaponID] > 0)
		{
			animator.SetBool("Use", false);
			yield break;
		}
		WeaponManager.instance.RemoveWeapon(weaponID);
		UI_System.SetActive(false);
		((Component)this).gameObject.SetActive(false);
	}
}
