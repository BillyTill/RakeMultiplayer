using UnityEngine;

[AddComponentMenu("Camera Filter Pack/Blur/Blurry")]
[ExecuteInEditMode]
public class CameraFilterPack_Blur_Blurry : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 20f)]
	public float Amount = 2f;

	[Range(1f, 8f)]
	public int FastFilter = 2;

	public static float ChangeAmount;

	public static int ChangeFastFilter;

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
		ChangeAmount = Amount;
		ChangeFastFilter = FastFilter;
		SCShader = Shader.Find("CameraFilterPack/Blur_Blurry");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			int fastFilter = FastFilter;
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Amount", Amount);
			material.SetVector("_ScreenResolution", Vector4.op_Implicit(new Vector2((float)(Screen.width / fastFilter), (float)(Screen.height / fastFilter))));
			int num = sourceTexture.width / fastFilter;
			int num2 = sourceTexture.height / fastFilter;
			if (FastFilter > 1)
			{
				RenderTexture temporary = RenderTexture.GetTemporary(num, num2, 0);
				((Texture)temporary).filterMode = (FilterMode)2;
				Graphics.Blit((Texture)(object)sourceTexture, temporary, material);
				Graphics.Blit((Texture)(object)temporary, destTexture);
				RenderTexture.ReleaseTemporary(temporary);
			}
			else
			{
				Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
			}
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
			Amount = ChangeAmount;
			FastFilter = ChangeFastFilter;
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
