using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraManager : MonoBehaviour
{
	public static CameraManager instance;

	public int currentCameraID;

	public CameraComputer cameraComp;

	public AudioClip cameraSelect;

	public AudioSource audioSource;

	public Transform currentCamera;

	private int CameraCount;

	public List<Transform> gameCameras = new List<Transform>();

	public List<Transform> allCameras = new List<Transform>();

	public List<Button> cameraButtons = new List<Button>();

	public int cameraCount
	{
		get
		{
			int num = 0;
			for (int i = 0; i < gameCameras.Count; i++)
			{
				if ((Object)(object)gameCameras[i] != (Object)null)
				{
					num++;
				}
			}
			return num;
		}
	}

	public void Start()
	{
		instance = this;
	}

	private void Update()
	{
	}

	public int GetCameraCount()
	{
		return gameCameras.Count;
	}

	public Transform GetCurrentCamera()
	{
		return gameCameras[currentCameraID];
	}

	public void AddCamera(Transform camera)
	{
		gameCameras.Add(camera);
		RefreshButtons();
	}

	public int FindCameraID(Transform camera)
	{
		int result = 0;
		for (int i = 0; i < gameCameras.Count; i++)
		{
			if ((Object)(object)gameCameras[i] == (Object)(object)camera)
			{
				result = i;
				break;
			}
		}
		return result;
	}

	public void RemoveCamera(Transform camera)
	{
		gameCameras.Remove(camera);
		RefreshButtons();
		if (cameraComp.bWatching)
		{
			if (gameCameras.Count > 0)
			{
				ActiveCamera(0);
			}
			else
			{
				cameraComp.Exit();
			}
		}
	}

	private void RefreshButtons()
	{
		for (int i = 0; i < cameraButtons.Count; i++)
		{
			if (i < gameCameras.Count)
			{
				((Selectable)cameraButtons[i]).interactable = true;
			}
			else
			{
				((Selectable)cameraButtons[i]).interactable = false;
			}
		}
	}

	public void Event_activeCamera(int camID)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		ColorBlock colors = ((Selectable)cameraButtons[camID]).colors;
		((ColorBlock)(ref colors)).normalColor = Color.red;
		((Selectable)cameraButtons[camID]).colors = colors;
	}

	public void Event_disableCamera(int camID)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		ColorBlock colors = ((Selectable)cameraButtons[camID]).colors;
		((ColorBlock)(ref colors)).normalColor = Color.white;
		((Selectable)cameraButtons[camID]).colors = colors;
	}

	public void DisableAllCameras()
	{
		for (int i = 0; i < gameCameras.Count; i++)
		{
			((Component)gameCameras[i].GetChild(0)).gameObject.SetActive(false);
		}
	}

	public void ActiveCamera(int id)
	{
		for (int i = 0; i < gameCameras.Count; i++)
		{
			if (i == id && (Object)(object)gameCameras[i] != (Object)null)
			{
				((Component)gameCameras[i].GetChild(0)).gameObject.SetActive(true);
				currentCameraID = id;
				currentCamera = gameCameras[i].GetChild(0);
				cameraComp.ResetCameraPosition();
				((Component)gameCameras[i].parent).GetComponent<RecordCamera_obj>().isRecord = true;
				audioSource.PlayOneShot(cameraSelect);
				if ((Object)(object)RainManager.instance != (Object)null)
				{
					RainManager.instance.currentPlayer = currentCamera;
				}
			}
			else if ((Object)(object)gameCameras[i] != (Object)null)
			{
				((Component)gameCameras[i].GetChild(0)).gameObject.SetActive(false);
			}
		}
		Debug.Log((object)id);
	}

	public void Shot()
	{
		NotificationsManager.Instance.PostNotification((Component)(object)this, "Event_PlayerFire");
	}
}
