using System.Collections.Generic;
using UnityEngine;

public class NotificationsManager : MonoBehaviour
{
	private Dictionary<string, List<Component>> Listeners = new Dictionary<string, List<Component>>();

	private static NotificationsManager instance;

	public static NotificationsManager Instance
	{
		get
		{
			if ((Object)(object)instance == (Object)null)
			{
				instance = new NotificationsManager();
			}
			return instance;
		}
	}

	public void AddListener(Component Sender, string NotificationName)
	{
		if (!Listeners.ContainsKey(NotificationName))
		{
			Listeners.Add(NotificationName, new List<Component>());
		}
		Listeners[NotificationName].Add(Sender);
	}

	public void RemoveListener(Component Sender, string NotificationName)
	{
		if (!Listeners.ContainsKey(NotificationName))
		{
			return;
		}
		for (int num = Listeners[NotificationName].Count - 1; num >= 0; num--)
		{
			if (((Object)Listeners[NotificationName][num]).GetInstanceID() == ((Object)Sender).GetInstanceID())
			{
				Listeners[NotificationName].RemoveAt(num);
			}
		}
	}

	public void PostNotification(Component Sender, string NotificationName)
	{
		if (!Listeners.ContainsKey(NotificationName))
		{
			return;
		}
		foreach (Component item in Listeners[NotificationName])
		{
			item.SendMessage(NotificationName, (object)Sender, (SendMessageOptions)1);
		}
	}

	public void ClearListeners()
	{
		Listeners.Clear();
	}

	public void RemoveRedundancies()
	{
		Dictionary<string, List<Component>> dictionary = new Dictionary<string, List<Component>>();
		foreach (KeyValuePair<string, List<Component>> listener in Listeners)
		{
			for (int num = listener.Value.Count - 1; num >= 0; num--)
			{
				if ((Object)(object)listener.Value[num] == (Object)null)
				{
					listener.Value.RemoveAt(num);
				}
			}
			if (listener.Value.Count > 0)
			{
				dictionary.Add(listener.Key, listener.Value);
			}
		}
		Listeners = dictionary;
	}

	private void OnLevelWasLoaded()
	{
		RemoveRedundancies();
	}

	private void Awake()
	{
		if (Object.op_Implicit((Object)(object)instance))
		{
			Object.DestroyImmediate((Object)(object)((Component)this).gameObject);
			return;
		}
		instance = this;
		Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
	}
}
