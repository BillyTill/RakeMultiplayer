using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Food_weapon : Weapon
{
	public Animator animator;

	public AudioSource audioSource;

	public AudioClip use;

	public Transform spawnPoint;

	public GameObject projectile;

	public Animator UI;

	public GameObject UI_System;

	public Transform sight;

	public bool bSetup;

	public Text ammoDisplay;

	private bool bCanSetup;

	public Transform camera;

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
		if (Object.op_Implicit((Object)(object)sight))
		{
			((Component)sight).gameObject.SetActive(false);
		}
	}

	private void Update()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		ammoDisplay.text = ammo.ToString();
		Ray val = default(Ray);
		((Ray)(ref val))._002Ector(camera.position, camera.forward);
		RaycastHit val2 = default(RaycastHit);
		if (Physics.Raycast(val, ref val2, 3f))
		{
			if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<TerrainCollider>()) && ((Object)((RaycastHit)(ref val2)).collider).name == ((Object)Terrain.activeTerrain).name)
			{
				float num = Terrain.activeTerrain.SampleHeight(((RaycastHit)(ref val2)).point);
				if (((RaycastHit)(ref val2)).point.y <= num + 0.01f)
				{
					bCanSetup = true;
					((Component)sight).gameObject.SetActive(true);
					sight.position = ((RaycastHit)(ref val2)).point;
				}
				else
				{
					bCanSetup = false;
				}
			}
			else
			{
				bCanSetup = false;
				((Component)sight).gameObject.SetActive(false);
			}
		}
		else
		{
			bCanSetup = false;
			((Component)sight).gameObject.SetActive(false);
		}
		if (Input.GetButtonDown("Fire1") && !bSetup && bCanSetup)
		{
			((MonoBehaviour)this).StartCoroutine(Setup());
		}
	}

	private IEnumerator Setup()
	{
		Ray ray = new Ray(camera.position, camera.forward);
		RaycastHit hit = default(RaycastHit);
		if (Physics.Raycast(ray, ref hit, 3f) && Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref hit)).collider).GetComponent<TerrainCollider>()))
		{
			bSetup = true;
			List<int> weaponsAmmo;
			List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
			int index2;
			int index = (index2 = weaponID);
			index2 = weaponsAmmo[index2];
			list[index] = index2 - 1;
			animator.SetBool("Setup", true);
			audioSource.PlayOneShot(use);
			yield return (object)new WaitForSeconds(0.7f);
			Vector3 spawnPos = ((RaycastHit)(ref hit)).point;
			GameObject point = (GameObject)Object.Instantiate((Object)(object)projectile, spawnPos, Quaternion.identity);
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
}
