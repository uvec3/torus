//Simple software renderer in C# using WPF bitmap as output and GlmSharp for math operations.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlmSharp;

namespace TorusSimulation
{
    public class Camera
    {
        public float near =1f;
        public vec2 size= new vec2(1f, 1f);
        public Transform viewTransform = new Transform();

        public Camera()
        {
            viewTransform.translation = new vec3(0, 0, -5);
            viewTransform.rotation = mat3.Identity;
        }
    }

    public struct Triangle
    {
        public vec3 a;
        public vec3 b;
        public vec3 c;
        public vec4 color_a;
        public vec4 color_b;
        public vec4 color_c;
        public vec3 normal_a;
        public vec3 normal_b;
        public vec3 normal_c;

        public Triangle(vec3 a, vec3 b, vec3 c, vec4 color= new vec4(), vec3 normal= new vec3())
        {
            this.a = a;
            this.b = b;
            this.c = c;
            color_a= color_b= color_c= color;
            normal_a= normal_b= normal_c= normal;
        }

        public vec4 Color{
            set => color_a= color_b= color_c = value;
        }
    }

    public struct Line
    {
        public vec3 a;
        public vec3 b;
        float width;

        public Line(vec3 a, vec3 b, float width=1.0f)
        {
            this.a = a;
            this.b = b;
            this.width = width;
        }
    }

    public class Light
    {
        public vec3 position;
        public float intensity;

        public float[] shadowMap;
        public mat4 lightProjectionMatrix;
    }

    public struct Transform
    {
        public vec3 translation;
        public mat3 rotation;

        public static readonly Transform identity = new Transform(mat3.Identity, new vec3(0));


        public Transform(Transform transform)
        {
            this.translation = transform.translation;
            this.rotation = transform.rotation;
        }
        public Transform( mat3 rotation, vec3 translation)
        {
            this.translation = translation;
            this.rotation = rotation;
        }

        public Transform(mat3 rotation)
        {
            this.translation = new vec3(0);
            this.rotation = rotation;
        }

        public Transform( vec3 translation)
        {
            this.translation = translation;
            this.rotation = mat3.Identity;
        }
        
        public void Rotate(vec3 axis, float angle)
        {
            var rot= Eng.rotateBasis(mat3.Identity, axis, angle);
            rotation = rot * rotation;
            translation = rot * translation;
        }


        public static Transform operator *(Transform t1, Transform t2)
        {
            Transform result = new Transform
            {
                rotation = t1.rotation * t2.rotation,
                translation = t1.translation + t1.rotation * t2.translation
            };
            return result;
        }
    }

    public enum Face
    {
        Front,
        Back,
        Both
    }

    public class Eng: Image
    {

        public vec4 clearColor= new vec4(0, 0, 0, 1);
        public Face showFace = Face.Both;
        public bool enableLighting = true;
        public readonly List<Light> LightSources = new List<Light>();
        public float ambientLight = 0.2f;
        public readonly Camera camera;
        public Transform ViewTransform
        {
            get => camera.viewTransform;
            set => camera.viewTransform = value;
        }


        private int width = 1024;
        private int height= 1024;
        private byte[] frameBuffer;
        private float[] depthBuffer;
        private vec3[] lightsInViewSpace;
        private WriteableBitmap bmp;
        private vec2 halfExtent;

        public Eng()
        {
            camera = new Camera();
            camera.size = new vec2(0.2f, 0.2f);
            camera.near = 0.4f;
            SetExtent(width, height);
        }

        public void SetExtent(int w, int h)
        {
            width = w;
            height = h;
            halfExtent = new vec2(width/2f, height/2f);

            bmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            frameBuffer = new byte[width * height*4];
            depthBuffer = new float[width * height];
            this.Source = bmp;
            camera.size = new vec2((float)width/(float)height*camera.size.y, camera.size.y);
        }


        public void Render(Triangle[] triangles, Transform transform)
        {
            Render(triangles, transform.rotation, transform.translation);
        }

        public void Render(Triangle[] triangles, mat3 rotation, vec3 translation)
        {
            lightsInViewSpace = new vec3[LightSources.Count];
            for (int i = 0; i < LightSources.Count; ++i)
                lightsInViewSpace[i] = camera.viewTransform.rotation * LightSources[i].position + camera.viewTransform.translation;

            //apply view transform
            rotation =  camera.viewTransform.rotation*rotation;
            translation = camera.viewTransform.translation + camera.viewTransform.rotation * translation;

            for(int i = 0; i < triangles.Length; ++i)
            {
                Triangle transformedTriangle = new Triangle();

                transformedTriangle.a = rotation * triangles[i].a + translation;
                transformedTriangle.b = rotation * triangles[i].b + translation;
                transformedTriangle.c = rotation * triangles[i].c + translation;


                //clipping against near plane
                if ((transformedTriangle.a.z > -camera.near || transformedTriangle.b.z > -camera.near ||//partially behind camera
                    transformedTriangle.c.z > -camera.near))
                {
                    //completely behind camera
                    if(transformedTriangle.a.z>-camera.near && transformedTriangle.b.z>-camera.near &&
                       transformedTriangle.c.z>-camera.near)
                        continue;//skip triangle

                    transformedTriangle.normal_a= rotation * triangles[i].normal_a;
                    transformedTriangle.normal_b= rotation * triangles[i].normal_b;
                    transformedTriangle.normal_c= rotation * triangles[i].normal_c;

                    transformedTriangle.color_a= triangles[i].color_a;
                    transformedTriangle.color_b= triangles[i].color_b;
                    transformedTriangle.color_c= triangles[i].color_c;

                    //2 points behind camera
                    if (transformedTriangle.a.z >= -camera.near && transformedTriangle.b.z >= -camera.near &&//ab behind camera
                        transformedTriangle.c.z < -camera.near) //replace a&b
                    {
                        var newA= IntersectionWithCameraPlane(transformedTriangle.a, transformedTriangle.c);
                        var newB= IntersectionWithCameraPlane(transformedTriangle.b, transformedTriangle.c);
                        var bary_a= GetBarycentricCoordinates(transformedTriangle.a, transformedTriangle.b, transformedTriangle.c, newA);
                        var bary_b= GetBarycentricCoordinates(transformedTriangle.a, transformedTriangle.b, transformedTriangle.c, newB);
                        var newColorA= bary_a.x * transformedTriangle.color_a + bary_a.y * transformedTriangle.color_b + bary_a.z * transformedTriangle.color_c;
                        var newColorB= bary_b.x * transformedTriangle.color_a + bary_b.y * transformedTriangle.color_b + bary_b.z * transformedTriangle.color_c;
                        var newNormal_a= bary_a.x * transformedTriangle.normal_a + bary_a.y * transformedTriangle.normal_b + bary_a.z * transformedTriangle.normal_c;
                        var newNormal_b= bary_b.x * transformedTriangle.normal_a + bary_b.y * transformedTriangle.normal_b + bary_b.z * transformedTriangle.normal_c;
                        transformedTriangle.color_a= newColorA;
                        transformedTriangle.color_b= newColorB;
                        transformedTriangle.normal_a= newNormal_a;
                        transformedTriangle.normal_b= newNormal_b;
                        transformedTriangle.a= newA;
                        transformedTriangle.b= newB;
                    }
                    else if (transformedTriangle.a.z >= -camera.near && transformedTriangle.c.z >= -camera.near &&//ac behind camera
                        transformedTriangle.b.z < -camera.near) //replace a&c
                    {
                        var newA= IntersectionWithCameraPlane(transformedTriangle.a, transformedTriangle.b);
                        var newC= IntersectionWithCameraPlane(transformedTriangle.c, transformedTriangle.b);
                        var bary_a= GetBarycentricCoordinates(transformedTriangle.a, transformedTriangle.b, transformedTriangle.c, newA);
                        var bary_c= GetBarycentricCoordinates(transformedTriangle.a, transformedTriangle.b, transformedTriangle.c, newC);
                        var newColorA= bary_a.x * transformedTriangle.color_a + bary_a.y * transformedTriangle.color_b + bary_a.z * transformedTriangle.color_c;
                        var newColorC= bary_c.x * transformedTriangle.color_a + bary_c.y * transformedTriangle.color_b + bary_c.z * transformedTriangle.color_c;
                        var newNormal_a= bary_a.x * transformedTriangle.normal_a + bary_a.y * transformedTriangle.normal_b + bary_a.z * transformedTriangle.normal_c;
                        var newNormal_c= bary_c.x * transformedTriangle.normal_a + bary_c.y * transformedTriangle.normal_b + bary_c.z * transformedTriangle.normal_c;
                        transformedTriangle.color_a= newColorA;
                        transformedTriangle.color_c= newColorC;
                        transformedTriangle.normal_a= newNormal_a;
                        transformedTriangle.normal_c= newNormal_c;
                        transformedTriangle.a= newA;
                        transformedTriangle.c= newC;
                    }
                    else if (transformedTriangle.b.z >= -camera.near && transformedTriangle.c.z >= -camera.near &&//bc behind camera
                        transformedTriangle.a.z < -camera.near) //replace b&c
                    {
                        var newB= IntersectionWithCameraPlane(transformedTriangle.b, transformedTriangle.a);
                        var newC= IntersectionWithCameraPlane(transformedTriangle.c, transformedTriangle.a);
                        var bary_b= GetBarycentricCoordinates(transformedTriangle.a, transformedTriangle.b, transformedTriangle.c, newB);
                        var bary_c= GetBarycentricCoordinates(transformedTriangle.a, transformedTriangle.b, transformedTriangle.c, newC);
                        var newColorB= bary_b.x * transformedTriangle.color_a + bary_b.y * transformedTriangle.color_b + bary_b.z * transformedTriangle.color_c;
                        var newColorC= bary_c.x * transformedTriangle.color_a + bary_c.y * transformedTriangle.color_b + bary_c.z * transformedTriangle.color_c;
                        var newNormal_b= bary_b.x * transformedTriangle.normal_a + bary_b.y * transformedTriangle.normal_b + bary_b.z * transformedTriangle.normal_c;
                        var newNormal_c= bary_c.x * transformedTriangle.normal_a + bary_c.y * transformedTriangle.normal_b + bary_c.z * transformedTriangle.normal_c;
                        transformedTriangle.color_b= newColorB;
                        transformedTriangle.color_c= newColorC;
                        transformedTriangle.normal_b= newNormal_b;
                        transformedTriangle.normal_c= newNormal_c;
                        transformedTriangle.b= newB;
                        transformedTriangle.c= newC;
                    }
                    //1 point behind camera
                    else
                    {
                        //create additional triangle to fill quadrangle
                        Triangle additionalTriangle = new Triangle();
                        if (transformedTriangle.b.z >= -camera.near) //b behind
                        {
                            //swap
                            (transformedTriangle.a, transformedTriangle.b, transformedTriangle.c) = (transformedTriangle.b, transformedTriangle.c, transformedTriangle.a);
                            (transformedTriangle.color_a, transformedTriangle.color_b, transformedTriangle.color_c) = (transformedTriangle.color_b, transformedTriangle.color_c, transformedTriangle.color_a);
                            (transformedTriangle.normal_a, transformedTriangle.normal_b, transformedTriangle.normal_c) = (transformedTriangle.normal_b, transformedTriangle.normal_c, transformedTriangle.normal_a);
                        }
                        else if (transformedTriangle.c.z >= -camera.near) //c behind
                        {
                            //swap
                            (transformedTriangle.a, transformedTriangle.b, transformedTriangle.c) = (transformedTriangle.c, transformedTriangle.a, transformedTriangle.b);
                            (transformedTriangle.color_a, transformedTriangle.color_b, transformedTriangle.color_c) = (transformedTriangle.color_c, transformedTriangle.color_a, transformedTriangle.color_b);
                            (transformedTriangle.normal_a, transformedTriangle.normal_b, transformedTriangle.normal_c) = (transformedTriangle.normal_c, transformedTriangle.normal_a, transformedTriangle.normal_b);
                        }

                        vec3 ac_intersection= IntersectionWithCameraPlane(transformedTriangle.a, transformedTriangle.c);
                        vec3 ab_intersection= IntersectionWithCameraPlane(transformedTriangle.a, transformedTriangle.b);

                        vec3 ac_i_barycentric= GetBarycentricCoordinates(transformedTriangle.a, transformedTriangle.b, transformedTriangle.c, ac_intersection);
                        vec3 ab_i_barycentric= GetBarycentricCoordinates(transformedTriangle.a, transformedTriangle.b, transformedTriangle.c, ab_intersection);


                        additionalTriangle.a= ac_intersection;
                        additionalTriangle.b= ab_intersection;
                        additionalTriangle.c= transformedTriangle.b;

                        additionalTriangle.color_a= lerp(transformedTriangle.color_a,transformedTriangle.color_b,transformedTriangle.color_c, ac_i_barycentric);
                        additionalTriangle.color_b= lerp(transformedTriangle.color_a,transformedTriangle.color_b,transformedTriangle.color_c, ab_i_barycentric);
                        additionalTriangle.color_c= transformedTriangle.color_b;

                        additionalTriangle.normal_a= lerp(transformedTriangle.normal_a,transformedTriangle.normal_b,transformedTriangle.normal_c, ac_i_barycentric);
                        additionalTriangle.normal_b=  lerp(transformedTriangle.normal_a,transformedTriangle.normal_b,transformedTriangle.normal_c, ab_i_barycentric);
                        additionalTriangle.normal_c= transformedTriangle.normal_b;

                        RasterizeTriangle(additionalTriangle);

                        transformedTriangle.a= additionalTriangle.a;
                        transformedTriangle.color_a= additionalTriangle.color_a;
                        transformedTriangle.normal_a= additionalTriangle.normal_a;
                    }
                    
                    RasterizeTriangle(transformedTriangle);
                }
                else
                {
                    transformedTriangle.normal_a= rotation * triangles[i].normal_a;
                    transformedTriangle.normal_b= rotation * triangles[i].normal_b;
                    transformedTriangle.normal_c= rotation * triangles[i].normal_c;

                    transformedTriangle.color_a= triangles[i].color_a;
                    transformedTriangle.color_b= triangles[i].color_b;
                    transformedTriangle.color_c= triangles[i].color_c;

                    RasterizeTriangle(transformedTriangle);
                }
            }
        }


        private void RasterizeTriangle(Triangle triangle)
        {
            vec3 realNormal= glm.Cross(triangle.b - triangle.a, triangle.c - triangle.a);

            if(showFace == Face.Front&& glm.Dot(triangle.a,realNormal)>0)
                return;
            if(showFace == Face.Back&& glm.Dot(triangle.a,realNormal)<0)
                return;

            triangle.normal_a= glm.Normalized(triangle.normal_a);
            triangle.normal_b= glm.Normalized(triangle.normal_b);
            triangle.normal_c= glm.Normalized(triangle.normal_c);

            vec2 left = transformToScreenCoordinates(Project(triangle.a));
            vec2 middle = transformToScreenCoordinates(Project(triangle.b));
            vec2 right = transformToScreenCoordinates(Project(triangle.c));

            mat2 FromTriangle = new mat2(middle- left, right - left);
            mat2 ToTriangle = FromTriangle.Inverse;
            vec2 triangleOffset = left;

            vec3 w_inverse = 1/-new vec3(triangle.a.z, triangle.b.z, triangle.c.z);

            //Rasterization
            if (left.x > middle.x)
                (left, middle) = (middle, left);
            if (middle.x > right.x)
                (middle, right) = (right, middle);
            if (left.x > middle.x)
                (left, middle) = (middle, left);

            float left_middle_tg = (middle.y - left.y) / (middle.x - left.x);
            float middle_right_tg = (right.y - middle.y) / (right.x - middle.x);
            float left_right_tg = (right.y - left.y) / (right.x - left.x);

            float top_line_tg1, bottom_line_tg1, top_line_tg2, bottom_line_tg2;
            if (left_middle_tg > left_right_tg)
            {
                top_line_tg1 = left_middle_tg;
                bottom_line_tg1 = left_right_tg;
                top_line_tg2 = middle_right_tg;
                bottom_line_tg2 = left_right_tg;
            }
            else
            {
                top_line_tg1 = left_right_tg;
                bottom_line_tg1 = left_middle_tg;
                top_line_tg2 = left_right_tg;
                bottom_line_tg2 = middle_right_tg;
            }

            //left part of the triangle
            if(!float.IsNaN(top_line_tg1)&&!float.IsNaN(bottom_line_tg1))
            {
                Parallel.For((int)Math.Round(Math.Max(0,left.x)), (int)Math.Round(Math.Min(width,middle.x)), x =>
                {
                    float cellCenter = x + 0.5f;
                    float x_offset = cellCenter - left.x;

                    int start = (int)Math.Round(left.y + bottom_line_tg1 * x_offset);
                    int end = (int)Math.Round(left.y + top_line_tg1 * x_offset);
                    if(start<0)
                        start = 0;
                    if(end>height)
                        end = height;
                    for (int y = start; y < end; ++y)
                    {
                        vec2 bar = ToTriangle * (new vec2(x, y) - triangleOffset);
                        //perspective correction
                        vec3 b = new vec3(1 - bar.x - bar.y, bar.x, bar.y) * w_inverse;
                        b = b/(b.x + b.y + b.z);

                        drawFragment(x, y, triangle,b);
                    }
                });
            }

            //right part of the triangle
            if(!float.IsNaN(top_line_tg2)&&!float.IsNaN(bottom_line_tg2))
            {
                Parallel.For((int)Math.Round(Math.Max(0,middle.x)), (int)Math.Round(Math.Min(width,right.x)), x =>
                {
                    float cellCenter = x + 0.5f;
                    float x_offset = cellCenter - right.x;

                    int start = (int)Math.Round(right.y + bottom_line_tg2 * x_offset);
                    int end = (int)Math.Round(right.y + top_line_tg2 * x_offset);
                    if(start<0)
                        start = 0;
                    if(end>height)
                        end = height;
                    for (int y = start; y < end; ++y)
                    {


                        vec2 bar = ToTriangle * (new vec2(x, y) - triangleOffset);
                        //perspective correction
                        vec3 b = new vec3(1 - bar.x - bar.y, bar.x, bar.y) * w_inverse;
                        b = b/(b.x + b.y + b.z);
                        drawFragment(x, y, triangle, b);
                    }
                });
            }
        }

        void drawFragment(int x, int y, Triangle triangle, vec3 barycentricCoordinates)
        {
            vec3 pointOnTriangle = barycentricCoordinates.x * triangle.a + barycentricCoordinates.y * triangle.b + barycentricCoordinates.z * triangle.c;
            //depth test
            if(depthBuffer[y * width + x] > pointOnTriangle.z)
                return;
            //write to depth buffer
            depthBuffer[y * width + x] = pointOnTriangle.z;

            vec4 color= barycentricCoordinates.x * triangle.color_a + barycentricCoordinates.y * triangle.color_b +
                        barycentricCoordinates.z * triangle.color_c;

            if(enableLighting && (triangle.normal_a.x!=0 || triangle.normal_a.y!=0 || triangle.normal_a.z!=0))//lighting
            {
                //interpolate normal
                vec3 normal= barycentricCoordinates.x * triangle.normal_a +
                             barycentricCoordinates.y * triangle.normal_b +
                             barycentricCoordinates.z * triangle.normal_c;


                //don't normalize normal after interpolation for better performance
                //it is not noticeable in most cases and isn't necessary when real normal are used on a triangle
                //(because then they face the same direction))
                //normal = glm.Normalized(normal);

                //calculate lighting
                float light=ambientLight;
                for(int i = 0; i < LightSources.Count; ++i)
                {
                    vec3 lightVector = lightsInViewSpace[i] - pointOnTriangle;
                    float lightDistance = glm.Length(lightVector);

                    if (LightSources[i].intensity != 0)
                    {
                        float lightValue = glm.Dot(normal, lightVector / (lightDistance * lightDistance) );
                        light += lightCut(lightValue)* LightSources[i].intensity;
                    }
                    else//don't apply light fading with distance if intensity is 0
                    {
                        float lightValue = glm.Dot(normal, lightVector / (lightDistance));
                        light += lightCut(lightValue) * (1 - ambientLight);
                    }
                }
                //apply lighting to base color
                color *= light;
            }

            //write fragment to frame buffer
            frameBuffer[(y * width + x)*4] = (byte)(Math.Min(1.0f, Math.Max(0.0f, color.b)) * 255);//B
            frameBuffer[(y * width + x)*4+1] = (byte)(Math.Min(1.0f, Math.Max(0.0f, color.g)) * 255);//G
            frameBuffer[(y * width + x)*4+2] = (byte)(Math.Min(1.0f, Math.Max(0.0f, color.r)) * 255);//R
            frameBuffer[(y * width + x)*4+3] = 255;//A
        }


        public void Render(Line[] lines, Transform transform)
        {
            Render(lines, transform.rotation, transform.translation);
        }

        public void Render(Line[] lines)
        {
            Render(lines, mat3.Identity, new vec3(0,0,0));
        }

        public void Render(Line[] lines, mat3 rotation, vec3 translation)
        {
            //apply view transform
            rotation =  camera.viewTransform.rotation*rotation;
            translation = camera.viewTransform.translation + camera.viewTransform.rotation * translation;

            for(int i = 0; i < lines.Length; ++i)
            {
                Line transformedLine = new Line();

                transformedLine.a = rotation * lines[i].a + translation;
                transformedLine.b = rotation * lines[i].b + translation;

                RenderLine(transformedLine);
            }
        }

        public void RenderLine(Line line)
        {
            vec2 ap = transformToScreenCoordinates(Project(line.a));
            vec2 bp = transformToScreenCoordinates(Project(line.b));
            if(glm.IsNaN(ap.x) || glm.IsNaN(ap.y) || glm.IsNaN(bp.x) || glm.IsNaN(bp.y))
                return;

            if (glm.Abs(ap.x - bp.x) > glm.Abs(ap.y - bp.y))
            {
                if(ap.x>bp.x)
                    (ap, bp) = (bp, ap);
                float tg = (bp.y - ap.y) / (bp.x - ap.x);
                int startX = (int)Math.Round(ap.x);
                int stopX = (int)Math.Round(bp.x);
                if(startX<0)
                    startX = 0;
                if(stopX>width)
                    stopX = width;
                for (int x = startX; x < stopX; ++x)
                {
                    float cellCenter = x + 0.5f;
                    float y_offset = cellCenter - bp.x;
                    int y = (int)Math.Round(bp.y + tg * y_offset);

                    if (y >= 0 && y < height)
                    {
                        frameBuffer[(y * width + x) * 4] = (byte)(255); //B
                        frameBuffer[(y * width + x) * 4 + 1] = (byte)(255); //G
                        frameBuffer[(y * width + x) * 4 + 2] = (byte)(255); //R
                        frameBuffer[(y * width + x) * 4 + 3] = 255; //A
                    }
                }
            }
            else
            {
                if(ap.y>bp.y)
                    (ap, bp) = (bp, ap);
                float tg = (bp.x - ap.x) / (bp.y - ap.y);
                for (int y = (int)Math.Round(ap.y); y < Math.Round(bp.y); ++y)
                {
                    float cellCenter = y + 0.5f;
                    float x_offset = cellCenter - bp.y;
                    int x = (int)Math.Round(bp.x + tg * x_offset);

                    if(x<0 || x>=width || y<0 || y>=height)
                        continue;
                    frameBuffer[(y * width + x)*4] = (byte)( 255);//B
                    frameBuffer[(y * width + x)*4+1] = (byte)( 255);//G
                    frameBuffer[(y * width + x)*4+2] = (byte)(255);//R
                    frameBuffer[(y * width + x)*4+3] = 255;//A
                }
            }
        }

        //Takes projection of light vector on normal and returns light intensity according to showFace property (Front, Back or Both)
        float lightCut(float light)
        {
            if(showFace== Face.Front)
                return Math.Max(0, light);
            if(showFace== Face.Back)
                return -Math.Min(0, light);
            return Math.Abs(light);
        }


        //Finds intersection of line segment ab with camera plane (z=-camera.near)
        private vec3 IntersectionWithCameraPlane(vec3 a, vec3 b)
        {
            vec3 ab = b - a;
            float t= (-camera.near - a.z)/ab.z;
            return a+ab*t;
        }

        //Barycentric interpolation
        vec3 lerp(vec3 a, vec3 b, vec3 c, vec3 bar)
        {
            return bar.x * a + bar.y * b + bar.z * c;
        }

        //Barycentric interpolation
        vec4 lerp(vec4 a, vec4 b, vec4 c, vec3 bar)
        {
            return bar.x * a + bar.y * b + bar.z * c;
        }

        vec3 Project(vec3 point)
        {
            return point/(-point.z)*camera.near;
        }

        vec2 transformToScreenCoordinates(vec3 point)
        {
            point.x = (point.x/camera.size.x + 1) * halfExtent.x;
            point.y = (-point.y/camera.size.y + 1) * halfExtent.y;
            return point.xy;
        }

        vec3 transformFromScreenToSpaceCoordinates(vec2 point)
        {
            vec3 result = new vec3
            {
                x = (point.x / halfExtent.x - 1)*camera.size.x,
                y = -(point.y / halfExtent.y - 1)*camera.size.y,
                z = -camera.near
            };
            return result;
        }


        vec3 GetBarycentricCoordinates(vec3 a, vec3 b, vec3 c, vec3 p)
        {
            vec3 result = new vec3();
            vec3 ab = b - a;
            vec3 ac = c - a;
            vec3 ap = p - a;
            float ab2 = glm.Dot(ab, ab);
            float d01 = glm.Dot(ab, ac);
            float ac2 = glm.Dot(ac, ac);
            float d20 = glm.Dot(ap, ab);
            float d21 = glm.Dot(ap, ac);
            float denom = ab2 * ac2 - d01 * d01;
            result.y = (ac2 * d20 - d01 * d21) / denom;
            result.z = (ab2 * d21 - d01 * d20) / denom;
            result.x = 1.0f - result.y - result.z;
            return result;
        }


        //Updates bitmap with frame buffer and clears frame and depth buffers for the next frame
        public void Present()
        {
            //update bitmap
            bmp.WritePixels(new Int32Rect(0, 0, width, height), frameBuffer, width * 4, 0);

            //clear frame buffer
            for (int i = 0; i < frameBuffer.Length / 4; ++i)
            {
                frameBuffer[i*4] = (byte)(clearColor.b * 255);//B
                frameBuffer[i*4+1] = (byte)(clearColor.g * 255);//G
                frameBuffer[i*4+2] = (byte)(clearColor.r * 255);//R
                frameBuffer[i*4+3] = (byte)(clearColor.a * 255);//A
            }

            //clear depth buffer
            for (int i = 0; i < depthBuffer.Length; ++i)
                depthBuffer[i] = float.MinValue;
        }



        //Computes normals for an array of triangles
        public static void CalculateNormals(Triangle[] triangles)
        {
            for (int i = 0; i < triangles.Length; ++i)
            {
                triangles[i].normal_a=triangles[i].normal_b=triangles[i].normal_c =
                    glm.Normalized(glm.Cross(triangles[i].b - triangles[i].a, triangles[i].c - triangles[i].a));
            }
        }

        //Rotates vector l around axis by angle (in radians)
        public static vec3 rotateVector(vec3 l, vec3 axis, float angle)
        {
            axis= glm.Normalized(axis);

            vec3 lp= glm.Dot(l, axis)*axis;
            vec3 lt= l-lp;
            if(lt.Length<0.000001f)
                return l;
            vec3 q = glm.Normalized(lt);
            vec3 s = glm.Cross(q, axis);

            mat3 T = new mat3(axis.x, q.x, s.x,
                              axis.y, q.y, s.y,
                              axis.z, q.z, s.z);

            mat3 T_transpose = new mat3(axis.x, axis.y, axis.z,
                                        q.x, q.y, q.z,
                                        s.x, s.y, s.z);

            mat3 R = new mat3(1, 0, 0,
                              0, (float)Math.Cos(angle), (float)-Math.Sin(angle),
                              0, (float)Math.Sin(angle), (float)Math.Cos(angle));

            return T_transpose * R * T * l;
        }


        //Rotates basis around axis by angle (in radians)
        public static mat3 rotateBasis(mat3 basis, vec3 axis, float angle)
        {
            return new mat3(rotateVector(basis.Column0, axis, angle),
                            rotateVector(basis.Column1, axis, angle),
                            rotateVector(basis.Column2, axis, angle));
        }
    }
}
