using UnityEngine;

[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/FX/8bits")]
public class CameraFilterPack_FX_8bits : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(-1f, 1f)]
	public float Brightness;

	[Range(80f, 640f)]
	public int ResolutionX = 160;

	[Range(60f, 480f)]
	public int ResolutionY = 240;

	public static float ChangeBrightness;

	public static int ChangeResolutionX;

	public static int ChangeResolutionY;

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
		ChangeBrightness = Brightness;
		ChangeResolutionX = ResolutionX;
		ChangeResolutionY = ResolutionY;
		SCShader = Shader.Find("CameraFilterPack/FX_8bits");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			if (Brightness == 0f)
			{
				Brightness = 0.001f;
			}
			material.SetFloat("_Distortion", Brightness);
			RenderTexture temporary = RenderTexture.GetTemporary(ResolutionX, ResolutionY, 0);
			Graphics.Blit((Texture)(object)sourceTexture, temporary, material);
			((Texture)temporary).filterMode = (FilterMode)0;
			Graphics.Blit((Texture)(object)temporary, destTexture);
			RenderTexture.ReleaseTemporary(temporary);
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
			Brightness = ChangeBrightness;
			ResolutionX = ChangeResolutionX;
			ChangeResolutionY = ResolutionY;
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
