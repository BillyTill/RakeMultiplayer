using UnityEngine;

[ExecuteInEditMode]
public class Vintage : MonoBehaviour
{
	private Shader curShader;

	private Color Yellow = Color.yellow;

	private Color Cyan = Color.cyan;

	private Color Magenta = Color.magenta;

	public float YellowLevel = 0.01f;

	public float CyanLevel = 0.03f;

	public float MagentaLevel = 0.04f;

	private Material curMaterial;

	private Material material
	{
		get
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Expected O, but got Unknown
			if ((Object)(object)curMaterial == (Object)null)
			{
				curMaterial = new Material(curShader);
				((Object)curMaterial).hideFlags = (HideFlags)61;
			}
			return curMaterial;
		}
	}

	private void Start()
	{
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
		else
		{
			curShader = Shader.Find("Custom/Vintage");
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)curShader != (Object)null)
		{
			material.SetFloat("_YellowLevel", YellowLevel);
			material.SetFloat("_CyanLevel", CyanLevel);
			material.SetFloat("_MagentaLevel", MagentaLevel);
			material.SetColor("_Yellow", Yellow);
			material.SetColor("_Cyan", Cyan);
			material.SetColor("_Magenta", Magenta);
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)curMaterial))
		{
			Object.DestroyImmediate((Object)(object)curMaterial);
		}
	}
}
