using UnityEngine;

public class Inventory_item : MonoBehaviour
{
	public enum ItemType
	{
		Count,
		Ammo
	}

	public string name;

	public int ID;

	public int count;

	public ItemType itemType;
}
