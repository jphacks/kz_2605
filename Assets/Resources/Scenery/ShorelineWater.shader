Shader "JPHACKS2026/ShorelineWater"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 local:TEXCOORD0; float fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            float shoreline(float x, float t)
            {
                return 8.25 + .82*sin(t*.83 + .16*sin(x*.3)) + .18*sin(x*.62+t*.31);
            }
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = v.positionOS.xyz;
                float depth = saturate((p.z-8.0)/12.0);
                p.y = .025 + depth*(.14+.075*sin(p.z*2.1+_Time.y*1.9+p.x*.16)+.035*sin(p.x*.9-p.z*1.2+_Time.y*1.3));
                o.local = p;
                o.positionCS = TransformObjectToHClip(p);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t = _Time.y;
                float x = i.local.x, z = i.local.z;
                float edge = z-shoreline(x,t);
                // A wet, darker sand band remains visible during the backwash.
                clip(edge+1.1);
                float ripples = sin(z*3.1+t*2.1+x*.24)*.5+.5;
                float deep = saturate(edge/11);
                half3 water = lerp(half3(.16,.56,.57),half3(.045,.28,.43),deep);
                water += ripples*.025;
                half3 sand = half3(.48,.40,.27);
                half3 color = lerp(sand,water,smoothstep(-.08,.55,edge));
                float noise = sin(x*8.3+sin(z*7+t))*sin(z*13.2-x*3.1+t*.4);
                float lace = smoothstep(-.4,.6,noise);
                float lip = (1-smoothstep(.04,.33+lace*.2,abs(edge-.12)))*smoothstep(-.1,.06,edge);
                float phase = z*1.9+t*1.65+.22*sin(x*.8);
                float breaker = pow(saturate(sin(phase)),18)*smoothstep(.7,1.5,edge)*(1-smoothstep(3,8,edge));
                float foam = saturate(lip*(.65+lace*.35)+breaker*lace*.75);
                color = lerp(color,half3(.90,.96,.92),foam);
                float3 normal = normalize(float3(-.10*cos(x*.9+t),1,-.17*cos(z*2.1+t*1.9+x*.16)));
                Light light = GetMainLight();
                color *= .7 + .3*saturate(dot(normal,light.direction));
                color = MixFog(color,i.fog);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
