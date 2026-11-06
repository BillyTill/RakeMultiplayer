using UnityEngine;

public class Food_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(5))
		{
			if (WeaponManager.instance.bFullAmmo(5))
			{
				InformationUI.instance.DrawHint("Full count of feed");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(5, 1);
			InformationUI.instance.PickupInfo("+ Add packet of feed 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(5, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add packet of feed 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
