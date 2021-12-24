using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace fzmnm
{
    class AtmosphericShaderEmulator
    {
        //Planet Geometry
        Matrix4x4 worldToPlanetTRS;
        float depthToPlanetS;
        float planetRadius;
        float atmosphereRadius;

        //Sun Light
        Vector3 dirToSun;
        Vector3 sunColor;

        //Atmosphere
        Vector3 atmosphereRayleighScattering;
        Vector3 atmosphereRayleighAbsorption;
        Vector3 atmosphereRayleighPhaseCoeff;
        float atmosphereRayleighScaleHeight;

        Vector3 atmosphereMieScattering;
        Vector3 atmosphereMieAbsorption;
        Vector3 atmosphereMiePhaseCoeff;
        float atmosphereMieScaleHeight;

        Vector3 atmosphereOzoneAbsorption;
        float atmosphereOzoneMinRadius;
        float atmosphereOzoneMaxRadius;
        public bool includeMieInscattering = true;

        //LightMarch Parameters
        float fixedStepLength;
        int fixedStepNum;
        int totalStepNum;

        //PostProcessing
        float toneMappingExposure;
        Matrix4x4 spectralColor2RGB;
        Matrix4x4 RGB2spectralColor;

        public void ReadMaterial(Material mat)
        {
            worldToPlanetTRS = mat.GetMatrix("worldToPlanetTRS");
            depthToPlanetS = mat.GetFloat("depthToPlanetS");
            planetRadius = mat.GetFloat("planetRadius");
            atmosphereRadius = mat.GetFloat("atmosphereRadius");

            dirToSun = mat.GetVector("dirToSun");
            sunColor = mat.GetVector("sunColor");

            atmosphereRayleighScattering = mat.GetVector("atmosphere_rayleighScattering");
            atmosphereRayleighAbsorption = mat.GetVector("atmosphere_rayleighAbsorption");
            atmosphereRayleighPhaseCoeff = mat.GetVector("atmosphere_rayleighPhaseCoeff");
            atmosphereRayleighScaleHeight = mat.GetFloat("atmosphere_rayleighScaleHeight");

            atmosphereMieScattering = mat.GetVector("atmosphere_mieScattering");
            atmosphereMieAbsorption = mat.GetVector("atmosphere_mieAbsorption");
            atmosphereMiePhaseCoeff = mat.GetVector("atmosphere_miePhaseCoeff");
            atmosphereMieScaleHeight = mat.GetFloat("atmosphere_mieScaleHeight");

            atmosphereOzoneAbsorption = mat.GetVector("atmosphere_ozoneAbsorption");
            atmosphereOzoneMinRadius = mat.GetFloat("atmosphere_ozoneMinRadius");
            atmosphereOzoneMaxRadius = mat.GetFloat("atmosphere_ozoneMaxRadius");

            fixedStepLength = mat.GetFloat("fixedStepLength");
            fixedStepNum = mat.GetInt("fixedStepNum");
            totalStepNum = mat.GetInt("totalStepNum");

            toneMappingExposure = mat.GetFloat("toneMappingExposure");
            spectralColor2RGB = mat.GetMatrix("spectralColor2RGB");
            RGB2spectralColor = mat.GetMatrix("RGB2spectralColor");
        }


        Vector3 exp(Vector3 v) => new Vector3(Mathf.Exp(v.x), Mathf.Exp(v.y), Mathf.Exp(v.z));
        float step(float a, float b) => a < b ? 1 : 0;

        float rayleighPhaseFunction(float cosTh, Vector3 rayleighPhaseCoeff)
        {
            return rayleighPhaseCoeff.x + cosTh * (rayleighPhaseCoeff.y + cosTh * (rayleighPhaseCoeff.z));
        }
        float miePhaseFunction(float cosTh, Vector3 miePhaseCoeff)
        {
            return Mathf.Clamp(miePhaseCoeff.x * Mathf.Pow(miePhaseCoeff.y - miePhaseCoeff.z * cosTh, -1.5f), 0, 100);
        }

        float chapman(float x, float cosChi)
        {
            //http://www.thetenthplanet.de/archives/4519
            float c = Mathf.Sqrt(1.57079632679f * x);
            if (cosChi >= 0)
                return c / ((c - 1) * cosChi + 1);
            else
            {
                float sinChi = Mathf.Sqrt(Mathf.Clamp01(1 - cosChi * cosChi));
                return c / ((c - 1) * cosChi - 1) + 2 * c * Mathf.Exp(x - x * sinChi) * Mathf.Sqrt(sinChi);
            }
        }

        Vector2 raySphere(float R, float rSquare, float rCosChi)
        {
            //R: planet radius
            //r: dist to planet center
            //cosChi: angle between ray and local zenith
            float b = 2 * rCosChi;
            float c = rSquare - R * R;
            float d = b * b - 4 * c;
            if (d > 0)
            {
                float s = Mathf.Sqrt(d);
                float dstToSphereNear = Mathf.Max(0, (-b - s) / 2);
                float dstToSphereFar = (-b + s) / 2;
                if (dstToSphereFar >= 0) return new Vector2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
            }
            return new Vector2(0, 0);
        }

        struct Atmosphere_Output
        {
            public Vector3 scattering;
            public Vector3 absorption;
            public Vector3 inscatteringLightDepth;
        };

        Atmosphere_Output atmosphereStep(float r, float h, float cosChi, float rayleighPhaseStrength, float miePhaseStrength)
        {

            //cosChi: angle between dirToLight and local zenith

            Atmosphere_Output output;


            float rayleighExp = Mathf.Exp(-h / atmosphereRayleighScaleHeight);
            float mieExp = Mathf.Exp(-h / atmosphereMieScaleHeight);
            float ozoneExistence = step(atmosphereOzoneMinRadius, r) - step(atmosphereOzoneMaxRadius, r);

            //absorption at this point
            output.absorption = rayleighExp * atmosphereRayleighAbsorption + mieExp * atmosphereMieAbsorption + ozoneExistence * atmosphereOzoneAbsorption;

            //get the depth of inscattering lights
            output.inscatteringLightDepth =
                 atmosphereRayleighAbsorption * rayleighExp * atmosphereRayleighScaleHeight * chapman(r / atmosphereRayleighScaleHeight, cosChi)
                + atmosphereMieAbsorption * mieExp * atmosphereMieScaleHeight * chapman(r / atmosphereMieScaleHeight, cosChi)
                + atmosphereOzoneAbsorption * (raySphere(atmosphereOzoneMaxRadius, r * r, r * cosChi).y - raySphere(atmosphereOzoneMinRadius, r * r, r * cosChi).y);

            output.scattering =
                 rayleighExp * atmosphereRayleighScattering * rayleighPhaseStrength
                + mieExp * atmosphereMieScattering * miePhaseStrength;

            return output;
        }

        Vector3 raymarch(Vector3 color, Vector3 rayOrigin, Vector3 rayDir, float rayLength)
        {

            Vector3 totalDepth = Vector3.zero;
            Vector3 scatteredLight = Vector3.zero;

            float cosTh = Vector3.Dot(rayDir, dirToSun);
            float rayleighPhaseStrength = rayleighPhaseFunction(cosTh, atmosphereRayleighPhaseCoeff);
            float miePhaseStrength = miePhaseFunction(cosTh, atmosphereMiePhaseCoeff);
            if (!includeMieInscattering) miePhaseStrength = 0;

            float step = Mathf.Min(fixedStepLength, rayLength / totalStepNum);
            float longStep = (rayLength - step * fixedStepNum) / (totalStepNum - fixedStepNum);
            float dst = 0;
            for (int i = 0; i < totalStepNum; ++i)
            {
                if (i >= fixedStepNum)
                    step = longStep;

                dst += step / 2;

                Vector3 scatterPos = rayOrigin + rayDir * dst;

                float r = Vector3.Magnitude(scatterPos);
                float h = r - planetRadius;
                float cosChi = Vector3.Dot(scatterPos, dirToSun) / r;

                Atmosphere_Output output1 = atmosphereStep(r, h, cosChi, rayleighPhaseStrength, miePhaseStrength);

                totalDepth += .5f * step * output1.absorption;
                scatteredLight += step * Vector3.Scale(Vector3.Scale(sunColor, output1.scattering), exp(-(totalDepth + output1.inscatteringLightDepth)));
                totalDepth += .5f * step * output1.absorption;

                dst += step / 2;
            }
            return Vector3.Scale(color, exp(-totalDepth)) + scatteredLight;
        }

        public Vector3 frag(Vector3 color, Vector3 cameraPos, Vector3 viewVector)
        {
            //Get the screen depth and camera ray
            Vector3 rayOrigin = worldToPlanetTRS.MultiplyPoint(cameraPos);
            Vector3 rayDir = Vector3.Normalize(worldToPlanetTRS.MultiplyVector(viewVector / depthToPlanetS));

            //Intersect the ray to the atmosphere
            Vector2 hitInfo = raySphere(atmosphereRadius, Vector3.Dot(rayOrigin, rayOrigin), Vector3.Dot(rayDir, rayOrigin));
            float dstToAtmosphere = hitInfo.x;
            float dstThroughAtmosphere = hitInfo.y;

            return raymarch(color, rayOrigin + rayDir * dstToAtmosphere, rayDir, dstThroughAtmosphere);
        }

    }

}