using UnityEngine;

public class Window_obj : MonoBehaviour
{
	public GameObject glass;

	public GameObject brokenGlass;

	public GameObject particle;

	public AudioClip sound;

	public int bDestroy;

	public void SetBrokenState()
	{
		glass.SetActive(false);
		brokenGlass.SetActive(true);
		bDestroy = 1;
		((Collider)((Component)this).GetComponent<BoxCollider>()).enabled = false;
	}

	public void BreakGlass()
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		glass.SetActive(false);
		brokenGlass.SetActive(true);
		particle.SetActive(true);
		AudioSource.PlayClipAtPoint(sound, ((Component)this).transform.position);
		((Collider)((Component)this).GetComponent<BoxCollider>()).enabled = false;
	}

	public void TakeDamage(int amount, string damageType)
	{
		if (bDestroy == 0)
		{
			BreakGlass();
			bDestroy = 1;
		}
	}
}
