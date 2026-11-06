using UnityEngine;

public class SoundTrigger : MonoBehaviour
{
	public AudioClip sound;

	public int id;

	private void OnTriggerEnter(Collider info)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (((Component)info).gameObject.tag == "Player")
		{
			AudioSource.PlayClipAtPoint(sound, ((Component)this).transform.position, 1f);
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
