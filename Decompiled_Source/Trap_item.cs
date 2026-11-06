using UnityEngine;

public class Trap_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(4))
		{
			if (WeaponManager.instance.bFullAmmo(4))
			{
				InformationUI.instance.DrawHint("Full count of traps");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(4, 1);
			InformationUI.instance.PickupInfo("+ Add trap 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(4, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add trap 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
