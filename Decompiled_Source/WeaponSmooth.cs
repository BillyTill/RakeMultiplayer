using UnityEngine;

public class WeaponSmooth : MonoBehaviour
{
	public float amount = 0.02f;

	public float maxAmount = 0.03f;

	public float smooth = 3f;

	private Vector3 def;

	private void Start()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		def = ((Component)this).transform.localPosition;
	}

	private void Update()
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		float num = (0f - Input.GetAxis("Mouse X")) * amount;
		float num2 = (0f - Input.GetAxis("Mouse Y")) * amount;
		if (num > maxAmount)
		{
			num = maxAmount;
		}
		if (num < 0f - maxAmount)
		{
			num = 0f - maxAmount;
		}
		if (num2 > maxAmount)
		{
			num2 = maxAmount;
		}
		if (num2 < 0f - maxAmount)
		{
			num2 = 0f - maxAmount;
		}
		Vector3 val = default(Vector3);
		((Vector3)(ref val))._002Ector(def.x + num, def.y + num2, def.z);
		((Component)this).transform.localPosition = Vector3.Lerp(((Component)this).transform.localPosition, val, Time.deltaTime * smooth);
	}
}
