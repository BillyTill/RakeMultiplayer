using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Rifle_weapon : Weapon
{
	public Animator animator;

	public Animator UI;

	public GameObject UI_System;

	public bool bSight;

	private GameObject text;

	public Transform caster;

	public bool bReload;

	public bool bCanFire = true;

	public AudioSource SFX;

	public AudioClip fireSound;

	public AudioClip reloadSound;

	public int currentAmmo = 2;

	public int clipSize = 2;

	public GameObject[] decals;

	public GameObject muzzlePos;

	public GameObject decal;

	public GameObject muzzleFire;

	private void OnEnable()
	{
		UI_System.SetActive(true);
		bSight = false;
		bReload = false;
		bCanFire = true;
		UI.SetInteger("Bullets", currentAmmo);
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
	}

	private void Start()
	{
		text = ((Component)UI_System.transform.FindChild("Ammo_display")).gameObject;
	}

	private void Update()
	{
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		text.GetComponent<Text>().text = ammo.ToString();
		if (Input.GetButton("Fire2"))
		{
			bSight = true;
		}
		else
		{
			bSight = false;
		}
		if (Input.GetButtonDown("Fire1") && bSight && bCanFire && currentAmmo > 0)
		{
			((MonoBehaviour)this).StartCoroutine(Fire());
			Ray val = default(Ray);
			((Ray)(ref val))._002Ector(caster.position, caster.forward);
			RaycastHit val2 = default(RaycastHit);
			if (Physics.Raycast(val, ref val2, 1000f))
			{
				NotificationsManager.Instance.PostNotification((Component)(object)this, "Event_PlayerFire");
				if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Unit_obj>()))
				{
					((Component)((RaycastHit)(ref val2)).collider).GetComponent<Unit_obj>().TakeDamage(100, "SniperShot");
					Object.Instantiate((Object)(object)decals[1], ((RaycastHit)(ref val2)).point + ((RaycastHit)(ref val2)).normal * 0.001f, Quaternion.identity);
				}
				if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Window_obj>()))
				{
					((Component)((RaycastHit)(ref val2)).collider).GetComponent<Window_obj>().TakeDamage(100, "SniperShot");
				}
				Object.Instantiate((Object)(object)decals[0], ((RaycastHit)(ref val2)).point + ((RaycastHit)(ref val2)).normal * 0.001f, Quaternion.identity);
			}
		}
		if (Input.GetButtonDown("Reload") && currentAmmo < 2 && WeaponManager.instance.weaponsAmmo[weaponID] > 0 && !bReload)
		{
			Debug.Log((object)"reload");
			((MonoBehaviour)this).StartCoroutine(Reload());
		}
		if (bSight)
		{
			animator.SetBool("Sight", true);
		}
		else
		{
			animator.SetBool("Sight", false);
		}
	}

	private IEnumerator Fire()
	{
		bCanFire = false;
		Object.Instantiate((Object)(object)muzzleFire, muzzlePos.transform.position, muzzlePos.transform.rotation);
		animator.SetBool("Fire", true);
		currentAmmo--;
		SFX.PlayOneShot(fireSound);
		UI.SetInteger("Bullets", currentAmmo);
		yield return (object)new WaitForSeconds(0.2f);
		animator.SetBool("Fire", false);
		yield return (object)new WaitForSeconds(1f);
		bCanFire = true;
	}

	private IEnumerator Reload()
	{
		bCanFire = false;
		animator.SetBool("Reload", true);
		bReload = true;
		yield return (object)new WaitForSeconds(0.5f);
		SFX.PlayOneShot(reloadSound);
		yield return (object)new WaitForSeconds(1.5f);
		int addBullet = ((WeaponManager.instance.weaponsAmmo[weaponID] - (clipSize - currentAmmo) < 0) ? 1 : (clipSize - currentAmmo));
		List<int> weaponsAmmo;
		List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
		int index2;
		int index = (index2 = weaponID);
		index2 = weaponsAmmo[index2];
		list[index] = index2 - (clipSize - currentAmmo);
		if (WeaponManager.instance.weaponsAmmo[weaponID] < 0)
		{
			WeaponManager.instance.weaponsAmmo[weaponID] = 0;
		}
		currentAmmo += addBullet;
		UI.SetInteger("Bullets", currentAmmo);
		bCanFire = true;
		bReload = false;
		animator.SetBool("Reload", false);
	}
}
