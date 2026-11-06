using UnityEngine;

public class RifleAmmo_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(1))
		{
			if (WeaponManager.instance.bFullAmmo(1))
			{
				InformationUI.instance.DrawHint("Full ammo in the Rifle");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(1, 6);
			InformationUI.instance.PickupInfo("+ Add ammo of rifle 6");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(1, 6);
			InformationUI.instance.PickupInfo("+ Add ammo of rifle 6");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
