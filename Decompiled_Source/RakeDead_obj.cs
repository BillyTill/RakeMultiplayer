using System.Collections;
using UnityEngine;

public class RakeDead_obj : MonoBehaviour
{
	private void Start()
	{
		((MonoBehaviour)this).StartCoroutine(finishGame());
	}

	private IEnumerator finishGame()
	{
		yield return (object)new WaitForSeconds(4f);
		Application.LoadLevel(0);
	}
}
