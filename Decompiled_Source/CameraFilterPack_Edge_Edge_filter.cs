using UnityEngine;

[AddComponentMenu("Camera Filter Pack/Edge/Edge_filter")]
[ExecuteInEditMode]
public class CameraFilterPack_Edge_Edge_filter : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 10f)]
	public float RedAmplifier;

	[Range(0f, 10f)]
	public float GreenAmplifier = 2f;

	[Range(0f, 10f)]
	public float BlueAmplifier;

	public static float ChangeRedAmplifier;

	public static float ChangeGreenAmplifier;

	public static float ChangeBlueAmplifier;

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
		ChangeRedAmplifier = RedAmplifier;
		ChangeGreenAmplifier = GreenAmplifier;
		ChangeBlueAmplifier = BlueAmplifier;
		SCShader = Shader.Find("CameraFilterPack/Edge_Edge_filter");
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
			material.SetFloat("_RedAmplifier", RedAmplifier);
			material.SetFloat("_GreenAmplifier", GreenAmplifier);
			material.SetFloat("_BlueAmplifier", BlueAmplifier);
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
			RedAmplifier = ChangeRedAmplifier;
			GreenAmplifier = ChangeGreenAmplifier;
			BlueAmplifier = ChangeBlueAmplifier;
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
