public class TakeItems_objective : Objective
{
	public override void ObjectiveAction()
	{
		if (!Status)
		{
			ObjectiveManager.instance.CompleteObjective(this);
		}
	}
}
