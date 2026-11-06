using UnityEngine;

[AddComponentMenu("Camera Filter Pack/Distortion/BlackHole")]
[ExecuteInEditMode]
public class CameraFilterPack_Distortion_BlackHole : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(-1f, 1f)]
	public float PositionX;

	[Range(-1f, 1f)]
	public float PositionY;

	[Range(0f, 20f)]
	public float Size = 1.5f;

	[Range(0f, 180f)]
	public float Distortion = 30f;

	public static float ChangePositionX;

	public static float ChangePositionY;

	public static float ChangeSize;

	public static float ChangeDistortion;

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
		ChangePositionX = PositionX;
		ChangePositionY = PositionY;
		ChangeSize = Size;
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/Distortion_BlackHole");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_PositionX", PositionX);
			material.SetFloat("_PositionY", PositionY);
			material.SetFloat("_Distortion", Size);
			material.SetFloat("_Distortion2", Distortion);
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
			PositionX = ChangePositionX;
			PositionY = ChangePositionY;
			Size = ChangeSize;
			Distortion = ChangeDistortion;
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
