using UnityEngine;

public class FPSCamera : MonoBehaviour
{
	public Animator anim;

	private void Update()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		Ray val = default(Ray);
		((Ray)(ref val))._002Ector(((Component)this).transform.position, ((Component)this).transform.forward);
		RaycastHit val2 = default(RaycastHit);
		if (Physics.Raycast(val, ref val2, 4f))
		{
			Debug.DrawLine(((Component)this).transform.position, ((RaycastHit)(ref val2)).point);
			if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Item>()))
			{
				Item component = ((Component)((RaycastHit)(ref val2)).collider).GetComponent<Item>();
				InformationUI.instance.DrawItemInfo("Press E to pickup " + component.name, showType: true);
				if (Input.GetButtonDown("Use"))
				{
					component.PickupItem();
				}
			}
			else
			{
				InformationUI.instance.DrawItemInfo(string.Empty, showType: false);
			}
			if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<DynamicObject>()))
			{
				DynamicObject component2 = ((Component)((RaycastHit)(ref val2)).collider).GetComponent<DynamicObject>();
				InformationUI.instance.DrawItemInfo(component2.name, showType: true);
				if (Input.GetButtonDown("Use"))
				{
					component2.Action();
				}
			}
		}
		else
		{
			InformationUI.instance.DrawItemInfo(string.Empty, showType: false);
		}
	}
}
