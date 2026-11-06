using UnityEngine;

[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Edge/Sigmoid")]
public class CameraFilterPack_Edge_Sigmoid : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(1f, 10f)]
	public float Gain = 3f;

	public static float ChangeGain;

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
		ChangeGain = Gain;
		SCShader = Shader.Find("CameraFilterPack/Edge_Sigmoid");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Gain", Gain);
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
			Gain = ChangeGain;
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
