using UnityEngine;

public class AnimalCage_obj : MonoBehaviour
{
	public Animator anim;

	private bool bTraped;

	public GameObject currentAnimal;

	private void OnTriggerEnter(Collider info)
	{
		if (Object.op_Implicit((Object)(object)((Component)info).GetComponent<Animal_AI>()))
		{
			currentAnimal = ((Component)info).gameObject;
			CloseCage();
		}
	}

	public void OpenCage()
	{
		anim.SetBool("Close", false);
		if ((Object)(object)currentAnimal != (Object)null)
		{
			currentAnimal.GetComponent<Animal_AI>().AfterTrapEffect();
		}
	}

	public void CloseCage()
	{
		anim.SetBool("Close", true);
		if ((Object)(object)currentAnimal != (Object)null)
		{
			currentAnimal.GetComponent<Animal_AI>().Event_Trapped();
		}
	}
}
