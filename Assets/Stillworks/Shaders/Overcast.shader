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
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(cell),hash(cell + float2(1,0)),f.x),
                    lerp(hash(cell + float2(0,1)),hash(cell + 1),f.x),f.y);
            }
            float cloud(float2 p)
            {
                float value = 0, amplitude = .5;
                for (int octave = 0; octave < 5; octave++)
                { value += noise(p) * amplitude; p = p * 2.03 + 13.4; amplitude *= .5; }
                return value;
            }
            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.direction);
                half3 horizon = half3(.63,.62,.56);
                half3 zenith = half3(.14,.24,.33);
                half3 color = lerp(horizon, zenith, pow(saturate(d.y), .65));
                color = lerp(color, half3(.28,.31,.31), saturate(-d.y * 3));
                float3 sun = normalize(float3(.65,.56,.52));
                float alignment = saturate(dot(d,sun));
                float2 skyPlane = d.xz / max(.16,d.y + .12) * 3.2;
                float drift = _Time.y * .0015;
                float density = cloud(skyPlane + float2(drift,drift * .3));
                float coverage = smoothstep(.42,.72,density) * smoothstep(-.02,.18,d.y);
                float lightEdge = saturate(cloud(skyPlane + .16) - density + .12);
                half3 cloudColor = lerp(half3(.31,.37,.40),half3(.78,.74,.64),lightEdge * 3 + pow(alignment,8) * .5);
                color = lerp(color,cloudColor,coverage * .86);
                float wisps = smoothstep(.57,.76,cloud(skyPlane * float2(.55,3.5) + 71));
                color = lerp(color,half3(.65,.70,.72),wisps * .18 * saturate(d.y));
                color += half3(.26,.18,.09) * pow(alignment,24);
                color += half3(1.7,1.4,.9) * smoothstep(.9992,.99975,alignment) * (1 - coverage * .8);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
