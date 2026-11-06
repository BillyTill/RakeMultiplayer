using UnityEngine;

[AddComponentMenu("Camera Filter Pack/Blur/Movie")]
[ExecuteInEditMode]
public class CameraFilterPack_Blur_Movie : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 1000f)]
	public float Radius = 150f;

	[Range(0f, 1000f)]
	public float Factor = 200f;

	[Range(1f, 8f)]
	public int FastFilter = 2;

	public static float ChangeRadius;

	public static float ChangeFactor;

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
		ChangeRadius = Radius;
		ChangeFactor = Factor;
		ChangeFastFilter = FastFilter;
		SCShader = Shader.Find("CameraFilterPack/Blur_Movie");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			int fastFilter = FastFilter;
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Radius", Radius / (float)fastFilter);
			material.SetFloat("_Factor", Factor);
			material.SetVector("_ScreenResolution", Vector4.op_Implicit(new Vector2((float)(Screen.width / fastFilter), (float)(Screen.height / fastFilter))));
			int num = sourceTexture.width / fastFilter;
			int num2 = sourceTexture.height / fastFilter;
			if (FastFilter > 1)
			{
				RenderTexture temporary = RenderTexture.GetTemporary(num, num2, 0);
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
			Radius = ChangeRadius;
			Factor = ChangeFactor;
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
