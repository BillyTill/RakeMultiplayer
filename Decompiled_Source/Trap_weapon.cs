using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Trap_weapon : Weapon
{
	public Animator animator;

	public Transform spawnPoint;

	public GameObject projectile;

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
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		ammoDisplay.text = ammo.ToString();
		if (Input.GetButtonDown("Fire1") && !bSetup)
		{
			((MonoBehaviour)this).StartCoroutine(Setup());
		}
	}

	private IEnumerator Setup()
	{
		bSetup = true;
		List<int> weaponsAmmo;
		List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
		int index2;
		int index = (index2 = weaponID);
		index2 = weaponsAmmo[index2];
		list[index] = index2 - 1;
		animator.SetBool("Setup", true);
		yield return (object)new WaitForSeconds(0.7f);
		GameObject obj = (GameObject)Object.Instantiate((Object)(object)projectile, spawnPoint.position, spawnPoint.rotation);
		LevelItemsManager.instance.AddTrap(obj);
		yield return (object)new WaitForSeconds(0.2f);
		yield return (object)new WaitForSeconds(0.5f);
		if (WeaponManager.instance.weaponsAmmo[weaponID] > 0)
		{
			animator.SetBool("Setup", false);
		}
		else
		{
			WeaponManager.instance.RemoveWeapon(weaponID);
			UI_System.SetActive(false);
			((Component)this).gameObject.SetActive(false);
		}
		yield return (object)new WaitForSeconds(1f);
		bSetup = false;
	}
}
