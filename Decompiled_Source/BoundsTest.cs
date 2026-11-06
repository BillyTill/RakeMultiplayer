using UnityEngine;

public class BoundsTest : MonoBehaviour
{
	public Transform pointA;

	public Transform pointB;

	public float angle;

	public float rayWidth;

	public GameObject edge;

	private RaycastHit hitBS;

	private void Start()
	{
	}

	private void Update()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		if (Input.GetButtonDown("Jump"))
		{
			test();
		}
		angle = ((Component)this).transform.rotation.x;
		rayWidth = angle * 2f;
		RaycastHit val = default(RaycastHit);
		if (Physics.Raycast(((Component)this).transform.position + ((Component)this).transform.forward * 0.5f + ((Component)this).transform.up * 2f, -Vector3.up, ref val))
		{
		}
		if (Physics.Raycast(((Component)this).transform.position + ((Component)this).transform.forward * 0.5f + ((Component)this).transform.up * 2f, -Vector3.up, ref hitBS))
		{
		}
		Debug.DrawRay(((Component)this).transform.position + ((Component)this).transform.forward * 0.5f + ((Component)this).transform.up * 2f, -Vector3.up);
		Debug.DrawRay(((RaycastHit)(ref hitBS)).point - new Vector3(0f, 0.1f, 0f) - ((Component)this).transform.forward * 0.5f, ((Component)this).transform.forward);
	}

	private void OnDrawGizmos()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		Gizmos.color = Color.white;
		Gizmos.DrawWireSphere(((Component)this).transform.position + ((Component)this).transform.forward * 0.5f + ((Component)this).transform.up * 2f, 0.1f);
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(((RaycastHit)(ref hitBS)).point - new Vector3(0f, 0.1f, 0f) - ((Component)this).transform.forward * 0.5f, new Vector3(0.1f, 0.1f, 0.1f));
	}

	private void test()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Expected O, but got Unknown
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		RaycastHit val = default(RaycastHit);
		if (!Physics.Raycast(((Component)this).transform.position + ((Component)this).transform.forward * 0.5f + ((Component)this).transform.up * 2f, -Vector3.up, ref val))
		{
			return;
		}
		MonoBehaviour.print((object)((RaycastHit)(ref val)).distance);
		RaycastHit val2 = default(RaycastHit);
		if (((RaycastHit)(ref val)).distance < 1f && Physics.Raycast(((RaycastHit)(ref val)).point - new Vector3(0f, 0.1f, 0f) - ((Component)this).transform.forward * 0.5f, ((Component)this).transform.forward, ref val2))
		{
			MonoBehaviour.print((object)"ss");
			float distance = ((RaycastHit)(ref val)).distance;
			if (distance > 0f && distance < 1.5f)
			{
				MonoBehaviour.print((object)"ms");
				Vector3 val3 = default(Vector3);
				((Vector3)(ref val3))._002Ector(((RaycastHit)(ref val2)).point.x, ((RaycastHit)(ref val)).point.y, ((RaycastHit)(ref val2)).point.z);
				GameObject val4 = (GameObject)Object.Instantiate((Object)(object)pointA, val3, Quaternion.identity);
				edge = val4;
				edge.transform.up = ((RaycastHit)(ref val)).normal;
				edge.transform.forward = ((RaycastHit)(ref val2)).normal;
			}
		}
	}
}
