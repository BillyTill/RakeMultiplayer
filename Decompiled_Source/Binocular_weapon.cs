using UnityEngine;

public class Binocular_weapon : Weapon
{
	public Animator animator;

	public GameObject UI_System;

	public Camera camera;

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
		if (Input.GetMouseButton(0))
		{
			animator.SetBool("Sight", true);
			((Behaviour)camera).enabled = true;
		}
		else
		{
			animator.SetBool("Sight", false);
			((Behaviour)camera).enabled = false;
		}
	}
}
