using UnityEngine;

[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Colors/HSV")]
public class CameraFilterPack_Colors_HSV : MonoBehaviour
{
	public Shader SCShader;

	[Range(0f, 360f)]
	public float _HueShift = 180f;

	[Range(-32f, 32f)]
	public float _Saturation = 1f;

	[Range(-32f, 32f)]
	public float _ValueBrightness = 1f;

	private Material SCMaterial;

	public static float ChangeHueShift;

	public static float ChangeSaturation;

	public static float ChangeValueBrightness;

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
		ChangeHueShift = _HueShift;
		ChangeSaturation = _Saturation;
		ChangeValueBrightness = _ValueBrightness;
		SCShader = Shader.Find("CameraFilterPack/Colors_HSV");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		if ((Object)(object)SCShader != (Object)null)
		{
			material.SetFloat("_HueShift", _HueShift);
			material.SetFloat("_Sat", _Saturation);
			material.SetFloat("_Val", _ValueBrightness);
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
			_HueShift = ChangeHueShift;
			_Saturation = ChangeSaturation;
			_ValueBrightness = ChangeValueBrightness;
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
