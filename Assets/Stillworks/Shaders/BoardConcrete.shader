Shader "Stillworks/Board concrete"
{
    Properties
    {
        _BaseColor("Aggregate tint", Color) = (0.5,0.49,0.45,1)
        _Weather("Weathering", Range(0,1)) = 0.4
        _Board("Formwork seams", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Weather;
            half _Board;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(input.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            float hash(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normal);
                float3 p = i.world;
                float grain = hash(floor(p * 27));
                float broad = hash(floor(p * 0.42));
                float vertical = 1 - abs(n.y);
                float seam = 1 - smoothstep(0.012, 0.034 + fwidth(p.y), abs(frac(p.y / 1.2) - 0.5) * 1.2);
                float3 cell = floor(float3(p.x * 0.8, p.y * 0.075, p.z * 0.8));
                float stains = smoothstep(0.7, 0.98, hash(cell)) * vertical;
                half3 albedo = _BaseColor.rgb * (0.91 + grain * 0.12 + broad * 0.06 - seam * 0.12 * _Board * vertical - stains * 0.2 * _Weather);
                Light sun = GetMainLight(TransformWorldToShadowCoord(p));
                half diffuse = saturate(dot(n, sun.direction));
                half3 ambient = SampleSH(n) + half3(0.105, 0.12, 0.125);
                half3 color = albedo * (ambient + sun.color * diffuse * sun.shadowAttenuation);
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
