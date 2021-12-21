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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return output;
            }

            //Planet Geometry
            float4x4 worldToPlanetTRS;
            float depthToPlanetS;
            float planetRadius;
            float atmosphereRadius;

            //Sun Light
            float3 dirToSunlight;
            float3 sunlightColor;
                //Can also supports Moon

            //Atmosphere
            float3 atmosphereRayleighScattering;
            float3 atmosphereRayleighAbsorption;
            float3 atmosphereRayleighPhaseCoeff;
            float atmosphereRayleighScaleHeight;

            float3 atmosphereMieScattering;
            float3 atmosphereMieAbsorption;
            float3 atmosphereMiePhaseCoeff;
            float atmosphereMieScaleHeight;

            float3 atmosphereOzoneAbsorption;
            float atmosphereOzoneMinRadius;
            float atmosphereOzoneMaxRadius;

            //LightMarch
            float fixedStepLength;
            int fixedStepNum;
            int totalStepNum;
            //int numInScatteringPoints;

            //PostProcessing
            float toneMappingExposure;
            float4x4 spectralColor2RGB;
            float4x4 RGB2spectralColor;

            sampler2D _MainTex,_CameraDepthTexture;

      
            //(3/4,0,3/4)/(4pi) rayleigh, or (1.12,.4,0)/(4pi) modded
            float rayleighPhaseFunction(float cosTh, float3 rayleighPhaseCoeff){
                return rayleighPhaseCoeff.x+cosTh*(rayleighPhaseCoeff.y+cosTh*(rayleighPhaseCoeff.z));
            }    
            float miePhaseFunction(float cosTh, float3 miePhaseCoeff){
                return clamp(miePhaseCoeff.x*pow(miePhaseCoeff.y-miePhaseCoeff.z*cosTh,-1.5),0,100);
            }

            float chapman(float x,float cosChi){
                //http://www.thetenthplanet.de/archives/4519
                float c=sqrt(1.57079632679*x);
                if(cosChi>=0)
                    return c/((c-1)*cosChi+1);
                else{
                    float sinChi=sqrt(saturate(1-cosChi*cosChi));
                    return c/((c-1)*cosChi-1)+2*c*exp(x-x*sinChi)*sqrt(sinChi);
                }
            }

            float2 raySphere(float R, float rSquare, float rCosChi){
                //R: planet radius
                //r: dist to planet center
                //cosChi: angle between ray and local zenith
                float b=2*rCosChi;
                float c=rSquare-R*R;
                float d=b*b-4*c;
                if(d>0){
                    float s=sqrt(d);
                    float dstToSphereNear=max(0,(-b-s)/2);
                    float dstToSphereFar=(-b+s)/2;
                    if(dstToSphereFar>=0)return float2(dstToSphereNear, dstToSphereFar-dstToSphereNear);
                }
                return float2(0,0);
            }


            struct Atmosphere_Output{
                float3 scattering;
                float3 absorption;
                float3 inscatteringLightDepth;
            };

            Atmosphere_Output atmosphereStep(float r,float h, float cosChi, float rayleighPhaseStrength, float miePhaseStrength){

                //cosChi: angle between dirToLight and local zenith

                Atmosphere_Output output;


                float rayleighExp=exp(-h/atmosphereRayleighScaleHeight);
                float mieExp=exp(-h/atmosphereMieScaleHeight);
                float ozoneExistence=step(atmosphereOzoneMinRadius,r)-step(atmosphereOzoneMaxRadius,r);
                
                //absorption at this point
                output.absorption=rayleighExp*atmosphereRayleighAbsorption+mieExp*atmosphereMieAbsorption+ozoneExistence*atmosphereOzoneAbsorption;

                //get the depth of inscattering lights
                output.inscatteringLightDepth=
                     atmosphereRayleighAbsorption       *rayleighExp*atmosphereRayleighScaleHeight*chapman(r/atmosphereRayleighScaleHeight,cosChi)
                    +atmosphereMieAbsorption            *mieExp*atmosphereMieScaleHeight*chapman(r/atmosphereMieScaleHeight,cosChi)
                    +atmosphereOzoneAbsorption          *(raySphere(atmosphereOzoneMaxRadius,r*r,r*cosChi).y-raySphere(atmosphereOzoneMinRadius,r*r,r*cosChi).y);
                    
                output.scattering=
                     rayleighExp*atmosphereRayleighScattering*rayleighPhaseStrength
                    +mieExp*atmosphereMieScattering*miePhaseStrength;

                return output;
            }

            float3 raymarch(float3 color, float3 rayOrigin, float3 rayDir, float rayLength){

                float3 totalDepth=0;
                float3 scatteredLight=0;

                float cosTh=dot(rayDir,dirToSunlight);
                float rayleighPhaseStrength=rayleighPhaseFunction(cosTh,atmosphereRayleighPhaseCoeff);
                float miePhaseStrength=miePhaseFunction(cosTh,atmosphereMiePhaseCoeff);

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
                    float cosChi=dot(scatterPos,dirToSunlight)/r;

                    Atmosphere_Output output1=atmosphereStep(r,h,cosChi,rayleighPhaseStrength,miePhaseStrength);
                    
                    totalDepth+=.5*step*output1.absorption;
                    scatteredLight+=step*sunlightColor*output1.scattering*exp(-(totalDepth+output1.inscatteringLightDepth));
                    totalDepth+=.5*step*output1.absorption;

                    dst+=.5*step;
                }
                return color*exp(-totalDepth)+scatteredLight;
            }

            
            fixed3 frag (v2f input) : SV_Target
            {
                //Get the screen depth and camera ray
                float nonlinearDepth= SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.uv);
                bool hasDepth; if(UNITY_REVERSED_Z) hasDepth=nonlinearDepth>0;else hasDepth=nonlinearDepth<1;
                float depth; if(hasDepth)depth=LinearEyeDepth(nonlinearDepth)*length(input.viewVector)*depthToPlanetS; else depth=1e38;
                float3 rayOrigin=mul(worldToPlanetTRS,float4(_WorldSpaceCameraPos,1));
                float3 rayDir=normalize(mul(worldToPlanetTRS,input.viewVector*depthToPlanetS));

                //Intersect the ray to the atmosphere
                float2 hitInfo=raySphere(atmosphereRadius ,dot(rayOrigin,rayOrigin),dot(rayDir,rayOrigin));
                float dstToAtmosphere=hitInfo.x;
                float dstThroughAtmosphere=min(hitInfo.y,depth-hitInfo.x);

                //Get the screen color, convert to Spectral color space
                float3 color=tex2D(_MainTex,input.uv);
                color=mul(RGB2spectralColor,color); //The spectral color space is not the rgb color space

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