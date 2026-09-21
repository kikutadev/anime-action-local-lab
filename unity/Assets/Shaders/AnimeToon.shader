Shader "AnimeAction/Toon"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _ShadeColor ("Shade Color", Color) = (0.45,0.45,0.5,1)
        _RampThreshold ("Ramp Threshold", Range(0,1)) = 0.45
        _RampSoftness ("Ramp Softness", Range(0.001,0.25)) = 0.04
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.20
        _RimColor ("Rim Color", Color) = (0.35,0.5,0.8,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim Strength", Range(0,1)) = 0.16
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 150

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            fixed4 _Color;
            fixed4 _ShadeColor;
            fixed4 _RimColor;
            float _RampThreshold;
            float _RampSoftness;
            float _AmbientStrength;
            float _RimPower;
            float _RimStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
                float ndl = saturate(dot(normal, lightDir));

                float band = smoothstep(
                    _RampThreshold - _RampSoftness,
                    _RampThreshold + _RampSoftness,
                    ndl);

                float3 toon = lerp(_ShadeColor.rgb, _Color.rgb, band);
                float3 direct = toon * _LightColor0.rgb * lerp(0.65, 1.0, band);
                float3 ambient = toon * (UNITY_LIGHTMODEL_AMBIENT.rgb + _AmbientStrength);

                float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float rim = pow(saturate(1.0 - dot(normal, viewDir)), _RimPower) * _RimStrength;
                float3 result = direct + ambient + _RimColor.rgb * rim;

                return fixed4(result, _Color.a);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vertShadow(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 fragShadow(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
