using System;
using System.Collections.Generic;
using GlmSharp;

namespace TorusSimulation
{
    //Builds mesh from obj file
    public class Model
    {
        public Triangle[] mesh;
        public Model(string filename, bool useNormals=true)
        {
            List<Triangle> triangles = new List<Triangle>();
            List<vec3> vertices = new List<vec3>();
            List<vec2> uv = new List<vec2>();
            List<vec3> normals = new List<vec3>();

            //read obj
            string[] lines = System.IO.File.ReadAllLines(filename);

            for(int i=0;i<lines.Length;++i)
            {
                string[] tokens = lines[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if(tokens.Length==0)
                    continue;
                if(tokens[0]=="v")
                {
                    vertices.Add(new vec3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3])));
                }
                else if(tokens[0]=="vt")
                {
                    uv.Add(new vec2(float.Parse(tokens[1]), float.Parse(tokens[2])));
                }
                else if(tokens[0]=="vn")
                {
                    normals.Add(new vec3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3])));
                }
                else if(tokens[0]=="f")
                {
                    for (int j = 2; j < tokens.Length; ++j)
                    {
                        Triangle t = new Triangle();

                        t.a = vertices[int.Parse(tokens[1].Split('/')[0]) - 1];
                        t.b = vertices[int.Parse(tokens[j-1].Split('/')[0]) - 1];
                        t.c = vertices[int.Parse(tokens[j].Split('/')[0]) - 1];

                        // t.uv_a = uv[int.Parse(tokens[1].Split('/')[1])-1];
                        // t.uv_b = uv[int.Parse(tokens[j-1].Split('/')[1])-1];
                        // t.uv_c = uv[int.Parse(tokens[j].Split('/')[1])-1];

                        t.normal_a = normals[int.Parse(tokens[1].Split('/')[2]) - 1];
                        t.normal_b = normals[int.Parse(tokens[j-1].Split('/')[2]) - 1];
                        t.normal_c = normals[int.Parse(tokens[j].Split('/')[2]) - 1];

                        t.color_a = vec4.Ones;
                        t.color_b = vec4.Ones;
                        t.color_c = vec4.Ones;

                        triangles.Add(t);
                    }
                }
            }

            mesh = triangles.ToArray();
            if(!useNormals)
                Eng.CalculateNormals(mesh);
        }
    }
}