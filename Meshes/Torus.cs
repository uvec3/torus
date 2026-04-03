using System;
using GlmSharp;

namespace TorusSimulation
{
    public class Torus
    {
        public static Triangle[] BuildTor(float outerR, float innerR, int n=10, bool fakeNormals=true)
        {
            Triangle[] triangles = new Triangle[n*n*2];

            float R = (outerR + innerR) / 2;
            float r = (outerR - innerR) / 2;
            int t = -1;

            float da = 2 * (float)Math.PI / n;
            float dA = 2 * (float)Math.PI / n;

            mat3 MR = new mat3(
                (float)Math.Cos(dA), 0, (float)Math.Sin(dA),
                0, 1, 0,
                -(float)Math.Sin(dA), 0, (float)Math.Cos(dA)
            ).Transposed;

            mat3 mR = new mat3(
                (float)Math.Cos(da), -(float)Math.Sin(da), 0,
                (float)Math.Sin(da), (float)Math.Cos(da), 0,
                0, 0, 1
            ).Transposed;


            mat3 M0 = mat3.Identity;
            vec3 c0=new vec3(R,0,0);

            vec4 color0 = new vec4(1, 1, 1, 1);
            for (int i = 0; i < n; ++i)
            {
                float d=2*(float)Math.PI*i/n;

                vec4 color1 = new vec4(glm.Cos(d)/2+1, glm.Cos(d*n/2)/2+1, 1, 1);

                mat3 M1 = MR * M0;
                vec3 c1 = MR * c0;
                vec3 v0=new vec3(1,0,0);
                vec3 v1;
                for (int j = 0; j < n; ++j)
                {
                    v1=mR*v0;

                    vec3 a0 = M0 * v0;
                    vec3 b0 = M1 * v0;
                    vec3 a1=M0*v1;
                    vec3 b1=M1*v1;

                    triangles[++t] = new Triangle(c0+a0*r,c1+b1*r,c0+a1*r);
                    triangles[t].color_a = color1;
                    triangles[t].color_b = color1;
                    triangles[t].color_c = color1;
                    triangles[t].normal_a = a0;
                    triangles[t].normal_b = b1;
                    triangles[t].normal_c = a1;

                    triangles[++t] = new Triangle(c0+a0*r,c1+b0*r,c1+b1*r);
                    triangles[t].color_a = color1;
                    triangles[t].color_b = color1;
                    triangles[t].color_c = color1;
                    triangles[t].normal_a = a0;
                    triangles[t].normal_b = b0;
                    triangles[t].normal_c = b1;

                    v0=v1;
                }
                M0 = M1;
                c0 = c1;
                color0 = color1;
            }

            if(!fakeNormals)
                Eng.CalculateNormals(triangles);
            return triangles;
        }
    }
}