using System.Collections;
using UnityEngine;

public class ScreemerSystem : MonoBehaviour
{
	public GameObject rake;

	public string DmgType;

	public int id;

	public AudioClip triggerSound;

	public AudioClip attackSound;

	public float timeOnAttack;

	public float timeDelay;

	public bool bTakeDamage = true;

	private GameObject playerObj;

	public Transform playerViewPoint;

	private bool bProcess;

	private void Update()
	{
		if (bProcess)
		{
			playerObj.GetComponent<CameraMouseLook>().Focus(playerViewPoint);
		}
	}

	private void OnTriggerEnter(Collider player)
	{
		if (((Component)player).tag == "Player")
		{
			playerObj = ((Component)player).gameObject;
			((MonoBehaviour)this).StartCoroutine(playerDamage());
		}
	}

	private IEnumerator playerDamage()
	{
		yield return (object)new WaitForSeconds(timeDelay);
		AudioSource.PlayClipAtPoint(triggerSound, ((Component)this).transform.position);
		playerObj.GetComponent<CameraMouseLook>().Focus(playerViewPoint);
		playerObj.GetComponent<PlayerMovement>().FreezePlayer();
		playerObj.GetComponent<CameraMouseLook>().bControl = false;
		playerObj.GetComponent<FPSController>().HidePlayerWeapon();
		rake.SetActive(true);
		bProcess = true;
		rake.GetComponent<Animation>().Play();
		yield return (object)new WaitForSeconds(timeOnAttack);
		bProcess = false;
		AudioSource.PlayClipAtPoint(attackSound, ((Component)this).transform.position);
		if (bTakeDamage)
		{
			playerObj.GetComponent<FPSController>().TakeDamage(10, DmgType);
		}
		playerObj.GetComponent<PlayerMovement>().UnFreezePlayer();
		playerObj.GetComponent<CameraMouseLook>().bControl = true;
		yield return (object)new WaitForSeconds(0.5f);
		Object.Destroy((Object)(object)rake);
		Object.Destroy((Object)(object)((Component)this).gameObject);
	}
}
