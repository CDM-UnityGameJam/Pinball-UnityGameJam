// Full-screen ink lines: draws the outline colour wherever scene depth or normals jump between
// neighbouring pixels (silhouettes and creases such as wall-meets-floor). Run by the
// FullScreenPassRendererFeature on Settings/Game_Renderer, which supplies _BlitTexture, depth and normals.
Shader "Hidden/Pinball/EdgeOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.16, 0.08, 0.25, 1)
        _Thickness ("Thickness (pixels at 1080p)", Range(0.5, 4)) = 1.5
        _DepthThreshold ("Depth Threshold (relative)", Range(0.001, 1)) = 0.06
        _NormalThreshold ("Normal Threshold", Range(0.01, 2)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "EdgeOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _OutlineColor;
            float _Thickness;
            float _DepthThreshold;
            float _NormalThreshold;

            float Depth(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Roberts cross over a diagonal pair of samples; width scales with screen height.
                float2 texel = (_Thickness * _ScreenParams.y / 1080.0) / _ScreenParams.xy;
                float2 a = uv + texel, b = uv - texel;
                float2 c = uv + float2(texel.x, -texel.y), d = uv + float2(-texel.x, texel.y);

                float da = Depth(a), db = Depth(b), dc = Depth(c), dd = Depth(d);
                float depthEdge = sqrt((da - db) * (da - db) + (dc - dd) * (dc - dd)) / max(min(min(da, db), min(dc, dd)), 1e-3);

                float3 na = SampleSceneNormals(a), nb = SampleSceneNormals(b);
                float3 nc = SampleSceneNormals(c), nd = SampleSceneNormals(d);
                float normalEdge = sqrt(dot(na - nb, na - nb) + dot(nc - nd, nc - nd));

                float edge = max(step(_DepthThreshold, depthEdge), step(_NormalThreshold, normalEdge));
                return half4(lerp(source.rgb, _OutlineColor.rgb, edge * _OutlineColor.a), source.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
