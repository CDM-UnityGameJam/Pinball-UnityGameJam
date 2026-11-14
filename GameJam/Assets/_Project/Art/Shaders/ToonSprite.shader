// Toon shading for flat sprite billboards (the monster enemies), matching Pinball/Toon on the ball:
// the same two-tone shadow band, thin lit-side rim, a dark silhouette outline of the same screen width, and a
// cast shadow on the board.
// A sprite has no real normals, so the shader bends one over the sprite's rectangle like a dome (_Bulge)
// and lights that with the main light. The outline is drawn just outside the art's alpha edge; the quad is
// grown by _Padding (a fraction of the sprite) so there is room for it even where the art touches the
// texture border. Needs Full Rect sprites from an unpacked texture (UV 0..1 covers the sprite), and works in the
// sprite's object space, so batching is disabled: a batched sprite is drawn in world space, which made the
// padding and dome change size whenever two monsters with the same art started or stopped batching.
// SpriteRenderer.color (EnemyView's hit/burn/stun/preview tint) multiplies the result, as on any sprite.
Shader "Pinball/Toon Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [Header(Toon)]
        _ShadowColor ("Shadow Tint", Color) = (0.62, 0.55, 0.85, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.45
        _ShadowSoftness ("Band Softness", Range(0.001, 0.5)) = 0.02
        _RimColor ("Rim Color", Color) = (1, 1, 1, 0.25)
        _RimPower ("Rim Power", Range(1, 10)) = 4
        [Header(Fake volume)]
        _Bulge ("Dome Strength", Range(0, 1)) = 0.85
        _Centre ("Dome Centre (UV)", Vector) = (0.5, 0.45, 0, 0)
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.16, 0.08, 0.25, 1)
        _OutlineWidth ("Outline Width (pixels at 1080p)", Range(0, 10)) = 3
        _Padding ("Quad Padding (fraction of sprite)", Range(0, 0.3)) = 0.1
        _PixelsPerUnit ("Sprite Pixels Per Unit", Float) = 100
        [Header(Flash)]
        _Flash ("Flash to white (per renderer)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "False" "DisableBatching" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BaseColor;
            float4 _ShadowColor;
            float _ShadowThreshold;
            float _ShadowSoftness;
            float4 _RimColor;
            float _RimPower;
            float _Bulge;
            float4 _Centre;
            float _AlphaCutoff;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _Padding;
            float _PixelsPerUnit;
            float _Flash;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ToonSprite"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 rightWS : TEXCOORD1;
                float3 upWS : TEXCOORD2;
                float3 forwardWS : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                // Grow the quad outward by _Padding of the sprite size; the UVs run past 0..1 to match.
                float2 corner = sign(v.uv - 0.5);
                float2 sizeOS = _MainTex_TexelSize.zw / max(_PixelsPerUnit, 0.0001);
                v.positionOS.xy += corner * _Padding * sizeOS;
                v.uv += corner * _Padding;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // The sprite's own axes in world space; the quad faces the camera (FaceCamera), so "out of
                // the sprite" is toward the viewer.
                o.rightWS = normalize(TransformObjectToWorldDir(float3(1, 0, 0)));
                o.upWS = normalize(TransformObjectToWorldDir(float3(0, 1, 0)));
                o.forwardWS = normalize(cross(o.rightWS, o.upWS));
                float3 toCamera = GetWorldSpaceViewDir(positionWS);
                if (dot(o.forwardWS, toCamera) < 0) o.forwardWS = -o.forwardWS;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // Sprite alpha, transparent outside the sprite's own 0..1 rectangle.
            float AlphaAt(float2 uv)
            {
                float inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * inside;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float inside = step(0, i.uv.x) * step(i.uv.x, 1) * step(0, i.uv.y) * step(i.uv.y, 1);
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _BaseColor * i.color;
                albedo.a *= inside;

                // Outline: the most opaque neighbour within _OutlineWidth screen pixels (12 directions).
                float2 dx = ddx(i.uv), dy = ddy(i.uv);
                float radius = _OutlineWidth * _ScreenParams.y / 1080.0;
                float neighbour = 0;
                [unroll] for (int k = 0; k < 12; k++)
                {
                    float angle = k * (6.2831853 / 12.0);
                    neighbour = max(neighbour, AlphaAt(i.uv + (dx * cos(angle) + dy * sin(angle)) * radius));
                }
                float outline = saturate(neighbour * i.color.a * _BaseColor.a);
                clip(max(albedo.a, outline) - _AlphaCutoff);

                // Dome normal: tilts toward the edges of the sprite rectangle.
                float2 d = (i.uv - _Centre.xy) * 2.0 * _Bulge;
                float z = sqrt(saturate(1.0 - dot(d, d)));
                float3 n = normalize(i.rightWS * d.x + i.upWS * d.y + i.forwardWS * max(z, 0.05));

                Light light = GetMainLight();
                float lit = saturate(dot(n, light.direction));
                float band = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, lit);
                half3 color = albedo.rgb * lerp(_ShadowColor.rgb, light.color, band);

                // Thin hard rim on the lit side, as on the ball.
                float rim = step(0.5, pow(1 - saturate(dot(n, i.forwardWS)), _RimPower)) * band;
                color += _RimColor.rgb * _RimColor.a * rim;

                // Art over its outline: where the sprite is transparent the outline colour shows through.
                color = lerp(color, half3(1, 1, 1), _Flash);
                half3 shaded = lerp(_OutlineColor.rgb, color, albedo.a);
                return half4(MixFog(shaded, i.fog), max(albedo.a, outline) * _OutlineColor.a + albedo.a * (1 - _OutlineColor.a));
            }
            ENDHLSL
        }

        // Casts the monster's silhouette (alpha-tested) into the main light's shadow map, like the ball.
        // Turn on Cast Shadows on the SpriteRenderer (it is hidden in the Inspector's normal mode).
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Off Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float alpha : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                // A flat card has no useful normal for normal bias; use the light direction itself.
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, _LightDirection, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.alpha = v.color.a * _BaseColor.a;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.alpha - 0.5);
                return 0;
            }
            ENDHLSL
        }
    }
}
