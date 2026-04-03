using System;
using GlmSharp;

namespace TorusSimulation
{
    public class TorusModel
    {
        public struct State
        {
            public float fi;
            public float theta;
            public float psi;
            public vec3 omega;
            public vec3 pos;
            public vec3 vel;

            public State(float[] vector)
            {
                fi = vector[0];
                theta = vector[1];
                psi= vector[2];

                omega = new vec3(vector[3],vector[4],vector[5]);
                pos = new vec3(vector[6],vector[7],vector[8]);
                vel = new vec3(vector[9],vector[10],vector[11]);
            }

            public float[] toVector()
            {
                return new float[]{fi,theta,psi,omega.x,omega.y,omega.z,pos.x,pos.y,pos.z,vel.x,vel.y,vel.z};
            }


            //Compute derivatives vector
            public float[] dx(Parameters p)
            {
                float R = (p.OuterRadius+p.InnerRadius)/2;
                float r = (p.OuterRadius-p.InnerRadius)/2;


                mat3 m_ax= new mat3 (1,0,0,
                    0,(float)Math.Cos(fi),-(float)Math.Sin(fi),
                    0,(float)Math.Sin(fi),(float)Math.Cos(fi)).Transposed;

                mat3 m_ay= new mat3 ((float)Math.Cos(theta),0,(float)Math.Sin(theta),
                    0,1,0,
                    -(float)Math.Sin(theta),0,(float)Math.Cos(theta)).Transposed;

                mat3 m_az= new mat3 ((float)Math.Cos(psi),-(float)Math.Sin(psi),0,
                    (float)Math.Sin(psi),(float)Math.Cos(psi),0,
                    0,0,1).Transposed;

                mat3 M = m_az*m_ay*m_ax;



                //angular velocity
                vec3 ax = m_az*m_ay*new vec3(1,0,0);
                vec3 ay = m_az*new vec3(0,1,0);
                vec3 az = new vec3(0,0,1);
                mat3 B = new mat3(ax,ay,az);
                vec3 angles_d = B.Inverse*M*omega;


                //Calculating forces
                float theta_n=theta%(float)(2*Math.PI);
                if(theta_n>Math.PI)
                    theta_n-=2*(float)Math.PI;
                if(theta_n<-Math.PI)
                    theta_n+=2*(float)Math.PI;

                vec3 lowerPoint = m_az*m_ay* new vec3(0,0,Math.Abs(theta_n)>(Math.PI/2)?R:-R);
                vec3 touchPoint = lowerPoint+new vec3(0,0,-r);
                vec3 touchPointVRot = vec3.Cross(M*omega,touchPoint);
                vec3 touchPointV= touchPointVRot+vel;

                //gravity force
                vec3 F_G = new vec3(0,0,-p.m*p.g);

                //Normal force application point
                vec3 r_N = touchPoint;
                //if tor has non-zero speed then add small displacement in the direction of its rolling along flat
                //to simulate rolling friction force.
                //Patch where tor is touching the flat is approximated as a circle with delta radius.
                //if delta is zero then F_G will go through (0,0,0) which means that no torque will be added by this force
                //and, as a result, no rolling friction
                if(touchPointVRot.Length>0)
                    r_N -= new vec3(touchPointVRot.xy, 0).Normalized * p.delta;

                //normal force
                vec3 F_N;
                bool under_the_surface = pos.z + r_N.z < -r / 100;
                //in the air
                if (pos.z + r_N.z > r / 100)
                {
                    F_N = new vec3(0, 0, 0);
                }
                //Below the ground
                else if (under_the_surface)
                {
                    F_N = new vec3(0, 0, -(pos.z + r_N.z) * p.m * p.g * 500);
                    if (touchPointV.z > 0)
                    {
                        F_N *= (1-p.absorption);
                    }
                }
                else
                {
                    F_N = new vec3(0,0,p.m*p.g);
                }


                //Friction (friction between tor and the surface)
                vec3 F_friction= new vec3(0,0,0);
                //friction exist only when touching point is moving otherwise it is 0
                if(touchPointV.Length>0)
                    F_friction = -touchPointV.Normalized *F_N.Length*p.mu;
                F_friction.z = 0;

                //friction force is applied to the point where tor touches to surface
                vec3 r_friction = touchPoint;

                float omega_sign = (M * omega).z > 0 ? 1 : -1;
                vec3 rot_friction_torque = new vec3(0, 0, -(omega_sign) * p.mu*p.delta * F_N.z);





                //derivative of position is velocity
                vec3 pos_d = vel;

                //acceleration (derivative of velocity)
                vec3 vel_d=(F_G+F_N+F_friction)/p.m;

                //torque
                vec3 torque = new vec3(0,0,0);
                torque+=vec3.Cross(r_N,F_N)+vec3.Cross(r_friction,F_friction);
                torque += rot_friction_torque;

                //angular acceleration
                vec3 J = new vec3(p.J_xx,p.J_yy,p.J_zz);
                vec3 omega_d = (M.Transposed * torque - vec3.Cross(omega, omega * J)) / J;


                //build resulting vector of derivatives
                float[] res = new float[12];
                res[0] = angles_d.x;
                res[1] = angles_d.y;
                res[2] = angles_d.z;
                res[3] = omega_d.x;
                res[4] = omega_d.y;
                res[5] = omega_d.z;
                res[6] = pos_d.x;
                res[7] = pos_d.y;
                res[8] = pos_d.z;
                res[9] = vel_d.x;
                res[10] = vel_d.y;
                res[11] = vel_d.z;
                return res;
            }

            public void Reset(Parameters p)
            {
                fi = 0;
                theta = 0;
                psi = 0;

                pos = new vec3(0,0,p.OuterRadius);
                omega = new vec3(0.0f,0.0f,0.0f);
                vel = new vec3(0.0f,0.0f,0.0f);
            }
        }

        public struct Parameters
        {
            public float InnerRadius;
            public float OuterRadius;
            public float m;//mass
            public float g;//acceleration
            public float delta;//deformation scale
            public float mu ;//friction coefficient
            public float absorption;//energy absorption on collision

            public float J_xx;
            public float J_yy;
            public float J_zz;


            public void update()
            {
                float R = (OuterRadius+InnerRadius)/2;
                float r = (OuterRadius-InnerRadius)/2;

                J_yy=J_zz=m/8*(4*R*R+5*r*r);
                J_xx=m/4*(4*R*R+3*r*r);
            }
        }

        public delegate void SyncStateCallback();
        public SyncStateCallback syncStateCallback;

        public State state;

        public Parameters parameters;

        private Triangle[] torMesh;
        public TorusModel()
        {
            parameters= new Parameters();
            parameters_changed();
            ResetState();
        }
        
        public void Render(Eng eng, float dt)
        {
            mat3 rot=new mat3(0,1,0,
                              0,0,1,
                              1,0,0).Transposed;



            mat3 m_ax= new mat3 (1,0,0,
                0,(float)Math.Cos(state.fi),-(float)Math.Sin(state.fi),
                0,(float)Math.Sin(state.fi),(float)Math.Cos(state.fi)).Transposed;

            mat3 m_ay= new mat3 ((float)Math.Cos(state.theta),0,(float)Math.Sin(state.theta),
                0,1,0,
                -(float)Math.Sin(state.theta),0,(float)Math.Cos(state.theta)).Transposed;

            mat3 m_az= new mat3 ((float)Math.Cos(state.psi),-(float)Math.Sin(state.psi),0,
                (float)Math.Sin(state.psi),(float)Math.Cos(state.psi),0,
                0,0,1).Transposed;

            eng.Render(torMesh, new Transform(m_az*m_ay*m_ax*rot, state.pos));
        }


        public void ResetState()
        {
            state.Reset(parameters);
        }

        public void parameters_changed()
        {
            //update precalculated parameters
            parameters.update();
            //rebuild mesh
            torMesh= Torus.BuildTor(parameters.OuterRadius, parameters.InnerRadius, 30, true);
        }

        //advance simulation by time dt
        public void update(float dt)
        {
            float direction = dt < 0?-1:1;
            dt=Math.Abs(dt);
            //max allowed dt step for runge kutta
            float maxStep = 0.001f;
            //number of steps to cover dt
            int stepsCount= (int)Math.Ceiling(dt/maxStep);
            //actual step
            float step = dt/stepsCount;

            for (int i = 0; i < stepsCount; ++i)
            {
                state = RungeKutta(state, step*direction);
            }
        }


        State RungeKutta(State s, float h)
        {
            float[] x = s.toVector();
            int n = x.Length;

            float[] k1 = s.dx(parameters);

            float[] x2 = new float[n];
            for (int i = 0; i < n; i++)
                x2[i] = x[i] + h / 2 * k1[i];
            float[] k2 = new State(x2).dx(parameters);

            float[] x3 = new float[n];
            for (int i = 0; i < n; i++)
                x3[i] = x[i] + h / 2 * k2[i];
            float[] k3 =new State(x3).dx(parameters);

            float[] x4 = new float[n];
            for (int i = 0; i < n; i++)
                x4[i] = x[i] + h * k3[i];
            float[] k4 = new State(x4).dx(parameters);

            float[] res = new float[n];
            for (int i = 0; i < n; i++)
                res[i] = x[i] + h / 6 * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]);
            return new State(res);
        }
    }
}