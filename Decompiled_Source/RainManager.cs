using System.Collections.Generic;
using UnityEngine;

public class RainManager : MonoBehaviour
{
	public List<GameObject> rains = new List<GameObject>();

	public Transform rain;

	public Transform currentPlayer;

	public Transform defaultPlayer;

	public static RainManager instance;

	private void Start()
	{
		instance = this;
	}

	public void SetPlayerRainState(bool state)
	{
		rains[0].SetActive(state);
	}

	public void Update()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)currentPlayer != (Object)null)
		{
			Vector3 position = currentPlayer.position;
			position.y += 10f;
			rain.position = position;
		}
	}
}
