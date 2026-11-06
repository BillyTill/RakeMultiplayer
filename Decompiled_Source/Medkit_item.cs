using UnityEngine;

public class Medkit_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(8))
		{
			if (WeaponManager.instance.bFullAmmo(8))
			{
				InformationUI.instance.DrawHint("Full count of med kits");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(8, 1);
			InformationUI.instance.PickupInfo("+ Add medkit 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(8, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add medkit 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
