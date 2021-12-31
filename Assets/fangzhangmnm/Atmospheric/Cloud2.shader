

Shader "Hidden/Cloud2"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile WEATHERMAP_UNIFORM WEATHERMAP_PLANAR WEATHERMAP_SPHERICAL
            #pragma multi_compile CLOUD_DEBUG_SHOW_NONE CLOUD_DEBUG_SHOW_STEPS CLOUD_DEBUG_SHOW_SAMPLES

            #include "UnityCG.cginc"
            #include "Cloud.cginc"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO 
            };

            v2f vert (appdata v)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(v); 
                UNITY_INITIALIZE_OUTPUT(v2f, output); 
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output); 

                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return output;
            }
            



            //Postfx Textures
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            

            
            
            //Next: cloud height adjust
            //Next: cloud should not be completely dark
            //Next: global mapping shader variant
            
            
            fixed3 frag (v2f input) : SV_Target
            {
                #if RENDER_TO_TEXTURE
                    return float3(1,0,0);
                #endif


                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                //Get the screen depth and camera ray
                fixed nonlinearDepth= SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.uv);
                bool hasDepth; if(UNITY_REVERSED_Z) hasDepth=nonlinearDepth>0;else hasDepth=nonlinearDepth<1;
                float depth; if(hasDepth)depth=LinearEyeDepth(nonlinearDepth)*length(input.viewVector)*depthToPlanetS; else depth=1e38;
                float3 rayOrigin=mul(worldToPlanetTRS,float4(_WorldSpaceCameraPos,1));
                float3 rayDir=normalize(mul(worldToPlanetTRS,input.viewVector*depthToPlanetS));

                //Get the screen color, convert to Spectral color space
                float3 color=UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex,input.uv);
                color=mul(RGB2spectralColor,color); //The spectral color space is not the rgb color space

                //Draw the sun disc
                float cosTh=dot(rayDir,dirToSun);
                if(!hasDepth)
                    color+=sunColor*sunDisc(cosTh,sunDiscCoeff);

                //Raymarch
                Raymarch_Output raymarch_output=raymarch(color, rayOrigin,rayDir,depth);
                color=color*raymarch_output.transmittance+raymarch_output.scatteredLight;

                
                #if CLOUD_DEBUG_SHOW_STEPS || CLOUD_DEBUG_SHOW_SAMPLES
                    if(input.uv.x>.99)
                        color=rainbow(input.uv.y);
                #endif
                //Tonemapping HDR to LDR
                if(toneMappingExposure>0)
                    color.xyz=1-exp(-toneMappingExposure*color.xyz);

                //Convert to RGB color space
                color=mul(spectralColor2RGB,color);

                return color;
            }
            ENDCG
        }
    }
}