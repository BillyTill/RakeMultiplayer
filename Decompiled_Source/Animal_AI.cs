using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Animal_AI : Unit_obj
{
	public enum Animal_state
	{
		idle,
		walk,
		eating,
		walking,
		escape
	}

	public Animator animator;

	public AnimalHead animalHead;

	public Animal_state ActiveState;

	public float dangerDistance;

	public NavMeshAgent nav;

	public int Health = 1000;

	public bool isAlive = true;

	public float walkSpeed = 2f;

	public float runSpeed = 7f;

	public Transform camera;

	public bool bKillingProccess;

	public bool bHungry;

	public bool bDanger;

	public bool bWalk;

	public bool bTakeDamage;

	public bool bTraped;

	public int hungryCounter;

	public float staminaCounter;

	public GameObject ragdoll;

	public Vector3 currentDist;

	private float stopDistance = 0.5f;

	public Transform enemy;

	public float takeDamageWaitTime;

	public float walkingWaitTime;

	public float walkingWaitTimeMax;

	public float eatingWaitTime;

	public float eatingWaitTimeMax;

	public float dangerWaitTime;

	public bool bEnemyInRadius;

	public Transform point;

	public List<Transform> escapePoints = new List<Transform>();

	public List<Transform> walkPoints = new List<Transform>();

	public List<Transform> foodPoints = new List<Transform>();

	public GameObject bloodSpot;

	public Transform detectionTrigger;

	public Vector3 currentWalkingPoint;

	public Vector3 currentEatingPoint;

	public Vector3 currentEscapePoint;

	public int eatingPointID;

	public AudioClip walkSound;

	public AudioClip[] howlSounds;

	public bool StepDown;

	public AudioSource audioSource;

	public bool stateSound;

	public float walkSoundDelay;

	public float runSoundDelay;

	private void OnTriggerEnter(Collider info)
	{
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<Enemy_AI>()))
		{
			enemy = ((Component)info).gameObject.transform;
			bEnemyInRadius = true;
		}
	}

	private void OnTriggerExit(Collider info)
	{
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<Enemy_AI>()))
		{
			bEnemyInRadius = false;
		}
	}

	private void Start()
	{
		NotificationsManager.Instance.AddListener((Component)(object)this, "Event_PlayerFire");
		SetWalkingState();
	}

	public void Event_PlayerFire(Component sender)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (!bDanger)
		{
			Debug.Log((object)"PlayerFire");
			float num = Vector3.Distance(((Component)this).transform.position, sender.GetComponent<Transform>().position);
			if (num < 300f)
			{
				SetDangerState();
			}
		}
	}

	public void Event_Trapped()
	{
		SetTrapedState();
	}

	public void SetCamera(bool state)
	{
		if ((Object)(object)camera != (Object)null && Object.op_Implicit((Object)(object)((Component)camera).GetComponent<RecordCamera_obj>()))
		{
			((Component)camera).gameObject.SetActive(state);
		}
	}

	public void SetHungryState()
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		ResetAllDestination();
		RefreshPoints();
		nav.Resume();
		bHungry = true;
		nav.speed = walkSpeed;
		if (currentEatingPoint == Vector3.zero)
		{
			int num = Random.Range(0, foodPoints.Count);
			eatingPointID = num;
			currentEatingPoint = foodPoints[eatingPointID].position;
		}
	}

	public void SetDangerState()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		audioSource.PlayOneShot(howlSounds[Random.Range(0, howlSounds.Length)]);
		ResetAllDestination();
		nav.Resume();
		bDanger = true;
		animator.SetBool("bEat", false);
		animator.SetBool("bWalk", false);
		animator.SetFloat("Speed", 2f);
		nav.speed = runSpeed;
		if (currentEscapePoint == Vector3.zero)
		{
			currentEscapePoint = escapePoints[Random.Range(0, escapePoints.Count)].position;
		}
	}

	public void SetWalkingState()
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		nav.ResetPath();
		ResetAllDestination();
		nav.Resume();
		bWalk = true;
		animator.SetFloat("Speed", 1f);
		nav.speed = walkSpeed;
		if (currentWalkingPoint == Vector3.zero)
		{
			currentWalkingPoint = walkPoints[Random.Range(0, walkPoints.Count)].position;
		}
	}

	public void SetTrapedState()
	{
		bTraped = true;
		bHungry = false;
		SetCamera(state: true);
	}

	private void Update()
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		if (isAlive)
		{
			if (bTraped)
			{
				Idle();
			}
			else if (bDanger)
			{
				Escape();
			}
			else
			{
				if (bEnemyInRadius && (Object)(object)enemy != (Object)null)
				{
					if (Vector3.Distance(enemy.position, ((Component)this).transform.position) < dangerDistance)
					{
						SetDangerState();
					}
					if (animalHead.bSeen())
					{
						SetDangerState();
					}
				}
				if (bHungry)
				{
					Eating();
				}
				else
				{
					Walking();
				}
			}
		}
		if (bKillingProccess)
		{
			Killing();
		}
	}

	public void Killing()
	{
		nav.Stop();
		bHungry = false;
		bWalk = false;
		animator.SetFloat("Speed", 0f);
		bDanger = false;
	}

	private void Idle()
	{
		Stop();
		animator.SetFloat("Speed", 0f);
		animator.SetBool("bEat", false);
	}

	private void Walking()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(currentWalkingPoint, ((Component)this).transform.position) < stopDistance)
		{
			animator.SetFloat("Speed", 0f);
			animator.SetBool("bWalk", true);
			ActiveState = Animal_state.walking;
			walkingWaitTime += Time.deltaTime;
			if (walkingWaitTime > walkingWaitTimeMax)
			{
				animator.SetBool("bWalk", false);
				int[] array = new int[2] { 0, 1 };
				animator.SetInteger("WalkType", Random.Range(0, array.Length));
				walkingWaitTime = 0f;
				animator.SetFloat("Speed", 1f);
				Vector3 val = Random.insideUnitSphere * 5f;
				val += currentWalkingPoint;
				NavMeshHit val2 = default(NavMeshHit);
				NavMesh.SamplePosition(val, ref val2, 5f, 1);
				int[] array2 = new int[4] { 10, 15, 20, 25 };
				hungryCounter += array2[Random.Range(0, array2.Length)];
				UpdateAnimalState();
				currentWalkingPoint = ((NavMeshHit)(ref val2)).position;
				int[] array3 = new int[2] { 3, 3 };
				walkingWaitTimeMax = array3[Random.Range(0, array3.Length)];
			}
		}
		else if (!nav.pathPending)
		{
			ActiveState = Animal_state.walk;
			PlayFootstep(walkSoundDelay);
			nav.SetDestination(currentWalkingPoint);
			animator.SetFloat("Speed", 1f);
		}
		else
		{
			animator.SetFloat("Speed", 0f);
		}
	}

	private void Eating()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(currentEatingPoint, ((Component)this).transform.position) < 10f && Vector3.Distance(currentEatingPoint, ((Component)this).transform.position) > stopDistance && ((Component)foodPoints[eatingPointID]).GetComponent<FoodPoint>().isUse)
		{
			SetHungryState();
		}
		if (Vector3.Distance(currentEatingPoint, ((Component)this).transform.position) < stopDistance)
		{
			((Component)foodPoints[eatingPointID]).GetComponent<FoodPoint>().isUse = true;
			animator.SetFloat("Speed", 0f);
			animator.SetBool("bEat", true);
			eatingWaitTime += Time.deltaTime;
			ActiveState = Animal_state.eating;
			if (eatingWaitTime > eatingWaitTimeMax)
			{
				((Component)foodPoints[eatingPointID]).GetComponent<FoodPoint>().isUse = false;
				((Component)foodPoints[eatingPointID]).GetComponent<FoodPoint>().Eat();
				eatingWaitTime = 0f;
				hungryCounter = 0;
				bHungry = false;
				currentWalkingPoint = Vector3.zero;
				animator.SetBool("bEat", false);
				animator.SetFloat("Speed", 1f);
				SetWalkingState();
			}
		}
		else
		{
			PlayFootstep(walkSoundDelay);
			ActiveState = Animal_state.walk;
			animator.SetBool("bEat", false);
			nav.SetDestination(currentEatingPoint);
			animator.SetFloat("Speed", 1f);
		}
	}

	private void Escape()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(currentEscapePoint, ((Component)this).transform.position) < stopDistance)
		{
			animator.SetFloat("Speed", 0f);
			animator.SetBool("bWalk", false);
			animator.SetBool("bEat", false);
			if (!bEnemyInRadius)
			{
				dangerWaitTime += Time.deltaTime;
				ActiveState = Animal_state.idle;
				if (dangerWaitTime > 5f)
				{
					dangerWaitTime = 0f;
					currentEatingPoint = Vector3.zero;
					currentWalkingPoint = Vector3.zero;
					bDanger = false;
					if (bWalk)
					{
						SetWalkingState();
					}
					if (bHungry)
					{
						SetHungryState();
					}
				}
			}
			else
			{
				SetDangerState();
				dangerWaitTime = 0f;
			}
		}
		else
		{
			PlayFootstep(runSoundDelay);
			ActiveState = Animal_state.escape;
			animator.SetBool("bEat", false);
			nav.SetDestination(currentEscapePoint);
			animator.SetFloat("Speed", 2f);
		}
	}

	public void ResetAllDestination()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		currentEatingPoint = Vector3.zero;
		currentEscapePoint = Vector3.zero;
		currentWalkingPoint = Vector3.zero;
	}

	private void RefreshPoints()
	{
		for (int i = 0; i < foodPoints.Count; i++)
		{
			if ((Object)(object)foodPoints[i] == (Object)null)
			{
				foodPoints.RemoveAt(i);
			}
		}
	}

	private void Stop()
	{
		nav.Stop();
	}

	private bool bReached()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(((Component)this).transform.position, currentDist) < stopDistance)
		{
			return true;
		}
		return false;
	}

	public void AfterTrapEffect()
	{
		SetDangerState();
		bTraped = false;
	}

	private void PlayFootstep(float time)
	{
		if (!StepDown)
		{
			((MonoBehaviour)this).StartCoroutine(FootStep(time));
		}
	}

	private IEnumerator FootStep(float stepDelay)
	{
		StepDown = true;
		audioSource.PlayOneShot(walkSound);
		yield return (object)new WaitForSeconds(stepDelay);
		StepDown = false;
	}

	public void UpdateAnimalState()
	{
		if ((float)hungryCounter > 100f)
		{
			SetHungryState();
		}
	}

	public override void TakeDamage(int amount, string damageType)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		Health -= amount;
		if (damageType == "SniperShot")
		{
			SetDangerState();
		}
		if (damageType == "TrapDamage")
		{
			SetDangerState();
		}
		if (Health <= 0)
		{
			isAlive = false;
			animator.SetBool("bDead", true);
			Stop();
			Vector3 position = ((Component)this).transform.position;
			position.y += 1f;
			Ray val = default(Ray);
			((Ray)(ref val))._002Ector(position, Vector3.down);
			RaycastHit val2 = default(RaycastHit);
			if (Physics.Raycast(val, ref val2, 100f))
			{
				Object.Instantiate((Object)(object)bloodSpot, ((RaycastHit)(ref val2)).point + ((RaycastHit)(ref val2)).normal * 0.2f, Quaternion.LookRotation(-((RaycastHit)(ref val2)).normal));
			}
			NotificationsManager.Instance.RemoveListener((Component)(object)this, "Event_PlayerFire");
			animator.SetFloat("Speed", 0f);
			animator.SetBool("bEat", false);
			Object.Instantiate((Object)(object)ragdoll, ((Component)this).transform.position, Quaternion.identity);
			if ((Object)(object)camera != (Object)null && Object.op_Implicit((Object)(object)((Component)camera).GetComponent<RecordCamera_obj>()))
			{
				CameraManager.instance.RemoveCamera(((Component)camera).GetComponent<RecordCamera_obj>().setupCamera.transform);
			}
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
