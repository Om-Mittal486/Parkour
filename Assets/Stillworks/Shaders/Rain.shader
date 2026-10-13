Shader "Stillworks/Rain streaks"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings vert(Attributes i)
            { Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; o.color = i.color; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float width = smoothstep(0,.32,i.uv.x) * (1 - smoothstep(.68,1,i.uv.x));
                float ends = smoothstep(0,.12,i.uv.y) * (1 - smoothstep(.75,1,i.uv.y));
                return half4(i.color.rgb,i.color.a * width * ends);
            }
            ENDHLSL
        }
    }
}
