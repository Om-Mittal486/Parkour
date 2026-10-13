Shader "Stillworks/Board concrete"
{
    Properties
    {
        _BaseColor("Aggregate tint", Color) = (0.5,0.49,0.45,1)
        _Weather("Weathering", Range(0,1)) = 0.4
        _Board("Formwork seams", Range(0,1)) = 1
        _DetailTex("Aggregate / cracks / paint", 2D) = "white" {}
        _SurfaceScale("Metres per texture tile", Float) = 3
        _Bump("Surface relief", Range(0,0.2)) = 0.035
        _Paint("Expose concrete through chipped paint", Range(0,1)) = 0
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
            float _SurfaceScale;
            half _Bump;
            half _Paint;
            CBUFFER_END
            TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);
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
            half4 Surface(float3 p, half3 weights)
            {
                return SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,p.zy) * weights.x +
                    SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,p.xz) * weights.y +
                    SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,p.xy) * weights.z;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normal);
                float3 p = i.world;
                half3 weights = pow(abs(n),4); weights /= max(dot(weights,half3(1,1,1)),.0001);
                half4 detail = Surface(p / max(_SurfaceScale,.1),weights);
                half4 weather = Surface(p / 19,weights);
                float vertical = 1 - abs(n.y);
                float seam = 1 - smoothstep(0.012, 0.034 + fwidth(p.y), abs(frac(p.y / 1.2) - 0.5) * 1.2);
                float stains = (1 - smoothstep(.55,.82,weather.g)) * vertical;
                float chipped = (1 - smoothstep(.15,.3,detail.a)) * _Paint;
                half3 tint = lerp(_BaseColor.rgb,half3(.48,.47,.43),chipped);
                half3 albedo = tint * detail.rgb * 1.16 * (1 - seam * .12 * _Board * vertical - stains * .22 * _Weather);
                float3 dpdx = ddx(p), dpdy = ddy(p);
                float3 r1 = cross(dpdy,n), r2 = cross(n,dpdx);
                float determinant = dot(dpdx,r1);
                float3 gradient = (ddx(detail.a) * r1 + ddy(detail.a) * r2) * sign(determinant) / max(abs(determinant),.00001);
                n = normalize(n - gradient * _Bump);
                Light sun = GetMainLight(TransformWorldToShadowCoord(p));
                half diffuse = saturate(dot(n, sun.direction));
                half3 ambient = SampleSH(n) + half3(0.19, 0.215, 0.23);
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
