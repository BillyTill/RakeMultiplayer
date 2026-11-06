using UnityEngine;

[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Oculus/NightVision1")]
public class CameraFilterPack_Oculus_NightVision1 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private float Distortion = 1f;

	private Material SCMaterial;

	private Vector4 ScreenResolution;

	[Range(0f, 100f)]
	public float Vignette = 1.3f;

	[Range(1f, 150f)]
	public float Linecount = 90f;

	public static float ChangeVignette;

	public static float ChangeLinecount;

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
		ChangeVignette = Vignette;
		ChangeLinecount = Linecount;
		SCShader = Shader.Find("CameraFilterPack/Oculus_NightVision1");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Distortion", Distortion);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			material.SetFloat("_Vignette", Vignette);
			material.SetFloat("_Linecount", Linecount);
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
		if (!Application.isPlaying)
		{
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
