using UnityEngine;

[AddComponentMenu("Camera Filter Pack/Distortion/Water_Drop")]
[ExecuteInEditMode]
public class CameraFilterPack_Distortion_Water_Drop : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(-1f, 1f)]
	public float CenterX;

	[Range(-1f, 1f)]
	public float CenterY;

	[Range(0f, 10f)]
	public float WaveIntensity = 1f;

	[Range(0f, 20f)]
	public int NumberOfWaves = 5;

	public static float ChangeCenterX;

	public static float ChangeCenterY;

	public static float ChangeWaveIntensity;

	public static int ChangeNumberOfWaves;

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
		ChangeCenterX = CenterX;
		ChangeCenterY = CenterY;
		ChangeWaveIntensity = WaveIntensity;
		ChangeNumberOfWaves = NumberOfWaves;
		SCShader = Shader.Find("CameraFilterPack/Distortion_Water_Drop");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", Vector4.op_Implicit(new Vector2((float)Screen.width, (float)Screen.height)));
			material.SetFloat("_CenterX", CenterX);
			material.SetFloat("_CenterY", CenterY);
			material.SetFloat("_WaveIntensity", WaveIntensity);
			material.SetInt("_NumberOfWaves", NumberOfWaves);
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
			CenterX = ChangeCenterX;
			CenterY = ChangeCenterY;
			WaveIntensity = ChangeWaveIntensity;
			NumberOfWaves = ChangeNumberOfWaves;
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
