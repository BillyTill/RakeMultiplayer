using System;
using UnityEngine;

public class CameraMouseLook : MonoBehaviour
{
	public enum RotationAxes
	{
		MouseXAndY,
		MouseX,
		MouseY
	}

	public RotationAxes axes;

	public float sensitivityX = 15f;

	public float sensitivityY = 15f;

	public float minimumX = -360f;

	public float maximumX = 360f;

	public float smoothTime = 5f;

	public float minimumY = -60f;

	public float maximumY = 60f;

	private float rotationY;

	public Transform camera;

	public Transform character;

	public Quaternion m_CharacterTargetRot;

	public Quaternion m_CameraTargetRot;

	public float yRot;

	public float xRot;

	public bool bControl;

	private void Start()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		sensitivityX = PlayerPrefs.GetFloat("Sens:");
		sensitivityY = PlayerPrefs.GetFloat("Sens:");
		m_CharacterTargetRot = character.localRotation;
		m_CameraTargetRot = camera.localRotation;
	}

	private void Update()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		if (bControl)
		{
			yRot = Input.GetAxis("Mouse X") * sensitivityX;
			xRot = Input.GetAxis("Mouse Y") * sensitivityY;
			m_CameraTargetRot = ClampRotationAroundXAxis(m_CameraTargetRot);
			m_CharacterTargetRot *= Quaternion.Euler(0f, yRot, 0f);
			m_CameraTargetRot *= Quaternion.Euler(0f - xRot, 0f, 0f);
			character.localRotation = Quaternion.Slerp(character.localRotation, m_CharacterTargetRot, smoothTime * Time.deltaTime);
			camera.localRotation = Quaternion.Slerp(camera.localRotation, m_CameraTargetRot, smoothTime * Time.deltaTime);
		}
	}

	public void Focus(Transform target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = target.position - ((Component)this).transform.position;
		Quaternion val2 = Quaternion.LookRotation(val);
		((Component)this).transform.rotation = Quaternion.Lerp(((Component)this).transform.rotation, val2, Time.deltaTime * 5f);
	}

	private Quaternion ClampRotationAroundXAxis(Quaternion q)
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		q.x /= q.w;
		q.y /= q.w;
		q.z /= q.w;
		q.w = 1f;
		float num = 114.59156f * Mathf.Atan(q.x);
		num = Mathf.Clamp(num, minimumY, maximumY);
		q.x = Mathf.Tan((float)Math.PI / 360f * num);
		return q;
	}
}
