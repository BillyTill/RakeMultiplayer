using UnityEngine;

public class Trap_proj : MonoBehaviour
{
	public bool bSetup;

	public GameObject trap;

	public AudioClip activeSound;

	public AudioClip setupSound;

	public GameObject particle;

	private void OnTriggerEnter(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player" && bSetup)
		{
			Collider component = ((Component)info).GetComponent<Collider>();
			((Component)this).GetComponent<AudioSource>().PlayOneShot(activeSound);
			SetTrapActive();
			((Component)component).GetComponent<FPSController>().TakeDamage(35, "TrapDamage");
		}
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Rake" && bSetup)
		{
			Collider component2 = ((Component)info).GetComponent<Collider>();
			((Component)this).GetComponent<AudioSource>().PlayOneShot(activeSound);
			SetTrapActive();
			((Component)component2).GetComponent<Enemy_AI>().TakeDamage(35, "TrapDamage");
		}
		if (Object.op_Implicit((Object)(object)((Component)info).GetComponent<Animal_AI>()) && bSetup)
		{
			Collider component3 = ((Component)info).GetComponent<Collider>();
			((Component)this).GetComponent<AudioSource>().PlayOneShot(activeSound);
			SetTrapActive();
			((Component)component3).GetComponent<Animal_AI>().TakeDamage(100, "TrapDamage");
		}
	}

	public void SetTrapActive()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		trap.GetComponent<Animation>().Play();
		Object.Instantiate((Object)(object)particle, ((Component)this).transform.position, Quaternion.identity);
	}

	private void OnCollisionEnter(Collision collision)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (Object.op_Implicit((Object)(object)((Component)collision.collider).GetComponent<TerrainCollider>()))
		{
			Ray val = default(Ray);
			((Ray)(ref val))._002Ector(((Component)this).transform.position, Vector3.down);
			RaycastHit val2 = default(RaycastHit);
			if (Physics.Raycast(val, ref val2, 0.4f))
			{
				bSetup = true;
				((Component)this).GetComponent<AudioSource>().PlayOneShot(setupSound);
				((Component)this).transform.rotation = Quaternion.LookRotation(((RaycastHit)(ref val2)).normal);
			}
		}
	}
}
