using System.Collections.Generic;
using UnityEngine;

public class AnimalsManager : MonoBehaviour
{
	public List<Transform> currentAnimals = new List<Transform>();

	public List<Transform> spawnPoints = new List<Transform>();

	public List<GameObject> animalsType = new List<GameObject>();

	public List<Transform> foodPoints = new List<Transform>();

	public int prevPointID;

	public static AnimalsManager instance;

	private void Start()
	{
		instance = this;
	}

	public void FindClosetAnimal(Transform point)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		RefreshList();
		int index = 0;
		if ((Object)(object)point == (Object)null)
		{
			return;
		}
		float num = 0f;
		if ((Object)(object)currentAnimals[0] != (Object)null)
		{
			num = Vector3.Distance(point.position, currentAnimals[0].position);
		}
		for (int i = 0; i < currentAnimals.Count; i++)
		{
			if (Vector3.Distance(point.position, currentAnimals[i].position) < num)
			{
				num = Vector3.Distance(point.position, currentAnimals[i].position);
				index = i;
			}
		}
		Debug.Log((object)num);
		if (num < 250f)
		{
			((Component)currentAnimals[index]).GetComponent<Animal_AI>().foodPoints.Add(point);
		}
	}

	public void RefreshList()
	{
		for (int i = 0; i < currentAnimals.Count; i++)
		{
			if ((Object)(object)currentAnimals[i] == (Object)null)
			{
				currentAnimals.RemoveAt(i);
			}
		}
	}

	public void SpawnAnimal()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		int index = Random.Range(0, spawnPoints.Count);
		int index2 = Random.Range(0, animalsType.Count);
		Object.Instantiate((Object)(object)animalsType[index2], spawnPoints[index].position, Quaternion.identity);
	}

	public List<Transform> GetFoodPoints()
	{
		return foodPoints;
	}

	public void AddFoodPoint(Transform point)
	{
		foodPoints.Add(point);
		FindClosetAnimal(point);
	}

	public void RemoveFoodPoint(Transform point)
	{
		foodPoints.Remove(point);
	}
}
