using UnityEngine;

public class AI_Test : MonoBehaviour
{
	public NavMeshAgent nav;

	public bool bPathIsSet;

	public Transform target;

	public NavMeshPath path;

	private void Start()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		path = new NavMeshPath();
		nav.CalculatePath(target.position, path);
	}

	private void Update()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Invalid comparison between Unknown and I4
		nav.SetDestination(target.position);
		if ((int)nav.path.status == 1)
		{
			Debug.Log((object)"PAth is partial");
		}
		if ((int)nav.path.status == 2)
		{
			Debug.Log((object)"PAth is invalid");
		}
	}
}
