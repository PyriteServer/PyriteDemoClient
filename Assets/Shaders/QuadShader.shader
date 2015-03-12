Shader "Custom/QuadShader" {  
    Properties {  
        _MainTex ("Base (RGB)", 2D) = "white" {}  
        _UVExtents ("UV Extents", Vector) = (0,0,1,1)  //xy zw == xy1 xy2
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
    }  
    FallBack "Diffuse"  
}