Shader "Hidden/NewImageEffectShader 1"
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

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            float3 planetCenter;
            float3 dirToSun;
            float3 sunlight;
            float planetRadius;
            float scale;
            float atmosphereRadius;
            float densityFalloff;
            float3 rayleighScattering;
            float mieScattering;
            float mieG;
            int numInScatteringPoints;
            int numOpticalDepthPoints;

            float miePhaseFunction(float cosTh){
                float g2=mieG*mieG;
                return 1.5*(1-g2)/(2+g2)*(1+cosTh*cosTh)/pow(1+g2-2*mieG*cosTh,1.5);
            }
            float rayleighPhaseFunction(float cosTh){
                return .75*(1+cosTh*cosTh);
            }

            float2 raySphere(float3 sphereCenter, float sphereRadius, float3 rayOrigin, float3 rayDir){
                //returns dstToSphere,dstThroughSphere
                //dir is normalzied
                float3 offset=rayOrigin-sphereCenter;
                float b=2*dot(offset,rayDir);
                float c=dot(offset,offset)-sphereRadius*sphereRadius;
                float d=b*b-4*c;
                if(d>0){
                    float s=sqrt(d);
                    float dstToSphereNear=max(0,(-b-s)/2);
                    float dstToSphereFar=(-b+s)/2;
                    if(dstToSphereFar>=0)return float2(dstToSphereNear, dstToSphereFar-dstToSphereNear);
                }
                return float2(3.402823466e+38F,0);
            }
            
            float getDensity(float3 pos){
                float height01= (length(pos-planetCenter)-planetRadius)/(atmosphereRadius-planetRadius);
                return exp(-height01*densityFalloff)*(1-height01);
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
                float step=rayLength/numInScatteringPoints;
                float3 pos=rayOrigin+rayDir*(rayLength-step*.5);

                float cosTh=-dot(rayDir,dirToSun);
                float3 outScattering=rayleighScattering+mieScattering;
                float3 inScattering=sunlight*rayleighScattering*rayleighPhaseFunction(cosTh);
                if(mieScattering>0)
                    inScattering+=mieScattering*miePhaseFunction(cosTh);

                for(int i=0;i<numInScatteringPoints;++i){
                    float deltaDepth=getDensity(pos)*step;
                    color*=exp(-deltaDepth*outScattering);

                    //if(raySphere(planetCenter,planetRadius,pos,dirToSun).y<planetRadius*.05){
                    float sunRayLength=raySphere(planetCenter,atmosphereRadius,pos,dirToSun).y;
                    float sunRayOpticalDepth=getOpticalDepth(pos, dirToSun, sunRayLength);
                    color+=exp(-(sunRayOpticalDepth+deltaDepth/2)*outScattering)*inScattering*deltaDepth;
                    //}
                    pos-=rayDir*step;
                }
                return color;
            }

            fixed3 frag (v2f input) : SV_Target
            {
                fixed3 color=tex2D(_MainTex,input.uv);
                float nonlinearDepth= SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.uv);
                
                float3 rayOrigin= _WorldSpaceCameraPos/scale;
                float3 rayDir= normalize(input.viewVector);
                float2 hitInfo=raySphere(planetCenter, atmosphereRadius ,rayOrigin,rayDir);
                float dstToAtmosphere=hitInfo.x;
                float dstThroughAtmosphere=hitInfo.y;
                bool hasDepth;

                if(UNITY_REVERSED_Z) 
                    hasDepth=nonlinearDepth>0;
                else
                    hasDepth=nonlinearDepth<1;
                if(hasDepth){
                    float sceneDepth= LinearEyeDepth(nonlinearDepth)*length(input.viewVector)/scale;
                    dstThroughAtmosphere=min(dstThroughAtmosphere,sceneDepth-dstToAtmosphere);
                }else{
                    //color=0;
                }

                if(dstThroughAtmosphere>0)
                    color=raymarch(color, rayOrigin+rayDir*dstToAtmosphere,rayDir,dstThroughAtmosphere);

                return color;
            }
            ENDCG
        }
    }
}
