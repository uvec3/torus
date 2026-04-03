using System;
using GlmSharp;

namespace TorusSimulation
{
    public class Sphere
    {
        public static Triangle[] triangles;

        static Sphere()
        {
            buildSphere();
        }

        public static Triangle[] buildSphere(int n=3, bool fakeNormals=true)
        {
            triangles = new Triangle[4];
            //build tetrahedron
            float h = 1.4f;
            float r = (float)Math.Sqrt(1.0f-(h-0.5f)*(h-0.5f));
            vec3 a=new vec3(0,1,0);
            vec3 b=new vec3((float)Math.Cos(4*Math.PI/3.0f),1-h,(float)Math.Sin(4*Math.PI/3.0f));
            vec3 c=new vec3((float)Math.Cos(2*Math.PI/3.0f),1-h,(float)Math.Sin(2*Math.PI/3.0f));
            vec3 d=new vec3(r,1-h,0);

            a=glm.Normalized(a);
            b=glm.Normalized(b);
            c=glm.Normalized(c);
            d=glm.Normalized(d);
            vec4 colorA = new vec4(1, 1, 1, 0);
            vec4 colorB = new vec4(1, 0, 1, 1);
            vec4 colorC = new vec4(1, 1, 0, 1);
            vec4 colorD = new vec4(1, 1, 1, 1);

            triangles[0]=new Triangle(a,b,c, new vec4(1, 0, 0, 1));
            triangles[0].color_a = colorA;
            triangles[0].color_b = colorB;
            triangles[0].color_c = colorC;
            triangles[0].normal_a = a;
            triangles[0].normal_b = b;
            triangles[0].normal_c = c;

            triangles[1]=new Triangle(a,c,d, new vec4(1, 1, 1, 1));
            triangles[1].color_a = colorA;
            triangles[1].color_b = colorC;
            triangles[1].color_c = colorD;
            triangles[1].normal_a = a;
            triangles[1].normal_b = c;
            triangles[1].normal_c = d;

            triangles[2]=new Triangle(a,d,b, new vec4(1, 1, 1, 1));
            triangles[2].color_a = colorA;
            triangles[2].color_b = colorD;
            triangles[2].color_c = colorB;
            triangles[2].normal_a = a;
            triangles[2].normal_b = d;


            triangles[3]=new Triangle(c,b,d, new vec4(1, 1, 1, 1));
            triangles[3].color_a = colorC;
            triangles[3].color_b = colorB;
            triangles[3].color_c = colorD;
            triangles[3].normal_a = c;
            triangles[3].normal_b = b;
            triangles[3].normal_c = d;


            for (int k = 0; k < n; ++k)
            {
                Triangle[] newTriangles = new Triangle[triangles.Length * 4];
                for (int i = 0; i < triangles.Length; ++i)
                {
                    vec3 middle_ab = glm.Normalized((triangles[i].a + triangles[i].b) / 2);
                    vec3 middle_bc = glm.Normalized((triangles[i].b + triangles[i].c) / 2);
                    vec3 middle_ca = glm.Normalized((triangles[i].c + triangles[i].a) / 2);



                    vec4 color_ab = (triangles[i].color_a + triangles[i].color_b) / 2;
                    vec4 color_bc = (triangles[i].color_b + triangles[i].color_c) / 2;
                    vec4 color_ca = (triangles[i].color_c + triangles[i].color_a) / 2;

                    //a angle
                    newTriangles[i * 4 + 0] = new Triangle(triangles[i].a, middle_ab, middle_ca);
                    newTriangles[i * 4 + 0].color_a = triangles[i].color_a;
                    newTriangles[i * 4 + 0].color_b = color_ab;
                    newTriangles[i * 4 + 0].color_c = color_ca;

                    newTriangles[i * 4 + 0].normal_a = triangles[i].normal_a;
                    newTriangles[i * 4 + 0].normal_b = middle_ab;
                    newTriangles[i * 4 + 0].normal_c = middle_ca;

                    //b angle
                    newTriangles[i * 4 + 1] = new Triangle(triangles[i].b, middle_bc, middle_ab);
                    newTriangles[i * 4 + 1].color_a = triangles[i].color_b;
                    newTriangles[i * 4 + 1].color_b = color_bc;
                    newTriangles[i * 4 + 1].color_c = color_ab;

                    newTriangles[i * 4 + 1].normal_a = triangles[i].normal_b;
                    newTriangles[i * 4 + 1].normal_b = middle_bc;
                    newTriangles[i * 4 + 1].normal_c = middle_ab;


                    //c angle
                    newTriangles[i * 4 + 2] = new Triangle(triangles[i].c, middle_ca, middle_bc);
                    newTriangles[i * 4 + 2].color_a = triangles[i].color_c;
                    newTriangles[i * 4 + 2].color_b = color_ca;
                    newTriangles[i * 4 + 2].color_c = color_bc;

                    newTriangles[i * 4 + 2].normal_a = triangles[i].normal_c;
                    newTriangles[i * 4 + 2].normal_b = middle_ca;
                    newTriangles[i * 4 + 2].normal_c = middle_bc;

                    //center
                    newTriangles[i * 4 + 3] = new Triangle(middle_ab, middle_bc, middle_ca);
                    newTriangles[i * 4 + 3].color_a = color_ab;
                    newTriangles[i * 4 + 3].color_b = color_bc;
                    newTriangles[i * 4 + 3].color_c = color_ca;

                    newTriangles[i * 4 + 3].normal_a = middle_ab;
                    newTriangles[i * 4 + 3].normal_b = middle_bc;
                    newTriangles[i * 4 + 3].normal_c = middle_ca;

                }
                triangles = newTriangles;
            }

            if(!fakeNormals)
                Eng.CalculateNormals(triangles);
            return triangles;
        }
    }
}