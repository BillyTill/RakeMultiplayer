using UnityEngine;

public class SniperAmmo_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(6))
		{
			if (WeaponManager.instance.bFullAmmo(6))
			{
				InformationUI.instance.DrawHint("Full ammo in the sniper-rifle");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(6, 6);
			InformationUI.instance.PickupInfo("+ Add ammo of sniper-rifle 6");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(6, 6);
			InformationUI.instance.PickupInfo("+ Add ammo of sniper-rifle 6");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
