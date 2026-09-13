Shader "Stillworks/Overcast sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 direction : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.direction = v.vertex.xyz; return o; }
            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.direction);
                half3 horizon = half3(.51,.56,.56);
                half3 zenith = half3(.19,.25,.29);
                half3 color = lerp(horizon, zenith, pow(saturate(d.y), .65));
                color = lerp(color, half3(.28,.31,.31), saturate(-d.y * 3));
                float glow = pow(saturate(dot(d, normalize(float3(.7,.3,-.6)))), 16);
                color += half3(.12,.095,.055) * glow;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
