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
            float zeroHeightRadius;
            float atmosphereRadius;

            //Sun Light
            float3 dirToLight;
            float3 lightColor;

            //Atmosphere
            float atmosphereInvDensityFalloffHeight;
            float3 atmosphereAbsorption;
            float3 atmosphereEmission;
            float3 atmosphereRayleighInscattering;
            float3 atmosphereMieInscattering;
            float3 atmosphereMieCoeff;

            //LightMarch Parameters
            int numInScatteringPoints;
            int numOpticalDepthPoints;

            //PostProcessing
            float toneMappingExposure;
            float4x4 spectralColor2RGB;
            float4x4 RGB2spectralColor;

            sampler2D _MainTex,_CameraDepthTexture;

            float miePhase(float cosTh, float3 mieCoeff){
                return clamp(mieCoeff.x*pow(mieCoeff.y-mieCoeff.z*cosTh,-1.5),0,100);
            }           
            float rayleighPhase(float cosTh){
                return .75*(1+cosTh*cosTh);
            }
            
            float2 raySphere(float sphereRadius, float3 rayOrigin, float3 rayDir){
                //returns dstToSphere,dstThroughSphere
                //dir is normalzied
                float b=2*dot(rayOrigin,rayDir);
                float c=dot(rayOrigin,rayOrigin)-sphereRadius*sphereRadius;
                float d=b*b-4*c;
                if(d>0){
                    float s=sqrt(d);
                    float dstToSphereNear=max(0,(-b-s)/2);
                    float dstToSphereFar=(-b+s)/2;
                    if(dstToSphereFar>=0)return float2(dstToSphereNear, dstToSphereFar-dstToSphereNear);
                }
                return float2(0,0);
            }

            
            float getDensity(float3 pos){
                float height= max(length(pos)-zeroHeightRadius,0);
                return exp(-height*atmosphereInvDensityFalloffHeight);
            }

            float getOpticalDepth(float3 rayOrigin, float3 rayDir, float rayLength){
                float step=rayLength/numOpticalDepthPoints;
                float3 pos=rayOrigin+rayDir*(step*.5);
                float opticalDepth=0;
                for(int i=0;i<numOpticalDepthPoints;++i){
                    opticalDepth+=getDensity(pos)*step;
                    pos+=rayDir*step;
                }
                return opticalDepth;
            }

            float3 raymarch( float3 color, float3 rayOrigin, float3 rayDir, float rayLength){
                float dst=0;
                float3 light=0;
                float3 transmittance=float3(1,1,1);
                
                float cosTh=dot(rayDir,dirToLight);
                float3 atmosphereInscatteringLight=lightColor*
                                                 (atmosphereRayleighInscattering*rayleighPhase(cosTh)
                                                 +atmosphereMieInscattering*miePhase(cosTh,atmosphereMieCoeff));
                float step=rayLength/numInScatteringPoints;
                dst=step/2;
                for(int i=0;i<numInScatteringPoints;++i){
                    float3 pos=rayOrigin+rayDir*dst;
                    float atmosphereStepDensity=getDensity(pos)*step;

                    transmittance*=exp(-atmosphereStepDensity*atmosphereAbsorption);

                    float2 hitInfo=raySphere(atmosphereRadius,pos,dirToLight);
                    float inscatteringAtmosphereDepth=getOpticalDepth(pos, dirToLight, hitInfo.y);
                    //Todo Sphere Shadow
                    light+=(atmosphereEmission+atmosphereInscatteringLight*exp(-inscatteringAtmosphereDepth*atmosphereAbsorption))*atmosphereStepDensity*transmittance;
                    dst+=step;
                }

                return color*transmittance+light;
            }

            
            fixed3 frag (v2f input) : SV_Target
            {
                //Get the screen depth and camera ray
                float nonlinearDepth= SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.uv);

                bool hasDepth; if(UNITY_REVERSED_Z) hasDepth=nonlinearDepth>0;else hasDepth=nonlinearDepth<1;
                float depth; if(hasDepth)depth=LinearEyeDepth(nonlinearDepth)*length(input.viewVector)*depthToPlanetS; else depth=1e38;
                float3 rayOrigin=mul(worldToPlanetTRS,float4(_WorldSpaceCameraPos,1));
                float3 rayDir=normalize(mul(worldToPlanetTRS,input.viewVector));

                //Intersect the ray to the atmosphere
                float2 hitInfo=raySphere( atmosphereRadius ,rayOrigin,rayDir);
                float dstToAtmosphere=hitInfo.x;
                float dstThroughAtmosphere=min(hitInfo.y,depth-hitInfo.x);

                float3 col=tex2D(_MainTex,input.uv);

                
                //The spectral color space is not the rgb color space
                col=mul(RGB2spectralColor,col);
                if(dstThroughAtmosphere>0)
                    col=raymarch(col, rayOrigin+rayDir*dstToAtmosphere,rayDir,dstThroughAtmosphere);
                col=mul(spectralColor2RGB,col);
                
                //HDR mapping, which is very important for realitistic picture
                //Do the HDR mapping in the spectral color space
                if(toneMappingExposure>0)
                    col.xyz=1-exp(-toneMappingExposure*col.xyz);

                return col;
            }
            ENDCG
        }
    }
}
            /*
            float3 raymarch( float3 color, float3 rayOrigin, float3 rayDir, float rayLength){
                float step=rayLength/numInScatteringPoints;
                float3 pos=rayOrigin+rayDir*(rayLength-step*.5);

                float cosTh=dot(rayDir,sunlightDir);
                float3 outScattering=rayleighScattering+mieScattering;
                float3 inScattering=sunlight*rayleighScattering*rayleighPhaseFunction(cosTh);
                if(mieScattering>0)
                    inScattering+=mieScattering*miePhaseFunction(cosTh);

                for(int i=0;i<numInScatteringPoints;++i){
                    float deltaDepth=getDensity(pos)*step;

                    color= lerp(ambientLight,color,exp(-deltaDepth*outScattering));

                    //if(raySphere(planetRadius,pos-planetCenter,dirToSun).y<planetRadius*.05){
                    float sunRayLength=raySphere(planetRadius+atmosphereHeight,pos-planetCenter,-sunlightDir).y;
                    float sunRayOpticalDepth=getOpticalDepth(pos, -sunlightDir, sunRayLength);
                    color+=exp(-(sunRayOpticalDepth+deltaDepth/2)*outScattering)*inScattering*deltaDepth;
                    //}

                    pos-=rayDir*step;
                }
                return color;
            }
            */