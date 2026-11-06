using UnityEngine;

public class Camera_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(2))
		{
			if (WeaponManager.instance.bFullAmmo(2))
			{
				InformationUI.instance.DrawHint("Full count of cameras");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(2, 1);
			InformationUI.instance.PickupInfo("+ Add camera 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(2, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add camera 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
