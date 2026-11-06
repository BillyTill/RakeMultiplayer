using UnityEngine;

public class Item : MonoBehaviour
{
	public string name;

	public int count;

	public int id;

	public AudioClip pickupSound;

	public virtual void PickupItem()
	{
	}
}
