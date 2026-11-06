using UnityEngine;

public class BoxOfCameras_item : Item
{
	public Objective objective;

	public GameObject obj;

	public override void PickupItem()
	{
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		if (count > 0)
		{
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
				count--;
				if (count <= 0)
				{
					obj.SetActive(false);
				}
				if ((Object)(object)objective != (Object)null)
				{
					objective.ObjectiveAction();
				}
			}
			else
			{
				AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
				WeaponManager.instance.AddWeapon(2, 1, type: true);
				InformationUI.instance.PickupInfo("+ Add camera 1");
				if ((Object)(object)objective != (Object)null)
				{
					objective.ObjectiveAction();
				}
				count--;
				if (count <= 0)
				{
					obj.SetActive(false);
				}
			}
		}
		else
		{
			InformationUI.instance.DrawHint("Empty");
			obj.SetActive(false);
		}
	}
}
