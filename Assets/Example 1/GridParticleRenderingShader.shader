Shader "Instanced/GridTestParticleShader" {
	Properties{
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
	}
		SubShader{
			Tags { "RenderType" = "Opaque" }
			LOD 200

			CGPROGRAM
			// Physically based Standard lighting model
			#pragma surface surf Standard addshadow fullforwardshadows
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup

			sampler2D _MainTex;
			float _size;

			struct Input {
				float2 uv_MainTex;
			};

			struct Particle
			{
				float3 position;
				float4 color;	// TODO-SATJ: could probably get away with float3
			};

		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			StructuredBuffer<Particle> _particlesBuffer;
		#endif

			void setup()
			{
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
				float3 pos = _particlesBuffer[unity_InstanceID].position;
				float size = _size;

				unity_ObjectToWorld._11_21_31_41 = float4(size, 0, 0, 0);
				unity_ObjectToWorld._12_22_32_42 = float4(0, size, 0, 0);
				unity_ObjectToWorld._13_23_33_43 = float4(0, 0, size, 0);
				unity_ObjectToWorld._14_24_34_44 = float4(pos.xyz, 1);
				unity_WorldToObject = unity_ObjectToWorld;
				unity_WorldToObject._14_24_34 *= -1;
				unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
			#endif
			}

			half _Glossiness;
			half _Metallic;

			void surf(Input IN, inout SurfaceOutputStandard o) {
				float4 col = float4(1, 1, 1, 1);
				#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
				col = _particlesBuffer[unity_InstanceID].color;
				#endif
				o.Albedo = col.rgb;
				o.Metallic = _Metallic;
				o.Smoothness = _Glossiness;
				o.Alpha = col.a;
			}
			ENDCG
		}
			FallBack "Diffuse"
}