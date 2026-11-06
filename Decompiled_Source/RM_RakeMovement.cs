using UnityEngine;

public class RM_RakeMovement : MonoBehaviour
{
	public bool isGrounded;

	public float groundMovementSpeed = 6f;

	public float groundRunSpeed = 10f;

	public float flySpeed = 3f;

	public float speed = 6f;

	public float jumpSpeed = 8f;

	public float gravity = 20f;

	public Vector3 moveDirection = Vector3.zero;

	public Animator weaponAnim;

	public Animator cameraAnim;

	public bool BCanJump;

	public bool isGround()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		RaycastHit val = default(RaycastHit);
		if (Physics.SphereCast(((Component)this).transform.position, ((Component)this).GetComponent<CharacterController>().radius, Vector3.down, ref val, ((Component)this).GetComponent<CharacterController>().height / 2f))
		{
			return true;
		}
		return false;
	}

	private void Update()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Invalid comparison between Unknown and I4
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		Debug.DrawRay(((Component)this).transform.position, Vector3.down * 1.2f, Color.yellow);
		Debug.DrawLine(((Component)this).transform.position, Vector3.down);
		CharacterController component = ((Component)this).GetComponent<CharacterController>();
		if (isGrounded)
		{
			float axis = Input.GetAxis("Horizontal");
			float axis2 = Input.GetAxis("Vertical");
			float axis3 = Input.GetAxis("Run");
			float num = axis * axis + axis2 * axis2;
			num = Mathf.Clamp(num, 0f, 1f);
			num += axis3;
			if (axis3 == 1f)
			{
				speed = groundRunSpeed;
			}
			else
			{
				speed = groundMovementSpeed;
			}
			moveDirection = new Vector3(axis, 0f, axis2);
			cameraAnim.SetFloat("Speed", num);
			weaponAnim.SetFloat("FloatSpeed", 0f);
			weaponAnim.SetFloat("Speed", num);
			moveDirection = ((Component)this).transform.TransformDirection(moveDirection);
			moveDirection *= speed;
		}
		else
		{
			float num2 = Input.GetAxis("Horizontal") * speed / 2f;
			float num3 = Mathf.Clamp(Input.GetAxis("Vertical"), 0f, 1f) * speed;
			moveDirection = new Vector3(num2, moveDirection.y, num3);
			moveDirection = ((Component)this).transform.TransformDirection(moveDirection);
		}
		if (Input.GetButton("Jump") && isGrounded)
		{
			weaponAnim.SetFloat("FloatSpeed", 1f);
			moveDirection.y = jumpSpeed;
		}
		if (!isGrounded)
		{
			ref Vector3 reference = ref moveDirection;
			reference.y -= gravity * Time.deltaTime;
		}
		component.Move(moveDirection * Time.deltaTime);
		if ((int)component.collisionFlags == 1 || isGround())
		{
			isGrounded = true;
		}
		if ((int)component.collisionFlags == 0 && !isGround())
		{
			isGrounded = false;
		}
		weaponAnim.SetBool("Grounded", isGrounded);
		cameraAnim.SetBool("Grounded", isGrounded);
	}
}
