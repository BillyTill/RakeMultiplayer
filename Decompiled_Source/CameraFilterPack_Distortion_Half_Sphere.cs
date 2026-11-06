using UnityEngine;

[AddComponentMenu("Camera Filter Pack/Distortion/Half_Sphere")]
[ExecuteInEditMode]
public class CameraFilterPack_Distortion_Half_Sphere : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(1f, 6f)]
	private Vector4 ScreenResolution;

	private Material SCMaterial;

	public float SphereSize = 2.5f;

	[Range(-1f, 1f)]
	public float SpherePositionX;

	[Range(-1f, 1f)]
	public float SpherePositionY;

	[Range(1f, 10f)]
	public float Strength = 5f;

	public static float ChangeSphereSize;

	public static float ChangeSpherePositionX;

	public static float ChangeSpherePositionY;

	public static float ChangeStrength;

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
		ChangeSphereSize = SphereSize;
		ChangeSpherePositionX = SpherePositionX;
		ChangeSpherePositionY = SpherePositionY;
		ChangeStrength = Strength;
		SCShader = Shader.Find("CameraFilterPack/Distortion_Half_Sphere");
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
			material.SetFloat("_SphereSize", SphereSize);
			material.SetFloat("_SpherePositionX", SpherePositionX);
			material.SetFloat("_SpherePositionY", SpherePositionY);
			material.SetFloat("_Strength", Strength);
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
			SphereSize = ChangeSphereSize;
			SpherePositionX = ChangeSpherePositionX;
			SpherePositionY = ChangeSpherePositionY;
			Strength = ChangeStrength;
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
