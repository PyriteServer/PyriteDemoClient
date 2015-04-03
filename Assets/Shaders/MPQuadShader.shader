Shader "Custom/MPQuadShader" {  
    Properties {  
        _MainTex ("Base (RGB)", 2D) = "white" {}  
        _UVExtents ("UV Extents", Vector) = (0,0,1,1)  //xy zw == xy1 xy2

		_MainTex2 ("Base (RGB)", 2D) = "white" {}  
        _UVExtents2 ("UV Extents", Vector) = (0,0,1,1)  //xy zw == xy1 xy2

		_MainTex3 ("Base (RGB)", 2D) = "white" {}  
        _UVExtents3 ("UV Extents", Vector) = (0,0,1,1)  //xy zw == xy1 xy2

		_MainTex4 ("Base (RGB)", 2D) = "white" {}  
        _UVExtents4 ("UV Extents", Vector) = (0,0,1,1)  //xy zw == xy1 xy2
    }  
    SubShader {  
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}  
        Pass { Blend One One }
        LOD 200  
  
        CGPROGRAM  
        #pragma surface surf Lambert alpha  
  
        sampler2D _MainTex;  
        float4 _UVExtents;
  
        struct Input {  
            float2 uv_MainTex;  
        };  
  
        //this variable stores the current texture coordinates multiplied by 2  
        float2 uv_scaler;  
  
        void surf (Input IN, inout SurfaceOutput o) {  
  
  			//scaler
  			uv_scaler = float2(1.0/(_UVExtents.z-_UVExtents.x),1.0/(_UVExtents.w-_UVExtents.y));  			           
  
            //this if statement assures that the input textures won't overlap  
            if(IN.uv_MainTex.x >= _UVExtents.x && IN.uv_MainTex.x <= _UVExtents.z &&
               IN.uv_MainTex.y >= _UVExtents.y && IN.uv_MainTex.y <= _UVExtents.w)  
            {  
            	//sample the texture (Add the offset then multiply by scaler
            	half4 c0 = tex2D (_MainTex, (IN.uv_MainTex - float2(_UVExtents.x, _UVExtents.y)) * float2(uv_scaler.x, uv_scaler.y));  
            
                //sum the colors and the alpha, passing them to the Output Surface 'o'  
	            o.Albedo = c0.rgb;
	            o.Alpha = c0.a;
            }  
            else
            {
            	//o.Alpha = 0;
            }
        }  
        ENDCG  

		// pass 2
		CGPROGRAM  
        #pragma surface surf Lambert alpha  
  
        sampler2D _MainTex2;  
        float4 _UVExtents2;
  
        struct Input {  
            float2 uv_MainTex;  
        };  
  
        //this variable stores the current texture coordinates multiplied by 2  
        float2 uv_scaler;  
  
        void surf (Input IN, inout SurfaceOutput o) {  
  
  			//scaler
  			uv_scaler = float2(1.0/(_UVExtents2.z-_UVExtents2.x),1.0/(_UVExtents2.w-_UVExtents2.y));  			           
  
            //this if statement assures that the input textures won't overlap  
            if(IN.uv_MainTex.x >= _UVExtents2.x && IN.uv_MainTex.x <= _UVExtents2.z &&
               IN.uv_MainTex.y >= _UVExtents2.y && IN.uv_MainTex.y <= _UVExtents2.w)  
            {  
            	//sample the texture (Add the offset then multiply by scaler
            	half4 c0 = tex2D (_MainTex2, (IN.uv_MainTex - float2(_UVExtents2.x, _UVExtents2.y)) * float2(uv_scaler.x, uv_scaler.y));  
            
                //sum the colors and the alpha, passing them to the Output Surface 'o'  
	            o.Albedo = c0.rgb;
	            o.Alpha = c0.a;
            }  
            else
            {
            	//o.Alpha = 0;
            }
        }  
        ENDCG  

		// pass 3
		CGPROGRAM  
        #pragma surface surf Lambert alpha  
  
        sampler2D _MainTex3;  
        float4 _UVExtents3;
  
        struct Input {  
            float2 uv_MainTex;  
        };  
  
        //this variable stores the current texture coordinates multiplied by 2  
        float2 uv_scaler;  
  
        void surf (Input IN, inout SurfaceOutput o) {  
  
  			//scaler
  			uv_scaler = float2(1.0/(_UVExtents3.z-_UVExtents3.x),1.0/(_UVExtents3.w-_UVExtents3.y));  			           
  
            //this if statement assures that the input textures won't overlap  
            if(IN.uv_MainTex.x >= _UVExtents3.x && IN.uv_MainTex.x <= _UVExtents3.z &&
               IN.uv_MainTex.y >= _UVExtents3.y && IN.uv_MainTex.y <= _UVExtents3.w)  
            {  
            	//sample the texture (Add the offset then multiply by scaler
            	half4 c0 = tex2D (_MainTex3, (IN.uv_MainTex - float2(_UVExtents3.x, _UVExtents3.y)) * float2(uv_scaler.x, uv_scaler.y));  
            
                //sum the colors and the alpha, passing them to the Output Surface 'o'  
	            o.Albedo = c0.rgb;
	            o.Alpha = c0.a;
            }  
            else
            {
            	//o.Alpha = 0;
            }
        }  
        ENDCG 

		// pass 4
		CGPROGRAM  
        #pragma surface surf Lambert alpha  
  
        sampler2D _MainTex4;  
        float4 _UVExtents4;
  
        struct Input {  
            float2 uv_MainTex;  
        };  
  
        //this variable stores the current texture coordinates multiplied by 2  
        float2 uv_scaler;  
  
        void surf (Input IN, inout SurfaceOutput o) {  
  
  			//scaler
  			uv_scaler = float2(1.0/(_UVExtents4.z-_UVExtents4.x),1.0/(_UVExtents4.w-_UVExtents4.y));  			           
  
            //this if statement assures that the input textures won't overlap  
            if(IN.uv_MainTex.x >= _UVExtents4.x && IN.uv_MainTex.x <= _UVExtents4.z &&
               IN.uv_MainTex.y >= _UVExtents4.y && IN.uv_MainTex.y <= _UVExtents4.w)  
            {  
            	//sample the texture (Add the offset then multiply by scaler
            	half4 c0 = tex2D (_MainTex4, (IN.uv_MainTex - float2(_UVExtents4.x, _UVExtents4.y)) * float2(uv_scaler.x, uv_scaler.y));  
            
                //sum the colors and the alpha, passing them to the Output Surface 'o'  
	            o.Albedo = c0.rgb;
	            o.Alpha = c0.a;
            }  
            else
            {
            	//o.Alpha = 0;
            }
        }  
        ENDCG 

    }  
    FallBack "Diffuse"  
}