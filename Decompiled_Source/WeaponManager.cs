using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
	public List<Transform> playerWeapons = new List<Transform>();

	public List<int> weaponsAmmo = new List<int>();

	public List<Transform> allWeapons = new List<Transform>();

	public int currentWeapon;

	public int currentWeaponID;

	private int maxWeapon;

	public bool bChangeWeapon;

	public Animator anim;

	public Transform weaponSelect;

	public static WeaponManager instance;

	private void Start()
	{
		instance = this;
	}

	private void Update()
	{
		if (bChangeWeapon || FPSController.instance.playerState != FPSController.Player_state.Full)
		{
			return;
		}
		if (Input.GetKeyDown((KeyCode)49))
		{
			if (CheckWeapon(1))
			{
				SelectWeaponID(1);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have rifle");
			}
		}
		if (Input.GetKeyDown((KeyCode)50))
		{
			if (CheckWeapon(6))
			{
				SelectWeaponID(6);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have this weapon");
			}
		}
		if (Input.GetKeyDown((KeyCode)118))
		{
			if (CheckWeapon(5))
			{
				SelectWeaponID(5);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have feed");
			}
		}
		if (Input.GetKeyDown((KeyCode)116))
		{
			if (CheckWeapon(4))
			{
				SelectWeaponID(4);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have trap");
			}
		}
		if (Input.GetKeyDown((KeyCode)109))
		{
			if (CheckWeapon(3))
			{
				SelectWeaponID(3);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have map");
			}
		}
		if (Input.GetKeyDown((KeyCode)99))
		{
			if (CheckWeapon(2))
			{
				SelectWeaponID(2);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have camera");
			}
		}
		if (Input.GetKeyDown((KeyCode)98))
		{
			if (CheckWeapon(7))
			{
				SelectWeaponID(7);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have binoculars");
			}
		}
	}

	public List<Transform> GetPlayerWeapons()
	{
		return playerWeapons;
	}

	public void SelectWeapon(int id)
	{
		if (playerWeapons.Count > 0)
		{
			if (id > playerWeapons.Count - 1)
			{
				id = playerWeapons.Count - 1;
			}
			((MonoBehaviour)this).StartCoroutine("changeWeapon", (object)id);
		}
	}

	public void SelectWeaponID(int id)
	{
		for (int i = 0; i < playerWeapons.Count; i++)
		{
			if (((Component)playerWeapons[i]).GetComponent<Weapon>().weaponID == id)
			{
				((MonoBehaviour)this).StartCoroutine("changeWeapon", (object)i);
			}
		}
	}

	public void HideCurrentWeapon()
	{
		HideWeapon(currentWeapon);
	}

	public void HideWeapon(int id)
	{
		((MonoBehaviour)this).StartCoroutine("hideWeapon", (object)id);
	}

	public void AddWeapon(int id, int count, bool type)
	{
		if (CheckWeapon(id))
		{
			AddAmmo(id, count);
			return;
		}
		playerWeapons.Add(allWeapons[id]);
		AddAmmo(id, count);
	}

	public void RemoveWeapon(int id)
	{
		for (int i = 0; i < playerWeapons.Count; i++)
		{
			if (id == ((Component)playerWeapons[i]).GetComponent<Weapon>().weaponID)
			{
				if (id == currentWeaponID)
				{
					currentWeaponID = 0;
					weaponSelect = playerWeapons[0];
				}
				playerWeapons.Remove(playerWeapons[i]);
			}
		}
	}

	public int CurrentWeapon()
	{
		currentWeaponID = ((Component)weaponSelect).GetComponent<Weapon>().weaponID;
		return currentWeaponID;
	}

	public bool CheckWeapon(int id)
	{
		foreach (Transform playerWeapon in playerWeapons)
		{
			if (id == ((Component)playerWeapon).GetComponent<Weapon>().weaponID)
			{
				return true;
			}
		}
		return false;
	}

	public bool bFullAmmo(int id)
	{
		if (weaponsAmmo[id] < 1000)
		{
			return false;
		}
		return true;
	}

	public void AddAmmo(int id, int count)
	{
		List<int> list2;
		List<int> list = (list2 = weaponsAmmo);
		int index2;
		int index = (index2 = id);
		index2 = list2[index2];
		list[index] = index2 + count;
	}

	private IEnumerator hideWeapon(int i)
	{
		anim.SetBool("ChangeWeapon", true);
		bChangeWeapon = true;
		yield return (object)new WaitForSeconds(0.5f);
		anim.SetBool("ChangeWeapon", false);
		bChangeWeapon = false;
		((Component)playerWeapons[i]).gameObject.SetActive(false);
		currentWeaponID = 0;
		SelectWeapon(0);
	}

	private IEnumerator changeWeapon(int i)
	{
		anim.SetBool("ChangeWeapon", true);
		bChangeWeapon = true;
		yield return (object)new WaitForSeconds(0.5f);
		((Component)weaponSelect).gameObject.SetActive(false);
		anim.SetBool("ChangeWeapon", false);
		((Component)playerWeapons[i]).gameObject.SetActive(true);
		weaponSelect = playerWeapons[i];
		currentWeapon = i;
		yield return (object)new WaitForSeconds(1f);
		bChangeWeapon = false;
	}
}
