﻿// Copyright (C) 2019-2020 Alexander Bogarsukov. All rights reserved.
// See the LICENSE.md file in the project root for more information.

// Renders outline based on a texture produces by 'UnityF/Outline/RenderColor' output.
// Modified version of 'Custom/Post Outline' shader taken from https://willweissman.wordpress.com/tutorials/shaders/unity-shaderlab-object-outlines/.
Shader "UnityFx/Outline/HPass"
{
	Properties
	{
		_Width("Outline thickness (in pixels)", Range(1, 32)) = 5
	}

	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always
		Lighting Off

		Pass
		{
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment Frag
			#include "UnityCG.cginc"

			CBUFFER_START(UnityPerMaterial)
				int _Width;
			CBUFFER_END

			UNITY_DECLARE_TEX2D(_MaskTex);
			float2 _MaskTex_TexelSize;
			float _GaussSamples[32];

			struct v2f
			{
				float4 pos : POSITION;
				float2 uvs : TEXCOORD0;
			};

			v2f Vert(appdata_base v)
			{
				v2f o;

				o.pos = float4(v.vertex.xy, 0.0, 1.0);
				o.uvs = ComputeScreenPos(o.pos);

				return o;
			}

			float Frag(v2f i) : COLOR
			{
				float TX_x = _MaskTex_TexelSize.x;
				float intensity;
				int n = _Width;

				for (int k = -n; k <= n; k += 1)
				{
					intensity += UNITY_SAMPLE_TEX2D(_MaskTex, i.uvs.xy + float2(k * TX_x, 0)).r * _GaussSamples[abs(k)];
				}

				return intensity;
			}

			ENDHLSL
		}
	}
}
