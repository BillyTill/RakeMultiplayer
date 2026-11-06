using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Inventory : MonoBehaviour
{
	public List<GameObject> items = new List<GameObject>();

	public List<Transform> playerItems = new List<Transform>();

	public RectTransform pos;

	public GameObject drawer;

	public Text text;

	public bool inventoryState;

	public GameObject inventoryObj;

	public AudioClip openSound;

	public AudioClip closeSound;

	public AudioSource audioSource;

	private void RefreshItems()
	{
		playerItems = WeaponManager.instance.GetPlayerWeapons();
		foreach (GameObject item in items)
		{
			item.gameObject.SetActive(false);
		}
		for (int i = 0; i < playerItems.Count; i++)
		{
			int weaponID = ((Component)playerItems[i]).GetComponent<Weapon>().weaponID;
			items[weaponID].SetActive(true);
			items[weaponID].GetComponent<Inventory_item>().count = ((Component)playerItems[i]).GetComponent<Weapon>().ammo;
		}
	}

	public void OpenInventory()
	{
		audioSource.PlayOneShot(openSound);
		drawer.SetActive(true);
		inventoryObj.SetActive(true);
		inventoryState = true;
		RefreshItems();
	}

	public void CloseInventory()
	{
		audioSource.PlayOneShot(closeSound);
		inventoryObj.SetActive(false);
		drawer.SetActive(false);
		inventoryState = false;
	}

	private void Update()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		if (!inventoryState)
		{
			return;
		}
		pos.anchoredPosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y - pos.sizeDelta.y);
		Ray val = Camera.main.ScreenPointToRay(Input.mousePosition);
		RaycastHit val2 = default(RaycastHit);
		if (Physics.Raycast(val, ref val2, 100f))
		{
			if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Inventory_item>()))
			{
				string text = ((Component)((RaycastHit)(ref val2)).collider).GetComponent<Inventory_item>().itemType.ToString();
				drawer.SetActive(true);
				this.text.text = "Name: " + ((Component)((RaycastHit)(ref val2)).collider).GetComponent<Inventory_item>().name + "\r\n" + text + " " + ((Component)((RaycastHit)(ref val2)).collider).GetComponent<Inventory_item>().count;
				if (Input.GetButtonDown("Fire1"))
				{
					WeaponManager.instance.SelectWeaponID(((Component)((RaycastHit)(ref val2)).collider).GetComponent<Inventory_item>().ID);
					FPSController.instance.Event_playerWeaponSelect();
				}
			}
			else
			{
				drawer.SetActive(false);
			}
		}
		else
		{
			drawer.SetActive(false);
		}
	}
}
