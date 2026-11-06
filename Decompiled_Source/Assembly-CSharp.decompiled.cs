using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("0.0.0.0")]
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
[AddComponentMenu("Camera Filter Pack/Blur/Bloom")]
[ExecuteInEditMode]
public class CameraFilterPack_Blur_Bloom : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 10f)]
	public float Amount = 4.5f;

	[Range(0f, 1f)]
	public float Glow = 0.5f;

	public static float ChangeAmount;

	public static float ChangeGlow;

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
		ChangeGlow = Glow;
		SCShader = Shader.Find("CameraFilterPack/Blur_Bloom");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Amount", Amount);
			material.SetFloat("_Glow", Glow);
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
			Amount = ChangeAmount;
			Glow = ChangeGlow;
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
[AddComponentMenu("Camera Filter Pack/Blur/DitherOffset")]
[ExecuteInEditMode]
public class CameraFilterPack_Blur_DitherOffset : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(1f, 16f)]
	public int Level = 4;

	public Vector2 Distance = new Vector2(30f, 0f);

	public static int ChangeLevel;

	public static Vector2 ChangeDistance;

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
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		ChangeLevel = Level;
		ChangeDistance = Distance;
		SCShader = Shader.Find("CameraFilterPack/Blur_DitherOffset");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Level", (float)Level);
			material.SetVector("_Distance", Vector4.op_Implicit(Distance));
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (Application.isPlaying)
		{
			Level = ChangeLevel;
			Distance = ChangeDistance;
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
[AddComponentMenu("Camera Filter Pack/Blur/Dithering2x2")]
[ExecuteInEditMode]
public class CameraFilterPack_Blur_Dithering2x2 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(2f, 16f)]
	public int Level = 4;

	public Vector2 Distance = new Vector2(30f, 0f);

	public static int ChangeLevel;

	public static Vector2 ChangeDistance;

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
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		ChangeLevel = Level;
		ChangeDistance = Distance;
		SCShader = Shader.Find("CameraFilterPack/Blur_Dithering2x2");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Level", (float)Level);
			material.SetVector("_Distance", Vector4.op_Implicit(Distance));
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (Application.isPlaying)
		{
			Level = ChangeLevel;
			Distance = ChangeDistance;
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
[AddComponentMenu("Camera Filter Pack/Blur/Focus")]
[ExecuteInEditMode]
public class CameraFilterPack_Blur_Focus : MonoBehaviour
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
	public float _Size = 5f;

	[Range(0.12f, 64f)]
	public float _Eyes = 2f;

	public static float ChangeCenterX;

	public static float ChangeCenterY;

	public static float ChangeSize;

	public static float ChangeEyes;

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
		ChangeSize = _Size;
		ChangeEyes = _Eyes;
		SCShader = Shader.Find("CameraFilterPack/Blur_Focus");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_CenterX", CenterX);
			material.SetFloat("_CenterY", CenterY);
			float num = Mathf.Round(_Size / 0.2f) * 0.2f;
			material.SetFloat("_Size", num);
			material.SetFloat("_Circle", _Eyes);
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
			CenterX = ChangeCenterX;
			CenterY = ChangeCenterY;
			_Size = ChangeSize;
			_Eyes = ChangeEyes;
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
[AddComponentMenu("Camera Filter Pack/Blur/GaussianBlur")]
[ExecuteInEditMode]
public class CameraFilterPack_Blur_GaussianBlur : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(1f, 16f)]
	public float Size = 10f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	public static float ChangeSize;

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
		ChangeSize = Size;
		SCShader = Shader.Find("CameraFilterPack/Blur_GaussianBlur");
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
			material.SetFloat("_Distortion", Size);
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
			Size = ChangeSize;
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
[AddComponentMenu("Camera Filter Pack/Blur/Noise")]
[ExecuteInEditMode]
public class CameraFilterPack_Blur_Noise : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(2f, 16f)]
	public int Level = 4;

	public Vector2 Distance = new Vector2(30f, 0f);

	public static int ChangeLevel;

	public static Vector2 ChangeDistance;

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
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		ChangeLevel = Level;
		ChangeDistance = Distance;
		SCShader = Shader.Find("CameraFilterPack/Blur_Noise");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Level", (float)Level);
			material.SetVector("_Distance", Vector4.op_Implicit(Distance));
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (Application.isPlaying)
		{
			Level = ChangeLevel;
			Distance = ChangeDistance;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Blur/Regular")]
public class CameraFilterPack_Blur_Regular : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(1f, 16f)]
	public int Level = 4;

	public Vector2 Distance = new Vector2(30f, 0f);

	public static int ChangeLevel;

	public static Vector2 ChangeDistance;

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
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		ChangeLevel = Level;
		ChangeDistance = Distance;
		SCShader = Shader.Find("CameraFilterPack/Blur_Regular");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Level", (float)Level);
			material.SetVector("_Distance", Vector4.op_Implicit(Distance));
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (Application.isPlaying)
		{
			Level = ChangeLevel;
			Distance = ChangeDistance;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Blur/Steam")]
public class CameraFilterPack_Blur_Steam : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 1f)]
	public float Radius = 0.1f;

	[Range(0f, 1f)]
	public float Quality = 0.75f;

	public static float ChangeRadius;

	public static float ChangeQuality;

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
		ChangeQuality = Quality;
		SCShader = Shader.Find("CameraFilterPack/Blur_Steam");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Radius", Radius);
			material.SetFloat("_Quality", Quality);
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
			Radius = ChangeRadius;
			Quality = ChangeQuality;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Colors/BrightContrastSaturation")]
public class CameraFilterPack_Color_BrightContrastSaturation : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 10f)]
	public float Brightness = 2f;

	[Range(0f, 10f)]
	public float Saturation = 1.5f;

	[Range(0f, 10f)]
	public float Contrast = 1.5f;

	public static float ChangeBrightness;

	public static float ChangeSaturation;

	public static float ChangeContrast;

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
		ChangeSaturation = Saturation;
		ChangeContrast = Contrast;
		SCShader = Shader.Find("CameraFilterPack/Color_BrightContrastSaturation");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_Brightness", Brightness);
			material.SetFloat("_Saturation", Saturation);
			material.SetFloat("_Contrast", Contrast);
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
			Brightness = ChangeBrightness;
			Saturation = ChangeSaturation;
			Contrast = ChangeContrast;
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
[AddComponentMenu("Camera Filter Pack/Colors/Chromatic_Aberration")]
[ExecuteInEditMode]
public class CameraFilterPack_Color_Chromatic_Aberration : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(-0.02f, 0.02f)]
	public float Offset = 0.02f;

	public static float ChangeOffset;

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
		ChangeOffset = Offset;
		SCShader = Shader.Find("CameraFilterPack/Color_Chromatic_Aberration");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Distortion", Offset);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
			Offset = ChangeOffset;
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
[AddComponentMenu("Camera Filter Pack/Colors/Contrast")]
[ExecuteInEditMode]
public class CameraFilterPack_Color_Contrast : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 10f)]
	public float Contrast = 4.5f;

	public static float ChangeContrast;

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
		ChangeContrast = Contrast;
		SCShader = Shader.Find("CameraFilterPack/Color_Contrast");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_Contrast", Contrast);
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
			Contrast = ChangeContrast;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Colors/GrayScale")]
public class CameraFilterPack_Color_GrayScale : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/Color_GrayScale");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Colors/Invert")]
public class CameraFilterPack_Color_Invert : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/Color_Invert");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Colors/Noise")]
public class CameraFilterPack_Color_Noise : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 1f)]
	public float Noise = 0.235f;

	public static float ChangeNoise;

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
		ChangeNoise = Noise;
		SCShader = Shader.Find("CameraFilterPack/Color_Noise");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Noise", Noise);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
			Noise = ChangeNoise;
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
[AddComponentMenu("Camera Filter Pack/Colors/RGB")]
[ExecuteInEditMode]
public class CameraFilterPack_Color_RGB : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	public Color ColorRGB = new Color(1f, 1f, 1f);

	public static Color ChangeColorRGB;

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
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		ChangeColorRGB = ColorRGB;
		SCShader = Shader.Find("CameraFilterPack/Color_RGB");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetColor("_ColorRGB", ColorRGB);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (Application.isPlaying)
		{
			ColorRGB = ChangeColorRGB;
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
[AddComponentMenu("Camera Filter Pack/Colors/Sepia")]
[ExecuteInEditMode]
public class CameraFilterPack_Color_Sepia : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/Color_Sepia");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[AddComponentMenu("Camera Filter Pack/Colors/Switching")]
[ExecuteInEditMode]
public class CameraFilterPack_Color_Switching : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 5f)]
	public int Color = 1;

	public static int ChangeColor;

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
		ChangeColor = Color;
		SCShader = Shader.Find("CameraFilterPack/Color_Switching");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Distortion", (float)Color);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
			Color = ChangeColor;
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
[AddComponentMenu("Camera Filter Pack/Colors/Brightness")]
[ExecuteInEditMode]
public class CameraFilterPack_Colors_Brightness : MonoBehaviour
{
	public Shader SCShader;

	[Range(0f, 2f)]
	public float _Brightness = 1.5f;

	private Material SCMaterial;

	public static float ChangeBrightness;

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
		ChangeBrightness = _Brightness;
		SCShader = Shader.Find("CameraFilterPack/Colors_Brightness");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		if ((Object)(object)SCShader != (Object)null)
		{
			material.SetFloat("_Val", _Brightness);
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
			_Brightness = ChangeBrightness;
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
[AddComponentMenu("Camera Filter Pack/Colors/HUE_Rotate")]
[ExecuteInEditMode]
public class CameraFilterPack_Colors_HUE_Rotate : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(1f, 20f)]
	public float Speed = 10f;

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
		ChangeSpeed = Speed;
		SCShader = Shader.Find("CameraFilterPack/Colors_HUE_Rotate");
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
			material.SetFloat("_Speed", Speed);
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Colors/Threshold")]
public class CameraFilterPack_Colors_Threshold : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(0f, 1f)]
	public float Threshold = 0.3f;

	private Material SCMaterial;

	public static float ChangeThreshold;

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
		ChangeThreshold = Threshold;
		SCShader = Shader.Find("CameraFilterPack/Colors_Threshold");
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
			material.SetFloat("_Distortion", Threshold);
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
			Threshold = ChangeThreshold;
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
[AddComponentMenu("Camera Filter Pack/Distortion/BigFace")]
[ExecuteInEditMode]
public class CameraFilterPack_Distortion_BigFace : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 6.5f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	public float _Size = 5f;

	[Range(2f, 10f)]
	public float Distortion = 2.5f;

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
		ChangeSize = _Size;
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/Distortion_BigFace");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Distortion", Distortion);
			material.SetFloat("_Size", _Size);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
			_Size = ChangeSize;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Distortion/Dream")]
public class CameraFilterPack_Distortion_Dream : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(1f, 10f)]
	public float Distortion = 1f;

	private Material SCMaterial;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/Distortion_Dream");
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
[AddComponentMenu("Camera Filter Pack/Distortion/Dream2")]
[ExecuteInEditMode]
public class CameraFilterPack_Distortion_Dream2 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 100f)]
	public float Distortion = 6f;

	[Range(0f, 32f)]
	public float Speed = 5f;

	public static float ChangeDistortion;

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
		ChangeSpeed = Speed;
		SCShader = Shader.Find("CameraFilterPack/Distortion_Dream2");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Speed", Speed);
			material.SetFloat("_Distortion", Distortion);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
[AddComponentMenu("Camera Filter Pack/Distortion/FishEye")]
[ExecuteInEditMode]
public class CameraFilterPack_Distortion_FishEye : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 1f)]
	public float Distortion = 0.35f;

	public static float ChangeDistortion = 0.35f;

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
		SCShader = Shader.Find("CameraFilterPack/Distortion_FishEye");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Distortion", Distortion);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Distortion/Flag")]
public class CameraFilterPack_Distortion_Flag : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(0f, 2f)]
	public float Distortion = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/Distortion_Flag");
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
			material.SetFloat("_Distortion", Distortion);
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Distortion/Heat")]
public class CameraFilterPack_Distortion_Heat : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(1f, 100f)]
	public float Distortion = 35f;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/Distortion_Heat");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Distortion", Distortion);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Distortion/Lens")]
public class CameraFilterPack_Distortion_Lens : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(-1f, 1f)]
	public float CenterX;

	[Range(-1f, 1f)]
	public float CenterY;

	[Range(0f, 3f)]
	public float Distortion = 1f;

	public static float ChangeCenterX;

	public static float ChangeCenterY;

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
		ChangeCenterX = CenterX;
		ChangeCenterY = CenterY;
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/Distortion_Lens");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_CenterX", CenterX);
			material.SetFloat("_CenterY", CenterY);
			material.SetFloat("_Distortion", Distortion);
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
			CenterX = ChangeCenterX;
			CenterY = ChangeCenterY;
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
[AddComponentMenu("Camera Filter Pack/Distortion/Noise")]
[ExecuteInEditMode]
public class CameraFilterPack_Distortion_Noise : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 3f)]
	public float Distortion = 1f;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/Distortion_Noise");
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
			material.SetFloat("_Distortion", Distortion);
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Distortion/Wave_Horizontal")]
public class CameraFilterPack_Distortion_Wave_Horizontal : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(1f, 100f)]
	public float WaveIntensity = 32f;

	public static float ChangeWaveIntensity;

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
		ChangeWaveIntensity = WaveIntensity;
		SCShader = Shader.Find("CameraFilterPack/Distortion_Wave_Horizontal");
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
			material.SetFloat("_WaveIntensity", WaveIntensity);
			material.SetFloat("_TimeX", TimeX);
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
			WaveIntensity = ChangeWaveIntensity;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Drawing/CellShading")]
public class CameraFilterPack_Drawing_CellShading : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 1f)]
	public float EdgeSize = 0.1f;

	[Range(0f, 10f)]
	public float ColorLevel = 4f;

	public static float ChangeEdgeSize;

	public static float ChangeColorLevel;

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
		ChangeEdgeSize = EdgeSize;
		ChangeColorLevel = ColorLevel;
		SCShader = Shader.Find("CameraFilterPack/Drawing_CellShading");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_EdgeSize", EdgeSize);
			material.SetFloat("_ColorLevel", ColorLevel);
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
			EdgeSize = ChangeEdgeSize;
			ColorLevel = ChangeColorLevel;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Drawing/Comics")]
public class CameraFilterPack_Drawing_Comics : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(0f, 1f)]
	public float DotSize = 0.5f;

	public static float ChangeDotSize;

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
		ChangeDotSize = DotSize;
		SCShader = Shader.Find("CameraFilterPack/Drawing_Comics");
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
			material.SetFloat("_DotSize", DotSize);
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
			DotSize = ChangeDotSize;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Drawing/Crosshatch")]
public class CameraFilterPack_Drawing_Crosshatch : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(1f, 10f)]
	public float Width = 2f;

	public static float ChangeWidth;

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
		ChangeWidth = Width;
		SCShader = Shader.Find("CameraFilterPack/Drawing_Crosshatch");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Distortion", Width);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
			Width = ChangeWidth;
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
[AddComponentMenu("Camera Filter Pack/Drawing/Halftone")]
[ExecuteInEditMode]
public class CameraFilterPack_Drawing_Halftone : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(0f, 1f)]
	public float Threshold = 0.6f;

	[Range(1f, 16f)]
	public float DotSize = 4f;

	public static float ChangeThreshold;

	public static float ChangeDotSize;

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
		ChangeThreshold = Threshold;
		ChangeDotSize = DotSize;
		SCShader = Shader.Find("CameraFilterPack/Drawing_Halftone");
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
			material.SetFloat("_Distortion", Threshold);
			material.SetFloat("_DotSize", DotSize);
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
			Threshold = ChangeThreshold;
			DotSize = ChangeDotSize;
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
[AddComponentMenu("Camera Filter Pack/Drawing/Laplacian")]
[ExecuteInEditMode]
public class CameraFilterPack_Drawing_Laplacian : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/Drawing_Laplacian");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
		if (!Application.isPlaying)
		{
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Drawing/Manga")]
public class CameraFilterPack_Drawing_Manga : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(1f, 8f)]
	public float DotSize = 4.72f;

	public static float ChangeDotSize;

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
		ChangeDotSize = DotSize;
		SCShader = Shader.Find("CameraFilterPack/Drawing_Manga");
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
			material.SetFloat("_DotSize", DotSize);
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
			DotSize = ChangeDotSize;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Drawing/Manga2")]
public class CameraFilterPack_Drawing_Manga2 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(1f, 8f)]
	public float DotSize = 4.72f;

	public static float ChangeDotSize;

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
		ChangeDotSize = DotSize;
		SCShader = Shader.Find("CameraFilterPack/Drawing_Manga2");
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
			material.SetFloat("_DotSize", DotSize);
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
			DotSize = ChangeDotSize;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Drawing/Manga3")]
public class CameraFilterPack_Drawing_Manga3 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(1f, 8f)]
	public float DotSize = 4.72f;

	public static float ChangeDotSize;

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
		ChangeDotSize = DotSize;
		SCShader = Shader.Find("CameraFilterPack/Drawing_Manga3");
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
			material.SetFloat("_DotSize", DotSize);
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
			DotSize = ChangeDotSize;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Drawing/Manga4")]
public class CameraFilterPack_Drawing_Manga4 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(1f, 8f)]
	public float DotSize = 4.72f;

	public static float ChangeDotSize;

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
		ChangeDotSize = DotSize;
		SCShader = Shader.Find("CameraFilterPack/Drawing_Manga4");
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
			material.SetFloat("_DotSize", DotSize);
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
			DotSize = ChangeDotSize;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Drawing/Manga5")]
public class CameraFilterPack_Drawing_Manga5 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(1f, 8f)]
	public float DotSize = 4.72f;

	public static float ChangeDotSize;

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
		ChangeDotSize = DotSize;
		SCShader = Shader.Find("CameraFilterPack/Drawing_Manga5");
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
			material.SetFloat("_DotSize", DotSize);
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
			DotSize = ChangeDotSize;
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
[AddComponentMenu("Camera Filter Pack/Drawing/Manga_Color")]
[ExecuteInEditMode]
public class CameraFilterPack_Drawing_Manga_Color : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(1f, 8f)]
	public float DotSize = 1.6f;

	public static float ChangeDotSize;

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
		ChangeDotSize = DotSize;
		SCShader = Shader.Find("CameraFilterPack/Drawing_Manga_Color");
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
			material.SetFloat("_DotSize", DotSize);
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
			DotSize = ChangeDotSize;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Drawing/Toon")]
public class CameraFilterPack_Drawing_Toon : MonoBehaviour
{
	public Shader SCShader;

	private Material SCMaterial;

	private float TimeX = 1f;

	[Range(0f, 2f)]
	public float Threshold = 1f;

	[Range(0f, 8f)]
	public float DotSize = 1f;

	public static float ChangeThreshold;

	public static float ChangeDotSize;

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
		ChangeThreshold = Threshold;
		ChangeDotSize = DotSize;
		SCShader = Shader.Find("CameraFilterPack/Drawing_Toon");
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
			material.SetFloat("_Distortion", Threshold);
			material.SetFloat("_DotSize", DotSize);
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
			Threshold = ChangeThreshold;
			DotSize = ChangeDotSize;
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
[AddComponentMenu("Camera Filter Pack/Edge/BlackLine")]
[ExecuteInEditMode]
public class CameraFilterPack_Edge_BlackLine : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/Edge_BlackLine");
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[AddComponentMenu("Camera Filter Pack/Edge/Edge_filter")]
[ExecuteInEditMode]
public class CameraFilterPack_Edge_Edge_filter : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 10f)]
	public float RedAmplifier;

	[Range(0f, 10f)]
	public float GreenAmplifier = 2f;

	[Range(0f, 10f)]
	public float BlueAmplifier;

	public static float ChangeRedAmplifier;

	public static float ChangeGreenAmplifier;

	public static float ChangeBlueAmplifier;

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
		ChangeRedAmplifier = RedAmplifier;
		ChangeGreenAmplifier = GreenAmplifier;
		ChangeBlueAmplifier = BlueAmplifier;
		SCShader = Shader.Find("CameraFilterPack/Edge_Edge_filter");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_RedAmplifier", RedAmplifier);
			material.SetFloat("_GreenAmplifier", GreenAmplifier);
			material.SetFloat("_BlueAmplifier", BlueAmplifier);
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
			RedAmplifier = ChangeRedAmplifier;
			GreenAmplifier = ChangeGreenAmplifier;
			BlueAmplifier = ChangeBlueAmplifier;
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
[AddComponentMenu("Camera Filter Pack/Edge/Golden")]
[ExecuteInEditMode]
public class CameraFilterPack_Edge_Golden : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/Edge_Golden");
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Edge/Neon")]
public class CameraFilterPack_Edge_Neon : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(1f, 10f)]
	public float EdgeWeight = 1f;

	public static float ChangeEdgeWeight;

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
		ChangeEdgeWeight = EdgeWeight;
		SCShader = Shader.Find("CameraFilterPack/Edge_Neon");
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
			material.SetFloat("_EdgeWeight", EdgeWeight);
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
			EdgeWeight = ChangeEdgeWeight;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Edge/Sobel")]
public class CameraFilterPack_Edge_Sobel : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/Edge_Sobel");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/FX/8bits_gb")]
public class CameraFilterPack_FX_8bits_gb : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(-1f, 1f)]
	public float Brightness;

	public static float ChangeBrightness;

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
		SCShader = Shader.Find("CameraFilterPack/FX_8bits_gb");
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
			RenderTexture temporary = RenderTexture.GetTemporary(160, 144, 0);
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/FX/Ascii")]
public class CameraFilterPack_FX_Ascii : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/FX_Ascii");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[AddComponentMenu("Camera Filter Pack/FX/Dot_Circle")]
[ExecuteInEditMode]
public class CameraFilterPack_FX_Dot_Circle : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(4f, 32f)]
	public float Value = 7f;

	public static float ChangeValue;

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
		ChangeValue = Value;
		SCShader = Shader.Find("CameraFilterPack/FX_Dot_Circle");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			material.SetFloat("_Value", Value);
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
			Value = ChangeValue;
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
[AddComponentMenu("Camera Filter Pack/FX/Drunk")]
[ExecuteInEditMode]
public class CameraFilterPack_FX_Drunk : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 20f)]
	public float Value = 6f;

	public static float ChangeValue;

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
		ChangeValue = Value;
		SCShader = Shader.Find("CameraFilterPack/FX_Drunk");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Value", Value);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
			Value = ChangeValue;
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
[AddComponentMenu("Camera Filter Pack/FX/Funk")]
[ExecuteInEditMode]
public class CameraFilterPack_FX_Funk : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/FX_Funk");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[AddComponentMenu("Camera Filter Pack/FX/Grid")]
[ExecuteInEditMode]
public class CameraFilterPack_FX_Grid : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(0f, 5f)]
	public float Distortion = 1f;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/FX_Grid");
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
[AddComponentMenu("Camera Filter Pack/FX/Hexagon")]
[ExecuteInEditMode]
public class CameraFilterPack_FX_Hexagon : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/FX_Hexagon");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/FX/Hexagon_Black")]
public class CameraFilterPack_FX_Hexagon_Black : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0.2f, 10f)]
	public float Value = 1f;

	public static float ChangeValue;

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
		ChangeValue = Value;
		SCShader = Shader.Find("CameraFilterPack/FX_Hexagon_Black");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Value", Value);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
			Value = ChangeValue;
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
[AddComponentMenu("Camera Filter Pack/FX/InverChromiLum")]
[ExecuteInEditMode]
public class CameraFilterPack_FX_InverChromiLum : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/FX_InverChromiLum");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[AddComponentMenu("Camera Filter Pack/FX/Mirror")]
[ExecuteInEditMode]
public class CameraFilterPack_FX_Mirror : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/FX_Mirror");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[AddComponentMenu("Camera Filter Pack/FX/Psycho")]
[ExecuteInEditMode]
public class CameraFilterPack_FX_Psycho : MonoBehaviour
{
	public Shader SCShader;

	private Material SCMaterial;

	private float TimeX = 1f;

	[Range(0f, 1f)]
	public float Distortion = 1f;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/FX_Psycho");
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/FX/Spot")]
public class CameraFilterPack_FX_Spot : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	public Vector2 center = new Vector2(0.5f, 0.5f);

	public float Radius = 0.2f;

	public static Vector2 Changecenter;

	public static float ChangeRadius;

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
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Changecenter = center;
		ChangeRadius = Radius;
		SCShader = Shader.Find("CameraFilterPack/FX_Spot");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_PositionX", center.x);
			material.SetFloat("_PositionY", center.y);
			material.SetFloat("_Radius", Radius);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (Application.isPlaying)
		{
			center = Changecenter;
			Radius = ChangeRadius;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/FX/ZebraColor")]
public class CameraFilterPack_FX_ZebraColor : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(1f, 10f)]
	public float Value = 3f;

	public static float ChangeValue;

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
		ChangeValue = Value;
		SCShader = Shader.Find("CameraFilterPack/FX_ZebraColor");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Value", Value);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
			Value = ChangeValue;
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
[AddComponentMenu("Camera Filter Pack/FX/SuperDot")]
[ExecuteInEditMode]
public class CameraFilterPack_FX_superDot : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/FX_superDot");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Oculus/NightVision1")]
public class CameraFilterPack_Oculus_NightVision1 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private float Distortion = 1f;

	private Material SCMaterial;

	private Vector4 ScreenResolution;

	[Range(0f, 100f)]
	public float Vignette = 1.3f;

	[Range(1f, 150f)]
	public float Linecount = 90f;

	public static float ChangeVignette;

	public static float ChangeLinecount;

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
		ChangeVignette = Vignette;
		ChangeLinecount = Linecount;
		SCShader = Shader.Find("CameraFilterPack/Oculus_NightVision1");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Distortion", Distortion);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			material.SetFloat("_Vignette", Vignette);
			material.SetFloat("_Linecount", Linecount);
			Graphics.Blit((Texture)(object)sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit((Texture)(object)sourceTexture, destTexture);
		}
	}

	private void Update()
	{
		if (!Application.isPlaying)
		{
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
[AddComponentMenu("Camera Filter Pack/Oculus/NightVision2")]
[ExecuteInEditMode]
public class CameraFilterPack_Oculus_NightVision2 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 1f)]
	public float BinocularSize = 0.5f;

	[Range(0f, 1f)]
	public float BinocularDistance = 0.5f;

	[Range(0f, 1f)]
	public float Greenness = 0.4f;

	public static float ChangeBinocularSize;

	public static float ChangeBinocularDistance;

	public static float ChangeGreenness;

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
		ChangeBinocularSize = BinocularSize;
		ChangeBinocularDistance = BinocularDistance;
		ChangeGreenness = Greenness;
		SCShader = Shader.Find("CameraFilterPack/Oculus_NightVision2");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_BinocularSize", BinocularSize);
			material.SetFloat("_BinocularDistance", BinocularDistance);
			material.SetFloat("_Greenness", Greenness);
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
			BinocularSize = ChangeBinocularSize;
			BinocularDistance = ChangeBinocularDistance;
			Greenness = ChangeGreenness;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Pixelisation/OilPaint")]
public class CameraFilterPack_Pixelisation_OilPaint : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 5f)]
	public float Value = 1f;

	public static float ChangeValue;

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
		ChangeValue = Value;
		SCShader = Shader.Find("CameraFilterPack/Pixelisation_OilPaint");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			material.SetFloat("_Value", Value);
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
			Value = ChangeValue;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Pixelisation/OilPaintHQ")]
public class CameraFilterPack_Pixelisation_OilPaintHQ : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	[Range(0f, 5f)]
	public float Value = 2f;

	public static float ChangeValue;

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
		ChangeValue = Value;
		SCShader = Shader.Find("CameraFilterPack/Pixelisation_OilPaintHQ");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			material.SetFloat("_Value", Value);
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
			Value = ChangeValue;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Sharpen/Sharpen")]
public class CameraFilterPack_Sharpen_Sharpen : MonoBehaviour
{
	public Shader SCShader;

	[Range(0.001f, 40f)]
	public float Value = 4f;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

	public static float ChangeValue;

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
		ChangeValue = Value;
		SCShader = Shader.Find("CameraFilterPack/Sharpen_Sharpen");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
			material.SetFloat("_Value", Value);
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
			Value = ChangeValue;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/TV/ARCADE")]
public class CameraFilterPack_TV_ARCADE : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/TV_ARCADE");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[AddComponentMenu("Camera Filter Pack/TV/Chromatical")]
[ExecuteInEditMode]
public class CameraFilterPack_TV_Chromatical : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/TV_Chromatical");
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/TV/Distorted")]
public class CameraFilterPack_TV_Distorted : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(1f, 10f)]
	public float Distortion = 1f;

	private Material SCMaterial;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/TV_Distorted");
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
[AddComponentMenu("Camera Filter Pack/TV/Old")]
[ExecuteInEditMode]
public class CameraFilterPack_TV_Old : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(1f, 10f)]
	public float Distortion = 1f;

	private Material SCMaterial;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/TV_Old");
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
[AddComponentMenu("Camera Filter Pack/TV/Old_Movie")]
[ExecuteInEditMode]
public class CameraFilterPack_TV_Old_Movie : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(1f, 10f)]
	public float Distortion = 1f;

	private Material SCMaterial;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/TV_Old_Movie");
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/TV/Posterize")]
public class CameraFilterPack_TV_Posterize : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(1f, 256f)]
	public float Posterize = 64f;

	private Material SCMaterial;

	public static float ChangePosterize;

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
		ChangePosterize = Posterize;
		SCShader = Shader.Find("CameraFilterPack/TV_Posterize");
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
			material.SetFloat("_Distortion", Posterize);
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
			Posterize = ChangePosterize;
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
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/TV/RGB Display")]
public class CameraFilterPack_TV_Rgb : MonoBehaviour
{
	public Shader SCShader;

	private Vector4 ScreenResolution;

	private float TimeX = 1f;

	[Range(1f, 10f)]
	private float Distortion = 1f;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/TV_Rgb");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetFloat("_Distortion", Distortion);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/TV/VCR Distortion")]
public class CameraFilterPack_TV_Vcr : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(1f, 10f)]
	public float Distortion = 1f;

	private Material SCMaterial;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/TV_Vcr");
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
[AddComponentMenu("Camera Filter Pack/TV/Video3D")]
[ExecuteInEditMode]
public class CameraFilterPack_TV_Video3D : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/TV_Video3D");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[AddComponentMenu("Camera Filter Pack/TV/Videoflip")]
[ExecuteInEditMode]
public class CameraFilterPack_TV_Videoflip : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Vector4 ScreenResolution;

	private Material SCMaterial;

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
		SCShader = Shader.Find("CameraFilterPack/TV_Videoflip");
		if (!SystemInfo.supportsImageEffects)
		{
			((Behaviour)this).enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)SCShader != (Object)null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			material.SetVector("_ScreenResolution", new Vector4((float)sourceTexture.width, (float)sourceTexture.height, 0f, 0f));
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
		if (Object.op_Implicit((Object)(object)SCMaterial))
		{
			Object.DestroyImmediate((Object)(object)SCMaterial);
		}
	}
}
[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/TV/Vintage")]
public class CameraFilterPack_TV_Vintage : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(1f, 10f)]
	public float Distortion = 1f;

	private Material SCMaterial;

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
		ChangeDistortion = Distortion;
		SCShader = Shader.Find("CameraFilterPack/TV_Vintage");
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
[RequireComponent(typeof(Animator))]
public class RootMotionScript : MonoBehaviour
{
	private void OnAnimatorMove()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		Animator component = ((Component)this).GetComponent<Animator>();
		if (Object.op_Implicit((Object)(object)component))
		{
			Vector3 position = ((Component)this).transform.position;
			position.z += component.GetFloat("Runspeed") * Time.deltaTime;
			((Component)this).transform.position = position;
		}
	}
}
[ExecuteInEditMode]
public class VisualizeJoint : MonoBehaviour
{
	private void Start()
	{
	}

	private void Update()
	{
	}

	private void OnDrawGizmos()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		Gizmos.color = Color.red;
		Gizmos.DrawSphere(((Component)this).transform.position, 0.015f);
		foreach (Transform item in ((Component)this).transform)
		{
			Transform val = item;
			Gizmos.DrawLine(((Component)this).transform.position, val.position);
		}
	}
}
public class showcasecam : MonoBehaviour
{
	public Transform rotationPoint;

	private void Start()
	{
	}

	private void Update()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		((Component)this).transform.RotateAround(rotationPoint.position, Vector3.up, 20f * Time.deltaTime);
	}
}
public class AppQuit : MonoBehaviour, IQuittable
{
	public void OnQuit()
	{
		Debug.Log((object)"AppQuit.Quit");
		Application.Quit();
	}
}
public interface IPausable
{
	void OnUnPause();

	void OnPause();
}
public interface IQuittable
{
	void OnQuit();
}
public class PauseManager : MonoBehaviour
{
	public GameObject pausable;

	public Canvas pauseCanvas;

	private bool isPaused;

	private Animator anim;

	private Component[] pausableInterfaces;

	private Component[] quittableInterfaces;

	private void Start()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		if ((Object)(object)Object.FindObjectOfType<EventSystem>() == (Object)null)
		{
			GameObject val = new GameObject("EventSystem", new Type[1] { typeof(EventSystem) });
			val.AddComponent<StandaloneInputModule>();
		}
		pausableInterfaces = pausable.GetComponents(typeof(IPausable));
		quittableInterfaces = pausable.GetComponents(typeof(IQuittable));
		anim = ((Component)pauseCanvas).GetComponent<Animator>();
		((Behaviour)pauseCanvas).enabled = false;
	}

	private void Update()
	{
		if (Input.GetKeyDown((KeyCode)27))
		{
			if (isPaused)
			{
				OnUnPause();
			}
			else
			{
				OnPause();
			}
		}
		((Behaviour)pauseCanvas).enabled = isPaused;
		anim.SetBool("IsPaused", isPaused);
	}

	public void OnQuit()
	{
		Debug.Log((object)"PauseManager.OnQuit");
		Component[] array = quittableInterfaces;
		foreach (Component val in array)
		{
			((IQuittable)val)?.OnQuit();
		}
	}

	public void OnUnPause()
	{
		Debug.Log((object)"PauseManager.OnUnPause");
		isPaused = false;
		Component[] array = pausableInterfaces;
		foreach (Component val in array)
		{
			((IPausable)val)?.OnUnPause();
		}
	}

	public void OnPause()
	{
		Debug.Log((object)"PauseManager.OnPause");
		isPaused = true;
		Component[] array = pausableInterfaces;
		foreach (Component val in array)
		{
			((IPausable)val)?.OnPause();
		}
	}
}
public class TimePause : MonoBehaviour, IPausable
{
	public float pauseDelay = 0.3f;

	private float timeScale;

	public void OnUnPause()
	{
		Debug.Log((object)"TestPause.OnUnPause");
		Time.timeScale = timeScale;
	}

	public void OnPause()
	{
		Debug.Log((object)"TestPause.OnPause");
		timeScale = Time.timeScale;
		((MonoBehaviour)this).Invoke("StopTime", pauseDelay);
	}

	private void StopTime()
	{
		Time.timeScale = 0f;
	}
}
public class BoundsTest : MonoBehaviour
{
	public Transform pointA;

	public Transform pointB;

	public float angle;

	public float rayWidth;

	public GameObject edge;

	private RaycastHit hitBS;

	private void Start()
	{
	}

	private void Update()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		if (Input.GetButtonDown("Jump"))
		{
			test();
		}
		angle = ((Component)this).transform.rotation.x;
		rayWidth = angle * 2f;
		RaycastHit val = default(RaycastHit);
		if (Physics.Raycast(((Component)this).transform.position + ((Component)this).transform.forward * 0.5f + ((Component)this).transform.up * 2f, -Vector3.up, ref val))
		{
		}
		if (Physics.Raycast(((Component)this).transform.position + ((Component)this).transform.forward * 0.5f + ((Component)this).transform.up * 2f, -Vector3.up, ref hitBS))
		{
		}
		Debug.DrawRay(((Component)this).transform.position + ((Component)this).transform.forward * 0.5f + ((Component)this).transform.up * 2f, -Vector3.up);
		Debug.DrawRay(((RaycastHit)(ref hitBS)).point - new Vector3(0f, 0.1f, 0f) - ((Component)this).transform.forward * 0.5f, ((Component)this).transform.forward);
	}

	private void OnDrawGizmos()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		Gizmos.color = Color.white;
		Gizmos.DrawWireSphere(((Component)this).transform.position + ((Component)this).transform.forward * 0.5f + ((Component)this).transform.up * 2f, 0.1f);
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(((RaycastHit)(ref hitBS)).point - new Vector3(0f, 0.1f, 0f) - ((Component)this).transform.forward * 0.5f, new Vector3(0.1f, 0.1f, 0.1f));
	}

	private void test()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Expected O, but got Unknown
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		RaycastHit val = default(RaycastHit);
		if (!Physics.Raycast(((Component)this).transform.position + ((Component)this).transform.forward * 0.5f + ((Component)this).transform.up * 2f, -Vector3.up, ref val))
		{
			return;
		}
		MonoBehaviour.print((object)((RaycastHit)(ref val)).distance);
		RaycastHit val2 = default(RaycastHit);
		if (((RaycastHit)(ref val)).distance < 1f && Physics.Raycast(((RaycastHit)(ref val)).point - new Vector3(0f, 0.1f, 0f) - ((Component)this).transform.forward * 0.5f, ((Component)this).transform.forward, ref val2))
		{
			MonoBehaviour.print((object)"ss");
			float distance = ((RaycastHit)(ref val)).distance;
			if (distance > 0f && distance < 1.5f)
			{
				MonoBehaviour.print((object)"ms");
				Vector3 val3 = default(Vector3);
				((Vector3)(ref val3))..ctor(((RaycastHit)(ref val2)).point.x, ((RaycastHit)(ref val)).point.y, ((RaycastHit)(ref val2)).point.z);
				GameObject val4 = (GameObject)Object.Instantiate((Object)(object)pointA, val3, Quaternion.identity);
				edge = val4;
				edge.transform.up = ((RaycastHit)(ref val)).normal;
				edge.transform.forward = ((RaycastHit)(ref val2)).normal;
			}
		}
	}
}
public class RM_RakeMovement : MonoBehaviour
{
	public bool isGrounded;

	public float groundMovementSpeed = 6f;

	public float groundRunSpeed = 10f;

	public float flySpeed = 3f;

	public float speed = 6f;

	public float jumpSpeed = 8f;

	public float gravity = 20f;

	public Vector3 moveDirection = Vector3.zero;

	public Animator weaponAnim;

	public Animator cameraAnim;

	public bool BCanJump;

	public bool isGround()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		RaycastHit val = default(RaycastHit);
		if (Physics.SphereCast(((Component)this).transform.position, ((Component)this).GetComponent<CharacterController>().radius, Vector3.down, ref val, ((Component)this).GetComponent<CharacterController>().height / 2f))
		{
			return true;
		}
		return false;
	}

	private void Update()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Invalid comparison between Unknown and I4
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		Debug.DrawRay(((Component)this).transform.position, Vector3.down * 1.2f, Color.yellow);
		Debug.DrawLine(((Component)this).transform.position, Vector3.down);
		CharacterController component = ((Component)this).GetComponent<CharacterController>();
		if (isGrounded)
		{
			float axis = Input.GetAxis("Horizontal");
			float axis2 = Input.GetAxis("Vertical");
			float axis3 = Input.GetAxis("Run");
			float num = axis * axis + axis2 * axis2;
			num = Mathf.Clamp(num, 0f, 1f);
			num += axis3;
			if (axis3 == 1f)
			{
				speed = groundRunSpeed;
			}
			else
			{
				speed = groundMovementSpeed;
			}
			moveDirection = new Vector3(axis, 0f, axis2);
			cameraAnim.SetFloat("Speed", num);
			weaponAnim.SetFloat("FloatSpeed", 0f);
			weaponAnim.SetFloat("Speed", num);
			moveDirection = ((Component)this).transform.TransformDirection(moveDirection);
			moveDirection *= speed;
		}
		else
		{
			float num2 = Input.GetAxis("Horizontal") * speed / 2f;
			float num3 = Mathf.Clamp(Input.GetAxis("Vertical"), 0f, 1f) * speed;
			moveDirection = new Vector3(num2, moveDirection.y, num3);
			moveDirection = ((Component)this).transform.TransformDirection(moveDirection);
		}
		if (Input.GetButton("Jump") && isGrounded)
		{
			weaponAnim.SetFloat("FloatSpeed", 1f);
			moveDirection.y = jumpSpeed;
		}
		if (!isGrounded)
		{
			ref Vector3 reference = ref moveDirection;
			reference.y -= gravity * Time.deltaTime;
		}
		component.Move(moveDirection * Time.deltaTime);
		if ((int)component.collisionFlags == 1 || isGround())
		{
			isGrounded = true;
		}
		if ((int)component.collisionFlags == 0 && !isGround())
		{
			isGrounded = false;
		}
		weaponAnim.SetBool("Grounded", isGrounded);
		cameraAnim.SetBool("Grounded", isGrounded);
	}
}
public class AI_Test : MonoBehaviour
{
	public NavMeshAgent nav;

	public bool bPathIsSet;

	public Transform target;

	public NavMeshPath path;

	private void Start()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		path = new NavMeshPath();
		nav.CalculatePath(target.position, path);
	}

	private void Update()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Invalid comparison between Unknown and I4
		nav.SetDestination(target.position);
		if ((int)nav.path.status == 1)
		{
			Debug.Log((object)"PAth is partial");
		}
		if ((int)nav.path.status == 2)
		{
			Debug.Log((object)"PAth is invalid");
		}
	}
}
public class AnimalCage_obj : MonoBehaviour
{
	public Animator anim;

	private bool bTraped;

	public GameObject currentAnimal;

	private void OnTriggerEnter(Collider info)
	{
		if (Object.op_Implicit((Object)(object)((Component)info).GetComponent<Animal_AI>()))
		{
			currentAnimal = ((Component)info).gameObject;
			CloseCage();
		}
	}

	public void OpenCage()
	{
		anim.SetBool("Close", false);
		if ((Object)(object)currentAnimal != (Object)null)
		{
			currentAnimal.GetComponent<Animal_AI>().AfterTrapEffect();
		}
	}

	public void CloseCage()
	{
		anim.SetBool("Close", true);
		if ((Object)(object)currentAnimal != (Object)null)
		{
			currentAnimal.GetComponent<Animal_AI>().Event_Trapped();
		}
	}
}
public class AnimalHead : MonoBehaviour
{
	public Transform target;

	public float fieldOfViewAngle;

	public Animal_AI animal_AI;

	public bool bSeen()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		target = animal_AI.enemy;
		if ((Object)(object)target != (Object)null)
		{
			Vector3 val = ((Component)target).transform.position - ((Component)this).transform.position;
			Vector3 forward = ((Component)this).transform.forward;
			float num = Vector3.Angle(val, forward);
			if (num < fieldOfViewAngle)
			{
				Debug.Log((object)"See");
				return true;
			}
			Debug.Log((object)"Unseen");
			return false;
		}
		return false;
	}
}
public class AnimalRecordCamera_obj : RECCameras_obj
{
	public GameObject setupCamera;

	public Transform parent;

	public Transform collar;

	public Transform backlight;

	public override void Action()
	{
		if (WeaponManager.instance.CurrentWeapon() != 2)
		{
			if (WeaponManager.instance.CheckWeapon(2))
			{
				InformationUI.instance.DrawHint("Please select camera");
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have camera");
			}
		}
	}

	private void Update()
	{
		UpdateColor();
	}

	private void UpdateColor()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		Renderer component = ((Component)backlight).GetComponent<Renderer>();
		Color val = default(Color);
		((Color)(ref val))..ctor(Color.yellow.r, Color.yellow.g, Color.yellow.b, Mathf.PingPong(Time.time * 0.1f, 0.1f));
		component.material.SetColor("_TintColor", val);
	}

	public override void SetupCamera()
	{
		((Component)backlight).gameObject.SetActive(false);
		Debug.Log((object)"FF");
		setupCamera.gameObject.SetActive(true);
		((Component)collar).gameObject.SetActive(true);
		CameraManager.instance.AddCamera(setupCamera.transform);
		Object.Destroy((Object)(object)((Component)this).gameObject);
	}
}
public class Animal_AI : Unit_obj
{
	public enum Animal_state
	{
		idle,
		walk,
		eating,
		walking,
		escape
	}

	public Animator animator;

	public AnimalHead animalHead;

	public Animal_state ActiveState;

	public float dangerDistance;

	public NavMeshAgent nav;

	public int Health = 1000;

	public bool isAlive = true;

	public float walkSpeed = 2f;

	public float runSpeed = 7f;

	public Transform camera;

	public bool bKillingProccess;

	public bool bHungry;

	public bool bDanger;

	public bool bWalk;

	public bool bTakeDamage;

	public bool bTraped;

	public int hungryCounter;

	public float staminaCounter;

	public GameObject ragdoll;

	public Vector3 currentDist;

	private float stopDistance = 0.5f;

	public Transform enemy;

	public float takeDamageWaitTime;

	public float walkingWaitTime;

	public float walkingWaitTimeMax;

	public float eatingWaitTime;

	public float eatingWaitTimeMax;

	public float dangerWaitTime;

	public bool bEnemyInRadius;

	public Transform point;

	public List<Transform> escapePoints = new List<Transform>();

	public List<Transform> walkPoints = new List<Transform>();

	public List<Transform> foodPoints = new List<Transform>();

	public GameObject bloodSpot;

	public Transform detectionTrigger;

	public Vector3 currentWalkingPoint;

	public Vector3 currentEatingPoint;

	public Vector3 currentEscapePoint;

	public int eatingPointID;

	public AudioClip walkSound;

	public AudioClip[] howlSounds;

	public bool StepDown;

	public AudioSource audioSource;

	public bool stateSound;

	public float walkSoundDelay;

	public float runSoundDelay;

	private void OnTriggerEnter(Collider info)
	{
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<Enemy_AI>()))
		{
			enemy = ((Component)info).gameObject.transform;
			bEnemyInRadius = true;
		}
	}

	private void OnTriggerExit(Collider info)
	{
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<Enemy_AI>()))
		{
			bEnemyInRadius = false;
		}
	}

	private void Start()
	{
		NotificationsManager.Instance.AddListener((Component)(object)this, "Event_PlayerFire");
		SetWalkingState();
	}

	public void Event_PlayerFire(Component sender)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (!bDanger)
		{
			Debug.Log((object)"PlayerFire");
			float num = Vector3.Distance(((Component)this).transform.position, sender.GetComponent<Transform>().position);
			if (num < 300f)
			{
				SetDangerState();
			}
		}
	}

	public void Event_Trapped()
	{
		SetTrapedState();
	}

	public void SetCamera(bool state)
	{
		if ((Object)(object)camera != (Object)null && Object.op_Implicit((Object)(object)((Component)camera).GetComponent<RecordCamera_obj>()))
		{
			((Component)camera).gameObject.SetActive(state);
		}
	}

	public void SetHungryState()
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		ResetAllDestination();
		RefreshPoints();
		nav.Resume();
		bHungry = true;
		nav.speed = walkSpeed;
		if (currentEatingPoint == Vector3.zero)
		{
			int num = Random.Range(0, foodPoints.Count);
			eatingPointID = num;
			currentEatingPoint = foodPoints[eatingPointID].position;
		}
	}

	public void SetDangerState()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		audioSource.PlayOneShot(howlSounds[Random.Range(0, howlSounds.Length)]);
		ResetAllDestination();
		nav.Resume();
		bDanger = true;
		animator.SetBool("bEat", false);
		animator.SetBool("bWalk", false);
		animator.SetFloat("Speed", 2f);
		nav.speed = runSpeed;
		if (currentEscapePoint == Vector3.zero)
		{
			currentEscapePoint = escapePoints[Random.Range(0, escapePoints.Count)].position;
		}
	}

	public void SetWalkingState()
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		nav.ResetPath();
		ResetAllDestination();
		nav.Resume();
		bWalk = true;
		animator.SetFloat("Speed", 1f);
		nav.speed = walkSpeed;
		if (currentWalkingPoint == Vector3.zero)
		{
			currentWalkingPoint = walkPoints[Random.Range(0, walkPoints.Count)].position;
		}
	}

	public void SetTrapedState()
	{
		bTraped = true;
		bHungry = false;
		SetCamera(state: true);
	}

	private void Update()
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		if (isAlive)
		{
			if (bTraped)
			{
				Idle();
			}
			else if (bDanger)
			{
				Escape();
			}
			else
			{
				if (bEnemyInRadius && (Object)(object)enemy != (Object)null)
				{
					if (Vector3.Distance(enemy.position, ((Component)this).transform.position) < dangerDistance)
					{
						SetDangerState();
					}
					if (animalHead.bSeen())
					{
						SetDangerState();
					}
				}
				if (bHungry)
				{
					Eating();
				}
				else
				{
					Walking();
				}
			}
		}
		if (bKillingProccess)
		{
			Killing();
		}
	}

	public void Killing()
	{
		nav.Stop();
		bHungry = false;
		bWalk = false;
		animator.SetFloat("Speed", 0f);
		bDanger = false;
	}

	private void Idle()
	{
		Stop();
		animator.SetFloat("Speed", 0f);
		animator.SetBool("bEat", false);
	}

	private void Walking()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(currentWalkingPoint, ((Component)this).transform.position) < stopDistance)
		{
			animator.SetFloat("Speed", 0f);
			animator.SetBool("bWalk", true);
			ActiveState = Animal_state.walking;
			walkingWaitTime += Time.deltaTime;
			if (walkingWaitTime > walkingWaitTimeMax)
			{
				animator.SetBool("bWalk", false);
				int[] array = new int[2] { 0, 1 };
				animator.SetInteger("WalkType", Random.Range(0, array.Length));
				walkingWaitTime = 0f;
				animator.SetFloat("Speed", 1f);
				Vector3 val = Random.insideUnitSphere * 5f;
				val += currentWalkingPoint;
				NavMeshHit val2 = default(NavMeshHit);
				NavMesh.SamplePosition(val, ref val2, 5f, 1);
				int[] array2 = new int[4] { 10, 15, 20, 25 };
				hungryCounter += array2[Random.Range(0, array2.Length)];
				UpdateAnimalState();
				currentWalkingPoint = ((NavMeshHit)(ref val2)).position;
				int[] array3 = new int[2] { 3, 3 };
				walkingWaitTimeMax = array3[Random.Range(0, array3.Length)];
			}
		}
		else if (!nav.pathPending)
		{
			ActiveState = Animal_state.walk;
			PlayFootstep(walkSoundDelay);
			nav.SetDestination(currentWalkingPoint);
			animator.SetFloat("Speed", 1f);
		}
		else
		{
			animator.SetFloat("Speed", 0f);
		}
	}

	private void Eating()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(currentEatingPoint, ((Component)this).transform.position) < 10f && Vector3.Distance(currentEatingPoint, ((Component)this).transform.position) > stopDistance && ((Component)foodPoints[eatingPointID]).GetComponent<FoodPoint>().isUse)
		{
			SetHungryState();
		}
		if (Vector3.Distance(currentEatingPoint, ((Component)this).transform.position) < stopDistance)
		{
			((Component)foodPoints[eatingPointID]).GetComponent<FoodPoint>().isUse = true;
			animator.SetFloat("Speed", 0f);
			animator.SetBool("bEat", true);
			eatingWaitTime += Time.deltaTime;
			ActiveState = Animal_state.eating;
			if (eatingWaitTime > eatingWaitTimeMax)
			{
				((Component)foodPoints[eatingPointID]).GetComponent<FoodPoint>().isUse = false;
				((Component)foodPoints[eatingPointID]).GetComponent<FoodPoint>().Eat();
				eatingWaitTime = 0f;
				hungryCounter = 0;
				bHungry = false;
				currentWalkingPoint = Vector3.zero;
				animator.SetBool("bEat", false);
				animator.SetFloat("Speed", 1f);
				SetWalkingState();
			}
		}
		else
		{
			PlayFootstep(walkSoundDelay);
			ActiveState = Animal_state.walk;
			animator.SetBool("bEat", false);
			nav.SetDestination(currentEatingPoint);
			animator.SetFloat("Speed", 1f);
		}
	}

	private void Escape()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(currentEscapePoint, ((Component)this).transform.position) < stopDistance)
		{
			animator.SetFloat("Speed", 0f);
			animator.SetBool("bWalk", false);
			animator.SetBool("bEat", false);
			if (!bEnemyInRadius)
			{
				dangerWaitTime += Time.deltaTime;
				ActiveState = Animal_state.idle;
				if (dangerWaitTime > 5f)
				{
					dangerWaitTime = 0f;
					currentEatingPoint = Vector3.zero;
					currentWalkingPoint = Vector3.zero;
					bDanger = false;
					if (bWalk)
					{
						SetWalkingState();
					}
					if (bHungry)
					{
						SetHungryState();
					}
				}
			}
			else
			{
				SetDangerState();
				dangerWaitTime = 0f;
			}
		}
		else
		{
			PlayFootstep(runSoundDelay);
			ActiveState = Animal_state.escape;
			animator.SetBool("bEat", false);
			nav.SetDestination(currentEscapePoint);
			animator.SetFloat("Speed", 2f);
		}
	}

	public void ResetAllDestination()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		currentEatingPoint = Vector3.zero;
		currentEscapePoint = Vector3.zero;
		currentWalkingPoint = Vector3.zero;
	}

	private void RefreshPoints()
	{
		for (int i = 0; i < foodPoints.Count; i++)
		{
			if ((Object)(object)foodPoints[i] == (Object)null)
			{
				foodPoints.RemoveAt(i);
			}
		}
	}

	private void Stop()
	{
		nav.Stop();
	}

	private bool bReached()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(((Component)this).transform.position, currentDist) < stopDistance)
		{
			return true;
		}
		return false;
	}

	public void AfterTrapEffect()
	{
		SetDangerState();
		bTraped = false;
	}

	private void PlayFootstep(float time)
	{
		if (!StepDown)
		{
			((MonoBehaviour)this).StartCoroutine(FootStep(time));
		}
	}

	private IEnumerator FootStep(float stepDelay)
	{
		StepDown = true;
		audioSource.PlayOneShot(walkSound);
		yield return (object)new WaitForSeconds(stepDelay);
		StepDown = false;
	}

	public void UpdateAnimalState()
	{
		if ((float)hungryCounter > 100f)
		{
			SetHungryState();
		}
	}

	public override void TakeDamage(int amount, string damageType)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		Health -= amount;
		if (damageType == "SniperShot")
		{
			SetDangerState();
		}
		if (damageType == "TrapDamage")
		{
			SetDangerState();
		}
		if (Health <= 0)
		{
			isAlive = false;
			animator.SetBool("bDead", true);
			Stop();
			Vector3 position = ((Component)this).transform.position;
			position.y += 1f;
			Ray val = default(Ray);
			((Ray)(ref val))..ctor(position, Vector3.down);
			RaycastHit val2 = default(RaycastHit);
			if (Physics.Raycast(val, ref val2, 100f))
			{
				Object.Instantiate((Object)(object)bloodSpot, ((RaycastHit)(ref val2)).point + ((RaycastHit)(ref val2)).normal * 0.2f, Quaternion.LookRotation(-((RaycastHit)(ref val2)).normal));
			}
			NotificationsManager.Instance.RemoveListener((Component)(object)this, "Event_PlayerFire");
			animator.SetFloat("Speed", 0f);
			animator.SetBool("bEat", false);
			Object.Instantiate((Object)(object)ragdoll, ((Component)this).transform.position, Quaternion.identity);
			if ((Object)(object)camera != (Object)null && Object.op_Implicit((Object)(object)((Component)camera).GetComponent<RecordCamera_obj>()))
			{
				CameraManager.instance.RemoveCamera(((Component)camera).GetComponent<RecordCamera_obj>().setupCamera.transform);
			}
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class AnimalsManager : MonoBehaviour
{
	public List<Transform> currentAnimals = new List<Transform>();

	public List<Transform> spawnPoints = new List<Transform>();

	public List<GameObject> animalsType = new List<GameObject>();

	public List<Transform> foodPoints = new List<Transform>();

	public int prevPointID;

	public static AnimalsManager instance;

	private void Start()
	{
		instance = this;
	}

	public void FindClosetAnimal(Transform point)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		RefreshList();
		int index = 0;
		if ((Object)(object)point == (Object)null)
		{
			return;
		}
		float num = 0f;
		if ((Object)(object)currentAnimals[0] != (Object)null)
		{
			num = Vector3.Distance(point.position, currentAnimals[0].position);
		}
		for (int i = 0; i < currentAnimals.Count; i++)
		{
			if (Vector3.Distance(point.position, currentAnimals[i].position) < num)
			{
				num = Vector3.Distance(point.position, currentAnimals[i].position);
				index = i;
			}
		}
		Debug.Log((object)num);
		if (num < 250f)
		{
			((Component)currentAnimals[index]).GetComponent<Animal_AI>().foodPoints.Add(point);
		}
	}

	public void RefreshList()
	{
		for (int i = 0; i < currentAnimals.Count; i++)
		{
			if ((Object)(object)currentAnimals[i] == (Object)null)
			{
				currentAnimals.RemoveAt(i);
			}
		}
	}

	public void SpawnAnimal()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		int index = Random.Range(0, spawnPoints.Count);
		int index2 = Random.Range(0, animalsType.Count);
		Object.Instantiate((Object)(object)animalsType[index2], spawnPoints[index].position, Quaternion.identity);
	}

	public List<Transform> GetFoodPoints()
	{
		return foodPoints;
	}

	public void AddFoodPoint(Transform point)
	{
		foodPoints.Add(point);
		FindClosetAnimal(point);
	}

	public void RemoveFoodPoint(Transform point)
	{
		foodPoints.Remove(point);
	}
}
public class AutoDestroy : MonoBehaviour
{
	public float TD;

	private float timer;

	private void Update()
	{
		timer += Time.deltaTime;
		if (timer > TD)
		{
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class BoxOfCameras_item : Item
{
	public Objective objective;

	public GameObject obj;

	public override void PickupItem()
	{
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		if (count > 0)
		{
			if (WeaponManager.instance.CheckWeapon(2))
			{
				if (WeaponManager.instance.bFullAmmo(2))
				{
					InformationUI.instance.DrawHint("Full count of cameras");
					return;
				}
				AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
				WeaponManager.instance.AddAmmo(2, 1);
				InformationUI.instance.PickupInfo("+ Add camera 1");
				count--;
				if (count <= 0)
				{
					obj.SetActive(false);
				}
				if ((Object)(object)objective != (Object)null)
				{
					objective.ObjectiveAction();
				}
			}
			else
			{
				AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
				WeaponManager.instance.AddWeapon(2, 1, type: true);
				InformationUI.instance.PickupInfo("+ Add camera 1");
				if ((Object)(object)objective != (Object)null)
				{
					objective.ObjectiveAction();
				}
				count--;
				if (count <= 0)
				{
					obj.SetActive(false);
				}
			}
		}
		else
		{
			InformationUI.instance.DrawHint("Empty");
			obj.SetActive(false);
		}
	}
}
public class CameraAngle : MonoBehaviour
{
	public Transform camera;

	public Transform target;

	private bool isView;

	public Transform cam;

	public int id;

	public void OnTriggerEnter(Collider info)
	{
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<Unit_obj>()))
		{
			target = ((Component)info).gameObject.transform;
		}
	}

	public void OnTriggerExit(Collider info)
	{
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<Unit_obj>()) && (Object)(object)target == (Object)(object)((Component)info).gameObject.transform)
		{
			isView = false;
			target = null;
			CameraManager.instance.Event_disableCamera(id);
		}
	}

	private void Update()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)target != (Object)null)
		{
			Vector3 val = ((Component)target).transform.position - ((Component)this).transform.position;
			if (Vector3.Angle(((Component)camera).transform.position, val) < 60f && !isView)
			{
				isView = true;
				id = CameraManager.instance.FindCameraID(cam);
				CameraManager.instance.Event_activeCamera(id);
			}
		}
	}
}
public class CameraCanvas : MonoBehaviour
{
	public List<Image> points = new List<Image>();

	private void OnEnable()
	{
		GetCamerasState();
	}

	private void GetCamerasState()
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		for (int i = 0; i < CameraManager.instance.allCameras.Count; i++)
		{
			if (((Component)CameraManager.instance.allCameras[i]).GetComponent<RecordCamera_obj>().cameraState == RecordCamera_obj.Camera_state.Setup)
			{
				((Graphic)points[i]).color = Color.green;
			}
			else
			{
				((Graphic)points[i]).color = Color.red;
			}
		}
	}
}
public class CameraComputer : DynamicObject
{
	public Animator effects;

	public bool processing;

	public bool bWatching;

	public GameObject cameraManager_UI;

	public float rotationSpeed;

	public float smoothTime;

	private Quaternion cameraTargetRot = Quaternion.identity;

	private float rotationY;

	public AudioSource audioSource;

	public AudioSource motion;

	public static CameraComputer instance;

	public AudioClip startSound;

	public override void Action()
	{
		if (!processing)
		{
			if (CameraManager.instance.GetCameraCount() > 0)
			{
				((MonoBehaviour)this).StartCoroutine(changeType(type: false));
			}
			else
			{
				InformationUI.instance.DrawHint("You did not setup cameras");
			}
		}
	}

	private void Start()
	{
		instance = this;
	}

	private void Update()
	{
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		if (bWatching)
		{
			if (Input.GetKeyDown((KeyCode)101))
			{
				((MonoBehaviour)this).StartCoroutine(changeType(type: true));
			}
			motion.volume = Mathf.Abs(Input.GetAxis("Horizontal"));
			rotationY += Input.GetAxis("Horizontal") * rotationSpeed;
			rotationY = Mathf.Clamp(rotationY, -45f, 45f);
			if ((Object)(object)CameraManager.instance.currentCamera != (Object)null)
			{
				CameraManager.instance.currentCamera.localEulerAngles = new Vector3(((Component)this).transform.localEulerAngles.x, rotationY, 0f);
			}
		}
	}

	public void Exit()
	{
		((MonoBehaviour)this).StartCoroutine(changeType(type: true));
	}

	public void ResetCameraPosition()
	{
		rotationY = 0f;
		((MonoBehaviour)this).StartCoroutine(switchEffect());
	}

	private IEnumerator switchEffect()
	{
		effects.SetBool("Switch", true);
		yield return (object)new WaitForSeconds(0.2f);
		effects.SetBool("Switch", false);
	}

	private IEnumerator changeType(bool type)
	{
		if (!type)
		{
			audioSource.PlayOneShot(startSound);
			effects.SetBool("Fade", true);
			bWatching = false;
			processing = true;
			yield return (object)new WaitForSeconds(1f);
			FPSController.instance.DisablePlayer();
			effects.SetBool("Fade", false);
			CameraManager.instance.ActiveCamera(0);
			yield return (object)new WaitForSeconds(1f);
			cameraManager_UI.SetActive(true);
			bWatching = true;
			Cursor.lockState = (CursorLockMode)0;
			Cursor.visible = true;
			motion.mute = false;
			processing = false;
			yield break;
		}
		audioSource.PlayOneShot(startSound);
		effects.SetBool("Fade", true);
		bWatching = false;
		motion.mute = true;
		processing = true;
		yield return (object)new WaitForSeconds(1f);
		FPSController.instance.EnablePlayer();
		effects.SetBool("Fade", false);
		CameraManager.instance.DisableAllCameras();
		cameraManager_UI.SetActive(false);
		yield return (object)new WaitForSeconds(1f);
		if ((Object)(object)RainManager.instance != (Object)null)
		{
			RainManager.instance.currentPlayer = RainManager.instance.defaultPlayer;
		}
		bWatching = false;
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		processing = false;
	}
}
public class CameraManager : MonoBehaviour
{
	public static CameraManager instance;

	public int currentCameraID;

	public CameraComputer cameraComp;

	public AudioClip cameraSelect;

	public AudioSource audioSource;

	public Transform currentCamera;

	private int CameraCount;

	public List<Transform> gameCameras = new List<Transform>();

	public List<Transform> allCameras = new List<Transform>();

	public List<Button> cameraButtons = new List<Button>();

	public int cameraCount
	{
		get
		{
			int num = 0;
			for (int i = 0; i < gameCameras.Count; i++)
			{
				if ((Object)(object)gameCameras[i] != (Object)null)
				{
					num++;
				}
			}
			return num;
		}
	}

	public void Start()
	{
		instance = this;
	}

	private void Update()
	{
	}

	public int GetCameraCount()
	{
		return gameCameras.Count;
	}

	public Transform GetCurrentCamera()
	{
		return gameCameras[currentCameraID];
	}

	public void AddCamera(Transform camera)
	{
		gameCameras.Add(camera);
		RefreshButtons();
	}

	public int FindCameraID(Transform camera)
	{
		int result = 0;
		for (int i = 0; i < gameCameras.Count; i++)
		{
			if ((Object)(object)gameCameras[i] == (Object)(object)camera)
			{
				result = i;
				break;
			}
		}
		return result;
	}

	public void RemoveCamera(Transform camera)
	{
		gameCameras.Remove(camera);
		RefreshButtons();
		if (cameraComp.bWatching)
		{
			if (gameCameras.Count > 0)
			{
				ActiveCamera(0);
			}
			else
			{
				cameraComp.Exit();
			}
		}
	}

	private void RefreshButtons()
	{
		for (int i = 0; i < cameraButtons.Count; i++)
		{
			if (i < gameCameras.Count)
			{
				((Selectable)cameraButtons[i]).interactable = true;
			}
			else
			{
				((Selectable)cameraButtons[i]).interactable = false;
			}
		}
	}

	public void Event_activeCamera(int camID)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		ColorBlock colors = ((Selectable)cameraButtons[camID]).colors;
		((ColorBlock)(ref colors)).normalColor = Color.red;
		((Selectable)cameraButtons[camID]).colors = colors;
	}

	public void Event_disableCamera(int camID)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		ColorBlock colors = ((Selectable)cameraButtons[camID]).colors;
		((ColorBlock)(ref colors)).normalColor = Color.white;
		((Selectable)cameraButtons[camID]).colors = colors;
	}

	public void DisableAllCameras()
	{
		for (int i = 0; i < gameCameras.Count; i++)
		{
			((Component)gameCameras[i].GetChild(0)).gameObject.SetActive(false);
		}
	}

	public void ActiveCamera(int id)
	{
		for (int i = 0; i < gameCameras.Count; i++)
		{
			if (i == id && (Object)(object)gameCameras[i] != (Object)null)
			{
				((Component)gameCameras[i].GetChild(0)).gameObject.SetActive(true);
				currentCameraID = id;
				currentCamera = gameCameras[i].GetChild(0);
				cameraComp.ResetCameraPosition();
				((Component)gameCameras[i].parent).GetComponent<RecordCamera_obj>().isRecord = true;
				audioSource.PlayOneShot(cameraSelect);
				if ((Object)(object)RainManager.instance != (Object)null)
				{
					RainManager.instance.currentPlayer = currentCamera;
				}
			}
			else if ((Object)(object)gameCameras[i] != (Object)null)
			{
				((Component)gameCameras[i].GetChild(0)).gameObject.SetActive(false);
			}
		}
		Debug.Log((object)id);
	}

	public void Shot()
	{
		NotificationsManager.Instance.PostNotification((Component)(object)this, "Event_PlayerFire");
	}
}
public class CameraMouseLook : MonoBehaviour
{
	public enum RotationAxes
	{
		MouseXAndY,
		MouseX,
		MouseY
	}

	public RotationAxes axes;

	public float sensitivityX = 15f;

	public float sensitivityY = 15f;

	public float minimumX = -360f;

	public float maximumX = 360f;

	public float smoothTime = 5f;

	public float minimumY = -60f;

	public float maximumY = 60f;

	private float rotationY;

	public Transform camera;

	public Transform character;

	public Quaternion m_CharacterTargetRot;

	public Quaternion m_CameraTargetRot;

	public float yRot;

	public float xRot;

	public bool bControl;

	private void Start()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		sensitivityX = PlayerPrefs.GetFloat("Sens:");
		sensitivityY = PlayerPrefs.GetFloat("Sens:");
		m_CharacterTargetRot = character.localRotation;
		m_CameraTargetRot = camera.localRotation;
	}

	private void Update()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		if (bControl)
		{
			yRot = Input.GetAxis("Mouse X") * sensitivityX;
			xRot = Input.GetAxis("Mouse Y") * sensitivityY;
			m_CameraTargetRot = ClampRotationAroundXAxis(m_CameraTargetRot);
			m_CharacterTargetRot *= Quaternion.Euler(0f, yRot, 0f);
			m_CameraTargetRot *= Quaternion.Euler(0f - xRot, 0f, 0f);
			character.localRotation = Quaternion.Slerp(character.localRotation, m_CharacterTargetRot, smoothTime * Time.deltaTime);
			camera.localRotation = Quaternion.Slerp(camera.localRotation, m_CameraTargetRot, smoothTime * Time.deltaTime);
		}
	}

	public void Focus(Transform target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = target.position - ((Component)this).transform.position;
		Quaternion val2 = Quaternion.LookRotation(val);
		((Component)this).transform.rotation = Quaternion.Lerp(((Component)this).transform.rotation, val2, Time.deltaTime * 5f);
	}

	private Quaternion ClampRotationAroundXAxis(Quaternion q)
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		q.x /= q.w;
		q.y /= q.w;
		q.z /= q.w;
		q.w = 1f;
		float num = 114.59156f * Mathf.Atan(q.x);
		num = Mathf.Clamp(num, minimumY, maximumY);
		q.x = Mathf.Tan((float)Math.PI / 360f * num);
		return q;
	}
}
public class Camera_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(2))
		{
			if (WeaponManager.instance.bFullAmmo(2))
			{
				InformationUI.instance.DrawHint("Full count of cameras");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(2, 1);
			InformationUI.instance.PickupInfo("+ Add camera 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(2, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add camera 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class DP : MonoBehaviour
{
	public RectTransform rt;

	public float scale;

	public float scaleZ;

	public Vector3 vec;

	private void Start()
	{
	}

	private void Update()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = ((Component)this).transform.position;
		float x = position.z / -100f;
		float y = position.x / 100f;
		vec.x = x;
		vec.y = y;
		rt.anchoredPosition = Vector2.op_Implicit(vec);
		((Transform)rt).localEulerAngles = new Vector3(0f, 0f, 0f - ((Component)this).transform.eulerAngles.y);
	}
}
public class DamageType : MonoBehaviour
{
	public int effectID;
}
public class DetectionTriggerAnimal : MonoBehaviour
{
	public Animal_AI ai;

	private void OnTriggerEnter(Collider info)
	{
		if (((Component)info).gameObject.tag == "Player")
		{
			ai.SetDangerState();
		}
		if (((Component)info).gameObject.tag == "Rake")
		{
			ai.bEnemyInRadius = true;
			ai.enemy = ((Component)info).gameObject.transform;
		}
	}

	private void OnTriggerStay(Collider info)
	{
		if (((Component)info).gameObject.tag == "Player")
		{
			ai.bEnemyInRadius = true;
		}
	}

	private void OnTriggerExit(Collider info)
	{
		if (((Component)info).gameObject.tag == "Player")
		{
			ai.bEnemyInRadius = false;
		}
	}
}
public class DetectionTriggerRake : MonoBehaviour
{
	public Enemy_AI ai;

	private void OnTriggerEnter(Collider info)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<RecordCamera_obj>()) && ((Component)info).gameObject.GetComponent<RecordCamera_obj>().cameraState == RecordCamera_obj.Camera_state.Setup && CameraComputer.instance.bWatching)
		{
			Vector3 val = ((Component)info).gameObject.transform.TransformPoint(Vector3.forward * 2f);
			NavMeshHit val2 = default(NavMeshHit);
			NavMesh.SamplePosition(val, ref val2, 5f, 1);
			ai.currentBreakPoint = ((NavMeshHit)(ref val2)).position;
			ai.viewRot = ((Component)info).gameObject.transform.position;
			ai.cameraBreak = ((Component)info).gameObject.transform;
			ai.SetBreakState();
		}
		if (Object.op_Implicit((Object)(object)((Component)info).gameObject.GetComponent<Animal_AI>()) && !ai.bHaunt && !ai.bPlayerHount && ai.bHungry)
		{
			ai.target = ((Component)info).gameObject.transform;
			ai.bHaunt = true;
		}
		if (((Component)info).gameObject.tag == "Player")
		{
			ai.SetPlayerHountState();
			ai.playerTarget = ((Component)info).gameObject.transform;
		}
	}
}
public class DrawRakeHealth : MonoBehaviour
{
	public Enemy_AI enemy;

	public GameObject panel;

	public GameObject bg;

	public Text text;

	private void Start()
	{
		NotificationsManager.Instance.AddListener((Component)(object)this, "OnDamageTaken");
		enemy = GameObject.FindGameObjectWithTag("Rake").GetComponent<Enemy_AI>();
	}

	private void Update()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		bg.GetComponent<RectTransform>().anchoredPosition = new Vector2(panel.GetComponent<RectTransform>().anchoredPosition.x - 2f, panel.GetComponent<RectTransform>().anchoredPosition.y);
		bg.GetComponent<RectTransform>().sizeDelta = new Vector2(252f, 14f);
		panel.GetComponent<RectTransform>().sizeDelta = new Vector2(enemy.Health / 10f, 10f);
	}

	public void OnDamageTaken()
	{
		((MonoBehaviour)this).StartCoroutine(hide());
	}

	private IEnumerator hide()
	{
		bg.SetActive(true);
		panel.SetActive(true);
		yield return (object)new WaitForSeconds(5f);
		bg.SetActive(false);
		panel.SetActive(false);
	}
}
public class DynamicObject : MonoBehaviour
{
	public string name = "Name";

	public string help = "Help";

	public virtual void Action()
	{
	}
}
public class Enemy_AI : Unit_obj
{
	public Animator animator;

	public NavMeshAgent nav;

	public List<Transform> patrolPoints = new List<Transform>();

	public List<Transform> escapePoints = new List<Transform>();

	public Vector3 currentPatrolPoint;

	public Vector3 currentHountPoint;

	public Vector3 currentEscapePoint;

	public Vector3 currentPartialPoint;

	public Vector3 currentBreakPoint;

	public Vector3 viewRot;

	public Transform cameraBreak;

	public int currentStage;

	public AudioClip walkSound;

	public AudioClip[] playerAttackRoars;

	public AudioClip[] playerAttackKicks;

	public AudioClip[] painRoar;

	public AudioClip cameraAttackRoar;

	public AudioClip[] idleSounds;

	public bool StepDown;

	public AudioSource audioSource;

	public bool stateSound;

	public bool bDanger;

	public bool bHaunt;

	public bool bVisible;

	public bool bPlayerHount;

	public bool bKillingProccess;

	public bool bPlayerKilling;

	public bool bCameraBreak;

	public bool bHungry;

	public int hungryCounter;

	public float patrolWaitTime;

	public float hauntWaitTime;

	public float dangerWaitTime;

	public float breakWaitTime;

	public float playerHountWaitTime;

	private float stopDistance = 0.5f;

	public Transform target;

	public Transform playerTarget;

	public float walkSpeed;

	public float runSpeed;

	public float hauntSpeed;

	public Transform focusPoint;

	public float Health;

	public int PlayerAttackType;

	public GameObject deadPrefab;

	private void Start()
	{
		Health = 2500f;
		NotificationsManager.Instance.AddListener((Component)(object)this, "Event_PlayerFire");
	}

	public void Event_PlayerFire(Component sender)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (!bDanger)
		{
			Debug.Log((object)"PlayerFire");
			float num = Vector3.Distance(((Component)this).transform.position, sender.GetComponent<Transform>().position);
			if (num < 300f)
			{
				SetDangerState();
			}
		}
	}

	public Vector3 FindClosetPoint()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		int index = 0;
		float num = Vector3.Distance(((Component)this).transform.position, patrolPoints[0].position);
		for (int i = 0; i < patrolPoints.Count; i++)
		{
			if (Vector3.Distance(((Component)this).transform.position, patrolPoints[i].position) < num)
			{
				num = Vector3.Distance(((Component)this).transform.position, patrolPoints[i].position);
				index = i;
			}
		}
		return patrolPoints[index].position;
	}

	public Vector3 BuildEscapePath()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)"RebuildEsacpePath");
		float num = 300f;
		Vector3 val = Random.insideUnitSphere * num;
		val += ((Component)this).transform.position;
		NavMeshHit val2 = default(NavMeshHit);
		NavMesh.SamplePosition(val, ref val2, num * 2f, 1 << NavMesh.GetNavMeshLayerFromName("Default"));
		return ((NavMeshHit)(ref val2)).position;
	}

	public override void TakeDamage(int amount, string damageType)
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		Health -= amount;
		NotificationsManager.Instance.PostNotification((Component)(object)this, "OnDamageTaken");
		if (damageType == "SniperShot")
		{
			audioSource.PlayOneShot(painRoar[Random.Range(0, painRoar.Length)]);
			SetDangerState();
		}
		if (damageType == "TrapDamage")
		{
			SetDangerState();
		}
		if (Health <= 0f)
		{
			Object.Instantiate((Object)(object)deadPrefab, ((Component)this).transform.position, ((Component)this).transform.rotation);
			FPSController.instance.effectAnimator.SetBool("Fade", true);
			InformationUI.instance.DrawObjective("You win!");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}

	public void OnDamageTaken()
	{
	}

	private void Update()
	{
		if ((Object)(object)target != (Object)null)
		{
			if (!Object.op_Implicit((Object)(object)((Component)target).GetComponent<Animal_AI>()))
			{
				bHaunt = false;
			}
		}
		else
		{
			bHaunt = false;
		}
		if (!bDanger)
		{
			if (!bHaunt && !bPlayerHount)
			{
				if (bCameraBreak)
				{
					CameraBreak();
				}
				else
				{
					Patrol();
				}
			}
			if (bPlayerHount)
			{
				if (!((Component)playerTarget).GetComponent<FPSController>().bHide)
				{
					PlayerHount();
				}
				else
				{
					bPlayerHount = false;
				}
			}
			if (bHaunt && !bPlayerHount && bHungry)
			{
				Haunting();
			}
		}
		else if (!bPlayerKilling)
		{
			Escape();
		}
	}

	public void SetDangerState()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (!bPlayerKilling)
		{
			nav.Resume();
			stateSound = false;
			currentEscapePoint = BuildEscapePath();
			bDanger = true;
			animator.SetInteger("WalkType", 1);
			animator.SetFloat("Speed", 2f);
		}
	}

	public void RefreshStats()
	{
		if (hungryCounter >= 100)
		{
			hungryCounter = 0;
			bHungry = true;
		}
	}

	public void SetPlayerHountState()
	{
		nav.Resume();
		stateSound = false;
		animator.SetInteger("WalkType", 1);
		bPlayerHount = true;
		bHaunt = false;
	}

	public void SetPatrolState()
	{
		stateSound = false;
		animator.SetBool("bCrouch", false);
		nav.Resume();
		animator.SetInteger("WalkType", 1);
		target = null;
	}

	public void SetBreakState()
	{
		stateSound = false;
		nav.Resume();
		bCameraBreak = true;
	}

	private void PlayerHount()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0993: Unknown result type (might be due to invalid IL or missing references)
		//IL_0998: Unknown result type (might be due to invalid IL or missing references)
		//IL_099e: Unknown result type (might be due to invalid IL or missing references)
		//IL_09a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a74: Unknown result type (might be due to invalid IL or missing references)
		//IL_070e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0713: Unknown result type (might be due to invalid IL or missing references)
		//IL_0719: Unknown result type (might be due to invalid IL or missing references)
		//IL_0724: Unknown result type (might be due to invalid IL or missing references)
		//IL_092d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0796: Unknown result type (might be due to invalid IL or missing references)
		//IL_079b: Unknown result type (might be due to invalid IL or missing references)
		//IL_07a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_07aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_07be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		//IL_048a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0490: Unknown result type (might be due to invalid IL or missing references)
		//IL_049b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0414: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_050d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0512: Unknown result type (might be due to invalid IL or missing references)
		//IL_051c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0521: Unknown result type (might be due to invalid IL or missing references)
		//IL_0535: Unknown result type (might be due to invalid IL or missing references)
		if (currentStage == 0)
		{
			currentHountPoint = playerTarget.position;
			if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 7f)
			{
				animator.SetFloat("Speed", 0f);
				animator.SetBool("bAttack", false);
				nav.Stop();
				playerHountWaitTime += Time.deltaTime;
				if (playerHountWaitTime > 10f)
				{
					playerHountWaitTime = 0f;
					bPlayerHount = false;
					nav.Resume();
				}
				if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 3f)
				{
					nav.Resume();
					bPlayerHount = false;
					bDanger = true;
					playerHountWaitTime = 0f;
				}
			}
			else
			{
				nav.Resume();
				nav.SetDestination(currentHountPoint);
				animator.SetBool("bAttack", false);
				nav.speed = walkSpeed;
				animator.SetFloat("Speed", 1f);
			}
		}
		if (currentStage == 1)
		{
			Vector3 val = ((Component)this).transform.position - ((Component)playerTarget).transform.position;
			Vector3 forward = playerTarget.forward;
			float num = Vector3.Angle(val, forward);
			if (num < 60f)
			{
				bVisible = true;
			}
			else if (!bVisible)
			{
				bVisible = false;
			}
			if (bVisible)
			{
				if (PlayerAttackType == 0)
				{
					int[] array = new int[2] { 1, 2 };
					PlayerAttackType = array[Random.Range(0, array.Length)];
				}
				if (PlayerAttackType == 1)
				{
					currentHountPoint = playerTarget.position;
					if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 5f)
					{
						nav.Stop();
						animator.SetFloat("Speed", 0f);
						bPlayerKilling = true;
						((Component)playerTarget).GetComponent<PlayerMovement>().FreezePlayer();
						((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = false;
						((Component)playerTarget).GetComponent<FPSController>().HidePlayerWeapon();
						Vector3 position = playerTarget.position;
						position.y = ((Component)this).transform.position.y;
						((Component)this).transform.LookAt(position);
						((Component)playerTarget).GetComponent<CameraMouseLook>().Focus(((Component)focusPoint).transform);
						animator.SetBool("bCrouch", false);
						if (!stateSound)
						{
							stateSound = true;
							audioSource.PlayOneShot(playerAttackRoars[Random.Range(0, playerAttackRoars.Length)]);
						}
						animator.SetBool("bPlayerAttack", true);
						playerHountWaitTime += Time.deltaTime;
						if (playerHountWaitTime > 1.2f)
						{
							audioSource.PlayOneShot(playerAttackKicks[Random.Range(0, playerAttackKicks.Length)]);
							bPlayerKilling = false;
							((Component)playerTarget).GetComponent<FPSController>().TakeDamage(10, "RakeKick");
							((Component)playerTarget).GetComponent<PlayerMovement>().UnFreezePlayer();
							((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = true;
							stateSound = false;
							SetDangerState();
							nav.Resume();
							bDanger = true;
							playerHountWaitTime = 0f;
							bPlayerHount = false;
							bVisible = false;
							animator.SetBool("bAttack", false);
							animator.SetBool("bPlayerAttack", false);
						}
					}
					else
					{
						nav.Resume();
						nav.SetDestination(currentHountPoint);
						animator.SetBool("bCrouch", false);
						animator.SetBool("bAttack", false);
						nav.speed = runSpeed;
						PlayFootstep(0.2f);
						animator.SetFloat("Speed", 2f);
					}
				}
				if (PlayerAttackType == 2)
				{
					currentHountPoint = playerTarget.position;
					if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 5f)
					{
						nav.Stop();
						animator.SetFloat("Speed", 0f);
						bPlayerKilling = true;
						((Component)playerTarget).GetComponent<PlayerMovement>().FreezePlayer();
						((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = false;
						((Component)playerTarget).GetComponent<FPSController>().HidePlayerWeapon();
						Vector3 position2 = playerTarget.position;
						position2.y = ((Component)this).transform.position.y;
						((Component)this).transform.LookAt(position2);
						((Component)playerTarget).GetComponent<CameraMouseLook>().Focus(((Component)focusPoint).transform);
						animator.SetBool("bCrouch", false);
						if (!stateSound)
						{
							stateSound = true;
							audioSource.PlayOneShot(playerAttackRoars[Random.Range(0, playerAttackRoars.Length)]);
						}
						animator.SetBool("bPlayerAppercut", true);
						playerHountWaitTime += Time.deltaTime;
						if (playerHountWaitTime > 1.2f)
						{
							audioSource.PlayOneShot(playerAttackKicks[Random.Range(0, playerAttackKicks.Length)]);
							bPlayerKilling = false;
							((Component)playerTarget).GetComponent<FPSController>().TakeDamage(10, "RakeUppercut");
							((Component)playerTarget).GetComponent<PlayerMovement>().UnFreezePlayer();
							((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = true;
							stateSound = false;
							PlayerAttackType = 0;
							SetDangerState();
							nav.Resume();
							bDanger = true;
							playerHountWaitTime = 0f;
							bPlayerHount = false;
							bVisible = false;
							animator.SetBool("bAttack", false);
							animator.SetBool("bPlayerAppercut", false);
						}
					}
					else
					{
						nav.Resume();
						nav.SetDestination(currentHountPoint);
						animator.SetBool("bCrouch", false);
						animator.SetBool("bAttack", false);
						nav.speed = runSpeed;
						PlayFootstep(0.2f);
						animator.SetFloat("Speed", 2f);
					}
				}
			}
			else
			{
				currentHountPoint = playerTarget.position;
				if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 3f)
				{
					nav.Stop();
					animator.SetFloat("Speed", 0f);
					bPlayerKilling = true;
					((Component)playerTarget).GetComponent<PlayerMovement>().FreezePlayer();
					((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = false;
					((Component)playerTarget).GetComponent<FPSController>().HidePlayerWeapon();
					Vector3 position3 = playerTarget.position;
					position3.y = ((Component)this).transform.position.y;
					((Component)this).transform.LookAt(position3);
					((Component)playerTarget).GetComponent<CameraMouseLook>().Focus(((Component)focusPoint).transform);
					animator.SetBool("bCrouch", false);
					if (!stateSound)
					{
						stateSound = true;
						audioSource.PlayOneShot(playerAttackRoars[Random.Range(0, playerAttackRoars.Length)]);
					}
					animator.SetBool("bPlayerAttack", true);
					playerHountWaitTime += Time.deltaTime;
					if (playerHountWaitTime > 1.2f)
					{
						audioSource.PlayOneShot(playerAttackKicks[Random.Range(0, playerAttackKicks.Length)]);
						bPlayerKilling = false;
						((Component)playerTarget).GetComponent<FPSController>().TakeDamage(10, "RakeUppercut");
						((Component)playerTarget).GetComponent<PlayerMovement>().UnFreezePlayer();
						((Component)playerTarget).GetComponent<CameraMouseLook>().bControl = true;
						stateSound = false;
						PlayerAttackType = 0;
						SetDangerState();
						nav.Resume();
						bDanger = true;
						playerHountWaitTime = 0f;
						bPlayerHount = false;
						bVisible = false;
						animator.SetBool("bAttack", false);
						animator.SetBool("bPlayerAttack", false);
					}
				}
				else
				{
					nav.Resume();
					nav.SetDestination(currentHountPoint);
					animator.SetBool("bAttack", false);
					nav.speed = hauntSpeed;
					animator.SetBool("bCrouch", true);
					animator.SetFloat("Speed", 0f);
				}
			}
		}
		if (currentStage != 2)
		{
			return;
		}
		currentHountPoint = playerTarget.position;
		if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 3f)
		{
			nav.Stop();
			animator.SetFloat("Speed", 0f);
			animator.SetBool("bCrouch", false);
			animator.SetBool("bAttack", true);
			playerHountWaitTime += Time.deltaTime;
			if (playerHountWaitTime > 5f)
			{
				nav.Resume();
				bDanger = true;
				bVisible = false;
				playerHountWaitTime = 0f;
				bPlayerHount = false;
				animator.SetBool("bAttack", false);
			}
		}
		else
		{
			nav.Resume();
			nav.SetDestination(currentHountPoint);
			animator.SetBool("bCrouch", false);
			animator.SetBool("bAttack", false);
			nav.speed = runSpeed;
			animator.SetFloat("Speed", 2f);
		}
	}

	private void Escape()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Invalid comparison between Unknown and I4
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Invalid comparison between Unknown and I4
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(currentEscapePoint, ((Component)this).transform.position) < stopDistance)
		{
			animator.SetFloat("Speed", 0f);
			dangerWaitTime += Time.deltaTime;
			if (dangerWaitTime > 2f)
			{
				dangerWaitTime = 0f;
				bPlayerHount = false;
				bVisible = false;
				bDanger = false;
				animator.SetFloat("Speed", 1f);
			}
			return;
		}
		if ((int)nav.path.status == 1 || (int)nav.path.status == 2)
		{
			animator.SetFloat("Speed", 0f);
			currentEscapePoint = BuildEscapePath();
			nav.Stop();
		}
		if (!nav.pathPending)
		{
			nav.Resume();
			nav.SetDestination(currentEscapePoint);
			PlayFootstep(0.2f);
			nav.speed = runSpeed;
			animator.SetFloat("Speed", 2f);
		}
		else
		{
			nav.Stop();
			animator.SetFloat("Speed", 0f);
		}
	}

	private void Patrol()
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Invalid comparison between Unknown and I4
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Invalid comparison between Unknown and I4
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)playerTarget != (Object)null && Vector3.Distance(playerTarget.position, ((Component)this).transform.position) < 60f)
		{
			SetPlayerHountState();
		}
		if (currentPatrolPoint == Vector3.zero)
		{
			nav.Resume();
			currentPatrolPoint = patrolPoints[Random.Range(0, patrolPoints.Count)].position;
		}
		if (Vector3.Distance(currentPatrolPoint, ((Component)this).transform.position) < stopDistance)
		{
			animator.SetFloat("Speed", 0f);
			patrolWaitTime += Time.deltaTime;
			if (!stateSound)
			{
				audioSource.PlayOneShot(idleSounds[Random.Range(0, idleSounds.Length)]);
				stateSound = true;
			}
			if (patrolWaitTime > 3f)
			{
				currentPatrolPoint = patrolPoints[Random.Range(0, patrolPoints.Count)].position;
				hungryCounter += 50;
				RefreshStats();
				if (nav.pathPending)
				{
					patrolWaitTime = 0f;
					stateSound = false;
				}
			}
		}
		else
		{
			if ((int)nav.path.status == 1 || (int)nav.path.status == 2)
			{
				animator.SetFloat("Speed", 0f);
				currentPatrolPoint = FindClosetPoint();
			}
			animator.SetBool("bAttack", false);
			nav.speed = walkSpeed;
			if (!nav.pathPending)
			{
				PlayFootstep(0.7f);
				nav.SetDestination(currentPatrolPoint);
				animator.SetFloat("Speed", 1f);
			}
			else
			{
				animator.SetFloat("Speed", 0f);
			}
		}
	}

	private void Haunting()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		currentHountPoint = target.position;
		if (Vector3.Distance(currentHountPoint, ((Component)this).transform.position) < 3f)
		{
			if (((Component)target).GetComponent<Animal_AI>().isAlive && !((Component)target).GetComponent<Animal_AI>().bTraped)
			{
				if (!bKillingProccess)
				{
					AnimalKilling();
				}
			}
			else
			{
				animator.SetBool("bAnimalKilling", false);
				bHaunt = false;
			}
		}
		else if (((Component)target).GetComponent<Animal_AI>().bDanger)
		{
			animator.SetBool("bAttack", false);
			nav.speed = runSpeed;
			animator.SetBool("bCrouch", false);
			animator.SetInteger("WalkType", 1);
			animator.SetFloat("Speed", 2f);
			nav.SetDestination(currentHountPoint);
		}
		else
		{
			animator.SetInteger("WalkType", 0);
			animator.SetBool("bAttack", false);
			nav.speed = hauntSpeed;
			animator.SetBool("bCrouch", true);
			animator.SetFloat("Speed", 0f);
			nav.SetDestination(currentHountPoint);
		}
	}

	public void CameraBreak()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		if (Vector3.Distance(currentBreakPoint, ((Component)this).transform.position) < 3f)
		{
			animator.SetBool("bCameraBreak", true);
			animator.SetFloat("Speed", 0f);
			breakWaitTime += Time.deltaTime;
			if (!stateSound)
			{
				stateSound = true;
				audioSource.PlayOneShot(cameraAttackRoar);
			}
			Vector3 val = viewRot - ((Component)this).transform.position;
			Quaternion val2 = Quaternion.LookRotation(viewRot);
			viewRot.y = ((Component)this).transform.position.y;
			((Component)this).transform.LookAt(viewRot);
			if (breakWaitTime > 1.9f)
			{
				breakWaitTime = 0f;
				animator.SetBool("bCameraBreak", false);
				if ((Object)(object)cameraBreak != (Object)null)
				{
					((Component)cameraBreak).GetComponent<RecordCamera_obj>().SetUnsetupState();
					bCameraBreak = false;
				}
			}
		}
		else
		{
			PlayFootstep(0.2f);
			animator.SetBool("bCameraBreak", false);
			nav.speed = runSpeed;
			animator.SetBool("bCrouch", false);
			animator.SetInteger("WalkType", 1);
			animator.SetFloat("Speed", 2f);
			nav.SetDestination(currentBreakPoint);
		}
	}

	private void PlayFootstep(float time)
	{
		if (!StepDown)
		{
			((MonoBehaviour)this).StartCoroutine(FootStep(time));
		}
	}

	private IEnumerator FootStep(float stepDelay)
	{
		StepDown = true;
		audioSource.PlayOneShot(walkSound);
		yield return (object)new WaitForSeconds(stepDelay);
		StepDown = false;
	}

	public void AnimalKilling()
	{
		nav.Stop();
		bKillingProccess = true;
		animator.SetFloat("Speed", 0f);
		animator.SetBool("bCrouch", false);
		animator.SetBool("bAnimalKilling", true);
		((Component)target).GetComponent<Animal_AI>().bKillingProccess = true;
		((MonoBehaviour)this).StartCoroutine(kill());
	}

	private IEnumerator kill()
	{
		yield return (object)new WaitForSeconds(2f);
		if ((Object)(object)target != (Object)null)
		{
			((Component)target).GetComponent<Animal_AI>().TakeDamage(250, "RakeAnimalKick");
		}
		bHungry = false;
		animator.SetBool("bAnimalKilling", false);
		bKillingProccess = false;
		animator.SetBool("bCrouch", false);
		SetPatrolState();
		currentPatrolPoint = Vector3.zero;
	}
}
public class FPSCamera : MonoBehaviour
{
	public Animator anim;

	private void Update()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		Ray val = default(Ray);
		((Ray)(ref val))..ctor(((Component)this).transform.position, ((Component)this).transform.forward);
		RaycastHit val2 = default(RaycastHit);
		if (Physics.Raycast(val, ref val2, 4f))
		{
			Debug.DrawLine(((Component)this).transform.position, ((RaycastHit)(ref val2)).point);
			if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Item>()))
			{
				Item component = ((Component)((RaycastHit)(ref val2)).collider).GetComponent<Item>();
				InformationUI.instance.DrawItemInfo("Press E to pickup " + component.name, showType: true);
				if (Input.GetButtonDown("Use"))
				{
					component.PickupItem();
				}
			}
			else
			{
				InformationUI.instance.DrawItemInfo(string.Empty, showType: false);
			}
			if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<DynamicObject>()))
			{
				DynamicObject component2 = ((Component)((RaycastHit)(ref val2)).collider).GetComponent<DynamicObject>();
				InformationUI.instance.DrawItemInfo(component2.name, showType: true);
				if (Input.GetButtonDown("Use"))
				{
					component2.Action();
				}
			}
		}
		else
		{
			InformationUI.instance.DrawItemInfo(string.Empty, showType: false);
		}
	}
}
public class FoodPoint : MonoBehaviour
{
	public int count;

	public bool isUse;

	public void Eat()
	{
		if (count == -1)
		{
			return;
		}
		count--;
		if (count <= 0)
		{
			AnimalsManager.instance.RemoveFoodPoint(((Component)this).transform);
			if ((Object)(object)((Component)this).transform.parent != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)((Component)this).transform.parent).gameObject);
			}
			else
			{
				Object.Destroy((Object)(object)((Component)this).gameObject);
			}
		}
	}
}
public class Food_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(5))
		{
			if (WeaponManager.instance.bFullAmmo(5))
			{
				InformationUI.instance.DrawHint("Full count of feed");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(5, 1);
			InformationUI.instance.PickupInfo("+ Add packet of feed 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(5, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add packet of feed 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class GamePauseMenu : MonoBehaviour
{
	public bool isPaused;

	public GameObject menu;

	private void Update()
	{
		if (Input.GetButtonDown("Escape"))
		{
			isPaused = !isPaused;
			if (isPaused)
			{
				menu.SetActive(true);
				FPSController.instance.SetPlayerDisableState("PAUSEMENU");
				Time.timeScale = 0f;
				Cursor.lockState = (CursorLockMode)0;
				Cursor.visible = true;
			}
			else
			{
				menu.SetActive(false);
				FPSController.instance.SetPlayerDisableState("BEFOREPAUSE");
				Time.timeScale = 1f;
				Cursor.lockState = (CursorLockMode)1;
				Cursor.visible = false;
			}
		}
	}

	public void Continue()
	{
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		menu.SetActive(false);
		FPSController.instance.SetPlayerDisableState("BEFOREPAUSE");
		Time.timeScale = 1f;
	}

	public void Exit()
	{
		Application.LoadLevel(0);
	}
}
public class GameTimeManager : MonoBehaviour
{
	public int day;

	public int month;

	public int year;

	public float second;

	public int fSecond;

	public float minute;

	public float hour;

	public static GameTimeManager instance;

	private void Start()
	{
		instance = this;
	}

	private void Update()
	{
		second += Time.deltaTime;
		fSecond = (int)second;
		if (second >= 60f)
		{
			second = 0f;
			minute += 1f;
			if (minute >= 60f)
			{
				minute = 0f;
				hour += 1f;
			}
		}
	}
}
public class GameTimer : MonoBehaviour
{
	public Text text;

	public float time;

	private float timeCounter;

	public int levelToLoad;

	private bool isTimeOut;

	public float second;

	public float fSecond;

	public int minute;

	public Animator effects;

	private void Start()
	{
		timeCounter = time;
	}

	private void Update()
	{
		text.text = minute.ToString();
		second -= Time.deltaTime;
		fSecond = (int)second;
		if (!(fSecond <= 0f))
		{
			return;
		}
		if (minute == 0)
		{
			if (!isTimeOut)
			{
				isTimeOut = true;
				second = 0f;
				minute = 0;
				InformationUI.instance.DrawObjective("Objective: Please return to trailer");
				((MonoBehaviour)this).StartCoroutine(loadLevel());
			}
		}
		else
		{
			second = 60f;
			minute--;
		}
	}

	private IEnumerator loadLevel()
	{
		SaveGameSystem.instance.SaveGame();
		yield return (object)new WaitForSeconds(4f);
		effects.SetBool("Fade", true);
		yield return (object)new WaitForSeconds(2f);
		Application.LoadLevel(levelToLoad);
	}
}
public class InformationUI : MonoBehaviour
{
	public static InformationUI instance;

	public Animator itemInfo_anim;

	public Text text;

	public Text hintText;

	public Text objectiveText;

	public GameObject pickupInfo;

	public GameObject hintObj;

	public Transform LogLayout;

	public Transform HintParent;

	private bool bShowHint;

	private bool bShowObjective;

	private void Start()
	{
		instance = this;
	}

	public void DrawObjective(string name)
	{
		if (!bShowObjective)
		{
			objectiveText.text = ((Object)text).ToString();
			((MonoBehaviour)this).StartCoroutine(objective(name));
		}
	}

	public void PickupInfo(string name)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		Object obj = Object.Instantiate((Object)(object)pickupInfo, pickupInfo.transform.position, pickupInfo.transform.rotation);
		GameObject val = (GameObject)(object)((obj is GameObject) ? obj : null);
		val.transform.SetParent(((Component)LogLayout).transform);
		val.GetComponent<Text>().text = name;
	}

	public void DrawItemInfo(string name, bool showType)
	{
		if (showType)
		{
			itemInfo_anim.SetBool("Show", showType);
			text.text = name;
		}
		else
		{
			itemInfo_anim.SetBool("Show", showType);
			text.text = string.Empty;
		}
	}

	public void DrawHint(string text)
	{
		if (!bShowHint)
		{
			hintText.text = text;
			((MonoBehaviour)this).StartCoroutine(hint(text));
		}
	}

	private IEnumerator objective(string text)
	{
		bShowObjective = true;
		objectiveText.text = text;
		yield return (object)new WaitForSeconds(5f);
		objectiveText.text = string.Empty;
		bShowObjective = false;
	}

	private IEnumerator hint(string text)
	{
		bShowHint = true;
		hintText.text = text;
		yield return (object)new WaitForSeconds(3f);
		hintText.text = string.Empty;
		bShowHint = false;
	}
}
public class Inventory : MonoBehaviour
{
	public List<GameObject> items = new List<GameObject>();

	public List<Transform> playerItems = new List<Transform>();

	public RectTransform pos;

	public GameObject drawer;

	public Text text;

	public bool inventoryState;

	public GameObject inventoryObj;

	public AudioClip openSound;

	public AudioClip closeSound;

	public AudioSource audioSource;

	private void RefreshItems()
	{
		playerItems = WeaponManager.instance.GetPlayerWeapons();
		foreach (GameObject item in items)
		{
			item.gameObject.SetActive(false);
		}
		for (int i = 0; i < playerItems.Count; i++)
		{
			int weaponID = ((Component)playerItems[i]).GetComponent<Weapon>().weaponID;
			items[weaponID].SetActive(true);
			items[weaponID].GetComponent<Inventory_item>().count = ((Component)playerItems[i]).GetComponent<Weapon>().ammo;
		}
	}

	public void OpenInventory()
	{
		audioSource.PlayOneShot(openSound);
		drawer.SetActive(true);
		inventoryObj.SetActive(true);
		inventoryState = true;
		RefreshItems();
	}

	public void CloseInventory()
	{
		audioSource.PlayOneShot(closeSound);
		inventoryObj.SetActive(false);
		drawer.SetActive(false);
		inventoryState = false;
	}

	private void Update()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		if (!inventoryState)
		{
			return;
		}
		pos.anchoredPosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y - pos.sizeDelta.y);
		Ray val = Camera.main.ScreenPointToRay(Input.mousePosition);
		RaycastHit val2 = default(RaycastHit);
		if (Physics.Raycast(val, ref val2, 100f))
		{
			if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Inventory_item>()))
			{
				string text = ((Component)((RaycastHit)(ref val2)).collider).GetComponent<Inventory_item>().itemType.ToString();
				drawer.SetActive(true);
				this.text.text = "Name: " + ((Component)((RaycastHit)(ref val2)).collider).GetComponent<Inventory_item>().name + "\r\n" + text + " " + ((Component)((RaycastHit)(ref val2)).collider).GetComponent<Inventory_item>().count;
				if (Input.GetButtonDown("Fire1"))
				{
					WeaponManager.instance.SelectWeaponID(((Component)((RaycastHit)(ref val2)).collider).GetComponent<Inventory_item>().ID);
					FPSController.instance.Event_playerWeaponSelect();
				}
			}
			else
			{
				drawer.SetActive(false);
			}
		}
		else
		{
			drawer.SetActive(false);
		}
	}
}
public class Inventory_item : MonoBehaviour
{
	public enum ItemType
	{
		Count,
		Ammo
	}

	public string name;

	public int ID;

	public int count;

	public ItemType itemType;
}
public class Item : MonoBehaviour
{
	public string name;

	public int count;

	public int id;

	public AudioClip pickupSound;

	public virtual void PickupItem()
	{
	}
}
public class LevelItemsManager : MonoBehaviour
{
	public List<GameObject> trapObjects = new List<GameObject>();

	public List<GameObject> items = new List<GameObject>();

	public List<GameObject> screemers = new List<GameObject>();

	public List<GameObject> soundTriggers = new List<GameObject>();

	public List<GameObject> windowsObj = new List<GameObject>();

	public static LevelItemsManager instance;

	private void Start()
	{
		instance = this;
	}

	public void Update()
	{
	}

	public void AddTrap(GameObject obj)
	{
		trapObjects.Add(obj);
	}

	public void RefreshTrapList()
	{
		for (int num = trapObjects.Count - 1; num >= 0; num--)
		{
			if ((Object)(object)trapObjects[num] == (Object)null)
			{
				trapObjects.RemoveAt(num);
			}
		}
	}

	public void RefreshSoundTriggersList()
	{
		for (int num = soundTriggers.Count - 1; num >= 0; num--)
		{
			if ((Object)(object)soundTriggers[num] == (Object)null || !soundTriggers[num].activeSelf)
			{
				soundTriggers.RemoveAt(num);
			}
		}
	}

	public void RefreshScreemersList()
	{
		for (int num = screemers.Count - 1; num >= 0; num--)
		{
			if ((Object)(object)screemers[num] == (Object)null || !screemers[num].activeSelf)
			{
				screemers.RemoveAt(num);
			}
		}
	}

	public void RefrashItemList()
	{
		for (int num = items.Count - 1; num >= 0; num--)
		{
			if ((Object)(object)items[num] == (Object)null || !items[num].activeSelf)
			{
				MonoBehaviour.print((object)("disable " + num));
				items.RemoveAt(num);
			}
		}
	}
}
public class LevelLoader : MonoBehaviour
{
	public static LevelLoader instance;

	private void Start()
	{
		instance = this;
	}

	public void LoadLevel(int id)
	{
		SaveGameSystem.instance.SaveGame();
	}
}
public class Medkit_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(8))
		{
			if (WeaponManager.instance.bFullAmmo(8))
			{
				InformationUI.instance.DrawHint("Full count of med kits");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(8, 1);
			InformationUI.instance.PickupInfo("+ Add medkit 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(8, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add medkit 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class Notebook_obj : DynamicObject
{
	public GameObject gui;

	public List<Sprite> notes = new List<Sprite>();

	public int currentNoteID;

	public Image currentNote;

	public void NextNote()
	{
		currentNoteID++;
		currentNoteID = Mathf.Clamp(currentNoteID, 0, notes.Count - 1);
		currentNote.sprite = notes[currentNoteID];
	}

	public void PrevNote()
	{
		currentNoteID--;
		currentNoteID = Mathf.Clamp(currentNoteID, 0, notes.Count - 1);
		currentNote.sprite = notes[currentNoteID];
	}

	public void Exit()
	{
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		gui.SetActive(false);
		FPSController.instance.SetPlayerDisableState("FULL");
	}

	public override void Action()
	{
		gui.SetActive(true);
		Cursor.lockState = (CursorLockMode)0;
		Cursor.visible = true;
		WeaponManager.instance.HideCurrentWeapon();
		FPSController.instance.SetPlayerDisableState("SCREEMER");
	}
}
public class NotificationsManager : MonoBehaviour
{
	private Dictionary<string, List<Component>> Listeners = new Dictionary<string, List<Component>>();

	private static NotificationsManager instance;

	public static NotificationsManager Instance
	{
		get
		{
			if ((Object)(object)instance == (Object)null)
			{
				instance = new NotificationsManager();
			}
			return instance;
		}
	}

	public void AddListener(Component Sender, string NotificationName)
	{
		if (!Listeners.ContainsKey(NotificationName))
		{
			Listeners.Add(NotificationName, new List<Component>());
		}
		Listeners[NotificationName].Add(Sender);
	}

	public void RemoveListener(Component Sender, string NotificationName)
	{
		if (!Listeners.ContainsKey(NotificationName))
		{
			return;
		}
		for (int num = Listeners[NotificationName].Count - 1; num >= 0; num--)
		{
			if (((Object)Listeners[NotificationName][num]).GetInstanceID() == ((Object)Sender).GetInstanceID())
			{
				Listeners[NotificationName].RemoveAt(num);
			}
		}
	}

	public void PostNotification(Component Sender, string NotificationName)
	{
		if (!Listeners.ContainsKey(NotificationName))
		{
			return;
		}
		foreach (Component item in Listeners[NotificationName])
		{
			item.SendMessage(NotificationName, (object)Sender, (SendMessageOptions)1);
		}
	}

	public void ClearListeners()
	{
		Listeners.Clear();
	}

	public void RemoveRedundancies()
	{
		Dictionary<string, List<Component>> dictionary = new Dictionary<string, List<Component>>();
		foreach (KeyValuePair<string, List<Component>> listener in Listeners)
		{
			for (int num = listener.Value.Count - 1; num >= 0; num--)
			{
				if ((Object)(object)listener.Value[num] == (Object)null)
				{
					listener.Value.RemoveAt(num);
				}
			}
			if (listener.Value.Count > 0)
			{
				dictionary.Add(listener.Key, listener.Value);
			}
		}
		Listeners = dictionary;
	}

	private void OnLevelWasLoaded()
	{
		RemoveRedundancies();
	}

	private void Awake()
	{
		if (Object.op_Implicit((Object)(object)instance))
		{
			Object.DestroyImmediate((Object)(object)((Component)this).gameObject);
			return;
		}
		instance = this;
		Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
	}
}
public class PlayerInput : MonoBehaviour
{
}
public class PlayerInventory : MonoBehaviour
{
	public List<int> weaponAmmo = new List<int>();

	public List<int> playerWeapon = new List<int>();

	public PlayerInventory instance;
}
public class RECCameras_obj : DynamicObject
{
	public virtual void SetupCamera()
	{
	}

	public virtual bool CheckSetup()
	{
		return false;
	}
}
public class RainManager : MonoBehaviour
{
	public List<GameObject> rains = new List<GameObject>();

	public Transform rain;

	public Transform currentPlayer;

	public Transform defaultPlayer;

	public static RainManager instance;

	private void Start()
	{
		instance = this;
	}

	public void SetPlayerRainState(bool state)
	{
		rains[0].SetActive(state);
	}

	public void Update()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)currentPlayer != (Object)null)
		{
			Vector3 position = currentPlayer.position;
			position.y += 10f;
			rain.position = position;
		}
	}
}
public class RakeDead_obj : MonoBehaviour
{
	private void Start()
	{
		((MonoBehaviour)this).StartCoroutine(finishGame());
	}

	private IEnumerator finishGame()
	{
		yield return (object)new WaitForSeconds(4f);
		Application.LoadLevel(0);
	}
}
public class RecordCamera_obj : RECCameras_obj
{
	public enum Camera_state
	{
		Unsetup,
		Setup
	}

	public bool isRecord;

	public Text text;

	public GameObject setupCamera;

	public GameObject cameraPoint;

	public GameObject cameraRender;

	public Camera_state cameraState;

	public Transform backlight;

	public Transform currentTarget;

	public Transform camRec;

	public AudioClip setupSound;

	public int cameraID;

	private void Start()
	{
		name = "Press 'E' to setup camera";
	}

	private void AddCameraItem()
	{
		if (WeaponManager.instance.CheckWeapon(2))
		{
			if (WeaponManager.instance.bFullAmmo(2))
			{
				InformationUI.instance.DrawHint("Full count of cameras");
				return;
			}
			WeaponManager.instance.AddAmmo(2, 1);
			InformationUI.instance.PickupInfo("+ Add camera 1");
			SetUnsetupState();
		}
		else
		{
			WeaponManager.instance.AddWeapon(2, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add camera 1");
			SetUnsetupState();
		}
	}

	public void SetSetupState()
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		cameraState = Camera_state.Setup;
		setupCamera.SetActive(true);
		cameraPoint.SetActive(false);
		cameraRender.SetActive(false);
		CameraManager.instance.AddCamera(setupCamera.transform);
		AudioSource.PlayClipAtPoint(setupSound, ((Component)this).transform.position);
		name = "Press 'E' to pickup camera ";
	}

	public void SetUnsetupState()
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		cameraState = Camera_state.Unsetup;
		setupCamera.SetActive(false);
		cameraPoint.SetActive(true);
		cameraRender.SetActive(false);
		CameraManager.instance.RemoveCamera(setupCamera.transform);
		AudioSource.PlayClipAtPoint(setupSound, ((Component)this).transform.position);
		name = "Press 'E' to setup camera ";
	}

	public override void SetupCamera()
	{
		if (cameraState == Camera_state.Unsetup)
		{
			SetSetupState();
		}
	}

	public override void Action()
	{
		if (cameraState == Camera_state.Unsetup && WeaponManager.instance.CurrentWeapon() != 2)
		{
			if (WeaponManager.instance.CheckWeapon(2))
			{
				InformationUI.instance.DrawHint("Please select camera");
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have camera");
			}
		}
		if (cameraState == Camera_state.Setup && WeaponManager.instance.CurrentWeapon() != 2)
		{
			Debug.Log((object)"addCAmeraitem");
			AddCameraItem();
		}
	}

	private void Update()
	{
		if (cameraState == Camera_state.Unsetup)
		{
			UpdateColor();
		}
		if (cameraState == Camera_state.Setup && !((Object)(object)currentTarget != (Object)null))
		{
		}
	}

	private void UpdateColor()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		Renderer component = ((Component)backlight).GetComponent<Renderer>();
		Color val = default(Color);
		((Color)(ref val))..ctor(Color.yellow.r, Color.yellow.g, Color.yellow.b, Mathf.PingPong(Time.time * 0.1f, 0.1f));
		component.material.SetColor("_TintColor", val);
	}

	public override bool CheckSetup()
	{
		if (cameraState == Camera_state.Unsetup)
		{
			if (CameraManager.instance.cameraCount >= 5)
			{
				InformationUI.instance.DrawHint("You cannot setup no more 5 cameras");
				return false;
			}
			return true;
		}
		AddCameraItem();
		return false;
	}
}
public class RifleAmmo_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(1))
		{
			if (WeaponManager.instance.bFullAmmo(1))
			{
				InformationUI.instance.DrawHint("Full ammo in the Rifle");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(1, 6);
			InformationUI.instance.PickupInfo("+ Add ammo of rifle 6");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(1, 6);
			InformationUI.instance.PickupInfo("+ Add ammo of rifle 6");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class Rifle_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(1))
		{
			if (WeaponManager.instance.bFullAmmo(1))
			{
				InformationUI.instance.DrawHint("Full ammo in the Rifle");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(1, 2);
			InformationUI.instance.PickupInfo("+ Add ammo of rifle 2");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(1, 2, type: true);
			InformationUI.instance.PickupInfo("+ Add rifle 1");
			InformationUI.instance.PickupInfo("+ Add ammo of rifle 2");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class SaveGameSystem : MonoBehaviour
{
	public static SaveGameSystem instance;

	public GameObject trapPrefab;

	private void Start()
	{
		instance = this;
		SaveCurrentLevel();
	}

	private void Update()
	{
	}

	public void SaveGame()
	{
		SaveWindows();
		SavePlayerWeapon();
		SavePlayerAmmo();
		SaveGameCameras();
		SaveTrapPosition();
		SaveItems();
		SaveScreemers();
		SaveSoundTriggers();
		SaveRakeHealth();
		SavePlayerHealth();
		SaveCurrentLevel();
		Debug.Log((object)"Game saved");
	}

	private void SaveWindows()
	{
		int count = LevelItemsManager.instance.windowsObj.Count;
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Window:" + i, LevelItemsManager.instance.windowsObj[i].GetComponent<Window_obj>().bDestroy);
		}
		Debug.Log((object)"Saved glass");
	}

	private void SaveCurrentLevel()
	{
		PlayerPrefs.SetInt("Level:", Application.loadedLevel);
	}

	private void SaveRakeHealth()
	{
		Enemy_AI component = GameObject.FindGameObjectWithTag("Rake").GetComponent<Enemy_AI>();
		PlayerPrefs.SetFloat("RakeHealth:", component.Health);
	}

	private void SavePlayerHealth()
	{
		FPSController component = GameObject.FindGameObjectWithTag("Player").GetComponent<FPSController>();
		PlayerPrefs.SetInt("PlayerHealth:", component.Health);
	}

	private void SaveSoundTriggers()
	{
		LevelItemsManager.instance.RefreshSoundTriggersList();
		int count = LevelItemsManager.instance.soundTriggers.Count;
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("SoundTrigger:" + i, LevelItemsManager.instance.soundTriggers[i].GetComponent<SoundTrigger>().id);
		}
		PlayerPrefs.SetInt("SoundTriggersCount", count);
	}

	private void SaveGameCameras()
	{
		int count = CameraManager.instance.gameCameras.Count;
		PlayerPrefs.SetInt("CamerasCount", count);
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Camera:" + i, ((Component)CameraManager.instance.gameCameras[i].parent).GetComponent<RecordCamera_obj>().cameraID);
		}
	}

	private void SaveScreemers()
	{
		LevelItemsManager.instance.RefreshScreemersList();
		int count = LevelItemsManager.instance.screemers.Count;
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Screemer:" + i, LevelItemsManager.instance.screemers[i].GetComponent<ScreemerSystem>().id);
		}
		PlayerPrefs.SetInt("ScreemersCount", count);
	}

	private void SaveItems()
	{
		LevelItemsManager.instance.RefrashItemList();
		int count = LevelItemsManager.instance.items.Count;
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Item:" + i, LevelItemsManager.instance.items[i].GetComponent<Item>().id);
		}
		PlayerPrefs.SetInt("ItemsCount", count);
	}

	private void SaveTrapPosition()
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		LevelItemsManager.instance.RefreshTrapList();
		int count = LevelItemsManager.instance.trapObjects.Count;
		PlayerPrefs.SetInt("TrapsCount", count);
		for (int i = 0; i < count; i++)
		{
			Transform transform = LevelItemsManager.instance.trapObjects[i].transform;
			PlayerPrefs.SetFloat("Trap:" + i + " x=", transform.position.x);
			PlayerPrefs.SetFloat("Trap:" + i + " y=", transform.position.y);
			PlayerPrefs.SetFloat("Trap:" + i + " z=", transform.position.z);
		}
	}

	private void SavePlayerWeapon()
	{
		int count = WeaponManager.instance.playerWeapons.Count;
		PlayerPrefs.SetInt("WeaponsCount:", count);
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Weapon:" + i, ((Component)WeaponManager.instance.playerWeapons[i]).GetComponent<Weapon>().weaponID);
		}
	}

	private void SavePlayerAmmo()
	{
		int count = WeaponManager.instance.weaponsAmmo.Count;
		for (int i = 0; i < count; i++)
		{
			PlayerPrefs.SetInt("Ammo:" + i, WeaponManager.instance.weaponsAmmo[i]);
		}
	}

	public void LoadGame()
	{
		LoadWindows();
		LoadPlayerWeapon();
		LoadPlayerAmmo();
		LoadGameCameras();
		LoadLevelTraps();
		LoadItems();
		LoadScreemers();
		LoadSoundTriggers();
		LoadRakeHealth();
		LoadPlayerHealth();
		Debug.Log((object)"Game Loaded");
	}

	private void LoadWindows()
	{
		int count = LevelItemsManager.instance.windowsObj.Count;
		for (int i = 0; i < count; i++)
		{
			if (PlayerPrefs.GetInt("Window:" + i) == 1)
			{
				LevelItemsManager.instance.windowsObj[i].GetComponent<Window_obj>().SetBrokenState();
			}
		}
		Debug.Log((object)"Loaded glass");
	}

	private void LoadRakeHealth()
	{
		Enemy_AI component = GameObject.FindGameObjectWithTag("Rake").GetComponent<Enemy_AI>();
		component.Health = PlayerPrefs.GetFloat("RakeHealth:");
	}

	private void LoadPlayerHealth()
	{
		FPSController component = GameObject.FindGameObjectWithTag("Player").GetComponent<FPSController>();
		component.Health = PlayerPrefs.GetInt("PlayerHealth:");
	}

	public void LoadPlayerWeapon()
	{
		WeaponManager.instance.playerWeapons.Clear();
		int num = PlayerPrefs.GetInt("WeaponsCount:");
		for (int i = 0; i < num; i++)
		{
			Transform item = WeaponManager.instance.allWeapons[PlayerPrefs.GetInt("Weapon:" + i)];
			WeaponManager.instance.playerWeapons.Add(item);
		}
	}

	public void LoadSoundTriggers()
	{
		int num = PlayerPrefs.GetInt("SoundTriggersCount");
		LevelItemsManager.instance.RefreshSoundTriggersList();
		for (int i = 0; i < LevelItemsManager.instance.soundTriggers.Count; i++)
		{
			LevelItemsManager.instance.soundTriggers[i].SetActive(false);
		}
		for (int j = 0; j < num; j++)
		{
			LevelItemsManager.instance.soundTriggers[PlayerPrefs.GetInt("SoundTrigger:" + j)].SetActive(true);
		}
	}

	public void LoadScreemers()
	{
		int num = PlayerPrefs.GetInt("ScreemersCount");
		LevelItemsManager.instance.RefreshScreemersList();
		for (int i = 0; i < LevelItemsManager.instance.screemers.Count; i++)
		{
			LevelItemsManager.instance.screemers[i].SetActive(false);
		}
		for (int j = 0; j < num; j++)
		{
			LevelItemsManager.instance.screemers[PlayerPrefs.GetInt("Screemer:" + j)].SetActive(true);
		}
	}

	public void LoadItems()
	{
		int num = PlayerPrefs.GetInt("ItemsCount");
		LevelItemsManager.instance.RefrashItemList();
		for (int i = 0; i < LevelItemsManager.instance.items.Count; i++)
		{
			LevelItemsManager.instance.items[i].SetActive(false);
		}
		for (int j = 0; j < num; j++)
		{
			LevelItemsManager.instance.items[PlayerPrefs.GetInt("Item:" + j)].SetActive(true);
		}
	}

	public void LoadLevelTraps()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		LevelItemsManager.instance.trapObjects.Clear();
		int num = PlayerPrefs.GetInt("TrapsCount");
		for (int i = 0; i < num; i++)
		{
			Vector3 zero = Vector3.zero;
			zero.x = PlayerPrefs.GetFloat("Trap:" + i + " x=");
			zero.y = PlayerPrefs.GetFloat("Trap:" + i + " y=") + 1f;
			zero.z = PlayerPrefs.GetFloat("Trap:" + i + " z=");
			GameObject obj = (GameObject)Object.Instantiate((Object)(object)trapPrefab, zero, trapPrefab.transform.rotation);
			LevelItemsManager.instance.AddTrap(obj);
		}
	}

	public void LoadGameCameras()
	{
		CameraManager.instance.gameCameras.Clear();
		int num = PlayerPrefs.GetInt("CamerasCount");
		for (int i = 0; i < num; i++)
		{
			if ((Object)(object)CameraManager.instance.allCameras[PlayerPrefs.GetInt("Camera:" + i)] != (Object)null)
			{
				((Component)CameraManager.instance.allCameras[PlayerPrefs.GetInt("Camera:" + i)]).GetComponent<RecordCamera_obj>().SetSetupState();
			}
		}
	}

	public void LoadPlayerAmmo()
	{
		int count = WeaponManager.instance.weaponsAmmo.Count;
		for (int i = 0; i < count; i++)
		{
			WeaponManager.instance.weaponsAmmo[i] = PlayerPrefs.GetInt("Ammo:" + i);
		}
	}
}
public class ScreemerSystem : MonoBehaviour
{
	public GameObject rake;

	public string DmgType;

	public int id;

	public AudioClip triggerSound;

	public AudioClip attackSound;

	public float timeOnAttack;

	public float timeDelay;

	public bool bTakeDamage = true;

	private GameObject playerObj;

	public Transform playerViewPoint;

	private bool bProcess;

	private void Update()
	{
		if (bProcess)
		{
			playerObj.GetComponent<CameraMouseLook>().Focus(playerViewPoint);
		}
	}

	private void OnTriggerEnter(Collider player)
	{
		if (((Component)player).tag == "Player")
		{
			playerObj = ((Component)player).gameObject;
			((MonoBehaviour)this).StartCoroutine(playerDamage());
		}
	}

	private IEnumerator playerDamage()
	{
		yield return (object)new WaitForSeconds(timeDelay);
		AudioSource.PlayClipAtPoint(triggerSound, ((Component)this).transform.position);
		playerObj.GetComponent<CameraMouseLook>().Focus(playerViewPoint);
		playerObj.GetComponent<PlayerMovement>().FreezePlayer();
		playerObj.GetComponent<CameraMouseLook>().bControl = false;
		playerObj.GetComponent<FPSController>().HidePlayerWeapon();
		rake.SetActive(true);
		bProcess = true;
		rake.GetComponent<Animation>().Play();
		yield return (object)new WaitForSeconds(timeOnAttack);
		bProcess = false;
		AudioSource.PlayClipAtPoint(attackSound, ((Component)this).transform.position);
		if (bTakeDamage)
		{
			playerObj.GetComponent<FPSController>().TakeDamage(10, DmgType);
		}
		playerObj.GetComponent<PlayerMovement>().UnFreezePlayer();
		playerObj.GetComponent<CameraMouseLook>().bControl = true;
		yield return (object)new WaitForSeconds(0.5f);
		Object.Destroy((Object)(object)rake);
		Object.Destroy((Object)(object)((Component)this).gameObject);
	}
}
public class SetupCamera : DynamicObject
{
	public Text text;

	private void Update()
	{
		text.text = "Cam state: Rec/Sound: on/Cam ID:" + (CameraManager.instance.currentCameraID + 1);
	}

	public override void Action()
	{
		if (WeaponManager.instance.CheckWeapon(2))
		{
			if (WeaponManager.instance.bFullAmmo(2))
			{
				InformationUI.instance.DrawHint("Full count of cameras");
				return;
			}
			WeaponManager.instance.AddAmmo(2, 1);
			InformationUI.instance.PickupInfo("+ Add camera 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			WeaponManager.instance.AddWeapon(2, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add camera 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class SniperAmmo_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(6))
		{
			if (WeaponManager.instance.bFullAmmo(6))
			{
				InformationUI.instance.DrawHint("Full ammo in the sniper-rifle");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(6, 6);
			InformationUI.instance.PickupInfo("+ Add ammo of sniper-rifle 6");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(6, 6);
			InformationUI.instance.PickupInfo("+ Add ammo of sniper-rifle 6");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class SniperRifle_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(6))
		{
			if (WeaponManager.instance.bFullAmmo(6))
			{
				InformationUI.instance.DrawHint("Full ammo in the Rifle");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(6, 2);
			InformationUI.instance.PickupInfo("+ Add ammo of rifle 2");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(6, 2, type: true);
			InformationUI.instance.PickupInfo("+ Add rifle 1");
			InformationUI.instance.PickupInfo("+ Add ammo of rifle 2");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class SuvivalGuideMenu : MonoBehaviour
{
	public List<Sprite> images = new List<Sprite>();

	public Image currentPage;

	public int currentPageID;

	public void NextPage()
	{
		currentPageID++;
		currentPageID = Mathf.Clamp(currentPageID, 0, images.Count - 1);
		currentPage.sprite = images[currentPageID];
	}

	public void PrevPage()
	{
		currentPageID--;
		currentPageID = Mathf.Clamp(currentPageID, 0, images.Count - 1);
		currentPage.sprite = images[currentPageID];
	}
}
public class TabletComputer_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(3))
		{
			if (WeaponManager.instance.bFullAmmo(3))
			{
				InformationUI.instance.DrawHint("You have this");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(3, 1);
			InformationUI.instance.PickupInfo("+ Add tablet-computer");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(3, 1, type: false);
			InformationUI.instance.PickupInfo("+ Add tablet-computer 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class TrapDoorSwitcher_obj : DynamicObject
{
	public bool DoorStage;

	public AnimalCage_obj cage;

	public void Update()
	{
		if (!DoorStage)
		{
			name = "Press E to open door";
		}
		else
		{
			name = "Press E to close door";
		}
	}

	public override void Action()
	{
		if (!DoorStage)
		{
			cage.OpenCage();
		}
		else
		{
			cage.CloseCage();
		}
	}
}
public class TrapRotationCorrect : MonoBehaviour
{
	private void Update()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		Ray val = default(Ray);
		((Ray)(ref val))..ctor(((Component)this).transform.position, Vector3.down);
		RaycastHit val2 = default(RaycastHit);
		if (Physics.Raycast(val, ref val2, 5f) && Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Terrain>()))
		{
			((Component)this).transform.rotation = Quaternion.LookRotation(((RaycastHit)(ref val2)).normal);
		}
	}
}
public class Trap_item : Item
{
	public override void PickupItem()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (WeaponManager.instance.CheckWeapon(4))
		{
			if (WeaponManager.instance.bFullAmmo(4))
			{
				InformationUI.instance.DrawHint("Full count of traps");
				return;
			}
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddAmmo(4, 1);
			InformationUI.instance.PickupInfo("+ Add trap 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			AudioSource.PlayClipAtPoint(pickupSound, ((Component)this).transform.position);
			WeaponManager.instance.AddWeapon(4, 1, type: true);
			InformationUI.instance.PickupInfo("+ Add trap 1");
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class Unit_obj : MonoBehaviour
{
	public virtual void TakeDamage(int amount, string damageType)
	{
	}
}
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
public class Weapon : MonoBehaviour
{
	public int weaponID;

	public int ammo;

	public int maxAmmo;

	private void Start()
	{
	}

	private void Update()
	{
	}
}
public class WeaponSmooth : MonoBehaviour
{
	public float amount = 0.02f;

	public float maxAmount = 0.03f;

	public float smooth = 3f;

	private Vector3 def;

	private void Start()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		def = ((Component)this).transform.localPosition;
	}

	private void Update()
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		float num = (0f - Input.GetAxis("Mouse X")) * amount;
		float num2 = (0f - Input.GetAxis("Mouse Y")) * amount;
		if (num > maxAmount)
		{
			num = maxAmount;
		}
		if (num < 0f - maxAmount)
		{
			num = 0f - maxAmount;
		}
		if (num2 > maxAmount)
		{
			num2 = maxAmount;
		}
		if (num2 < 0f - maxAmount)
		{
			num2 = 0f - maxAmount;
		}
		Vector3 val = default(Vector3);
		((Vector3)(ref val))..ctor(def.x + num, def.y + num2, def.z);
		((Component)this).transform.localPosition = Vector3.Lerp(((Component)this).transform.localPosition, val, Time.deltaTime * smooth);
	}
}
public class Binocular_weapon : Weapon
{
	public Animator animator;

	public GameObject UI_System;

	public Camera camera;

	private void OnEnable()
	{
		UI_System.SetActive(true);
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
	}

	private void Update()
	{
		if (Input.GetMouseButton(0))
		{
			animator.SetBool("Sight", true);
			((Behaviour)camera).enabled = true;
		}
		else
		{
			animator.SetBool("Sight", false);
			((Behaviour)camera).enabled = false;
		}
	}
}
public class Camera_weapon : Weapon
{
	public Animator animator;

	public Animator UI;

	public GameObject UI_System;

	public bool bSetup;

	public Text ammoDisplay;

	private void OnEnable()
	{
		UI_System.SetActive(true);
		bSetup = false;
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
	}

	private void Update()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		ammoDisplay.text = ammo.ToString();
		Ray val = default(Ray);
		((Ray)(ref val))..ctor(((Component)this).transform.position, ((Component)this).transform.forward);
		RaycastHit val2 = default(RaycastHit);
		if (Physics.Raycast(val, ref val2, 2.5f) && Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<RECCameras_obj>()))
		{
			RECCameras_obj component = ((Component)((RaycastHit)(ref val2)).collider).GetComponent<RECCameras_obj>();
			if (Input.GetButtonDown("Use") && WeaponManager.instance.weaponsAmmo[weaponID] > 0 && !bSetup)
			{
				((MonoBehaviour)this).StartCoroutine(Setup(component));
			}
		}
	}

	private IEnumerator Setup(RECCameras_obj cam)
	{
		if (cam.CheckSetup())
		{
			bSetup = true;
			List<int> weaponsAmmo;
			List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
			int index2;
			int index = (index2 = weaponID);
			index2 = weaponsAmmo[index2];
			list[index] = index2 - 1;
			animator.SetBool("Setup", true);
			yield return (object)new WaitForSeconds(0.1f);
			cam.SetupCamera();
			yield return (object)new WaitForSeconds(0.2f);
			bSetup = false;
			yield return (object)new WaitForSeconds(0.5f);
			if (WeaponManager.instance.weaponsAmmo[weaponID] > 0)
			{
				animator.SetBool("Setup", false);
				yield break;
			}
			WeaponManager.instance.RemoveWeapon(weaponID);
			UI_System.SetActive(false);
			((Component)this).gameObject.SetActive(false);
		}
	}
}
public class Food_weapon : Weapon
{
	public Animator animator;

	public AudioSource audioSource;

	public AudioClip use;

	public Transform spawnPoint;

	public GameObject projectile;

	public Animator UI;

	public GameObject UI_System;

	public Transform sight;

	public bool bSetup;

	public Text ammoDisplay;

	private bool bCanSetup;

	public Transform camera;

	private void OnEnable()
	{
		UI_System.SetActive(true);
		bSetup = false;
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
		if (Object.op_Implicit((Object)(object)sight))
		{
			((Component)sight).gameObject.SetActive(false);
		}
	}

	private void Update()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		ammoDisplay.text = ammo.ToString();
		Ray val = default(Ray);
		((Ray)(ref val))..ctor(camera.position, camera.forward);
		RaycastHit val2 = default(RaycastHit);
		if (Physics.Raycast(val, ref val2, 3f))
		{
			if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<TerrainCollider>()) && ((Object)((RaycastHit)(ref val2)).collider).name == ((Object)Terrain.activeTerrain).name)
			{
				float num = Terrain.activeTerrain.SampleHeight(((RaycastHit)(ref val2)).point);
				if (((RaycastHit)(ref val2)).point.y <= num + 0.01f)
				{
					bCanSetup = true;
					((Component)sight).gameObject.SetActive(true);
					sight.position = ((RaycastHit)(ref val2)).point;
				}
				else
				{
					bCanSetup = false;
				}
			}
			else
			{
				bCanSetup = false;
				((Component)sight).gameObject.SetActive(false);
			}
		}
		else
		{
			bCanSetup = false;
			((Component)sight).gameObject.SetActive(false);
		}
		if (Input.GetButtonDown("Fire1") && !bSetup && bCanSetup)
		{
			((MonoBehaviour)this).StartCoroutine(Setup());
		}
	}

	private IEnumerator Setup()
	{
		Ray ray = new Ray(camera.position, camera.forward);
		RaycastHit hit = default(RaycastHit);
		if (Physics.Raycast(ray, ref hit, 3f) && Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref hit)).collider).GetComponent<TerrainCollider>()))
		{
			bSetup = true;
			List<int> weaponsAmmo;
			List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
			int index2;
			int index = (index2 = weaponID);
			index2 = weaponsAmmo[index2];
			list[index] = index2 - 1;
			animator.SetBool("Setup", true);
			audioSource.PlayOneShot(use);
			yield return (object)new WaitForSeconds(0.7f);
			Vector3 spawnPos = ((RaycastHit)(ref hit)).point;
			GameObject point = (GameObject)Object.Instantiate((Object)(object)projectile, spawnPos, Quaternion.identity);
			yield return (object)new WaitForSeconds(0.2f);
			yield return (object)new WaitForSeconds(0.5f);
			if (WeaponManager.instance.weaponsAmmo[weaponID] > 0)
			{
				animator.SetBool("Setup", false);
			}
			else
			{
				WeaponManager.instance.RemoveWeapon(weaponID);
				UI_System.SetActive(false);
				((Component)this).gameObject.SetActive(false);
			}
			yield return (object)new WaitForSeconds(1f);
			bSetup = false;
		}
	}
}
public class MapCase_weapon : Weapon
{
	public Animator animator;

	public GameObject UI_System;

	private void OnEnable()
	{
		UI_System.SetActive(true);
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
	}

	private void Update()
	{
		if (Input.GetButton("Fire1"))
		{
			animator.SetBool("Zoom", true);
		}
		else
		{
			animator.SetBool("Zoom", false);
		}
	}
}
public class MedKit_weapon : Weapon
{
	public Animator animator;

	public AudioSource audioSource;

	public AudioClip useSound;

	public Animator UI;

	public GameObject UI_System;

	public bool bSetup;

	public Text ammoDisplay;

	private void OnEnable()
	{
		UI_System.SetActive(true);
		bSetup = false;
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
	}

	private void Update()
	{
		ammoDisplay.text = ammo.ToString();
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		if (Input.GetButtonDown("Fire1") && WeaponManager.instance.weaponsAmmo[weaponID] > 0 && !bSetup)
		{
			((MonoBehaviour)this).StartCoroutine(Use());
		}
	}

	private IEnumerator Use()
	{
		bSetup = true;
		List<int> weaponsAmmo;
		List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
		int index2;
		int index = (index2 = weaponID);
		index2 = weaponsAmmo[index2];
		list[index] = index2 - 1;
		animator.SetBool("Use", true);
		audioSource.PlayOneShot(useSound);
		yield return (object)new WaitForSeconds(0.1f);
		FPSController.instance.Health += 100;
		if (FPSController.instance.Health > 100)
		{
			FPSController.instance.Health = 100;
		}
		yield return (object)new WaitForSeconds(0.2f);
		bSetup = false;
		yield return (object)new WaitForSeconds(0.5f);
		if (WeaponManager.instance.weaponsAmmo[weaponID] > 0)
		{
			animator.SetBool("Use", false);
			yield break;
		}
		WeaponManager.instance.RemoveWeapon(weaponID);
		UI_System.SetActive(false);
		((Component)this).gameObject.SetActive(false);
	}
}
public class Rifle_weapon : Weapon
{
	public Animator animator;

	public Animator UI;

	public GameObject UI_System;

	public bool bSight;

	private GameObject text;

	public Transform caster;

	public bool bReload;

	public bool bCanFire = true;

	public AudioSource SFX;

	public AudioClip fireSound;

	public AudioClip reloadSound;

	public int currentAmmo = 2;

	public int clipSize = 2;

	public GameObject[] decals;

	public GameObject muzzlePos;

	public GameObject decal;

	public GameObject muzzleFire;

	private void OnEnable()
	{
		UI_System.SetActive(true);
		bSight = false;
		bReload = false;
		bCanFire = true;
		UI.SetInteger("Bullets", currentAmmo);
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
	}

	private void Start()
	{
		text = ((Component)UI_System.transform.FindChild("Ammo_display")).gameObject;
	}

	private void Update()
	{
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		text.GetComponent<Text>().text = ammo.ToString();
		if (Input.GetButton("Fire2"))
		{
			bSight = true;
		}
		else
		{
			bSight = false;
		}
		if (Input.GetButtonDown("Fire1") && bSight && bCanFire && currentAmmo > 0)
		{
			((MonoBehaviour)this).StartCoroutine(Fire());
			Ray val = default(Ray);
			((Ray)(ref val))..ctor(caster.position, caster.forward);
			RaycastHit val2 = default(RaycastHit);
			if (Physics.Raycast(val, ref val2, 1000f))
			{
				NotificationsManager.Instance.PostNotification((Component)(object)this, "Event_PlayerFire");
				if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Unit_obj>()))
				{
					((Component)((RaycastHit)(ref val2)).collider).GetComponent<Unit_obj>().TakeDamage(100, "SniperShot");
					Object.Instantiate((Object)(object)decals[1], ((RaycastHit)(ref val2)).point + ((RaycastHit)(ref val2)).normal * 0.001f, Quaternion.identity);
				}
				if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Window_obj>()))
				{
					((Component)((RaycastHit)(ref val2)).collider).GetComponent<Window_obj>().TakeDamage(100, "SniperShot");
				}
				Object.Instantiate((Object)(object)decals[0], ((RaycastHit)(ref val2)).point + ((RaycastHit)(ref val2)).normal * 0.001f, Quaternion.identity);
			}
		}
		if (Input.GetButtonDown("Reload") && currentAmmo < 2 && WeaponManager.instance.weaponsAmmo[weaponID] > 0 && !bReload)
		{
			Debug.Log((object)"reload");
			((MonoBehaviour)this).StartCoroutine(Reload());
		}
		if (bSight)
		{
			animator.SetBool("Sight", true);
		}
		else
		{
			animator.SetBool("Sight", false);
		}
	}

	private IEnumerator Fire()
	{
		bCanFire = false;
		Object.Instantiate((Object)(object)muzzleFire, muzzlePos.transform.position, muzzlePos.transform.rotation);
		animator.SetBool("Fire", true);
		currentAmmo--;
		SFX.PlayOneShot(fireSound);
		UI.SetInteger("Bullets", currentAmmo);
		yield return (object)new WaitForSeconds(0.2f);
		animator.SetBool("Fire", false);
		yield return (object)new WaitForSeconds(1f);
		bCanFire = true;
	}

	private IEnumerator Reload()
	{
		bCanFire = false;
		animator.SetBool("Reload", true);
		bReload = true;
		yield return (object)new WaitForSeconds(0.5f);
		SFX.PlayOneShot(reloadSound);
		yield return (object)new WaitForSeconds(1.5f);
		int addBullet = ((WeaponManager.instance.weaponsAmmo[weaponID] - (clipSize - currentAmmo) < 0) ? 1 : (clipSize - currentAmmo));
		List<int> weaponsAmmo;
		List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
		int index2;
		int index = (index2 = weaponID);
		index2 = weaponsAmmo[index2];
		list[index] = index2 - (clipSize - currentAmmo);
		if (WeaponManager.instance.weaponsAmmo[weaponID] < 0)
		{
			WeaponManager.instance.weaponsAmmo[weaponID] = 0;
		}
		currentAmmo += addBullet;
		UI.SetInteger("Bullets", currentAmmo);
		bCanFire = true;
		bReload = false;
		animator.SetBool("Reload", false);
	}
}
public class SniperRifle_weapon : Weapon
{
	public Animator animator;

	public Animator UI;

	public GameObject UI_System;

	public bool bSight;

	private GameObject text;

	public Transform caster;

	public bool bReload;

	public bool bCanFire = true;

	public AudioSource SFX;

	public AudioClip fireSound;

	public AudioClip reloadSound;

	public int currentAmmo = 2;

	public int clipSize = 2;

	public Camera sightRender;

	public GameObject muzzlePos;

	public GameObject decal;

	public GameObject muzzleFire;

	public CameraMouseLook cam;

	public PlayerMovement playerMove;

	public GameObject[] decals;

	public Material defaultMat;

	public Material sightMat;

	public GameObject sight;

	private void OnEnable()
	{
		UI_System.SetActive(true);
		bSight = false;
		bReload = false;
		bCanFire = true;
		UI.SetInteger("Bullets", currentAmmo);
	}

	private void OnDisable()
	{
		playerMove.bCanMove = true;
		cam.sensitivityX = 2f;
		cam.sensitivityY = 2f;
		((Behaviour)sightRender).enabled = false;
		animator.SetBool("Sight", false);
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
	}

	private void Start()
	{
		text = ((Component)UI_System.transform.FindChild("Ammo_display")).gameObject;
	}

	private void Update()
	{
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		text.GetComponent<Text>().text = ammo.ToString();
		if (Input.GetMouseButtonDown(1))
		{
			sight.GetComponent<Renderer>().material = sightMat;
		}
		if (Input.GetMouseButtonUp(1))
		{
			sight.GetComponent<Renderer>().material = defaultMat;
		}
		if (Input.GetButton("Fire2"))
		{
			bSight = true;
		}
		else
		{
			bSight = false;
		}
		if (Input.GetButtonDown("Fire1") && bSight && bCanFire && currentAmmo > 0)
		{
			((MonoBehaviour)this).StartCoroutine(Fire());
			Ray val = default(Ray);
			((Ray)(ref val))..ctor(caster.position, caster.forward);
			RaycastHit val2 = default(RaycastHit);
			if (Physics.Raycast(val, ref val2, 1000f))
			{
				NotificationsManager.Instance.PostNotification((Component)(object)this, "Event_PlayerFire");
				if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Unit_obj>()))
				{
					((Component)((RaycastHit)(ref val2)).collider).GetComponent<Unit_obj>().TakeDamage(200, "SniperShot");
					Object.Instantiate((Object)(object)decals[1], ((RaycastHit)(ref val2)).point + ((RaycastHit)(ref val2)).normal * 0.001f, Quaternion.identity);
				}
				if (Object.op_Implicit((Object)(object)((Component)((RaycastHit)(ref val2)).collider).GetComponent<Window_obj>()))
				{
					((Component)((RaycastHit)(ref val2)).collider).GetComponent<Window_obj>().TakeDamage(100, "SniperShot");
				}
				Object.Instantiate((Object)(object)decals[0], ((RaycastHit)(ref val2)).point + ((RaycastHit)(ref val2)).normal * 0.001f, Quaternion.identity);
			}
		}
		if (Input.GetButtonDown("Reload") && currentAmmo < 2 && WeaponManager.instance.weaponsAmmo[weaponID] > 0 && !bReload)
		{
			Debug.Log((object)"reload");
			((MonoBehaviour)this).StartCoroutine(Reload());
		}
		if (bSight)
		{
			playerMove.bCanMove = false;
			cam.sensitivityX = 0.5f;
			cam.sensitivityY = 0.5f;
			((Behaviour)sightRender).enabled = true;
			animator.SetBool("Sight", true);
		}
		else
		{
			playerMove.bCanMove = true;
			cam.sensitivityX = 2f;
			cam.sensitivityY = 2f;
			((Behaviour)sightRender).enabled = false;
			animator.SetBool("Sight", false);
		}
	}

	private IEnumerator Fire()
	{
		bCanFire = false;
		Object.Instantiate((Object)(object)muzzleFire, muzzlePos.transform.position, muzzlePos.transform.rotation);
		animator.SetBool("Fire", true);
		currentAmmo--;
		SFX.PlayOneShot(fireSound);
		UI.SetInteger("Bullets", currentAmmo);
		yield return (object)new WaitForSeconds(0.2f);
		animator.SetBool("Fire", false);
		yield return (object)new WaitForSeconds(1f);
		bCanFire = true;
	}

	private IEnumerator Reload()
	{
		bCanFire = false;
		animator.SetBool("Reload", true);
		bReload = true;
		yield return (object)new WaitForSeconds(0.5f);
		SFX.PlayOneShot(reloadSound);
		yield return (object)new WaitForSeconds(1f);
		int addBullet = ((WeaponManager.instance.weaponsAmmo[weaponID] - (clipSize - currentAmmo) < 0) ? 1 : (clipSize - currentAmmo));
		List<int> weaponsAmmo;
		List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
		int index2;
		int index = (index2 = weaponID);
		index2 = weaponsAmmo[index2];
		list[index] = index2 - (clipSize - currentAmmo);
		if (ammo < 0)
		{
			ammo = 0;
		}
		currentAmmo += addBullet;
		UI.SetInteger("Bullets", currentAmmo);
		bCanFire = true;
		bReload = false;
		animator.SetBool("Reload", false);
	}
}
public class Trap_proj : MonoBehaviour
{
	public bool bSetup;

	public GameObject trap;

	public AudioClip activeSound;

	public AudioClip setupSound;

	public GameObject particle;

	private void OnTriggerEnter(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player" && bSetup)
		{
			Collider component = ((Component)info).GetComponent<Collider>();
			((Component)this).GetComponent<AudioSource>().PlayOneShot(activeSound);
			SetTrapActive();
			((Component)component).GetComponent<FPSController>().TakeDamage(35, "TrapDamage");
		}
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Rake" && bSetup)
		{
			Collider component2 = ((Component)info).GetComponent<Collider>();
			((Component)this).GetComponent<AudioSource>().PlayOneShot(activeSound);
			SetTrapActive();
			((Component)component2).GetComponent<Enemy_AI>().TakeDamage(35, "TrapDamage");
		}
		if (Object.op_Implicit((Object)(object)((Component)info).GetComponent<Animal_AI>()) && bSetup)
		{
			Collider component3 = ((Component)info).GetComponent<Collider>();
			((Component)this).GetComponent<AudioSource>().PlayOneShot(activeSound);
			SetTrapActive();
			((Component)component3).GetComponent<Animal_AI>().TakeDamage(100, "TrapDamage");
		}
	}

	public void SetTrapActive()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		trap.GetComponent<Animation>().Play();
		Object.Instantiate((Object)(object)particle, ((Component)this).transform.position, Quaternion.identity);
	}

	private void OnCollisionEnter(Collision collision)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (Object.op_Implicit((Object)(object)((Component)collision.collider).GetComponent<TerrainCollider>()))
		{
			Ray val = default(Ray);
			((Ray)(ref val))..ctor(((Component)this).transform.position, Vector3.down);
			RaycastHit val2 = default(RaycastHit);
			if (Physics.Raycast(val, ref val2, 0.4f))
			{
				bSetup = true;
				((Component)this).GetComponent<AudioSource>().PlayOneShot(setupSound);
				((Component)this).transform.rotation = Quaternion.LookRotation(((RaycastHit)(ref val2)).normal);
			}
		}
	}
}
public class Trap_weapon : Weapon
{
	public Animator animator;

	public Transform spawnPoint;

	public GameObject projectile;

	public Animator UI;

	public GameObject UI_System;

	public bool bSetup;

	public Text ammoDisplay;

	private void OnEnable()
	{
		UI_System.SetActive(true);
		bSetup = false;
	}

	private void OnDisable()
	{
		if (Object.op_Implicit((Object)(object)UI_System))
		{
			UI_System.SetActive(false);
		}
	}

	private void Update()
	{
		ammo = WeaponManager.instance.weaponsAmmo[weaponID];
		ammoDisplay.text = ammo.ToString();
		if (Input.GetButtonDown("Fire1") && !bSetup)
		{
			((MonoBehaviour)this).StartCoroutine(Setup());
		}
	}

	private IEnumerator Setup()
	{
		bSetup = true;
		List<int> weaponsAmmo;
		List<int> list = (weaponsAmmo = WeaponManager.instance.weaponsAmmo);
		int index2;
		int index = (index2 = weaponID);
		index2 = weaponsAmmo[index2];
		list[index] = index2 - 1;
		animator.SetBool("Setup", true);
		yield return (object)new WaitForSeconds(0.7f);
		GameObject obj = (GameObject)Object.Instantiate((Object)(object)projectile, spawnPoint.position, spawnPoint.rotation);
		LevelItemsManager.instance.AddTrap(obj);
		yield return (object)new WaitForSeconds(0.2f);
		yield return (object)new WaitForSeconds(0.5f);
		if (WeaponManager.instance.weaponsAmmo[weaponID] > 0)
		{
			animator.SetBool("Setup", false);
		}
		else
		{
			WeaponManager.instance.RemoveWeapon(weaponID);
			UI_System.SetActive(false);
			((Component)this).gameObject.SetActive(false);
		}
		yield return (object)new WaitForSeconds(1f);
		bSetup = false;
	}
}
public class WeaponManager : MonoBehaviour
{
	public List<Transform> playerWeapons = new List<Transform>();

	public List<int> weaponsAmmo = new List<int>();

	public List<Transform> allWeapons = new List<Transform>();

	public int currentWeapon;

	public int currentWeaponID;

	private int maxWeapon;

	public bool bChangeWeapon;

	public Animator anim;

	public Transform weaponSelect;

	public static WeaponManager instance;

	private void Start()
	{
		instance = this;
	}

	private void Update()
	{
		if (bChangeWeapon || FPSController.instance.playerState != FPSController.Player_state.Full)
		{
			return;
		}
		if (Input.GetKeyDown((KeyCode)49))
		{
			if (CheckWeapon(1))
			{
				SelectWeaponID(1);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have rifle");
			}
		}
		if (Input.GetKeyDown((KeyCode)50))
		{
			if (CheckWeapon(6))
			{
				SelectWeaponID(6);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have this weapon");
			}
		}
		if (Input.GetKeyDown((KeyCode)118))
		{
			if (CheckWeapon(5))
			{
				SelectWeaponID(5);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have feed");
			}
		}
		if (Input.GetKeyDown((KeyCode)116))
		{
			if (CheckWeapon(4))
			{
				SelectWeaponID(4);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have trap");
			}
		}
		if (Input.GetKeyDown((KeyCode)109))
		{
			if (CheckWeapon(3))
			{
				SelectWeaponID(3);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have map");
			}
		}
		if (Input.GetKeyDown((KeyCode)99))
		{
			if (CheckWeapon(2))
			{
				SelectWeaponID(2);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have camera");
			}
		}
		if (Input.GetKeyDown((KeyCode)98))
		{
			if (CheckWeapon(7))
			{
				SelectWeaponID(7);
			}
			else
			{
				InformationUI.instance.DrawHint("You do not have binoculars");
			}
		}
	}

	public List<Transform> GetPlayerWeapons()
	{
		return playerWeapons;
	}

	public void SelectWeapon(int id)
	{
		if (playerWeapons.Count > 0)
		{
			if (id > playerWeapons.Count - 1)
			{
				id = playerWeapons.Count - 1;
			}
			((MonoBehaviour)this).StartCoroutine("changeWeapon", (object)id);
		}
	}

	public void SelectWeaponID(int id)
	{
		for (int i = 0; i < playerWeapons.Count; i++)
		{
			if (((Component)playerWeapons[i]).GetComponent<Weapon>().weaponID == id)
			{
				((MonoBehaviour)this).StartCoroutine("changeWeapon", (object)i);
			}
		}
	}

	public void HideCurrentWeapon()
	{
		HideWeapon(currentWeapon);
	}

	public void HideWeapon(int id)
	{
		((MonoBehaviour)this).StartCoroutine("hideWeapon", (object)id);
	}

	public void AddWeapon(int id, int count, bool type)
	{
		if (CheckWeapon(id))
		{
			AddAmmo(id, count);
			return;
		}
		playerWeapons.Add(allWeapons[id]);
		AddAmmo(id, count);
	}

	public void RemoveWeapon(int id)
	{
		for (int i = 0; i < playerWeapons.Count; i++)
		{
			if (id == ((Component)playerWeapons[i]).GetComponent<Weapon>().weaponID)
			{
				if (id == currentWeaponID)
				{
					currentWeaponID = 0;
					weaponSelect = playerWeapons[0];
				}
				playerWeapons.Remove(playerWeapons[i]);
			}
		}
	}

	public int CurrentWeapon()
	{
		currentWeaponID = ((Component)weaponSelect).GetComponent<Weapon>().weaponID;
		return currentWeaponID;
	}

	public bool CheckWeapon(int id)
	{
		foreach (Transform playerWeapon in playerWeapons)
		{
			if (id == ((Component)playerWeapon).GetComponent<Weapon>().weaponID)
			{
				return true;
			}
		}
		return false;
	}

	public bool bFullAmmo(int id)
	{
		if (weaponsAmmo[id] < 1000)
		{
			return false;
		}
		return true;
	}

	public void AddAmmo(int id, int count)
	{
		List<int> list2;
		List<int> list = (list2 = weaponsAmmo);
		int index2;
		int index = (index2 = id);
		index2 = list2[index2];
		list[index] = index2 + count;
	}

	private IEnumerator hideWeapon(int i)
	{
		anim.SetBool("ChangeWeapon", true);
		bChangeWeapon = true;
		yield return (object)new WaitForSeconds(0.5f);
		anim.SetBool("ChangeWeapon", false);
		bChangeWeapon = false;
		((Component)playerWeapons[i]).gameObject.SetActive(false);
		currentWeaponID = 0;
		SelectWeapon(0);
	}

	private IEnumerator changeWeapon(int i)
	{
		anim.SetBool("ChangeWeapon", true);
		bChangeWeapon = true;
		yield return (object)new WaitForSeconds(0.5f);
		((Component)weaponSelect).gameObject.SetActive(false);
		anim.SetBool("ChangeWeapon", false);
		((Component)playerWeapons[i]).gameObject.SetActive(true);
		weaponSelect = playerWeapons[i];
		currentWeapon = i;
		yield return (object)new WaitForSeconds(1f);
		bChangeWeapon = false;
	}
}
public class Window_obj : MonoBehaviour
{
	public GameObject glass;

	public GameObject brokenGlass;

	public GameObject particle;

	public AudioClip sound;

	public int bDestroy;

	public void SetBrokenState()
	{
		glass.SetActive(false);
		brokenGlass.SetActive(true);
		bDestroy = 1;
		((Collider)((Component)this).GetComponent<BoxCollider>()).enabled = false;
	}

	public void BreakGlass()
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		glass.SetActive(false);
		brokenGlass.SetActive(true);
		particle.SetActive(true);
		AudioSource.PlayClipAtPoint(sound, ((Component)this).transform.position);
		((Collider)((Component)this).GetComponent<BoxCollider>()).enabled = false;
	}

	public void TakeDamage(int amount, string damageType)
	{
		if (bDestroy == 0)
		{
			BreakGlass();
			bDestroy = 1;
		}
	}
}
public class Objective : MonoBehaviour
{
	public string name;

	public bool Status;

	public int id;

	public string description;

	public virtual void ObjectiveAction()
	{
	}
}
public class ObjectiveManager : MonoBehaviour
{
	public List<Objective> objective = new List<Objective>();

	public Objective currentObjective;

	public int currentObjectiveID;

	private int objCounter;

	public static ObjectiveManager instance;

	private bool bShowObjective;

	public void Start()
	{
		instance = this;
		ShowCurrentObjective();
	}

	public void CompleteObjective(Objective obj)
	{
		if ((Object)(object)currentObjective == (Object)(object)obj)
		{
			obj.Status = true;
			Debug.Log((object)obj);
			InformationUI.instance.DrawObjective("Objective: " + currentObjective.name + " has been completed");
			((MonoBehaviour)this).StartCoroutine(showObjective());
		}
	}

	private IEnumerator showObjective()
	{
		yield return (object)new WaitForSeconds(3f);
		SetupNextObjective();
	}

	public void SetupNextObjective()
	{
		objCounter++;
		if ((Object)(object)objective[objCounter] != (Object)null)
		{
			currentObjective = objective[objCounter];
			ShowCurrentObjective();
		}
	}

	public void SetupNewObjective(int id)
	{
	}

	public void RemoveObjective(Objective obj)
	{
	}

	public void ShowCurrentObjective()
	{
		InformationUI.instance.DrawObjective("Objective: " + currentObjective.name);
	}
}
public class SetupCameras_objective : Objective
{
	public override void ObjectiveAction()
	{
		if (!Status)
		{
			ObjectiveManager.instance.CompleteObjective(this);
		}
	}
}
public class TakeItems_objective : Objective
{
	public override void ObjectiveAction()
	{
		if (!Status)
		{
			ObjectiveManager.instance.CompleteObjective(this);
		}
	}
}
public class Door_obj : DynamicObject
{
	public enum Door_state
	{
		Open,
		Close
	}

	public Door_state doorState = Door_state.Close;

	public GameObject door;

	private bool isProcessing;

	public AudioClip doorOpenSound;

	public AudioClip doorCloseSound;

	public override void Action()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		if (doorState == Door_state.Close && !isProcessing)
		{
			AudioSource.PlayClipAtPoint(doorOpenSound, ((Component)this).transform.position);
			door.GetComponent<Animation>().Play("Door_open");
			((MonoBehaviour)this).StartCoroutine(coroutine(Door_state.Open));
		}
		if (doorState == Door_state.Open && !isProcessing)
		{
			AudioSource.PlayClipAtPoint(doorCloseSound, ((Component)this).transform.position);
			door.GetComponent<Animation>().Play("Door_close");
			((MonoBehaviour)this).StartCoroutine(coroutine(Door_state.Close));
		}
	}

	private IEnumerator coroutine(Door_state state)
	{
		isProcessing = true;
		yield return (object)new WaitForSeconds(0.5f);
		doorState = state;
		if (state == Door_state.Open)
		{
			name = "Press \"E\" to close door";
		}
		else
		{
			name = "Press \"E\" to open door";
		}
		yield return (object)new WaitForSeconds(0.5f);
		isProcessing = false;
	}
}
public class Flashlight_trigger : Trigger
{
	public bool SetLightState;

	public override void Action(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			((Component)info).GetComponent<FPSController>().SetLightState(SetLightState);
		}
	}
}
public class FootstepChanger : MonoBehaviour
{
	public int stepID;

	private void OnTriggerEnter(Collider player)
	{
		if (((Component)player).tag == "Player")
		{
			((Component)player).GetComponent<PlayerMovement>().SetFootstepSound(stepID);
		}
	}

	private void OnTriggerExit(Collider player)
	{
		if (((Component)player).tag == "Player")
		{
			((Component)player).GetComponent<PlayerMovement>().SetFootstepSound(0);
		}
	}
}
public class Forest_trigger : Trigger
{
	public Objective obj;

	public override void Action(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			obj.ObjectiveAction();
		}
	}
}
public class HideZoneTrigger : MonoBehaviour
{
	private void OnTriggerEnter(Collider info)
	{
		if (((Component)info).gameObject.tag == "Player")
		{
			((Component)info).gameObject.GetComponent<FPSController>().bHide = true;
		}
	}

	private void OnTriggerExit(Collider info)
	{
		if (((Component)info).gameObject.tag == "Player")
		{
			((Component)info).gameObject.GetComponent<FPSController>().bHide = false;
		}
	}
}
public class IORotator : MonoBehaviour
{
	public Vector3 rot;

	private void Start()
	{
	}

	private void Update()
	{
		((Component)this).transform.Rotate(rot.x * Time.deltaTime, rot.y * Time.deltaTime, 0f);
	}
}
public class LevelSkip : MonoBehaviour
{
	public GameObject HUD;

	public int levelToLoad;

	public CameraMouseLook cml;

	private void OnTriggerEnter(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			cml = ((Component)info).GetComponent<CameraMouseLook>();
			cml.bControl = false;
			HUD.SetActive(true);
			Cursor.lockState = (CursorLockMode)0;
			Cursor.visible = true;
			WeaponManager.instance.HideCurrentWeapon();
		}
	}

	private void OnTriggerExit(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			Exit();
		}
	}

	public void Load()
	{
	}

	public void Exit()
	{
		cml.bControl = true;
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		HUD.SetActive(false);
	}

	private IEnumerator wait()
	{
		SaveGameSystem.instance.SaveGame();
		FPSController.instance.effectAnimator.SetBool("Fade", true);
		HUD.SetActive(false);
		yield return (object)new WaitForSeconds(1f);
		Application.LoadLevel(levelToLoad);
	}

	public void SkipLevel(bool state)
	{
		if (state)
		{
			((MonoBehaviour)this).StartCoroutine(wait());
		}
		else
		{
			Exit();
		}
	}
}
public class Log_trigger : Trigger
{
	public string text;

	public override void Action(Collider info)
	{
		if (((Component)info).gameObject.tag == "Player")
		{
			InformationUI.instance.DrawHint(text);
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class MainMenu : MonoBehaviour
{
	public GameObject GameMenu;

	public GameObject newGame;

	public GameObject setting;

	public GameObject multiplayer;

	public GameObject credits;

	public int levelToload;

	public int savedLevel;

	public Animator effects;

	public GameObject loadLogo;

	public Text loadLevelText;

	public Button loadGameButton;

	public GameObject LoadingCanvas;

	public float mouseSens;

	public float audioVolume;

	public GameObject help;

	public void Start()
	{
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		Time.timeScale = 1f;
		if (PlayerPrefs.GetFloat("Sound:") != 0f)
		{
			AudioListener.volume = PlayerPrefs.GetFloat("Sound:");
		}
		else
		{
			AudioListener.volume = 1f;
		}
		if (PlayerPrefs.GetFloat("Sound:") != 0f)
		{
			mouseSens = PlayerPrefs.GetFloat("Sens:");
		}
		else
		{
			mouseSens = 2f;
		}
		Cursor.visible = true;
		Cursor.lockState = (CursorLockMode)0;
		savedLevel = PlayerPrefs.GetInt("Level:");
		if (savedLevel <= 1)
		{
			((Graphic)loadLevelText).color = new Color(1f, 1f, 1f, 0.3f);
			((Selectable)loadGameButton).interactable = false;
		}
	}

	private void OnApplicationQuit()
	{
	}

	public void ActiveHelpMenu()
	{
		help.SetActive(true);
	}

	public void ActiveSettings()
	{
		setting.SetActive(true);
	}

	public void ActiveGameMenu()
	{
		GameMenu.SetActive(true);
	}

	private IEnumerator startGame()
	{
		PlayerPrefs.SetFloat("Sound:", audioVolume);
		PlayerPrefs.SetFloat("Sens:", mouseSens);
		LoadingCanvas.SetActive(true);
		loadLogo.SetActive(true);
		effects.SetBool("Fade", true);
		newGame.SetActive(false);
		yield return (object)new WaitForSeconds(3f);
		Application.LoadLevel(levelToload);
	}

	public void ExitGame()
	{
		Application.Quit();
	}

	public void Back()
	{
		GameMenu.SetActive(false);
		help.SetActive(false);
		setting.SetActive(false);
		multiplayer.SetActive(false);
		credits.SetActive(false);
	}

	public void Loadgame()
	{
		levelToload = savedLevel;
		((MonoBehaviour)this).StartCoroutine(startGame());
	}

	public void StartNewGame()
	{
		PlayerPrefs.DeleteAll();
		levelToload = 1;
		((MonoBehaviour)this).StartCoroutine(startGame());
	}

	public void SetSoundVolume(float sound)
	{
		AudioListener.volume = sound;
		audioVolume = sound;
	}

	public void OpenCredits()
	{
		credits.SetActive(true);
	}

	public void SetMouseSens(float speed)
	{
		mouseSens = speed;
	}

	public void OpenMultiplayer()
	{
		multiplayer.SetActive(true);
	}

	public void Newgame()
	{
		newGame.SetActive(true);
	}
}
public class Menu_button : MonoBehaviour
{
	private void Start()
	{
	}

	private void Update()
	{
	}
}
public class Rain_trigger : MonoBehaviour
{
	public void OnTriggerEnter(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			RainManager.instance.SetPlayerRainState(state: false);
		}
	}

	public void OnTriggerExit(Collider info)
	{
		if (((Component)((Component)info).GetComponent<Collider>()).tag == "Player")
		{
			RainManager.instance.SetPlayerRainState(state: true);
		}
	}
}
public class SoundTrigger : MonoBehaviour
{
	public AudioClip sound;

	public int id;

	private void OnTriggerEnter(Collider info)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (((Component)info).gameObject.tag == "Player")
		{
			AudioSource.PlayClipAtPoint(sound, ((Component)this).transform.position, 1f);
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}
}
public class Trigger : MonoBehaviour
{
	public void OnTriggerEnter(Collider info)
	{
		Action(info);
	}

	public virtual void Action(Collider info)
	{
	}
}
public class Trigger_objective : Trigger
{
	public Objective objective;
}
public class FPSController : MonoBehaviour
{
	public enum Player_state
	{
		Full,
		Freezed,
		Disabled
	}

	public int Health;

	public Animator cameraAnimator;

	public Animator effectAnimator;

	public Animator itemInfo;

	public RectTransform playerHealth;

	public WeaponManager weaponManager;

	public static FPSController instance;

	public FPSCamera fpsCamera;

	public PlayerMovement playerMovement;

	public CameraMouseLook camMouseLook;

	public AudioClip[] painSounds;

	public Camera cam;

	public Camera weapCam;

	public Inventory inventory;

	public bool bUseInventory;

	public AudioListener listener;

	public GameObject playerCanvas;

	public bool bHide;

	public Light flashlight;

	public GameObject loadScreenObj;

	public Player_state playerState;

	public void SetPlayerState(Player_state state)
	{
		playerState = state;
	}

	public void Awake()
	{
	}

	public void Start()
	{
		instance = this;
		Debug.Log((object)("LOADEDLEVEL IS " + Application.loadedLevel));
		if (Application.loadedLevel == 1)
		{
			Debug.Log((object)"Level cannot load");
		}
		else
		{
			((MonoBehaviour)this).StartCoroutine(waitGame());
		}
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		((MonoBehaviour)this).StartCoroutine(loadScreen());
	}

	private IEnumerator waitGame()
	{
		yield return (object)new WaitForSeconds(1f);
		SaveGameSystem.instance.LoadGame();
	}

	private IEnumerator loadScreen()
	{
		yield return (object)new WaitForSeconds(2f);
		((Behaviour)effectAnimator).enabled = true;
		((Behaviour)listener).enabled = true;
		Object.Destroy((Object)(object)loadScreenObj);
	}

	private void Update()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		playerHealth.sizeDelta = new Vector2((float)Health * 2f, 10f);
		if (Input.GetButtonDown("Inventory"))
		{
			if (!bUseInventory)
			{
				inventory.OpenInventory();
				WeaponManager.instance.HideCurrentWeapon();
				bUseInventory = true;
				((Behaviour)camMouseLook).enabled = false;
				((Behaviour)playerMovement).enabled = false;
				Cursor.lockState = (CursorLockMode)0;
				Cursor.visible = true;
				SetPlayerDisableState("INVENTORY");
			}
			else
			{
				bUseInventory = false;
				inventory.CloseInventory();
				((Behaviour)camMouseLook).enabled = true;
				((Behaviour)playerMovement).enabled = true;
				Cursor.lockState = (CursorLockMode)1;
				Cursor.visible = false;
				SetPlayerDisableState("FULL");
			}
		}
		if (Input.GetButtonDown("Flashlight"))
		{
			SetLightState(!((Behaviour)flashlight).enabled);
		}
	}

	public void SetLightState(bool state)
	{
		((Behaviour)flashlight).enabled = state;
	}

	public void Event_playerWeaponSelect()
	{
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		bUseInventory = false;
		inventory.CloseInventory();
		EnablePlayerMovement();
	}

	public void CameraRander(bool type)
	{
		((Behaviour)cam).enabled = type;
		((Behaviour)weapCam).enabled = type;
	}

	public void HidePlayerWeapon()
	{
		weaponManager.HideCurrentWeapon();
		if (bUseInventory)
		{
			inventory.CloseInventory();
		}
	}

	public void DisablePlayer()
	{
		HidePlayerWeapon();
		playerCanvas.SetActive(false);
		((Behaviour)fpsCamera).enabled = false;
		((Behaviour)playerMovement).enabled = false;
		((Behaviour)camMouseLook).enabled = false;
		itemInfo.SetBool("Show", false);
		((Behaviour)listener).enabled = false;
		CameraRander(type: false);
		((Behaviour)inventory).enabled = false;
		((Behaviour)weaponManager).enabled = false;
	}

	public void DisablePlayerMovement()
	{
		((Behaviour)camMouseLook).enabled = false;
		((Behaviour)playerMovement).enabled = false;
	}

	public void EnablePlayerMovement()
	{
		playerState = Player_state.Full;
		((Behaviour)camMouseLook).enabled = true;
		((Behaviour)playerMovement).enabled = true;
	}

	public void SetPlayerDisableState(string state)
	{
		switch (state)
		{
		case "INVENTORY":
			((Behaviour)playerMovement).enabled = false;
			((Behaviour)camMouseLook).enabled = false;
			playerState = Player_state.Disabled;
			break;
		case "CAMERAMODE":
			HidePlayerWeapon();
			playerCanvas.SetActive(false);
			((Behaviour)fpsCamera).enabled = false;
			((Behaviour)playerMovement).enabled = false;
			((Behaviour)camMouseLook).enabled = false;
			itemInfo.SetBool("Show", false);
			((Behaviour)listener).enabled = false;
			CameraRander(type: false);
			((Behaviour)inventory).enabled = false;
			((Behaviour)weaponManager).enabled = false;
			break;
		case "SCREEMER":
			camMouseLook.bControl = false;
			playerMovement.bCanMove = false;
			((Behaviour)inventory).enabled = false;
			playerState = Player_state.Disabled;
			break;
		case "PAUSEMENU":
			HidePlayerWeapon();
			playerState = Player_state.Disabled;
			((Behaviour)inventory).enabled = false;
			break;
		case "BEFOREPAUSE":
			playerState = Player_state.Full;
			((Behaviour)inventory).enabled = true;
			((Behaviour)weaponManager).enabled = true;
			break;
		case "FULL":
			playerState = Player_state.Full;
			((Behaviour)inventory).enabled = true;
			playerCanvas.SetActive(true);
			((Behaviour)listener).enabled = true;
			((Behaviour)fpsCamera).enabled = true;
			((Behaviour)playerMovement).enabled = true;
			((Behaviour)camMouseLook).enabled = true;
			camMouseLook.bControl = true;
			playerMovement.bCanMove = true;
			itemInfo.SetBool("Show", true);
			CameraRander(type: true);
			((Behaviour)weaponManager).enabled = true;
			break;
		}
	}

	public void EnablePlayer()
	{
		playerState = Player_state.Full;
		((Behaviour)inventory).enabled = true;
		playerCanvas.SetActive(true);
		((Behaviour)listener).enabled = true;
		((Behaviour)fpsCamera).enabled = true;
		((Behaviour)playerMovement).enabled = true;
		((Behaviour)camMouseLook).enabled = true;
		itemInfo.SetBool("Show", true);
		CameraRander(type: true);
		((Behaviour)weaponManager).enabled = true;
	}

	public void TakeDamage(int amount, string damageType)
	{
		Health -= amount;
		((Component)this).GetComponent<AudioSource>().PlayOneShot(painSounds[Random.Range(0, painSounds.Length)]);
		if (damageType == "TrapDamage")
		{
			((MonoBehaviour)this).StartCoroutine(takeTrapDamage());
		}
		if (damageType == "RakeKick")
		{
			((MonoBehaviour)this).StartCoroutine(TakeDamage());
		}
		if (damageType == "RakeUppercut")
		{
			((MonoBehaviour)this).StartCoroutine(RakeUppercut());
		}
		if (Health < 0)
		{
			KillPlayer();
		}
	}

	public void KillPlayer()
	{
		cameraAnimator.SetFloat("Speed", 0f);
		cameraAnimator.SetBool("Dead", true);
		WeaponManager.instance.HideCurrentWeapon();
		playerMovement.bCanMove = false;
		camMouseLook.bControl = false;
		InformationUI.instance.DrawObjective("Game over");
		((MonoBehaviour)this).StartCoroutine(restart());
	}

	private IEnumerator restart()
	{
		yield return (object)new WaitForSeconds(5f);
		Application.LoadLevel(0);
	}

	private IEnumerator takeTrapDamage()
	{
		effectAnimator.SetBool("Blood", true);
		cameraAnimator.SetBool("LegHit", true);
		yield return (object)new WaitForSeconds(1.5f);
		cameraAnimator.SetBool("LegHit", false);
		yield return (object)new WaitForSeconds(3f);
		effectAnimator.SetBool("Blood", false);
	}

	private IEnumerator RakeUppercut()
	{
		effectAnimator.SetBool("Blood", true);
		cameraAnimator.SetBool("Falldown", true);
		yield return (object)new WaitForSeconds(4.5f);
		cameraAnimator.SetBool("Falldown", false);
		effectAnimator.SetBool("Blood", false);
	}

	private IEnumerator TakeDamage()
	{
		effectAnimator.SetBool("Blood", true);
		cameraAnimator.SetBool("Kick", true);
		yield return (object)new WaitForSeconds(4.5f);
		cameraAnimator.SetBool("Kick", false);
		effectAnimator.SetBool("Blood", false);
	}
}
public class PlayerMovement : MonoBehaviour
{
	public float speed = 6f;

	public float jumpSpeed = 8f;

	public float gravity = 20f;

	private Vector3 moveDirection = Vector3.zero;

	private bool StepDown;

	public float StepSpeed;

	public AudioSource audioSource;

	public AudioClip[] FootstepSound;

	public AudioClip stamina;

	public bool bPlayStaminaSound;

	public Animator anim;

	public Animator cameraAnim;

	public float playerStamina;

	public bool bRun;

	public bool bCanRun;

	public bool bCanMove;

	public int stepType;

	public void SetFootstepSound(int id)
	{
		stepType = id;
	}

	public void FreezePlayer()
	{
		bCanMove = false;
	}

	public void UnFreezePlayer()
	{
		bCanMove = true;
	}

	private void Update()
	{
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		if (bCanMove && FPSController.instance.playerState == FPSController.Player_state.Full)
		{
			if (bRun)
			{
				if (playerStamina > 1f)
				{
					playerStamina -= 0.1f;
				}
				else
				{
					if (!bPlayStaminaSound)
					{
						((Component)this).GetComponent<AudioSource>().PlayOneShot(stamina);
						bPlayStaminaSound = true;
					}
					bCanRun = false;
				}
			}
			else if (playerStamina < 100f)
			{
				playerStamina += 0.3f;
			}
			else
			{
				bPlayStaminaSound = false;
				bCanRun = true;
			}
			CharacterController component = ((Component)this).GetComponent<CharacterController>();
			if (component.isGrounded)
			{
				if (Input.GetButton("Vertical") || Input.GetButton("Horizontal"))
				{
					anim.SetFloat("Speed", 1f);
					cameraAnim.SetFloat("Speed", 1f);
					if (!StepDown)
					{
						StartPlay(StepSpeed);
					}
				}
				else
				{
					cameraAnim.SetFloat("Speed", 0f);
					anim.SetFloat("Speed", 0f);
				}
				if (Input.GetButton("Run") && bCanRun)
				{
					cameraAnim.SetFloat("Speed", 2f);
					anim.SetFloat("Speed", 2f);
					bRun = true;
					speed = 8f;
					StepSpeed = 0.4f;
				}
				else
				{
					bRun = false;
					speed = 4.5f;
					StepSpeed = 0.7f;
				}
				moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
				moveDirection = ((Component)this).transform.TransformDirection(moveDirection);
				moveDirection *= speed;
			}
			ref Vector3 reference = ref moveDirection;
			reference.y -= gravity * Time.deltaTime;
			component.Move(moveDirection * Time.deltaTime);
		}
		else
		{
			cameraAnim.SetFloat("Speed", 0f);
			anim.SetFloat("Speed", 0f);
		}
	}

	private void StartPlay(float time)
	{
		((MonoBehaviour)this).StartCoroutine(FootStep(time));
	}

	private IEnumerator FootStep(float stepDelay)
	{
		StepDown = true;
		audioSource.PlayOneShot(FootstepSound[stepType]);
		yield return (object)new WaitForSeconds(stepDelay);
		StepDown = false;
	}
}
[RequireComponent(typeof(Camera))]
public class EnableCameraDepthInForward : MonoBehaviour
{
	private void Start()
	{
		Set();
	}

	private void Set()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((Component)this).GetComponent<Camera>().depthTextureMode == 0)
		{
			((Component)this).GetComponent<Camera>().depthTextureMode = (DepthTextureMode)1;
		}
	}
}
