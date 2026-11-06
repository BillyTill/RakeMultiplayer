using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
	public GameObject GameMenu;

	public GameObject newGame;

	public GameObject setting;

	public GameObject multiplayer;

	public GameObject credits;

	public int levelToload;

	public int savedLevel;

	public Animator effects;

	public GameObject loadLogo;

	public Text loadLevelText;

	public Button loadGameButton;

	public GameObject LoadingCanvas;

	public float mouseSens;

	public float audioVolume;

	public GameObject help;

	public void Start()
	{
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		Time.timeScale = 1f;
		if (PlayerPrefs.GetFloat("Sound:") != 0f)
		{
			AudioListener.volume = PlayerPrefs.GetFloat("Sound:");
		}
		else
		{
			AudioListener.volume = 1f;
		}
		if (PlayerPrefs.GetFloat("Sound:") != 0f)
		{
			mouseSens = PlayerPrefs.GetFloat("Sens:");
		}
		else
		{
			mouseSens = 2f;
		}
		Cursor.visible = true;
		Cursor.lockState = (CursorLockMode)0;
		savedLevel = PlayerPrefs.GetInt("Level:");
		if (savedLevel <= 1)
		{
			((Graphic)loadLevelText).color = new Color(1f, 1f, 1f, 0.3f);
			((Selectable)loadGameButton).interactable = false;
		}
	}

	private void OnApplicationQuit()
	{
	}

	public void ActiveHelpMenu()
	{
		help.SetActive(true);
	}

	public void ActiveSettings()
	{
		setting.SetActive(true);
	}

	public void ActiveGameMenu()
	{
		GameMenu.SetActive(true);
	}

	private IEnumerator startGame()
	{
		PlayerPrefs.SetFloat("Sound:", audioVolume);
		PlayerPrefs.SetFloat("Sens:", mouseSens);
		LoadingCanvas.SetActive(true);
		loadLogo.SetActive(true);
		effects.SetBool("Fade", true);
		newGame.SetActive(false);
		yield return (object)new WaitForSeconds(3f);
		Application.LoadLevel(levelToload);
	}

	public void ExitGame()
	{
		Application.Quit();
	}

	public void Back()
	{
		GameMenu.SetActive(false);
		help.SetActive(false);
		setting.SetActive(false);
		multiplayer.SetActive(false);
		credits.SetActive(false);
	}

	public void Loadgame()
	{
		levelToload = savedLevel;
		((MonoBehaviour)this).StartCoroutine(startGame());
	}

	public void StartNewGame()
	{
		PlayerPrefs.DeleteAll();
		levelToload = 1;
		((MonoBehaviour)this).StartCoroutine(startGame());
	}

	public void SetSoundVolume(float sound)
	{
		AudioListener.volume = sound;
		audioVolume = sound;
	}

	public void OpenCredits()
	{
		credits.SetActive(true);
	}

	public void SetMouseSens(float speed)
	{
		mouseSens = speed;
	}

	public void OpenMultiplayer()
	{
		multiplayer.SetActive(true);
	}

	public void Newgame()
	{
		newGame.SetActive(true);
	}
}
