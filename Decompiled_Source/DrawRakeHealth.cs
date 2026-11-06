using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DrawRakeHealth : MonoBehaviour
{
	public Enemy_AI enemy;

	public GameObject panel;

	public GameObject bg;

	public Text text;

	private void Start()
	{
		NotificationsManager.Instance.AddListener((Component)(object)this, "OnDamageTaken");
		enemy = GameObject.FindGameObjectWithTag("Rake").GetComponent<Enemy_AI>();
	}

	private void Update()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		bg.GetComponent<RectTransform>().anchoredPosition = new Vector2(panel.GetComponent<RectTransform>().anchoredPosition.x - 2f, panel.GetComponent<RectTransform>().anchoredPosition.y);
		bg.GetComponent<RectTransform>().sizeDelta = new Vector2(252f, 14f);
		panel.GetComponent<RectTransform>().sizeDelta = new Vector2(enemy.Health / 10f, 10f);
	}

	public void OnDamageTaken()
	{
		((MonoBehaviour)this).StartCoroutine(hide());
	}

	private IEnumerator hide()
	{
		bg.SetActive(true);
		panel.SetActive(true);
		yield return (object)new WaitForSeconds(5f);
		bg.SetActive(false);
		panel.SetActive(false);
	}
}
