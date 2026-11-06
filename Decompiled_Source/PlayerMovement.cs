using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
	public float speed = 6f;

	public float jumpSpeed = 8f;

	public float gravity = 20f;

	private Vector3 moveDirection = Vector3.zero;

	private bool StepDown;

	public float StepSpeed;

	public AudioSource audioSource;

	public AudioClip[] FootstepSound;

	public AudioClip stamina;

	public bool bPlayStaminaSound;

	public Animator anim;

	public Animator cameraAnim;

	public float playerStamina;

	public bool bRun;

	public bool bCanRun;

	public bool bCanMove;

	public int stepType;

	public void SetFootstepSound(int id)
	{
		stepType = id;
	}

	public void FreezePlayer()
	{
		bCanMove = false;
	}

	public void UnFreezePlayer()
	{
		bCanMove = true;
	}

	private void Update()
	{
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		if (bCanMove && FPSController.instance.playerState == FPSController.Player_state.Full)
		{
			if (bRun)
			{
				if (playerStamina > 1f)
				{
					playerStamina -= 0.1f;
				}
				else
				{
					if (!bPlayStaminaSound)
					{
						((Component)this).GetComponent<AudioSource>().PlayOneShot(stamina);
						bPlayStaminaSound = true;
					}
					bCanRun = false;
				}
			}
			else if (playerStamina < 100f)
			{
				playerStamina += 0.3f;
			}
			else
			{
				bPlayStaminaSound = false;
				bCanRun = true;
			}
			CharacterController component = ((Component)this).GetComponent<CharacterController>();
			if (component.isGrounded)
			{
				if (Input.GetButton("Vertical") || Input.GetButton("Horizontal"))
				{
					anim.SetFloat("Speed", 1f);
					cameraAnim.SetFloat("Speed", 1f);
					if (!StepDown)
					{
						StartPlay(StepSpeed);
					}
				}
				else
				{
					cameraAnim.SetFloat("Speed", 0f);
					anim.SetFloat("Speed", 0f);
				}
				if (Input.GetButton("Run") && bCanRun)
				{
					cameraAnim.SetFloat("Speed", 2f);
					anim.SetFloat("Speed", 2f);
					bRun = true;
					speed = 8f;
					StepSpeed = 0.4f;
				}
				else
				{
					bRun = false;
					speed = 4.5f;
					StepSpeed = 0.7f;
				}
				moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
				moveDirection = ((Component)this).transform.TransformDirection(moveDirection);
				moveDirection *= speed;
			}
			ref Vector3 reference = ref moveDirection;
			reference.y -= gravity * Time.deltaTime;
			component.Move(moveDirection * Time.deltaTime);
		}
		else
		{
			cameraAnim.SetFloat("Speed", 0f);
			anim.SetFloat("Speed", 0f);
		}
	}

	private void StartPlay(float time)
	{
		((MonoBehaviour)this).StartCoroutine(FootStep(time));
	}

	private IEnumerator FootStep(float stepDelay)
	{
		StepDown = true;
		audioSource.PlayOneShot(FootstepSound[stepType]);
		yield return (object)new WaitForSeconds(stepDelay);
		StepDown = false;
	}
}
