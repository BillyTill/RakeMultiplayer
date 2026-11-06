using UnityEngine;

[AddComponentMenu("Camera Filter Pack/AAA/WaterDrop")]
[ExecuteInEditMode]
public class CameraFilterPack_AAA_WaterDrop : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(8f, 64f)]
	public float Distortion = 8f;

	[Range(0f, 7f)]
	public float SizeX = 1f;

	[Range(0f, 7f)]
	public float SizeY = 0.5f;

	[Range(0f, 10f)]
	public float Speed = 1f;

	private Material SCMaterial;

	public Texture2D Texture2;

	public static float ChangeDistortion;

	public static float ChangeSizeX;

	public static float ChangeSizeY;

	public static float ChangeSpeed;

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
		ChangeDistortion = Distortion;
		ChangeSizeX = SizeX;
		ChangeSizeY = SizeY;
		ChangeSpeed = Speed;
		Object obj = Resources.Load("CameraFilterPack_WaterDrop");
		Texture2 = (Texture2D)(object)((obj is Texture2D) ? obj : null);
		SCShader = Shader.Find("CameraFilterPack/AAA_WaterDrop");
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
			material.SetFloat("_Distortion", Distortion);
			material.SetFloat("_SizeX", SizeX);
			material.SetFloat("_SizeY", SizeY);
			material.SetFloat("_Speed", Speed);
			material.SetTexture("_MainTex2", (Texture)(object)Texture2);
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
			Distortion = ChangeDistortion;
			SizeX = ChangeSizeX;
			SizeY = ChangeSizeY;
			Speed = ChangeSpeed;
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
