using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SniperRifle_weapon : Weapon
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

	public Camera sightRender;

	public GameObject muzzlePos;

	public GameObject decal;

	public GameObject muzzleFire;

	public CameraMouseLook cam;

	public PlayerMovement playerMove;

	public GameObject[] decals;

	public Material defaultMat;

	public Material sightMat;

	public GameObject sight;

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
		playerMove.bCanMove = true;
		cam.sensitivityX = 2f;
		cam.sensitivityY = 2f;
		((Behaviour)sightRender).enabled = false;
		animator.SetBool("Sight", false);
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
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		text.GetComponent<Text>().text = ammo.ToString();
		if (Input.GetMouseButtonDown(1))
		{
			sight.GetComponent<Renderer>().material = sightMat;
		}
		if (Input.GetMouseButtonUp(1))
		{
			sight.GetComponent<Renderer>().material = defaultMat;
		}
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
					((Component)((RaycastHit)(ref val2)).collider).GetComponent<Unit_obj>().TakeDamage(200, "SniperShot");
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
			playerMove.bCanMove = false;
			cam.sensitivityX = 0.5f;
			cam.sensitivityY = 0.5f;
			((Behaviour)sightRender).enabled = true;
			animator.SetBool("Sight", true);
		}
		else
		{
			playerMove.bCanMove = true;
			cam.sensitivityX = 2f;
			cam.sensitivityY = 2f;
			((Behaviour)sightRender).enabled = false;
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
		yield return (object)new WaitForSeconds(1f);
		int addBullet = ((WeaponManager.instance.weaponsAmmo[weaponID] - (clipSize - currentAmmo) < 0) ? 1 : (clipSize - currentAmmo));
		List<int> weaponsAmmo;
		List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
		int index2;
		int index = (index2 = weaponID);
		index2 = weaponsAmmo[index2];
		list[index] = index2 - (clipSize - currentAmmo);
		if (ammo < 0)
		{
			ammo = 0;
		}
		currentAmmo += addBullet;
		UI.SetInteger("Bullets", currentAmmo);
		bCanFire = true;
		bReload = false;
		animator.SetBool("Reload", false);
	}
}
