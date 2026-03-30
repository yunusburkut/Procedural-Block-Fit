Shader "Blokfit/BoardGrid"
{
    Properties
    {
        _BaseColor   ("Board Color",  Color)              = (0.14, 0.15, 0.20, 1)
        _LineColor   ("Line Color",   Color)              = (0.28, 0.30, 0.40, 1)
        _GridSize    ("Grid Size",    Float)              = 4.0
        _LineScale   ("Line Scale",   Range(0.05, 2.0))   = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "UnlitForward"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _LineColor;
                float  _GridSize;
                float  _LineScale;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // bw = LineScale cells of border on each side, in UV space of expanded quad.
                float bw = _LineScale / (_GridSize + 2.0 * _LineScale);

                float onBorder = 1.0 - step(bw, uv.x) * step(bw, uv.y)
                                     * step(uv.x, 1.0 - bw) * step(uv.y, 1.0 - bw);

                half3 color = lerp(_BaseColor.rgb, _LineColor.rgb, onBorder);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
