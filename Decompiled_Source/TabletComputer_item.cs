using UnityEngine;

public class TabletComputer_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(3))
		{
			if (WeaponManager.instance.bFullAmmo(3))
			{
				InformationUI.instance.DrawHint("You have this");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(3, 1);
			InformationUI.instance.PickupInfo("+ Add tablet-computer");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(3, 1, type: false);
			InformationUI.instance.PickupInfo("+ Add tablet-computer 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
