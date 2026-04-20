using System;
using GlmSharp;

namespace TorusSimulation
{
    public class TorusModel
    {
        public struct State
        {
            public double fi;
            public double theta;
            public double psi;
            public dvec3 omega;
            public dvec3 pos;
            public dvec3 vel;

            public State(double[] vector)
            {
                fi = vector[0];
                theta = vector[1];
                psi= vector[2];

                omega = new dvec3(vector[3],vector[4],vector[5]);
                pos = new dvec3(vector[6],vector[7],vector[8]);
                vel = new dvec3(vector[9],vector[10],vector[11]);
            }

            public double[] toVector()
            {
                return new double[]{fi,theta,psi,omega.x,omega.y,omega.z,pos.x,pos.y,pos.z,vel.x,vel.y,vel.z};
            }


            public dmat3 modelToWorld()
            {
                dmat3 m_ax= new dmat3 (1,0,0,
                    0,Math.Cos(fi),-Math.Sin(fi),
                    0,Math.Sin(fi),Math.Cos(fi)).Transposed;

                dmat3 m_ay= new dmat3 (Math.Cos(theta),0,Math.Sin(theta),
                    0,1,0,
                    -Math.Sin(theta),0,Math.Cos(theta)).Transposed;

                dmat3 m_az= new dmat3 (Math.Cos(psi),-Math.Sin(psi),0,
                    Math.Sin(psi),Math.Cos(psi),0,
                    0,0,1).Transposed;

                return m_az*m_ay*m_ax;
            }


            //Compute derivatives vector
            public double[] dx(Parameters p)
            {
                double R = (p.OuterRadius+p.InnerRadius)/2;
                double r = (p.OuterRadius-p.InnerRadius)/2;


                dmat3 m_ax= new dmat3 (1,0,0,
                    0,Math.Cos(fi),-Math.Sin(fi),
                    0,Math.Sin(fi),Math.Cos(fi)).Transposed;

                dmat3 m_ay= new dmat3 (Math.Cos(theta),0,Math.Sin(theta),
                    0,1,0,
                    -Math.Sin(theta),0,Math.Cos(theta)).Transposed;

                dmat3 m_az= new dmat3 (Math.Cos(psi),-Math.Sin(psi),0,
                    Math.Sin(psi),Math.Cos(psi),0,
                    0,0,1).Transposed;

                dmat3 M = m_az*m_ay*m_ax;



                //angular velocity
                dvec3 ax = m_az*m_ay*new dvec3(1,0,0);
                dvec3 ay = m_az*new dvec3(0,1,0);
                dvec3 az = new dvec3(0,0,1);
                dmat3 B = new dmat3(ax,ay,az);
                dvec3 angles_d = B.Inverse*M*omega;


                //Calculating forces
                double theta_n=theta%(2*Math.PI);
                if(theta_n>Math.PI)
                    theta_n-=2*Math.PI;
                if(theta_n<-Math.PI)
                    theta_n+=2*Math.PI;

                dvec3 lowerPoint = m_az*m_ay* new dvec3(0,0,Math.Abs(theta_n)>(Math.PI/2)?R:-R);
                dvec3 touchPoint = lowerPoint+new dvec3(0,0,-r);
                dvec3 touchPointVRot = glm.Cross(M*omega,touchPoint);
                dvec3 touchPointV= touchPointVRot+vel;

                //gravity force
                dvec3 F_G = new dvec3(0,0,-p.m*p.g);

                //Normal force application point
                dvec3 r_N = touchPoint;
                //if torus has non-zero speed then add small displacement in the direction of its rolling along the flat
                //to simulate rolling friction force.
                //Patch where tor is touching the flat is approximated as a circle with delta radius.
                //if delta is zero then F_G will go through (0,0,0) which means that no torque will be added by this force
                //and, as a result, no rolling friction
                if(touchPointVRot.Length>0)
                    r_N -= new dvec3(touchPointVRot.xy, 0).Normalized * p.delta;

                //normal force
                dvec3 F_N= new dvec3(0, 0, 0);
                //in the air
                if (pos.z + r_N.z > 0)
                {
                    F_N = new dvec3(0, 0, 0);
                }
                //Below the ground
                else
                {

                    var sqr = new Func<double,double>(x => x * x);

                    //calculate damping coefficient
                    double e = Math.Sqrt(1 - p.absorption);
                    double ratio = -Math.Log(e) /
                                   Math.Sqrt( sqr(Math.PI) + sqr(Math.Log(e)) );
                    double c = 2 * ratio * Math.Sqrt(p.m * p.k);

                    //spring force
                    F_N = new dvec3(0, 0, -(pos.z + r_N.z) * p.k);
                    //damping force
                    F_N += -touchPointV.z * c * new dvec3(0,0,1);
                }



                //Friction (friction between tor and the surface)
                dvec3 F_friction= new dvec3(0,0,0);
                //friction exist only when touching point is moving otherwise it is 0
                if(touchPointV.Length>0.001)
                    F_friction = -touchPointV.Normalized *F_N.Length*p.mu;
                F_friction.z = 0;

                //friction force is applied to the point where tor touches to surface
                dvec3 r_friction = touchPoint;

                double omega_sign = glm.Sign((M * omega).z);
                dvec3 rot_friction_torque = new dvec3(0, 0, -(omega_sign) * p.mu*p.delta * F_N.z);


                //derivative of position is velocity
                dvec3 pos_d = vel;

                //acceleration (derivative of velocity)
                dvec3 vel_d=(F_G+F_N+F_friction)/p.m;

                //torque
                dvec3 torque = new dvec3(0,0,0);
                torque+=glm.Cross(r_N,F_N)+glm.Cross(r_friction,F_friction);
                torque += rot_friction_torque;

                //angular acceleration
                dvec3 J = new dvec3(p.J_xx,p.J_yy,p.J_zz);
                dvec3 omega_d = (M.Transposed * torque - glm.Cross(omega, omega * J)) / J;

                //build resulting vector of derivatives
                double[] res = new double[12];
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

                pos = new dvec3(0,0,p.OuterRadius);
                omega = new dvec3(0.0,0.0,0.0);
                vel = new dvec3(0.0,0.0,0.0);
            }
        }

        public struct Parameters
        {
            public double InnerRadius;
            public double OuterRadius;
            public double m;//mass
            public double g;//acceleration
            public double delta;//deformation size
            public double k;//contact stiffness
            public double mu ;//friction coefficient
            public double absorption;//energy absorption on collision

            public double J_xx;
            public double J_yy;
            public double J_zz;


            public void update()
            {
                double R = (OuterRadius+InnerRadius)/2;
                double r = (OuterRadius-InnerRadius)/2;

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

            eng.Render(torMesh, new Transform(m_az*m_ay*m_ax*rot, (vec3)state.pos));
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
            torMesh= Torus.BuildTor((float)parameters.OuterRadius, (float)parameters.InnerRadius, 30, true);
        }

        //advance simulation by time dt
        public void update(double dt)
        {
            double direction = dt < 0?-1:1;
            dt=Math.Abs(dt);
            //max allowed dt step for runge kutta
            double maxStep = 0.001;
            //number of steps to cover dt
            int stepsCount= (int)Math.Ceiling(dt/maxStep);
            //actual step
            double step = dt/stepsCount;

            for (int i = 0; i < stepsCount; ++i)
            {
                state = RungeKutta(state, step*direction);
            }
        }

        public void update(float dt)
        {
            update((double)dt);
        }

        public double gravitationalPotentialEnergy()
        {
            return state.pos.z * parameters.m * parameters.g;
        }

        public double springPotentialEnergy()
        {
            double R = (parameters.OuterRadius + parameters.InnerRadius) / 2;
            double r = (parameters.OuterRadius - parameters.InnerRadius) / 2;

            double theta_n = state.theta % (2 * Math.PI);
            if (theta_n > Math.PI)
                theta_n -= 2 * Math.PI;
            if (theta_n < -Math.PI)
                theta_n += 2 * Math.PI;

            dmat3 m_ay = new dmat3(Math.Cos(state.theta), 0, Math.Sin(state.theta),
                0, 1, 0,
                -Math.Sin(state.theta), 0, Math.Cos(state.theta)).Transposed;

            dmat3 m_az = new dmat3(Math.Cos(state.psi), -Math.Sin(state.psi), 0,
                Math.Sin(state.psi), Math.Cos(state.psi), 0,
                0, 0, 1).Transposed;

            dvec3 lowerPoint = m_az * m_ay * new dvec3(0, 0, Math.Abs(theta_n) > (Math.PI / 2) ? R : -R);
            dvec3 touchPoint = lowerPoint + new dvec3(0, 0, -r);

            double penetration = -(state.pos.z + touchPoint.z);
            if (penetration <= 0)
                return 0;

            return parameters.k * penetration * penetration / 2.0;
        }

        public double kineticEnergy()
        {
            return parameters.m* glm.Dot(state.vel,state.vel)/2.0;
        }

        public double rotationalKineticEnergy()
        {
            return (parameters.J_xx*state.omega.x*state.omega.x/2.0+
                   parameters.J_yy*state.omega.y*state.omega.y/2.0+
                   parameters.J_zz*state.omega.z*state.omega.z/2.0);
        }

        public double potentialEnergy()
        {
            return gravitationalPotentialEnergy() + springPotentialEnergy();
        }

        public double kineticEnergyTotal()
        {
            return kineticEnergy() + rotationalKineticEnergy();
        }

        public double totalEnergy()
        {
            return potentialEnergy() + kineticEnergyTotal();
        }



        State RungeKutta(State s, double h)
        {
            double[] x = s.toVector();
            int n = x.Length;

            double[] k1 = s.dx(parameters);

            double[] x2 = new double[n];
            for (int i = 0; i < n; i++)
                x2[i] = x[i] + h / 2 * k1[i];
            double[] k2 = new State(x2).dx(parameters);

            double[] x3 = new double[n];
            for (int i = 0; i < n; i++)
                x3[i] = x[i] + h / 2 * k2[i];
            double[] k3 =new State(x3).dx(parameters);

            double[] x4 = new double[n];
            for (int i = 0; i < n; i++)
                x4[i] = x[i] + h * k3[i];
            double[] k4 = new State(x4).dx(parameters);

            double[] res = new double[n];
            for (int i = 0; i < n; i++)
                res[i] = x[i] + h / 6 * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]);
            return new State(res);
        }
    }
}