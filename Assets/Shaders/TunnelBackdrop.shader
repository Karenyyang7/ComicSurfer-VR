Shader "Custom/TunnelBackdrop"
{
    // Opaque black backdrop for the time tunnel.
    //   ZTest Always  -> paints over the room regardless of the room's depth (the room is
    //                    physically close, so a normal far backdrop could not hide it).
    //   ZWrite On     -> resets the depth buffer to this quad's (far) depth, so the transparent
    //                    tunnel streaks/particles in front of it pass their depth test and render
    //                    against pure black instead of the room.
    Properties
    {
        _Color ("Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+400" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Backdrop"
            Cull Off
            ZTest Always
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
