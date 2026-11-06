using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InformationUI : MonoBehaviour
{
	public static InformationUI instance;

	public Animator itemInfo_anim;

	public Text text;

	public Text hintText;

	public Text objectiveText;

	public GameObject pickupInfo;

	public GameObject hintObj;

	public Transform LogLayout;

	public Transform HintParent;

	private bool bShowHint;

	private bool bShowObjective;

	private void Start()
	{
		instance = this;
	}

	public void DrawObjective(string name)
	{
		if (!bShowObjective)
		{
			objectiveText.text = ((Object)text).ToString();
			((MonoBehaviour)this).StartCoroutine(objective(name));
		}
	}

	public void PickupInfo(string name)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		Object obj = Object.Instantiate((Object)(object)pickupInfo, pickupInfo.transform.position, pickupInfo.transform.rotation);
		GameObject val = (GameObject)(object)((obj is GameObject) ? obj : null);
		val.transform.SetParent(((Component)LogLayout).transform);
		val.GetComponent<Text>().text = name;
	}

	public void DrawItemInfo(string name, bool showType)
	{
		if (showType)
		{
			itemInfo_anim.SetBool("Show", showType);
			text.text = name;
		}
		else
		{
			itemInfo_anim.SetBool("Show", showType);
			text.text = string.Empty;
		}
	}

	public void DrawHint(string text)
	{
		if (!bShowHint)
		{
			hintText.text = text;
			((MonoBehaviour)this).StartCoroutine(hint(text));
		}
	}

	private IEnumerator objective(string text)
	{
		bShowObjective = true;
		objectiveText.text = text;
		yield return (object)new WaitForSeconds(5f);
		objectiveText.text = string.Empty;
		bShowObjective = false;
	}

	private IEnumerator hint(string text)
	{
		bShowHint = true;
		hintText.text = text;
		yield return (object)new WaitForSeconds(3f);
		hintText.text = string.Empty;
		bShowHint = false;
	}
}
