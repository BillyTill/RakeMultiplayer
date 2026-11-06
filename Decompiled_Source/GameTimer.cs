using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameTimer : MonoBehaviour
{
	public Text text;

	public float time;

	private float timeCounter;

	public int levelToLoad;

	private bool isTimeOut;

	public float second;

	public float fSecond;

	public int minute;

	public Animator effects;

	private void Start()
	{
		timeCounter = time;
	}

	private void Update()
	{
		text.text = minute.ToString();
		second -= Time.deltaTime;
		fSecond = (int)second;
		if (!(fSecond <= 0f))
		{
			return;
		}
		if (minute == 0)
		{
			if (!isTimeOut)
			{
				isTimeOut = true;
				second = 0f;
				minute = 0;
				InformationUI.instance.DrawObjective("Objective: Please return to trailer");
				((MonoBehaviour)this).StartCoroutine(loadLevel());
			}
		}
		else
		{
			second = 60f;
			minute--;
		}
	}

	private IEnumerator loadLevel()
	{
		SaveGameSystem.instance.SaveGame();
		yield return (object)new WaitForSeconds(4f);
		effects.SetBool("Fade", true);
		yield return (object)new WaitForSeconds(2f);
		Application.LoadLevel(levelToLoad);
	}
}
