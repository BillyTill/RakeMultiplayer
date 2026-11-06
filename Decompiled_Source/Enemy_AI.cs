using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_AI : Unit_obj
{
	public Animator animator;

	public NavMeshAgent nav;

	public List<Transform> patrolPoints = new List<Transform>();

	public List<Transform> escapePoints = new List<Transform>();

	public Vector3 currentPatrolPoint;

	public Vector3 currentHountPoint;

	public Vector3 currentEscapePoint;

	public Vector3 currentPartialPoint;

	public Vector3 currentBreakPoint;

	public Vector3 viewRot;

	public Transform cameraBreak;

	public int currentStage;

	public AudioClip walkSound;

	public AudioClip[] playerAttackRoars;

	public AudioClip[] playerAttackKicks;

	public AudioClip[] painRoar;

	public AudioClip cameraAttackRoar;

	public AudioClip[] idleSounds;

	public bool StepDown;

	public AudioSource audioSource;

	public bool stateSound;

	public bool bDanger;

	public bool bHaunt;

	public bool bVisible;

	public bool bPlayerHount;

	public bool bKillingProccess;

	public bool bPlayerKilling;

	public bool bCameraBreak;

	public bool bHungry;

	public int hungryCounter;

	public float patrolWaitTime;

	public float hauntWaitTime;

	public float dangerWaitTime;

	public float breakWaitTime;

	public float playerHountWaitTime;

	private float stopDistance = 0.5f;

	public Transform target;

	public Transform playerTarget;

	public float walkSpeed;

	public float runSpeed;

	public float hauntSpeed;

	public Transform focusPoint;

	public float Health;

	public int PlayerAttackType;

	public GameObject deadPrefab;

	private void Start()
	{
		Health = 2500f;
		NotificationsManager.Instance.AddListener((Component)(object)this, "Event_PlayerFire");
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

	public Vector3 FindClosetPoint()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		int index = 0;
		float num = Vector3.Distance(((Component)this).transform.position, patrolPoints[0].position);
		for (int i = 0; i < patrolPoints.Count; i++)
		{
			if (Vector3.Distance(((Component)this).transform.position, patrolPoints[i].position) < num)
			{
				num = Vector3.Distance(((Component)this).transform.position, patrolPoints[i].position);
				index = i;
			}
		}
		return patrolPoints[index].position;
	}

	public Vector3 BuildEscapePath()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)"RebuildEsacpePath");
		float num = 300f;
		Vector3 val = Random.insideUnitSphere * num;
		val += ((Component)this).transform.position;
		NavMeshHit val2 = default(NavMeshHit);
		NavMesh.SamplePosition(val, ref val2, num * 2f, 1 << NavMesh.GetNavMeshLayerFromName("Default"));
		return ((NavMeshHit)(ref val2)).position;
	}

	public override void TakeDamage(int amount, string damageType)
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		Health -= amount;
		NotificationsManager.Instance.PostNotification((Component)(object)this, "OnDamageTaken");
		if (damageType == "SniperShot")
		{
			audioSource.PlayOneShot(painRoar[Random.Range(0, painRoar.Length)]);
			SetDangerState();
		}
		if (damageType == "TrapDamage")
		{
			SetDangerState();
		}
		if (Health <= 0f)
		{
			Object.Instantiate((Object)(object)deadPrefab, ((Component)this).transform.position, ((Component)this).transform.rotation);
			FPSController.instance.effectAnimator.SetBool("Fade", true);
			InformationUI.instance.DrawObjective("You win!");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}

	public void OnDamageTaken()
	{
	}

	private void Update()
	{
		if ((Object)(object)target != (Object)null)
		{
			if (!Object.op_Implicit((Object)(object)((Component)target).GetComponent<Animal_AI>()))
			{
				bHaunt = false;
			}
		}
		else
		{
			bHaunt = false;
		}
		if (!bDanger)
		{
			if (!bHaunt && !bPlayerHount)
			{
				if (bCameraBreak)
				{
					CameraBreak();
				}
				else
				{
					Patrol();
				}
			}
			if (bPlayerHount)
			{
				if (!((Component)playerTarget).GetComponent<FPSController>().bHide)
				{
					PlayerHount();
				}
				else
				{
					bPlayerHount = false;
				}
			}
			if (bHaunt && !bPlayerHount && bHungry)
			{
				Haunting();
			}
		}
		else if (!bPlayerKilling)
		{
			Escape();
		}
	}

	public void SetDangerState()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (!bPlayerKilling)
		{
			nav.Resume();
			stateSound = false;
			currentEscapePoint = BuildEscapePath();
			bDanger = true;
			animator.SetInteger("WalkType", 1);
			animator.SetFloat("Speed", 2f);
		}
	}

	public void RefreshStats()
	{
		if (hungryCounter >= 100)
		{
			hungryCounter = 0;
			bHungry = true;
		}
	}

	public void SetPlayerHountState()
	{
		nav.Resume();
		stateSound = false;
		animator.SetInteger("WalkType", 1);
		bPlayerHount = true;
		bHaunt = false;
	}

	public void SetPatrolState()
	{
		stateSound = false;
		animator.SetBool("bCrouch", false);
		nav.Resume();
		animator.SetInteger("WalkType", 1);
		target = null;
	}

	public void SetBreakState()
	{
		stateSound = false;
		nav.Resume();
		bCameraBreak = true;
	}

	private void PlayerHount()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0993: Unknown result type (might be due to invalid IL or missing references)
		//IL_0998: Unknown result type (might be due to invalid IL or missing references)
		//IL_099e: Unknown result type (might be due to invalid IL or missing references)
		//IL_09a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a74: Unknown result type (might be due to invalid IL or missing references)
		//IL_070e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0713: Unknown result type (might be due to invalid IL or missing references)
		//IL_0719: Unknown result type (might be due to invalid IL or missing references)
		//IL_0724: Unknown result type (might be due to invalid IL or missing references)
		//IL_092d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0796: Unknown result type (might be due to invalid IL or missing references)
		//IL_079b: Unknown result type (might be due to invalid IL or missing references)
		//IL_07a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_07aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_07be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		//IL_048a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0490: Unknown result type (might be due to invalid IL or missing references)
		//IL_049b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0414: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_050d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0512: Unknown result type (might be due to invalid IL or missing references)
		//IL_051c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0521: Unknown result type (might be due to invalid IL or missing references)
		//IL_0535: Unknown result type (might be due to invalid IL or missing references)
		if (currentStage == 0)
		{
			currentHountPoint = playerTarget.position;
			if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 7f)
			{
				animator.SetFloat("Speed", 0f);
				animator.SetBool("bAttack", false);
				nav.Stop();
				playerHountWaitTime += Time.deltaTime;
				if (playerHountWaitTime > 10f)
				{
					playerHountWaitTime = 0f;
					bPlayerHount = false;
					nav.Resume();
				}
				if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 3f)
				{
					nav.Resume();
					bPlayerHount = false;
					bDanger = true;
					playerHountWaitTime = 0f;
				}
			}
			else
			{
				nav.Resume();
				nav.SetDestination(currentHountPoint);
				animator.SetBool("bAttack", false);
				nav.speed = walkSpeed;
				animator.SetFloat("Speed", 1f);
			}
		}
		if (currentStage == 1)
		{
			Vector3 val = ((Component)this).transform.position - ((Component)playerTarget).transform.position;
			Vector3 forward = playerTarget.forward;
			float num = Vector3.Angle(val, forward);
			if (num < 60f)
			{
				bVisible = true;
			}
			else if (!bVisible)
			{
				bVisible = false;
			}
			if (bVisible)
			{
				if (PlayerAttackType == 0)
				{
					int[] array = new int[2] { 1, 2 };
					PlayerAttackType = array[Random.Range(0, array.Length)];
				}
				if (PlayerAttackType == 1)
				{
					currentHountPoint = playerTarget.position;
					if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 5f)
					{
						nav.Stop();
						animator.SetFloat("Speed", 0f);
						bPlayerKilling = true;
						((Component)playerTarget).GetComponent<PlayerMovement>().FreezePlayer();
						((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = false;
						((Component)playerTarget).GetComponent<FPSController>().HidePlayerWeapon();
						Vector3 position = playerTarget.position;
						position.y = ((Component)this).transform.position.y;
						((Component)this).transform.LookAt(position);
						((Component)playerTarget).GetComponent<CameraMouseLook>().Focus(((Component)focusPoint).transform);
						animator.SetBool("bCrouch", false);
						if (!stateSound)
						{
							stateSound = true;
							audioSource.PlayOneShot(playerAttackRoars[Random.Range(0, playerAttackRoars.Length)]);
						}
						animator.SetBool("bPlayerAttack", true);
						playerHountWaitTime += Time.deltaTime;
						if (playerHountWaitTime > 1.2f)
						{
							audioSource.PlayOneShot(playerAttackKicks[Random.Range(0, playerAttackKicks.Length)]);
							bPlayerKilling = false;
							((Component)playerTarget).GetComponent<FPSController>().TakeDamage(10, "RakeKick");
							((Component)playerTarget).GetComponent<PlayerMovement>().UnFreezePlayer();
							((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = true;
							stateSound = false;
							SetDangerState();
							nav.Resume();
							bDanger = true;
							playerHountWaitTime = 0f;
							bPlayerHount = false;
							bVisible = false;
							animator.SetBool("bAttack", false);
							animator.SetBool("bPlayerAttack", false);
						}
					}
					else
					{
						nav.Resume();
						nav.SetDestination(currentHountPoint);
						animator.SetBool("bCrouch", false);
						animator.SetBool("bAttack", false);
						nav.speed = runSpeed;
						PlayFootstep(0.2f);
						animator.SetFloat("Speed", 2f);
					}
				}
				if (PlayerAttackType == 2)
				{
					currentHountPoint = playerTarget.position;
					if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 5f)
					{
						nav.Stop();
						animator.SetFloat("Speed", 0f);
						bPlayerKilling = true;
						((Component)playerTarget).GetComponent<PlayerMovement>().FreezePlayer();
						((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = false;
						((Component)playerTarget).GetComponent<FPSController>().HidePlayerWeapon();
						Vector3 position2 = playerTarget.position;
						position2.y = ((Component)this).transform.position.y;
						((Component)this).transform.LookAt(position2);
						((Component)playerTarget).GetComponent<CameraMouseLook>().Focus(((Component)focusPoint).transform);
						animator.SetBool("bCrouch", false);
						if (!stateSound)
						{
							stateSound = true;
							audioSource.PlayOneShot(playerAttackRoars[Random.Range(0, playerAttackRoars.Length)]);
						}
						animator.SetBool("bPlayerAppercut", true);
						playerHountWaitTime += Time.deltaTime;
						if (playerHountWaitTime > 1.2f)
						{
							audioSource.PlayOneShot(playerAttackKicks[Random.Range(0, playerAttackKicks.Length)]);
							bPlayerKilling = false;
							((Component)playerTarget).GetComponent<FPSController>().TakeDamage(10, "RakeUppercut");
							((Component)playerTarget).GetComponent<PlayerMovement>().UnFreezePlayer();
							((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = true;
							stateSound = false;
							PlayerAttackType = 0;
							SetDangerState();
							nav.Resume();
							bDanger = true;
							playerHountWaitTime = 0f;
							bPlayerHount = false;
							bVisible = false;
							animator.SetBool("bAttack", false);
							animator.SetBool("bPlayerAppercut", false);
						}
					}
					else
					{
						nav.Resume();
						nav.SetDestination(currentHountPoint);
						animator.SetBool("bCrouch", false);
						animator.SetBool("bAttack", false);
						nav.speed = runSpeed;
						PlayFootstep(0.2f);
						animator.SetFloat("Speed", 2f);
					}
				}
			}
			else
			{
				currentHountPoint = playerTarget.position;
				if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 3f)
				{
					nav.Stop();
					animator.SetFloat("Speed", 0f);
					bPlayerKilling = true;
					((Component)playerTarget).GetComponent<PlayerMovement>().FreezePlayer();
					((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = false;
					((Component)playerTarget).GetComponent<FPSController>().HidePlayerWeapon();
					Vector3 position3 = playerTarget.position;
					position3.y = ((Component)this).transform.position.y;
					((Component)this).transform.LookAt(position3);
					((Component)playerTarget).GetComponent<CameraMouseLook>().Focus(((Component)focusPoint).transform);
					animator.SetBool("bCrouch", false);
					if (!stateSound)
					{
						stateSound = true;
						audioSource.PlayOneShot(playerAttackRoars[Random.Range(0, playerAttackRoars.Length)]);
					}
					animator.SetBool("bPlayerAttack", true);
					playerHountWaitTime += Time.deltaTime;
					if (playerHountWaitTime > 1.2f)
					{
						audioSource.PlayOneShot(playerAttackKicks[Random.Range(0, playerAttackKicks.Length)]);
						bPlayerKilling = false;
						((Component)playerTarget).GetComponent<FPSController>().TakeDamage(10, "RakeUppercut");
						((Component)playerTarget).GetComponent<PlayerMovement>().UnFreezePlayer();
						((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = true;
						stateSound = false;
						PlayerAttackType = 0;
						SetDangerState();
						nav.Resume();
						bDanger = true;
						playerHountWaitTime = 0f;
						bPlayerHount = false;
						bVisible = false;
						animator.SetBool("bAttack", false);
						animator.SetBool("bPlayerAttack", false);
					}
				}
				else
				{
					nav.Resume();
					nav.SetDestination(currentHountPoint);
					animator.SetBool("bAttack", false);
					nav.speed = hauntSpeed;
					animator.SetBool("bCrouch", true);
					animator.SetFloat("Speed", 0f);
				}
			}
		}
		if (currentStage != 2)
		{
			return;
		}
		currentHountPoint = playerTarget.position;
		if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 3f)
		{
			nav.Stop();
			animator.SetFloat("Speed", 0f);
			animator.SetBool("bCrouch", false);
			animator.SetBool("bAttack", true);
			playerHountWaitTime += Time.deltaTime;
			if (playerHountWaitTime > 5f)
			{
				nav.Resume();
				bDanger = true;
				bVisible = false;
				playerHountWaitTime = 0f;
				bPlayerHount = false;
				animator.SetBool("bAttack", false);
			}
		}
		else
		{
			nav.Resume();
			nav.SetDestination(currentHountPoint);
			animator.SetBool("bCrouch", false);
			animator.SetBool("bAttack", false);
			nav.speed = runSpeed;
			animator.SetFloat("Speed", 2f);
		}
	}

	private void Escape()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Invalid comparison between Unknown and I4
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Invalid comparison between Unknown and I4
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(currentEscapePoint, ((Component)this).transform.position) < stopDistance)
		{
			animator.SetFloat("Speed", 0f);
			dangerWaitTime += Time.deltaTime;
			if (dangerWaitTime > 2f)
			{
				dangerWaitTime = 0f;
				bPlayerHount = false;
				bVisible = false;
				bDanger = false;
				animator.SetFloat("Speed", 1f);
			}
			return;
		}
		if ((int)nav.path.status == 1 || (int)nav.path.status == 2)
		{
			animator.SetFloat("Speed", 0f);
			currentEscapePoint = BuildEscapePath();
			nav.Stop();
		}
		if (!nav.pathPending)
		{
			nav.Resume();
			nav.SetDestination(currentEscapePoint);
			PlayFootstep(0.2f);
			nav.speed = runSpeed;
			animator.SetFloat("Speed", 2f);
		}
		else
		{
			nav.Stop();
			animator.SetFloat("Speed", 0f);
		}
	}

	private void Patrol()
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Invalid comparison between Unknown and I4
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Invalid comparison between Unknown and I4
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)playerTarget != (Object)null && Vector3.Distance(playerTarget.position, ((Component)this).transform.position) < 60f)
		{
			SetPlayerHountState();
		}
		if (currentPatrolPoint == Vector3.zero)
		{
			nav.Resume();
			currentPatrolPoint = patrolPoints[Random.Range(0, patrolPoints.Count)].position;
		}
		if (Vector3.Distance(currentPatrolPoint, ((Component)this).transform.position) < stopDistance)
		{
			animator.SetFloat("Speed", 0f);
			patrolWaitTime += Time.deltaTime;
			if (!stateSound)
			{
				audioSource.PlayOneShot(idleSounds[Random.Range(0, idleSounds.Length)]);
				stateSound = true;
			}
			if (patrolWaitTime > 3f)
			{
				currentPatrolPoint = patrolPoints[Random.Range(0, patrolPoints.Count)].position;
				hungryCounter += 50;
				RefreshStats();
				if (nav.pathPending)
				{
					patrolWaitTime = 0f;
					stateSound = false;
				}
			}
		}
		else
		{
			if ((int)nav.path.status == 1 || (int)nav.path.status == 2)
			{
				animator.SetFloat("Speed", 0f);
				currentPatrolPoint = FindClosetPoint();
			}
			animator.SetBool("bAttack", false);
			nav.speed = walkSpeed;
			if (!nav.pathPending)
			{
				PlayFootstep(0.7f);
				nav.SetDestination(currentPatrolPoint);
				animator.SetFloat("Speed", 1f);
			}
			else
			{
				animator.SetFloat("Speed", 0f);
			}
		}
	}

	private void Haunting()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		currentHountPoint = target.position;
		if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 3f)
		{
			if (((Component)target).GetComponent<Animal_AI>().isAlive && !((Component)target).GetComponent<Animal_AI>().bTraped)
			{
				if (!bKillingProccess)
				{
					AnimalKilling();
				}
			}
			else
			{
				animator.SetBool("bAnimalKilling", false);
				bHaunt = false;
			}
		}
		else if (((Component)target).GetComponent<Animal_AI>().bDanger)
		{
			animator.SetBool("bAttack", false);
			nav.speed = runSpeed;
			animator.SetBool("bCrouch", false);
			animator.SetInteger("WalkType", 1);
			animator.SetFloat("Speed", 2f);
			nav.SetDestination(currentHountPoint);
		}
		else
		{
			animator.SetInteger("WalkType", 0);
			animator.SetBool("bAttack", false);
			nav.speed = hauntSpeed;
			animator.SetBool("bCrouch", true);
			animator.SetFloat("Speed", 0f);
			nav.SetDestination(currentHountPoint);
		}
	}

	public void CameraBreak()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(currentBreakPoint, ((Component)this).transform.position) < 3f)
		{
			animator.SetBool("bCameraBreak", true);
			animator.SetFloat("Speed", 0f);
			breakWaitTime += Time.deltaTime;
			if (!stateSound)
			{
				stateSound = true;
				audioSource.PlayOneShot(cameraAttackRoar);
			}
			Vector3 val = viewRot - ((Component)this).transform.position;
			Quaternion val2 = Quaternion.LookRotation(viewRot);
			viewRot.y = ((Component)this).transform.position.y;
			((Component)this).transform.LookAt(viewRot);
			if (breakWaitTime > 1.9f)
			{
				breakWaitTime = 0f;
				animator.SetBool("bCameraBreak", false);
				if ((Object)(object)cameraBreak != (Object)null)
				{
					((Component)cameraBreak).GetComponent<RecordCamera_obj>().SetUnsetupState();
					bCameraBreak = false;
				}
			}
		}
		else
		{
			PlayFootstep(0.2f);
			animator.SetBool("bCameraBreak", false);
			nav.speed = runSpeed;
			animator.SetBool("bCrouch", false);
			animator.SetInteger("WalkType", 1);
			animator.SetFloat("Speed", 2f);
			nav.SetDestination(currentBreakPoint);
		}
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

	public void AnimalKilling()
	{
		nav.Stop();
		bKillingProccess = true;
		animator.SetFloat("Speed", 0f);
		animator.SetBool("bCrouch", false);
		animator.SetBool("bAnimalKilling", true);
		((Component)target).GetComponent<Animal_AI>().bKillingProccess = true;
		((MonoBehaviour)this).StartCoroutine(kill());
	}

	private IEnumerator kill()
	{
		yield return (object)new WaitForSeconds(2f);
		if ((Object)(object)target != (Object)null)
		{
			((Component)target).GetComponent<Animal_AI>().TakeDamage(250, "RakeAnimalKick");
		}
		bHungry = false;
		animator.SetBool("bAnimalKilling", false);
		bKillingProccess = false;
		animator.SetBool("bCrouch", false);
		SetPatrolState();
		currentPatrolPoint = Vector3.zero;
	}
}
