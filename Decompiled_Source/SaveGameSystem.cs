using UnityEngine;

public class SaveGameSystem : MonoBehaviour
{
	public static SaveGameSystem instance;

	public GameObject trapPrefab;

	private void Start()
	{
		instance = this;
		SaveCurrentLevel();
	}

	private void Update()
	{
	}

	public void SaveGame()
	{
		SaveWindows();
		SavePlayerWeapon();
		SavePlayerAmmo();
		SaveGameCameras();
		SaveTrapPosition();
		SaveItems();
		SaveScreemers();
		SaveSoundTriggers();
		SaveRakeHealth();
		SavePlayerHealth();
		SaveCurrentLevel();
		Debug.Log((object)"Game saved");
	}

	private void SaveWindows()
	{
		int count = LevelItemsManager.instance.windowsObj.Count;
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Window:" + i, LevelItemsManager.instance.windowsObj[i].GetComponent<Window_obj>().bDestroy);
		}
		Debug.Log((object)"Saved glass");
	}

	private void SaveCurrentLevel()
	{
		PlayerPrefs.SetInt("Level:", Application.loadedLevel);
	}

	private void SaveRakeHealth()
	{
		Enemy_AI component = GameObject.FindGameObjectWithTag("Rake").GetComponent<Enemy_AI>();
		PlayerPrefs.SetFloat("RakeHealth:", component.Health);
	}

	private void SavePlayerHealth()
	{
		FPSController component = GameObject.FindGameObjectWithTag("Player").GetComponent<FPSController>();
		PlayerPrefs.SetInt("PlayerHealth:", component.Health);
	}

	private void SaveSoundTriggers()
	{
		LevelItemsManager.instance.RefreshSoundTriggersList();
		int count = LevelItemsManager.instance.soundTriggers.Count;
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("SoundTrigger:" + i, LevelItemsManager.instance.soundTriggers[i].GetComponent<SoundTrigger>().id);
		}
		PlayerPrefs.SetInt("SoundTriggersCount", count);
	}

	private void SaveGameCameras()
	{
		int count = CameraManager.instance.gameCameras.Count;
		PlayerPrefs.SetInt("CamerasCount", count);
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Camera:" + i, ((Component)CameraManager.instance.gameCameras[i].parent).GetComponent<RecordCamera_obj>().cameraID);
		}
	}

	private void SaveScreemers()
	{
		LevelItemsManager.instance.RefreshScreemersList();
		int count = LevelItemsManager.instance.screemers.Count;
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Screemer:" + i, LevelItemsManager.instance.screemers[i].GetComponent<ScreemerSystem>().id);
		}
		PlayerPrefs.SetInt("ScreemersCount", count);
	}

	private void SaveItems()
	{
		LevelItemsManager.instance.RefrashItemList();
		int count = LevelItemsManager.instance.items.Count;
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Item:" + i, LevelItemsManager.instance.items[i].GetComponent<Item>().id);
		}
		PlayerPrefs.SetInt("ItemsCount", count);
	}

	private void SaveTrapPosition()
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		LevelItemsManager.instance.RefreshTrapList();
		int count = LevelItemsManager.instance.trapObjects.Count;
		PlayerPrefs.SetInt("TrapsCount", count);
		for (int i = 0; i < count; i++)
		{
			Transform transform = LevelItemsManager.instance.trapObjects[i].transform;
			PlayerPrefs.SetFloat("Trap:" + i + " x=", transform.position.x);
			PlayerPrefs.SetFloat("Trap:" + i + " y=", transform.position.y);
			PlayerPrefs.SetFloat("Trap:" + i + " z=", transform.position.z);
		}
	}

	private void SavePlayerWeapon()
	{
		int count = WeaponManager.instance.playerWeapons.Count;
		PlayerPrefs.SetInt("WeaponsCount:", count);
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Weapon:" + i, ((Component)WeaponManager.instance.playerWeapons[i]).GetComponent<Weapon>().weaponID);
		}
	}

	private void SavePlayerAmmo()
	{
		int count = WeaponManager.instance.weaponsAmmo.Count;
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Ammo:" + i, WeaponManager.instance.weaponsAmmo[i]);
		}
	}

	public void LoadGame()
	{
		LoadWindows();
		LoadPlayerWeapon();
		LoadPlayerAmmo();
		LoadGameCameras();
		LoadLevelTraps();
		LoadItems();
		LoadScreemers();
		LoadSoundTriggers();
		LoadRakeHealth();
		LoadPlayerHealth();
		Debug.Log((object)"Game Loaded");
	}

	private void LoadWindows()
	{
		int count = LevelItemsManager.instance.windowsObj.Count;
		for (int i = 0; i < count; i++)
		{
			if (PlayerPrefs.GetInt("Window:" + i) == 1)
			{
				LevelItemsManager.instance.windowsObj[i].GetComponent<Window_obj>().SetBrokenState();
			}
		}
		Debug.Log((object)"Loaded glass");
	}

	private void LoadRakeHealth()
	{
		Enemy_AI component = GameObject.FindGameObjectWithTag("Rake").GetComponent<Enemy_AI>();
		component.Health = PlayerPrefs.GetFloat("RakeHealth:");
	}

	private void LoadPlayerHealth()
	{
		FPSController component = GameObject.FindGameObjectWithTag("Player").GetComponent<FPSController>();
		component.Health = PlayerPrefs.GetInt("PlayerHealth:");
	}

	public void LoadPlayerWeapon()
	{
		WeaponManager.instance.playerWeapons.Clear();
		int num = PlayerPrefs.GetInt("WeaponsCount:");
		for (int i = 0; i < num; i++)
		{
			Transform item = WeaponManager.instance.allWeapons[PlayerPrefs.GetInt("Weapon:" + i)];
			WeaponManager.instance.playerWeapons.Add(item);
		}
	}

	public void LoadSoundTriggers()
	{
		int num = PlayerPrefs.GetInt("SoundTriggersCount");
		LevelItemsManager.instance.RefreshSoundTriggersList();
		for (int i = 0; i < LevelItemsManager.instance.soundTriggers.Count; i++)
		{
			LevelItemsManager.instance.soundTriggers[i].SetActive(false);
		}
		for (int j = 0; j < num; j++)
		{
			LevelItemsManager.instance.soundTriggers[PlayerPrefs.GetInt("SoundTrigger:" + j)].SetActive(true);
		}
	}

	public void LoadScreemers()
	{
		int num = PlayerPrefs.GetInt("ScreemersCount");
		LevelItemsManager.instance.RefreshScreemersList();
		for (int i = 0; i < LevelItemsManager.instance.screemers.Count; i++)
		{
			LevelItemsManager.instance.screemers[i].SetActive(false);
		}
		for (int j = 0; j < num; j++)
		{
			LevelItemsManager.instance.screemers[PlayerPrefs.GetInt("Screemer:" + j)].SetActive(true);
		}
	}

	public void LoadItems()
	{
		int num = PlayerPrefs.GetInt("ItemsCount");
		LevelItemsManager.instance.RefrashItemList();
		for (int i = 0; i < LevelItemsManager.instance.items.Count; i++)
		{
			LevelItemsManager.instance.items[i].SetActive(false);
		}
		for (int j = 0; j < num; j++)
		{
			LevelItemsManager.instance.items[PlayerPrefs.GetInt("Item:" + j)].SetActive(true);
		}
	}

	public void LoadLevelTraps()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		LevelItemsManager.instance.trapObjects.Clear();
		int num = PlayerPrefs.GetInt("TrapsCount");
		for (int i = 0; i < num; i++)
		{
			Vector3 zero = Vector3.zero;
			zero.x = PlayerPrefs.GetFloat("Trap:" + i + " x=");
			zero.y = PlayerPrefs.GetFloat("Trap:" + i + " y=") + 1f;
			zero.z = PlayerPrefs.GetFloat("Trap:" + i + " z=");
			GameObject obj = (GameObject)Object.Instantiate((Object)(object)trapPrefab, zero, trapPrefab.transform.rotation);
			LevelItemsManager.instance.AddTrap(obj);
		}
	}

	public void LoadGameCameras()
	{
		CameraManager.instance.gameCameras.Clear();
		int num = PlayerPrefs.GetInt("CamerasCount");
		for (int i = 0; i < num; i++)
		{
			if ((Object)(object)CameraManager.instance.allCameras[PlayerPrefs.GetInt("Camera:" + i)] != (Object)null)
			{
				((Component)CameraManager.instance.allCameras[PlayerPrefs.GetInt("Camera:" + i)]).GetComponent<RecordCamera_obj>().SetSetupState();
			}
		}
	}

	public void LoadPlayerAmmo()
	{
		int count = WeaponManager.instance.weaponsAmmo.Count;
		for (int i = 0; i < count; i++)
		{
			WeaponManager.instance.weaponsAmmo[i] = PlayerPrefs.GetInt("Ammo:" + i);
		}
	}
}
