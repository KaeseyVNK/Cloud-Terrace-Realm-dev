Shader "CustomRenderTexture/New Custom Render Texture"
{
    Properties
    {
        _Speed ("Speed", Vector) = (0.1,0,0.1,0)
        _MainTex("InputTex", 2D) = "white" {}
        _MainTex2("InputTex", 2D) = "white" {}
     }

     SubShader
     {
        Blend One Zero
        Lighting Off

        Pass
        {
            Name "New Custom Render Texture"

            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            #pragma target 3.0

            float4      _Speed;
            sampler2D   _MainTex;
            float4      _MainText_A;

            sampler2D   _MainTex2;
            float4      _MainText2_A;


            float4 frag(v2f_customrendertexture IN) : COLOR
            {
                float4 cloud = tex2D(_MainTex, IN.localTexcoord.xy + frac(_Time * _Speed.xy));
                float4 cloud2 = tex2D(_MainTex2, IN.localTexcoord.xy + frac(_Time * _Speed.zw));
                float4 combined = cloud * cloud2;
                float intensity = (combined.r + combined.g + combined.b) / 3.0;
                // Tang do tuong phan cua may de tia sang ro net hon
                float contrastIntensity = pow(intensity, 1.6f);
                float outputVal = max(0.02f, contrastIntensity);
                return float4(outputVal, outputVal, outputVal, outputVal);
            }
            ENDCG
        }
    }
}
