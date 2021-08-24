// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "CheapCloud"
{
	Properties
	{
		_NoiseTexture3D("NoiseTexture3D", 3D) = "white" {}
		_Emission("Emission", Color) = (1,1,1,1)
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf Unlit alpha:fade keepalpha noshadow exclude_path:deferred 
		struct Input
		{
			float3 viewDir;
		};

		uniform float4 _Emission;
		uniform sampler3D _NoiseTexture3D;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			o.Emission = _Emission.rgb;
			float4 appendResult60 = (float4(_WorldSpaceCameraPos , 1.0));
			float4 transform12 = mul(unity_WorldToObject,appendResult60);
			float4 break62 = transform12;
			float4 appendResult63 = (float4(break62.x , break62.y , break62.z , 0.0));
			float4 ViewPos20 = appendResult63;
			float4 transform41 = mul(unity_WorldToObject,float4( i.viewDir , 0.0 ));
			float4 normalizeResult64 = normalize( transform41 );
			float4 ViewDir21 = normalizeResult64;
			float dotResult28 = dot( ViewPos20 , ViewDir21 );
			float4 IntersectPos26 = ( ViewPos20 - ( dotResult28 * ViewDir21 ) );
			float4 temp_output_57_0 = (float4( 0,0,0,0 ) + (IntersectPos26 - float4( -0.5,-0.5,-0.5,-0.5 )) * (float4( 1,1,1,1 ) - float4( 0,0,0,0 )) / (float4( 0.5,0.5,0.5,0.5 ) - float4( -0.5,-0.5,-0.5,-0.5 )));
			o.Alpha = tex3D( _NoiseTexture3D, temp_output_57_0.xyz ).a;
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18912
0;1087;1920;1006;603.5521;593.2669;1.183983;True;True
Node;AmplifyShaderEditor.WorldSpaceCameraPos;14;-1553.167,-460.6248;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DynamicAppendNode;60;-1242.985,-349.1914;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;1;False;1;FLOAT4;0
Node;AmplifyShaderEditor.WorldToObjectTransfNode;12;-1024.887,-376.9214;Inherit;False;1;0;FLOAT4;0,0,0,1;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;19;-1536.676,-242.6011;Inherit;False;World;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.BreakToComponentsNode;62;-802.2838,-392.0913;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.WorldToObjectTransfNode;41;-1281.304,-187.9097;Inherit;False;1;0;FLOAT4;0,0,0,1;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NormalizeNode;64;-1004.369,-60.9101;Inherit;False;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DynamicAppendNode;63;-646.284,-386.8914;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;20;-416.2944,-382.542;Inherit;False;ViewPos;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;21;-631.6647,-157.9647;Inherit;False;ViewDir;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;30;-1505.7,79.14973;Inherit;False;20;ViewPos;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;31;-1508.558,190.7651;Inherit;False;21;ViewDir;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DotProductOpNode;28;-1232.375,165.6032;Inherit;False;2;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;29;-1076.431,213.5047;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;37;-870.5051,165.6899;Inherit;False;2;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;26;-609.5237,146.327;Inherit;False;IntersectPos;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;43;-435.0144,492.4179;Inherit;True;26;IntersectPos;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TFHCRemapNode;57;-179.4597,478.7167;Inherit;False;5;0;FLOAT4;0,0,0,0;False;1;FLOAT4;-0.5,-0.5,-0.5,-0.5;False;2;FLOAT4;0.5,0.5,0.5,0.5;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;1,1,1,1;False;1;FLOAT4;0
Node;AmplifyShaderEditor.WorldPosInputsNode;56;-177.5582,196.0689;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SamplerNode;55;268.2906,371.584;Inherit;True;Property;_NoiseTexture3D;NoiseTexture3D;0;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;LockedToTexture3D;False;Object;-1;Auto;Texture3D;8;0;SAMPLER3D;;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;69;930.2512,-113.3377;Inherit;False;Property;_Emission;Emission;1;0;Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TFHCRemapNode;67;56.1852,622.7181;Inherit;False;5;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;2;FLOAT4;1,1,1,1;False;3;FLOAT4;-1,-1,-1,0;False;4;FLOAT4;2,2,2,1;False;1;FLOAT4;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;1201.741,-13.31422;Float;False;True;-1;2;ASEMaterialInspector;0;0;Unlit;CheapCloud;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Transparent;0.1;True;False;0;False;Transparent;;Transparent;ForwardOnly;16;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;False;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;60;0;14;0
WireConnection;12;0;60;0
WireConnection;62;0;12;0
WireConnection;41;0;19;0
WireConnection;64;0;41;0
WireConnection;63;0;62;0
WireConnection;63;1;62;1
WireConnection;63;2;62;2
WireConnection;20;0;63;0
WireConnection;21;0;64;0
WireConnection;28;0;30;0
WireConnection;28;1;31;0
WireConnection;29;0;28;0
WireConnection;29;1;31;0
WireConnection;37;0;30;0
WireConnection;37;1;29;0
WireConnection;26;0;37;0
WireConnection;57;0;43;0
WireConnection;55;1;57;0
WireConnection;67;0;57;0
WireConnection;0;2;69;0
WireConnection;0;9;55;4
ASEEND*/
//CHKSM=0511453A6EA6D1C08963EB2ACD4B84AA68586706