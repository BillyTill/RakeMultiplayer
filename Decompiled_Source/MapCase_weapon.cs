using UnityEngine;

public class MapCase_weapon : Weapon
{
	public Animator animator;

	public GameObject UI_System;

	private void OnEnable()
	{
		UI_System.SetActive(true);
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
	}

	private void Update()
	{
		if (Input.GetButton("Fire1"))
		{
			animator.SetBool("Zoom", true);
		}
		else
		{
			animator.SetBool("Zoom", false);
		}
	}
}
