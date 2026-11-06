using UnityEngine;

public class FoodPoint : MonoBehaviour
{
	public int count;

	public bool isUse;

	public void Eat()
	{
		if (count == -1)
		{
			return;
		}
		count--;
		if (count <= 0)
		{
			AnimalsManager.instance.RemoveFoodPoint(((Component)this).transform);
			if ((Object)(object)((Component)this).transform.parent != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)((Component)this).transform.parent).gameObject);
			}
			else
			{
				Object.Destroy((Object)(object)((Component)this).gameObject);
			}
		}
	}
}
