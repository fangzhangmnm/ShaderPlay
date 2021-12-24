Shader "Hidden/Atmosphere1"
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

            #include "UnityCG.cginc"
            #include "Atmosphere.cginc"

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

            //LightMarch
            uniform float fixedStepLength;
            uniform int fixedStepNum;
            uniform int totalStepNum;
            //int numInScatteringPoints;

            //PostProcessing
            uniform float toneMappingExposure;
            uniform float4x4 spectralColor2RGB;
            uniform float4x4 RGB2spectralColor;

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);


            float3 raymarch(float3 color, float3 rayOrigin, float3 rayDir, float rayLength){

                float3 totalDepth=0;
                float3 scatteredLight=0;

                float cosTh=dot(rayDir,dirToSun);
                float rayleighPhaseStrength=rayleighPhaseFunction(cosTh,atmosphere_rayleighPhaseCoeff);
                float miePhaseStrength=miePhaseFunction(cosTh,atmosphere_miePhaseCoeff);

                float step=min(fixedStepLength,rayLength/totalStepNum);
                float longStep=(rayLength-step*fixedStepNum)/(totalStepNum-fixedStepNum);

                float dst=0;
                for(int i=0;i<totalStepNum;++i){
                    if(i>=fixedStepNum)
                        step=longStep;

                    dst+=.5*step;

                    float3 scatterPos=rayOrigin+rayDir*dst;

                    float r=length(scatterPos);
                    float h=r-planetRadius;
                    float cosChi=dot(scatterPos,dirToSun)/r;

                    Atmosphere_Output output1=atmosphereStep(r,h,cosChi,rayleighPhaseStrength,miePhaseStrength);
                    
                    totalDepth+=.5*step*output1.absorption;
                    scatteredLight+=step*sunColor*output1.scattering*exp(-(totalDepth+output1.inscatteringLightDepth));
                    totalDepth+=.5*step*output1.absorption;

                    dst+=.5*step;
                }
                return color*exp(-totalDepth)+scatteredLight;
            }
            

            fixed3 frag (v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                //Get the screen depth and camera ray
                fixed nonlinearDepth= SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.uv);
                bool hasDepth; if(UNITY_REVERSED_Z) hasDepth=nonlinearDepth>0;else hasDepth=nonlinearDepth<1;
                float depth; if(hasDepth)depth=LinearEyeDepth(nonlinearDepth)*length(input.viewVector)*depthToPlanetS; else depth=1e38;
                float3 rayOrigin=mul(worldToPlanetTRS,float4(_WorldSpaceCameraPos,1));
                float3 rayDir=normalize(mul(worldToPlanetTRS,input.viewVector*depthToPlanetS));

                //Intersect the ray to the atmosphere
                float2 atmosphereHit=raySphere(atmosphereRadius ,dot(rayOrigin,rayOrigin),dot(rayDir,rayOrigin));
                float dstToAtmosphere=atmosphereHit.x;
                float dstThroughAtmosphere=min(atmosphereHit.y,depth-atmosphereHit.x);

                //Get the screen color, convert to Spectral color space
                float3 color=UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex,input.uv);
                color=mul(RGB2spectralColor,color); //The spectral color space is not the rgb color space

                //Draw the sun disc
                if(!hasDepth)
                    color+=sunColor*sunDisc(dot(rayDir,dirToSun),sunDiscCoeff);

                //Raymarch
                if(dstThroughAtmosphere>0)
                    color=raymarch(color, rayOrigin+rayDir*dstToAtmosphere,rayDir,dstThroughAtmosphere);
                
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