using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using float2 = UnityEngine.Vector2;
using float3 = UnityEngine.Vector3;
using float4 = UnityEngine.Vector4;
using float4x4 = UnityEngine.Matrix4x4;

namespace fzmnm
{
    public class ShaderEmulator
    {
        protected float3 exp(float3 v) => new float3(Mathf.Exp(v.x), Mathf.Exp(v.y), Mathf.Exp(v.z));
        protected float step(float a, float b) => a < b ? 1 : 0;
        protected float clamp(float v, float a, float b) => Mathf.Clamp(v, a, b);
        protected float pow(float a, float p) => Mathf.Pow(a, p);
        protected float saturate(float a) => Mathf.Clamp01(a);
        protected float sqrt(float a) => Mathf.Sqrt(a);
        protected float exp(float a) => Mathf.Exp(a);
        protected float max(float a, float b) => Mathf.Max(a, b);
        protected float2 max(float a, float2 b) => new float2(max(a, b.x), max(a, b.y));
        protected float4 max(float a, float4 b) => new float4(max(a, b.x), max(a, b.y), max(a, b.z), max(a, b.w));
        protected float min(float a, float b) => Mathf.Min(a, b);
        protected float2 min(float a, float2 b) => new float2(min(a, b.x), min(a, b.y));
        protected float dot(float3 a, float3 b) => float3.Dot(a, b);
        protected float length(float3 v) => v.magnitude;
        protected float3 vmul(float3 a, float3 b) => float3.Scale(a, b);
        protected float3 vmul(float3 a, float3 b, float3 c) => vmul(vmul(a, b), c);
        protected float3 normalize(float3 a) => float3.Normalize(a);
    }

    public class AtmosphereShaderEmulator: ShaderEmulator
    {
        //Planet Geometry
        float4x4 worldToPlanetTRS;
        float depthToPlanetS;
        float planetRadius;
        float atmosphereRadius;

        //Sun
        float3 dirToSun;
        float3 sunColor;
        float4 sunDiscCoeff;

        //Scattering
        float3 atmosphere_rayleighScattering;
        float3 atmosphere_rayleighAbsorption;
        float3 atmosphere_rayleighPhaseCoeff;
        float atmosphere_rayleighScaleHeight;
        
        float3 atmosphere_mieScattering;
        float3 atmosphere_mieAbsorption;
        float3 atmosphere_miePhaseCoeff;
        float atmosphere_mieScaleHeight;
        public bool includeMieInscattering = true;

        float3 atmosphere_ozoneAbsorption;
        float atmosphere_ozoneMeanRadius;
        float atmosphere_ozoneHalfDeltaRadius;

        //LightMarch Parameters
        //float fixedStepLength;
        //int fixedStepNum=5;
        int totalStepNum=30;

        public void ReadMaterial(Material mat)
        {
            worldToPlanetTRS = mat.GetMatrix("worldToPlanetTRS");
            depthToPlanetS = mat.GetFloat("depthToPlanetS");
            planetRadius = mat.GetFloat("planetRadius");
            atmosphereRadius = mat.GetFloat("atmosphereRadius");

            dirToSun = mat.GetVector("dirToSun");
            sunColor = mat.GetVector("sunColor");
            sunDiscCoeff = mat.GetVector("sunDiscCoeff");

            atmosphere_rayleighScattering = mat.GetVector("atmosphere_rayleighScattering");
            atmosphere_rayleighAbsorption = mat.GetVector("atmosphere_rayleighAbsorption");
            atmosphere_rayleighPhaseCoeff = mat.GetVector("atmosphere_rayleighPhaseCoeff");
            atmosphere_rayleighScaleHeight = mat.GetFloat("atmosphere_rayleighScaleHeight");

            atmosphere_mieScattering = mat.GetVector("atmosphere_mieScattering");
            atmosphere_mieAbsorption = mat.GetVector("atmosphere_mieAbsorption");
            atmosphere_miePhaseCoeff = mat.GetVector("atmosphere_miePhaseCoeff");
            atmosphere_mieScaleHeight = mat.GetFloat("atmosphere_mieScaleHeight");

            atmosphere_ozoneAbsorption = mat.GetVector("atmosphere_ozoneAbsorption");
            atmosphere_ozoneMeanRadius = mat.GetFloat("atmosphere_ozoneMeanRadius");
            atmosphere_ozoneHalfDeltaRadius = mat.GetFloat("atmosphere_ozoneHalfDeltaRadius");

        }



        //(3/4,0,3/4)/(4pi) rayleigh, or (1.12,.4,0)/(4pi) modded
        float rayleighPhaseFunction(float cosTh, float3 rayleighPhaseCoeff)
        {
            //Th: angle between ray and dirToLight
            return rayleighPhaseCoeff.x + cosTh * (rayleighPhaseCoeff.y + cosTh * (rayleighPhaseCoeff.z));
        }
        float miePhaseFunction(float cosTh, float3 miePhaseCoeff)
        {
            //Th: angle between ray and dirToLight
            return clamp(miePhaseCoeff.x * pow(miePhaseCoeff.y - miePhaseCoeff.z * cosTh, -1.5f), 0, 100);
        }

        float sunDisc(float cosTh, float4 sunDiscCoeff)
        {
            //Th: angle between ray and dirToLight
            cosTh = pow(saturate(cosTh), sunDiscCoeff.w);
            return miePhaseFunction(cosTh, sunDiscCoeff);
        }
        float chapman(float x, float cosChi)
        {
            //x: height of rayOrigin normalized by scaleHeight
            //Chi: angle between ray and local zenith
            //http://www.thetenthplanet.de/archives/4519
            float c = sqrt(1.57079632679f * x);
            if (cosChi >= 0)
                return c / ((c - 1) * cosChi + 1);
            else
            {
                float sinChi = sqrt(saturate(1 - cosChi * cosChi));
                return c / ((c - 1) * cosChi - 1) + 2 * c * exp((1 - sinChi) * x) * sqrt(sinChi);
            }
        }

        float2 raySphere(float R, float rSquare, float rCosChi)
        {
            //R: planet radius
            //r: dist to planet center
            //cosChi: angle between ray and local zenith
            //return start, end
            float b = -2 * rCosChi; //x^2-b x+c=0, c=rSquare-R*R
            float d = b * b - 4 * (rSquare - R * R);
            if (d <= 0) return new float2(0, 0); // line not overlap circle
            else
            {
                float s = sqrt(d);
                float x1 = (b - s) / 2;
                float x2 = (b + s) / 2;
                return max(0, new float2(x1, x2));
            }

        }
        float4 raySphereShell(float R1, float R2, float rSquare, float rCosChi)
        {
            //R1,R2: inner, outer cloud layer radius
            //r: dist to planet center
            //cosChi: angle between ray and local zenith
            //l: rayLength
            //return start1 < end1 < start2 < end2
            float b = -2 * rCosChi; float bSquare = b * b; //x^2-b x+c=0
            float d2 = bSquare - 4 * (rSquare - R2 * R2);
            if (d2 <= 0) return new float4(0, 0, 0, 0); // line not overlap outer circle
            else
            {
                float s2 = sqrt(d2);
                float x1 = (b - s2) / 2;
                float x4 = (b + s2) / 2;
                if (x4 <= 0) return new float4(0, 0, 0, 0); // ray start after x4
                else
                {
                    float d1 = bSquare - 4 * (rSquare - R1 * R1);
                    if (d1 <= 0) return max(0, new float4(x1, x4, x4, x4)); // line not overlap inner circle
                    else
                    {
                        float s1 = sqrt(d1);
                        float x2 = (b - s1) / 2;
                        float x3 = (b + s1) / 2;
                        if (x2 <= 0) return max(0, new float4(x3, x4, x4, x4)); // ray start after x1-x2
                        else return max(0, new float4(x1, x2, x3, x4));
                    }
                }
            }
        }

        struct Atmosphere_Output
        {
            public float3 scattering;
            public float3 absorption;
            public float3 inscatteringLightDepth;
        };

        float2 getAtmospherePhaseStrength(float cosTh)
        {
            return new float2(rayleighPhaseFunction(cosTh, atmosphere_rayleighPhaseCoeff),
                          miePhaseFunction(cosTh, atmosphere_miePhaseCoeff));
        }

        Atmosphere_Output atmosphereStep(float r, float h, float cosChi, float2 phaseStrength)
        {

            //cosChi: angle between dirToLight and local zenith

            Atmosphere_Output output;


            float rayleighExp = exp(-h / atmosphere_rayleighScaleHeight);
            float mieExp = exp(-h / atmosphere_mieScaleHeight);

            float4 ozoneHit = raySphereShell(atmosphere_ozoneMeanRadius - atmosphere_ozoneHalfDeltaRadius, atmosphere_ozoneMeanRadius + atmosphere_ozoneHalfDeltaRadius, r * r, r * cosChi);
            float ozoneExistence = max(0, 1 - (r - atmosphere_ozoneMeanRadius) / atmosphere_ozoneHalfDeltaRadius);

            //absorption at this point
            output.absorption = rayleighExp * atmosphere_rayleighAbsorption + mieExp * atmosphere_mieAbsorption + ozoneExistence * atmosphere_ozoneAbsorption;

            //get the depth of inscattering lights
            output.inscatteringLightDepth =
                 atmosphere_rayleighAbsorption * rayleighExp * atmosphere_rayleighScaleHeight * chapman(r / atmosphere_rayleighScaleHeight, cosChi)
                + atmosphere_mieAbsorption * mieExp * atmosphere_mieScaleHeight * chapman(r / atmosphere_mieScaleHeight, cosChi)
                + atmosphere_ozoneAbsorption * (ozoneHit.y - ozoneHit.x + ozoneHit.w - ozoneHit.z);

            output.scattering =rayleighExp * atmosphere_rayleighScattering * phaseStrength.x;
            if(includeMieInscattering)
                output.scattering+= mieExp * atmosphere_mieScattering * phaseStrength.y;

            return output;
        }

        float3 raymarch(float3 color, float3 rayOrigin, float3 rayDir, float rayLength)
        {
            //Intersect the ray to the atmosphere
            float2 atmosphereHit = min(rayLength, raySphere(atmosphereRadius, dot(rayOrigin, rayOrigin), dot(rayDir, rayOrigin)));
            float dstThroughAtmosphere = atmosphereHit.y - atmosphereHit.x;
            if (dstThroughAtmosphere <= 0) return color;

            float2 atmospherePhaseStrength = getAtmospherePhaseStrength(dot(rayDir, dirToSun));
            float3 totalDepth = float2.zero;
            float3 scatteredLight = float2.zero;

            float step = dstThroughAtmosphere;

            float dst = atmosphereHit.x;
            for (int i = 0; i < totalStepNum; ++i)
            {

                dst += .5f * step;

                float3 scatterPos = rayOrigin + rayDir * dst;

                float r = length(scatterPos),h = r - planetRadius,cosChi = dot(scatterPos, dirToSun) / r;

                Atmosphere_Output output1 = atmosphereStep(r, h, cosChi, atmospherePhaseStrength);

                totalDepth += .5f * step * output1.absorption;
                scatteredLight += step * vmul(sunColor , output1.scattering, exp(-(totalDepth + output1.inscatteringLightDepth)));
                totalDepth += .5f * step * output1.absorption;

                dst += .5f * step;
            }
            return vmul(color , exp(-totalDepth)) + scatteredLight;
        }

        public float3 frag(float3 color, float3 cameraPos, float3 viewVector)
        {
            //Get the screen depth and camera ray
            float3 rayOrigin = worldToPlanetTRS.MultiplyPoint(cameraPos);
            float3 rayDir = normalize(worldToPlanetTRS.MultiplyVector(viewVector / depthToPlanetS));


            return raymarch(color, rayOrigin, rayDir, 1e38f);
        }

    }

}