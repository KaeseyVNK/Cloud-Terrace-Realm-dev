Shader "Custom/ProceduralGrass"
{
    Properties
    {
        [Header(Visuals)]
        _MainTex("Base Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _BaseColor("Root Color Tint", Color) = (0.7, 0.7, 0.7, 1)
        _TipColor("Tip Color Tint", Color) = (1.0, 1.0, 1.0, 1)
        
        [Header(Wind Settings)]
        _WindDirection("Wind Direction (2D)", Vector) = (1, 0.3, 0, 0)
        _WindSpeed("Wind Speed", Float) = 1.2
        _WindFrequency("Wind Frequency", Float) = 0.08
        _WindStrength("Wind Strength", Float) = 0.35
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque" 
            "Queue" = "Geometry"
        }

        Cull Off // Render grass blades double-sided
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Required URP multi-compiles
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                uint instanceID     : SV_InstanceID; // Hardware instance ID semantic
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 worldPos     : TEXCOORD3;
                half4 color         : COLOR;
                float fogFactor     : TEXCOORD5;
            };

            struct GrassBladeData
            {
                float3 position;
                float rotation;
                float2 size;
                float bend;
                float windEffect;
                float3 bendDirection;
            };

            // Structured buffer containing the culled grass blades passed from C#
            StructuredBuffer<GrassBladeData> _CulledBuffer;

            // Shader properties
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _Cutoff;

            half4 _BaseColor;
            half4 _TipColor;
            float4 _WindDirection;
            float _WindSpeed;
            float _WindFrequency;
            float _WindStrength;

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // Get this blade's procedural data using hardware instance ID
                GrassBladeData blade = _CulledBuffer[input.instanceID];
                
                // 1. Scale local vertex position
                float3 localPos = input.positionOS.xyz;
                localPos.xz *= blade.size.x;
                localPos.y *= blade.size.y;
                
                // 2. Rotate around Y axis
                float sinRot, cosRot;
                sincos(blade.rotation, sinRot, cosRot);
                float3 rotPos;
                rotPos.x = localPos.x * cosRot - localPos.z * sinRot;
                rotPos.z = localPos.x * sinRot + localPos.z * cosRot;
                rotPos.y = localPos.y;
                
                // 3. Deform based on Wind and Interaction
                // The vertex Y position represents the height factor (0.0 at root, 1.0 at tip)
                float bendFactor = input.positionOS.y;
                
                // Wind Animation (sine wave variation across space and time)
                float windTime = _Time.y * _WindSpeed;
                float windNoise = sin(windTime + dot(blade.position.xz, float2(0.15, 0.08)) * _WindFrequency);
                float3 windDirection = normalize(float3(_WindDirection.x, 0, _WindDirection.y));
                float3 windOffset = windDirection * windNoise * _WindStrength * blade.windEffect * pow(bendFactor, 2.0);
                
                // Interactor bending offset (applied dynamically by compute shader)
                float3 interactorOffset = blade.bendDirection * pow(bendFactor, 1.8);
                
                // Combine offsets
                rotPos += windOffset + interactorOffset;
                
                // Preserve blade length by pulling Y coordinate down proportional to displacement
                float deformationAmount = length(windOffset.xz + interactorOffset.xz);
                rotPos.y -= deformationAmount * 0.4;
                
                // 4. Translate to world space
                float3 worldPos = blade.position + rotPos;
                output.worldPos = worldPos;
                
                // Rotate local face normal (pointing forward 0,0,1 initially) to world space
                float3 localNormal = float3(0, 0, 1);
                float3 rotNormal;
                rotNormal.x = localNormal.x * cosRot - localNormal.z * sinRot;
                rotNormal.z = localNormal.x * sinRot + localNormal.z * cosRot;
                rotNormal.y = localNormal.y;
                
                // Slant normal slightly upwards to improve lit appearance
                rotNormal = normalize(rotNormal + float3(0, 0.3, 0));
                output.normalWS = rotNormal;
                
                // Projection positions
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;
                
                // Color gradient from root to tip
                output.color = lerp(_BaseColor, _TipColor, bendFactor);
                
                // Fog computation
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Texture sampling and alpha clip test
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(texColor.a - _Cutoff);

                // Prepare base albedo color
                half3 albedo = texColor.rgb * input.color.rgb;

                // Apply Decals (DBuffer)
                #ifdef _DBUFFER
                    InputData inputData = (InputData)0;
                    SurfaceData surfaceData = (SurfaceData)0;
                    
                    inputData.positionWS = input.worldPos;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    
                    surfaceData.albedo = albedo;
                    surfaceData.normalTS = float3(0, 0, 1);
                    
                    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                    albedo = surfaceData.albedo;
                #endif

                // Shadow sampling (calculated per-pixel to prevent cascade interpolation artifacts)
                float4 shadowCoord = TransformWorldToShadowCoord(input.worldPos);
                Light mainLight = GetMainLight(shadowCoord);
                float shadowAtten = mainLight.shadowAttenuation;
                
                // Half-Lambertian lighting to keep grass illuminated even on back-facing angles
                float3 normal = normalize(input.normalWS);
                float3 lightDir = normalize(mainLight.direction);
                float ndl = saturate(dot(normal, lightDir) * 0.5 + 0.5);
                
                // Lighting accumulation
                float3 ambient = half3(0.25, 0.28, 0.25) * input.color.rgb; // Ambient tint matching grassy environment
                float3 diffuse = ndl * mainLight.color * shadowAtten;
                
                float3 finalColor = albedo * (ambient + diffuse);
                
                // Apply Fog
                finalColor = MixFog(finalColor, input.fogFactor);
                
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                uint instanceID     : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct GrassBladeData
            {
                float3 position;
                float rotation;
                float2 size;
                float bend;
                float windEffect;
                float3 bendDirection;
            };

            StructuredBuffer<GrassBladeData> _CulledBuffer;

            // Shader properties for alpha clipping in shadow pass
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _Cutoff;

            float4 _WindDirection;
            float _WindSpeed;
            float _WindFrequency;
            float _WindStrength;

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // Get this blade's procedural data using hardware instance ID
                GrassBladeData blade = _CulledBuffer[input.instanceID];
                
                float3 localPos = input.positionOS.xyz;
                localPos.xz *= blade.size.x;
                localPos.y *= blade.size.y;
                
                float sinRot, cosRot;
                sincos(blade.rotation, sinRot, cosRot);
                float3 rotPos;
                rotPos.x = localPos.x * cosRot - localPos.z * sinRot;
                rotPos.z = localPos.x * sinRot + localPos.z * cosRot;
                rotPos.y = localPos.y;
                
                float bendFactor = input.positionOS.y;
                
                // Wind Animation
                float windTime = _Time.y * _WindSpeed;
                float windNoise = sin(windTime + dot(blade.position.xz, float2(0.15, 0.08)) * _WindFrequency);
                float3 windDirection = normalize(float3(_WindDirection.x, 0, _WindDirection.y));
                float3 windOffset = windDirection * windNoise * _WindStrength * blade.windEffect * pow(bendFactor, 2.0);
                
                // Interactor bending offset
                float3 interactorOffset = blade.bendDirection * pow(bendFactor, 1.8);
                
                rotPos += windOffset + interactorOffset;
                
                float deformationAmount = length(windOffset.xz + interactorOffset.xz);
                rotPos.y -= deformationAmount * 0.4;
                
                float3 worldPos = blade.position + rotPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Clip shadows to match grass blades
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(texColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
