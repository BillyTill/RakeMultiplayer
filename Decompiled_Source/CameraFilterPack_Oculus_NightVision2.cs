using UnityEngine;

[AddComponentMenu("Camera Filter Pack/Oculus/NightVision2")]
[ExecuteInEditMode]
public class CameraFilterPack_Oculus_NightVision2 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 1f)]
	public float BinocularSize = 0.5f;

	[Range(0f, 1f)]
	public float BinocularDistance = 0.5f;

	[Range(0f, 1f)]
	public float Greenness = 0.4f;

	public static float ChangeBinocularSize;

	public static float ChangeBinocularDistance;

	public static float ChangeGreenness;

	private Material material
	{
		get
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Expected O, but got Unknown
			if ((Object)(object)SCMaterial == (Object)null)
			{
				SCMaterial = new Material(SCShader);
				((Object)SCMaterial).hideFlags = (HideFlags)61;
			}
			return SCMaterial;
		}
	}

	private void Start()
	{
		ChangeBinocularSize = BinocularSize;
		ChangeBinocularDistance = BinocularDistance;
		ChangeGreenness = Greenness;
		SCShader = Shader.Find("CameraFilterPack/Oculus_NightVision2");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_BinocularSize", BinocularSize);
			material.SetFloat("_BinocularDistance", BinocularDistance);
			material.SetFloat("_Greenness", Greenness);
			material.SetVector("_ScreenResolution", Vector4.op_Implicit(new Vector2((float)Screen.width, (float)Screen.height)));
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
		if (Application.isPlaying)
		{
			BinocularSize = ChangeBinocularSize;
			BinocularDistance = ChangeBinocularDistance;
			Greenness = ChangeGreenness;
		}
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
