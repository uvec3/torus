using System;
using GlmSharp;

namespace TorusSimulation
{
    public class Cylinder
    {
        public static Triangle[] triangles;

        static Cylinder()
        {
            buildCylinder();
        }

        public static Triangle[] buildCylinder(int n=10, bool fakeNormals=true)
        {
            vec4 color = new vec4(1.0f, 1.0f, 1.0f, 1.0f);

            triangles = new Triangle[(n-1)*2+n*2];
            int ti = -1;

            float da = 2*(float)Math.PI/n;
            float sinDa = (float)Math.Sin(da);
            float cosDa = (float)Math.Cos(da);
            mat3 rot = new mat3(cosDa, 0, sinDa,
                                0 , 1, 0,
                                -sinDa, 0, cosDa);

            //bottom
            vec3 p0 = new vec3(1, 0, 0);
            vec3 p1 = p0;
            vec3 p2 = rot*p1;
            for(int i=2; i<n; i++)
            {
                p1 = p2;
                p2 = rot*p2;
                triangles[++ti] = new Triangle(p0, p1, p2);
                triangles[ti].color_a = color;
                triangles[ti].color_b = color;
                triangles[ti].color_c = color;
                triangles[ti].normal_a = new vec3(0, -1, 0);
            }

            //top and sides
            rot = new mat3(cosDa, 0, -sinDa,
                0 , 1, 0,
                sinDa, 0, cosDa);
            p0 = new vec3(1, 1, 0);
            p1 = p0;
            p2 = rot*p1;
            vec3 down = new vec3(0, -1, 0);
            for(int i=2; i<n; i++)
            {
                p1 = p2;
                p2 = rot*p2;
                triangles[++ti] = new Triangle(p0, p1, p2);
                triangles[ti].color_a = color;
                triangles[ti].color_b = color;
                triangles[ti].color_c = color;
                triangles[ti].normal_a = new vec3(0, 1, 0);

                triangles[++ti] = new Triangle(p1,p1+down, p2);
                triangles[ti].color_a = color;
                triangles[ti].color_b = color;
                triangles[ti].color_c = color;
                triangles[ti].normal_a = p1+down;
                triangles[ti].normal_b = p1+down;
                triangles[ti].normal_c = p2+down;

                triangles[++ti] = new Triangle(p1+down, p2+down, p2);
                triangles[ti].color_a = color;
                triangles[ti].color_b = color;
                triangles[ti].color_c = color;
                triangles[ti].normal_a = p1+down;
                triangles[ti].normal_b = p2+down;
                triangles[ti].normal_c = p2+down;
            }

            p1 = p2;
            p2 = rot*p2;

            triangles[++ti] = new Triangle(p1,p1+down, p2);
            triangles[ti].color_a = color;
            triangles[ti].color_b = color;
            triangles[ti].color_c = color;
            triangles[ti].normal_a = p1+down;
            triangles[ti].normal_b = p1+down;
            triangles[ti].normal_c = p2+down;

            triangles[++ti] = new Triangle(p1+down, p2+down, p2);
            triangles[ti].color_a = color;
            triangles[ti].color_b = color;
            triangles[ti].color_c = color;
            triangles[ti].normal_a = p1+down;
            triangles[ti].normal_b = p2+down;
            triangles[ti].normal_c = p2+down;

            p1 = p2;
            p2 = rot*p2;

            triangles[++ti] = new Triangle(p1,p1+down, p2);
            triangles[ti].color_a = color;
            triangles[ti].color_b = color;
            triangles[ti].color_c = color;
            triangles[ti].normal_a = p1+down;
            triangles[ti].normal_b = p1+down;
            triangles[ti].normal_c = p2+down;

            triangles[++ti] = new Triangle(p1+down, p2+down, p2);
            triangles[ti].color_a = color;
            triangles[ti].color_b = color;
            triangles[ti].color_c = color;
            triangles[ti].normal_a = p1+down;
            triangles[ti].normal_b = p2+down;
            triangles[ti].normal_c = p2+down;

            if(!fakeNormals)
                Eng.CalculateNormals(triangles);
            return triangles;
        }
    }
}