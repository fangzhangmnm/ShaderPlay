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
            //Planet Geometry
            uniform float4x4 worldToPlanetTRS;
            uniform float depthToPlanetS;
            uniform float planetRadius;
            uniform float atmosphereRadius;
            //Lighting
            uniform float3 dirToSun;
            uniform float3 sunColor;
            uniform float3 planetColor;
            uniform float4 sunDiscCoeff;

            //LightMarch
            uniform float fixedStepLength;
            uniform int fixedStepNum;
            uniform int totalStepNum;


            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);


            float3 raymarch(float3 color, float3 rayOrigin, float3 rayDir, float rayLength){
                //Intersect the ray to the atmosphere
                float rr=dot(rayOrigin,rayOrigin);
                float rCosChi=dot(rayDir,rayOrigin);

                float2 atmosphereHit=min(rayLength,raySphere(atmosphereRadius ,rr,rCosChi));
                float dstThroughAtmosphere=atmosphereHit.y-atmosphereHit.x;
                if(dstThroughAtmosphere<=0)return color;

                float2 atmosphereSunPhaseStrength=getAtmospherePhaseStrength(dot(rayDir,dirToSun));
                float3 totalDepth=0;
                float3 scatteredLight=0;

                float step=min(fixedStepLength,dstThroughAtmosphere/totalStepNum);
                float longStep=(dstThroughAtmosphere-step*fixedStepNum)/(totalStepNum-fixedStepNum);

                float dst=atmosphereHit.x;
                for(int i=0;i<totalStepNum;++i){
                    if(i>=fixedStepNum)
                        step=longStep;

                    dst+=.5*step;

                    float3 scatterPos=rayOrigin+rayDir*dst;

                    float r=length(scatterPos),h=r-planetRadius,cosChi=dot(scatterPos,dirToSun)/r;

                    Atmosphere_Output output1=atmosphereStep(r,h,cosChi);
                    
                    totalDepth+=.5*step*output1.absorption;

                    float3 sunVertex=output1.rayleighScattering*atmosphereSunPhaseStrength.x+output1.mieScattering*atmosphereSunPhaseStrength.y;
                    float3 groundVertex=(output1.rayleighScattering+output1.mieScattering)*0.07957747154;

                    scatteredLight+=step*sunColor*sunVertex*exp(-(totalDepth+output1.inscatteringLightDepth));
                    scatteredLight+=step*sunColor*saturate(cosChi)*planetColor*groundVertex*exp(-totalDepth);
                    

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

                //Get the screen color, convert to Spectral color space
                float3 color=UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex,input.uv);

                color=mul(RGB2spectralColor,color); //The spectral color space is not the rgb color space

                //Draw the sun disc
                float cosTh=dot(rayDir,dirToSun);
                if(!hasDepth)
                    color+=sunColor*sunDisc(cosTh,sunDiscCoeff);

                //Raymarch
                color=raymarch(color, rayOrigin,rayDir,depth);

                
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