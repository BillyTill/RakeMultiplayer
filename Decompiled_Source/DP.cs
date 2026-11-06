using UnityEngine;

public class DP : MonoBehaviour
{
	public RectTransform rt;

	public float scale;

	public float scaleZ;

	public Vector3 vec;

	private void Start()
	{
	}

	private void Update()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = ((Component)this).transform.position;
		float x = position.z / -100f;
		float y = position.x / 100f;
		vec.x = x;
		vec.y = y;
		rt.anchoredPosition = Vector2.op_Implicit(vec);
		((Transform)rt).localEulerAngles = new Vector3(0f, 0f, 0f - ((Component)this).transform.eulerAngles.y);
	}
}
