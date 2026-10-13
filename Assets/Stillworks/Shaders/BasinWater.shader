Shader "Stillworks/Reservoir water"
{
    Properties { _BaseColor("Water tint",Color) = (.12,.20,.21,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings vert(Attributes i)
            {
                Varyings o; o.world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world); o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.world.xz;
                float t = _Time.y;
                float detail = 1 - smoothstep(70,350,distance(_WorldSpaceCameraPos,i.world));
                float waveX = (sin(p.x * .32 + p.y * .17 + t * .4) * .06 + sin(p.y * 1.3 - t * .6) * .018) * detail;
                float waveZ = (cos(p.y * .25 - p.x * .13 + t * .3) * .05 + cos(p.x * 1.2 + t * .5) * .018) * detail;
                half3 normal = normalize(float3(waveX,1,waveZ));
                half3 view = normalize(GetWorldSpaceViewDir(i.world));
                float fresnel = pow(1 - saturate(dot(normal,view)),4);
                Light sun = GetMainLight();
                float glint = pow(saturate(dot(normal,normalize(sun.direction + view))),80) * .25 * detail;
                half3 color = lerp(_BaseColor.rgb * .65,half3(.42,.51,.55),fresnel * .85) + sun.color * glint;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
