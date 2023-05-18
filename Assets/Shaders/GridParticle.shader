Shader "Instanced/GridTestParticleShader" {
	Properties{
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
        _Color("Color", Color) = (0.25, 0.5, 0.5, 1)
		_DensityRange ("Density Range", Range(0,500000)) = 1.0
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
            float3 _Color;
			float _DensityRange;

			struct Input {
				float2 uv_MainTex;
			};

			struct Particle
			{
                float pressure;
                float density;
                float3 currentForce;
                float3 velocity;
				float3 position;
				
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

				#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
					float dens = _particlesBuffer[unity_InstanceID].pressure;
					float4 col = float4(dens/_DensityRange, 0,0, 1);
				
					o.Albedo = col.rgb;
					o.Metallic = _Metallic;
					o.Smoothness = _Glossiness;
					o.Alpha = col.a;
				#endif

				
			}
			ENDCG
		}
			FallBack "Diffuse"
}