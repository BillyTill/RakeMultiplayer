using System.Collections;
using UnityEngine;

public class FPSController : MonoBehaviour
{
	public enum Player_state
	{
		Full,
		Freezed,
		Disabled
	}

	public int Health;

	public Animator cameraAnimator;

	public Animator effectAnimator;

	public Animator itemInfo;

	public RectTransform playerHealth;

	public WeaponManager weaponManager;

	public static FPSController instance;

	public FPSCamera fpsCamera;

	public PlayerMovement playerMovement;

	public CameraMouseLook camMouseLook;

	public AudioClip[] painSounds;

	public Camera cam;

	public Camera weapCam;

	public Inventory inventory;

	public bool bUseInventory;

	public AudioListener listener;

	public GameObject playerCanvas;

	public bool bHide;

	public Light flashlight;

	public GameObject loadScreenObj;

	public Player_state playerState;

	public void SetPlayerState(Player_state state)
	{
		playerState = state;
	}

	public void Awake()
	{
	}

	public void Start()
	{
		instance = this;
		Debug.Log((object)("LOADEDLEVEL IS " + Application.loadedLevel));
		if (Application.loadedLevel == 1)
		{
			Debug.Log((object)"Level cannot load");
		}
		else
		{
			((MonoBehaviour)this).StartCoroutine(waitGame());
		}
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		((MonoBehaviour)this).StartCoroutine(loadScreen());
	}

	private IEnumerator waitGame()
	{
		yield return (object)new WaitForSeconds(1f);
		SaveGameSystem.instance.LoadGame();
	}

	private IEnumerator loadScreen()
	{
		yield return (object)new WaitForSeconds(2f);
		((Behaviour)effectAnimator).enabled = true;
		((Behaviour)listener).enabled = true;
		Object.Destroy((Object)(object)loadScreenObj);
	}

	private void Update()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		playerHealth.sizeDelta = new Vector2((float)Health * 2f, 10f);
		if (Input.GetButtonDown("Inventory"))
		{
			if (!bUseInventory)
			{
				inventory.OpenInventory();
				WeaponManager.instance.HideCurrentWeapon();
				bUseInventory = true;
				((Behaviour)camMouseLook).enabled = false;
				((Behaviour)playerMovement).enabled = false;
				Cursor.lockState = (CursorLockMode)0;
				Cursor.visible = true;
				SetPlayerDisableState("INVENTORY");
			}
			else
			{
				bUseInventory = false;
				inventory.CloseInventory();
				((Behaviour)camMouseLook).enabled = true;
				((Behaviour)playerMovement).enabled = true;
				Cursor.lockState = (CursorLockMode)1;
				Cursor.visible = false;
				SetPlayerDisableState("FULL");
			}
		}
		if (Input.GetButtonDown("Flashlight"))
		{
			SetLightState(!((Behaviour)flashlight).enabled);
		}
	}

	public void SetLightState(bool state)
	{
		((Behaviour)flashlight).enabled = state;
	}

	public void Event_playerWeaponSelect()
	{
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		bUseInventory = false;
		inventory.CloseInventory();
		EnablePlayerMovement();
	}

	public void CameraRander(bool type)
	{
		((Behaviour)cam).enabled = type;
		((Behaviour)weapCam).enabled = type;
	}

	public void HidePlayerWeapon()
	{
		weaponManager.HideCurrentWeapon();
		if (bUseInventory)
		{
			inventory.CloseInventory();
		}
	}

	public void DisablePlayer()
	{
		HidePlayerWeapon();
		playerCanvas.SetActive(false);
		((Behaviour)fpsCamera).enabled = false;
		((Behaviour)playerMovement).enabled = false;
		((Behaviour)camMouseLook).enabled = false;
		itemInfo.SetBool("Show", false);
		((Behaviour)listener).enabled = false;
		CameraRander(type: false);
		((Behaviour)inventory).enabled = false;
		((Behaviour)weaponManager).enabled = false;
	}

	public void DisablePlayerMovement()
	{
		((Behaviour)camMouseLook).enabled = false;
		((Behaviour)playerMovement).enabled = false;
	}

	public void EnablePlayerMovement()
	{
		playerState = Player_state.Full;
		((Behaviour)camMouseLook).enabled = true;
		((Behaviour)playerMovement).enabled = true;
	}

	public void SetPlayerDisableState(string state)
	{
		switch (state)
		{
		case "INVENTORY":
			((Behaviour)playerMovement).enabled = false;
			((Behaviour)camMouseLook).enabled = false;
			playerState = Player_state.Disabled;
			break;
		case "CAMERAMODE":
			HidePlayerWeapon();
			playerCanvas.SetActive(false);
			((Behaviour)fpsCamera).enabled = false;
			((Behaviour)playerMovement).enabled = false;
			((Behaviour)camMouseLook).enabled = false;
			itemInfo.SetBool("Show", false);
			((Behaviour)listener).enabled = false;
			CameraRander(type: false);
			((Behaviour)inventory).enabled = false;
			((Behaviour)weaponManager).enabled = false;
			break;
		case "SCREEMER":
			camMouseLook.bControl = false;
			playerMovement.bCanMove = false;
			((Behaviour)inventory).enabled = false;
			playerState = Player_state.Disabled;
			break;
		case "PAUSEMENU":
			HidePlayerWeapon();
			playerState = Player_state.Disabled;
			((Behaviour)inventory).enabled = false;
			break;
		case "BEFOREPAUSE":
			playerState = Player_state.Full;
			((Behaviour)inventory).enabled = true;
			((Behaviour)weaponManager).enabled = true;
			break;
		case "FULL":
			playerState = Player_state.Full;
			((Behaviour)inventory).enabled = true;
			playerCanvas.SetActive(true);
			((Behaviour)listener).enabled = true;
			((Behaviour)fpsCamera).enabled = true;
			((Behaviour)playerMovement).enabled = true;
			((Behaviour)camMouseLook).enabled = true;
			camMouseLook.bControl = true;
			playerMovement.bCanMove = true;
			itemInfo.SetBool("Show", true);
			CameraRander(type: true);
			((Behaviour)weaponManager).enabled = true;
			break;
		}
	}

	public void EnablePlayer()
	{
		playerState = Player_state.Full;
		((Behaviour)inventory).enabled = true;
		playerCanvas.SetActive(true);
		((Behaviour)listener).enabled = true;
		((Behaviour)fpsCamera).enabled = true;
		((Behaviour)playerMovement).enabled = true;
		((Behaviour)camMouseLook).enabled = true;
		itemInfo.SetBool("Show", true);
		CameraRander(type: true);
		((Behaviour)weaponManager).enabled = true;
	}

	public void TakeDamage(int amount, string damageType)
	{
		Health -= amount;
		((Component)this).GetComponent<AudioSource>().PlayOneShot(painSounds[Random.Range(0, painSounds.Length)]);
		if (damageType == "TrapDamage")
		{
			((MonoBehaviour)this).StartCoroutine(takeTrapDamage());
		}
		if (damageType == "RakeKick")
		{
			((MonoBehaviour)this).StartCoroutine(TakeDamage());
		}
		if (damageType == "RakeUppercut")
		{
			((MonoBehaviour)this).StartCoroutine(RakeUppercut());
		}
		if (Health < 0)
		{
			KillPlayer();
		}
	}

	public void KillPlayer()
	{
		cameraAnimator.SetFloat("Speed", 0f);
		cameraAnimator.SetBool("Dead", true);
		WeaponManager.instance.HideCurrentWeapon();
		playerMovement.bCanMove = false;
		camMouseLook.bControl = false;
		InformationUI.instance.DrawObjective("Game over");
		((MonoBehaviour)this).StartCoroutine(restart());
	}

	private IEnumerator restart()
	{
		yield return (object)new WaitForSeconds(5f);
		Application.LoadLevel(0);
	}

	private IEnumerator takeTrapDamage()
	{
		effectAnimator.SetBool("Blood", true);
		cameraAnimator.SetBool("LegHit", true);
		yield return (object)new WaitForSeconds(1.5f);
		cameraAnimator.SetBool("LegHit", false);
		yield return (object)new WaitForSeconds(3f);
		effectAnimator.SetBool("Blood", false);
	}

	private IEnumerator RakeUppercut()
	{
		effectAnimator.SetBool("Blood", true);
		cameraAnimator.SetBool("Falldown", true);
		yield return (object)new WaitForSeconds(4.5f);
		cameraAnimator.SetBool("Falldown", false);
		effectAnimator.SetBool("Blood", false);
	}

	private IEnumerator TakeDamage()
	{
		effectAnimator.SetBool("Blood", true);
		cameraAnimator.SetBool("Kick", true);
		yield return (object)new WaitForSeconds(4.5f);
		cameraAnimator.SetBool("Kick", false);
		effectAnimator.SetBool("Blood", false);
	}
}
