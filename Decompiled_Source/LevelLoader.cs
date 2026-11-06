using UnityEngine;

public class LevelLoader : MonoBehaviour
{
	public static LevelLoader instance;

	private void Start()
	{
		instance = this;
	}

	public void LoadLevel(int id)
	{
		SaveGameSystem.instance.SaveGame();
	}
}
