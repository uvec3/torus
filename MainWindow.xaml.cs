using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GlmSharp;

namespace TorusSimulation
{
    public partial class MainWindow
    {
        //My rendering engine
        Eng eng = new Eng();

        //Initial view transformation, swaps y and z axes. Now y points into screen and z points up, x still points to the right.
        Transform viewTransform = new Transform(new mat3(1,0,0, 0,0,-1, 0,1,0), new vec3(0, -4, -10));

        //Object represent wheel mesh, its current state and simulates its physics
        TorusModel torusModel = new TorusModel();

        //meshes
        private Triangle[] axis=null;
        private Triangle[] square=null;

        private DispatcherTimer timer;
        double prevTime = 0;

        private List<ValueTuple<string, TorusModel.State, TorusModel.Parameters>> presets =
            new List<(string, TorusModel.State, TorusModel.Parameters)>();


        public MainWindow()
        {
            InitializeComponent();

            //create different initial state and parameters presets
            create_presets();

            // Populate presets combo box (if available)
            if (comboPresets != null)
            {
                comboPresets.Items.Clear();
                foreach (var p in presets)
                {
                    comboPresets.Items.Add(p.Item1);
                }
                if (comboPresets.Items.Count > 0)
                    comboPresets.SelectedIndex = 0;
            }

            btResetClick(null,null);

            //Add engine view as to the second column of the main grid
            grid.Children.Add(eng);
            Grid.SetColumn(eng, 2);

            //timer setup
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(0);
            timer.Tick += new EventHandler(draw);
            timer.Start();

            //add mouse event handlers
            eng.MouseMove += eng_MouseMove;
            eng.MouseDown += eng_MouseDown;
            eng.MouseUp += eng_MouseUp;
            eng.MouseWheel += EngOnMouseWheel;

            AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnGlobalPreviewKeyDown), true);

            //create meshes
            axis = new Triangle[6];
            axis[0] = new Triangle(new vec3(0, 0.1f, 0), new vec3(0, -0.1f, 0), new vec3(1f, 0.1f, 0), new vec4(1, 0, 0, 1));//x
            axis[1] = new Triangle(new vec3(-0.1f, 0, 0), new vec3(0.1f, 0, 0), new vec3(0, 1f, 0), new vec4(0, 1, 0, 1));//y
            axis[2] = new Triangle(new vec3(-0.1f, 0, 0f), new vec3(0.1f, 0, 0f), new vec3(0, 0, 1f), new vec4(0, 0, 1, 1));//z

            axis[3] = new Triangle(new vec3(0, 0, 0.1f), new vec3(0, 0, -0.1f), new vec3(1f, 0.1f, 0), new vec4(1, 0, 0, 1));//x
            axis[4] = new Triangle(new vec3(0, 0, -0.1f), new vec3(0, 0, 0.1f), new vec3(0, 1f, 0), new vec4(0, 1, 0, 1));//y
            axis[5] = new Triangle(new vec3(0, -0.1f, 0f), new vec3(0, 0.1f, 0f), new vec3(0, 0, 1f), new vec4(0, 0, 1, 1));//z

            square = new Triangle[2];
            square[0] = new Triangle(new vec3(-1, -1, 0), new vec3(1, -1, 0), new vec3(1, 1, 0), new vec4(0.7f, 0.5f, 0.5f, 1));
            square[1] = new Triangle(new vec3(-1, -1, 0), new vec3(1, 1, 0), new vec3(-1, 1, 0), new vec4(0.7f, 0.5f, 0.5f, 1));
            Eng.CalculateNormals(square);


            //add light source
            Light light = new Light();
            light.intensity = 0;
            light.position=new vec3(1,0,20);
            eng.LightSources.Add(light);

            torusModel.syncStateCallback= syncStateCallback;

            //start time
            prevTime = DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond;
            torusModel.ResetState();

            // ensure play/pause button initial text matches IsChecked
            if (tbPlayPause != null)
            {
                tbPlayPause.Content = tbPlayPause.IsChecked == true ? "Pause" : "Play";
            }
        }

        private void OnGlobalPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat)
                return;

            if (e.Key == Key.Space)
            {
                torusModel.state.vel.z = 10;
                e.Handled = true;
            }

            if (e.Key == Key.R)
            {
                btResetClick(null, null);
            }
        }


        private void create_presets()
        {
            TorusModel.Parameters default_params = new TorusModel.Parameters();
            default_params.InnerRadius = 0.8f;
            default_params.OuterRadius = 1.0f;
            default_params.delta = 0.1f;
            default_params.g = 9.8f;
            default_params.m = 10;
            default_params.mu = 0.3f;
            default_params.absorption = 0.4f;

            TorusModel.State default_state = new TorusModel.State();
            default_state.pos = new vec3(0,0,1);
            default_state.vel = new vec3(0);
            default_state.omega=new vec3(0);
            default_state.fi = default_state.psi = default_state.theta = 0;
            presets.Add(("default", default_state, default_params));

            TorusModel.State state = default_state;
            state.pos=new vec3(0,0,3);
            state.omega.x=-30;
            state.theta = (float)Math.PI/16;
            TorusModel.Parameters parameters = default_params;
            //parameters.delta = 0.05f;
            //parameters.absorption = 0.5f;
            //parameters.mu = 0.2f;


            presets.Add(("roll", state, parameters));

            state = default_state;
            state.omega.x = 2;
            parameters = default_params;
            parameters.InnerRadius = 1;
            parameters.OuterRadius = 2;
            presets.Add(("coin", state, parameters));
        }

        private void draw(object sender, EventArgs e)
        {
            double time = DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond;
            float dt= (float)(time - prevTime);

            //simulation dt can differ from actual time passed to speed up or slow down simulation
            float simulation_dt = dt*(float)sliderSpeed.Value;
            // check ToggleButton state instead of old rbPlay radio button
            if(tbPlayPause != null && tbPlayPause.IsChecked==false)
                simulation_dt = 0;

            //handle keyboard input for wheel rotation
            rotateWithKeyboard(dt);


            //update wheel state
            torusModel.update(simulation_dt);
            //updates view transformation
            updateViewTransform();
            //set camera transform
            eng.camera.viewTransform = viewTransform;

            //draw axis
            Face face = eng.showFace;
            eng.showFace = Face.Both;
            bool lighting = eng.enableLighting;
            eng.enableLighting = false;
            eng.Render(axis, new Transform(mat3.Identity*2, new vec3(0, 0, 0.01f)));
            eng.showFace =face;
            eng.enableLighting= lighting;

            //draw floor
            var squareTransform = new Transform(mat3.Identity*50, new vec3(0, 0, 0f));
            eng.Render(square, squareTransform);

            //draw wheel
            torusModel.Render(eng, simulation_dt);

            //update image
            eng.Present();

            //show dt and fps
            lbFrameTime.Content = (dt*1000).ToString("0.00") + " ms"+ " ("+(1/dt).ToString("0.0")+" fps)";
            prevTime = time;


            syncStateCallback();
        }

        // ToggleButton handlers to update Content text (Play/Pause)
        private void TbPlayPause_Checked(object sender, RoutedEventArgs e)
        {
            if (tbPlayPause != null)
                tbPlayPause.Content = "Pause";
        }

        private void TbPlayPause_Unchecked(object sender, RoutedEventArgs e)
        {
            if (tbPlayPause != null)
                tbPlayPause.Content = "Play";
        }

        void setSlider(Slider slider, double value)
        {
            //modify only if within the range
            if (slider != null)
            {
                if(value >= slider.Minimum && value <= slider.Maximum && slider.Value != value)
                {
                    slider.Value = value;
                }
            }
        }

        //Syncs state of wheel with UI controls
        void syncStateCallback()
        {
            //return if ui isn't initialized yet
            if(angle1==null)
                return;

            setSlider(angle1, (torusModel.state.fi/(float)Math.PI*180)%360);
            setSlider(angle2, (torusModel.state.theta/(float)Math.PI*180)%360);
            setSlider(angle3, (torusModel.state.psi/(float)Math.PI*180)%360);

            setSlider(posX, torusModel.state.pos.x);
            setSlider(posY, torusModel.state.pos.y);
            setSlider(posZ, torusModel.state.pos.z);

            txtAimX.Content = "X: " + torusModel.state.pos.x.ToString("0.000");
            txtAimY.Content = "Y: " + torusModel.state.pos.y.ToString("0.000");
            txtAimZ.Content = "Z: " + torusModel.state.pos.z.ToString("0.000");

            setSlider(omegaX, torusModel.state.omega.x);
            setSlider(omegaY, torusModel.state.omega.y);
            setSlider(omegaZ, torusModel.state.omega.z);

            txtOmegaX.Content = "ωx: " + torusModel.state.omega.x.ToString("0.000");
            txtOmegaY.Content = "ωy: " + torusModel.state.omega.y.ToString("0.000");
            txtOmegaZ.Content = "ωz: " + torusModel.state.omega.z.ToString("0.000");

            setSlider(Vx, torusModel.state.vel.x);
            setSlider(Vy, torusModel.state.vel.y);
            setSlider(Vz, torusModel.state.vel.z);

            txtVx.Content = "Vx: " + torusModel.state.vel.x.ToString("0.000");
            txtVy.Content = "Vy: " + torusModel.state.vel.y.ToString("0.000");
            txtVz.Content = "Vz: " + torusModel.state.vel.z.ToString("0.000");

            UpdateUiAngles();
        }

        private void UpdateUiAngles()
        {
            txtAngle1.Content = "ϕ:\t"+(torusModel.state.fi/Math.PI*180).ToString("0.0");
            txtAngle2.Content = "θ:\t"+(torusModel.state.theta/Math.PI*180).ToString("0.0");
            txtAngle3.Content = "ψ:\t"+(torusModel.state.psi/Math.PI*180).ToString("0.0");
        }



        //handles rotation of wheel with keyboard input (WASD)
        void rotateWithKeyboard(float dt)
        {
            vec3 delta = new vec3(0, 0, 0);
            if (Keyboard.IsKeyDown(Key.W))
                delta += new vec3(-1,0,0)*dt*10;
            if (Keyboard.IsKeyDown(Key.S))
                delta -= new vec3(-1,0,0)*dt*10;
            if (Keyboard.IsKeyDown(Key.A))
                delta += new vec3(0,-1,0)*dt*10;
            if (Keyboard.IsKeyDown(Key.D))
                delta-= new vec3(0,-1,0)*dt*10;

            if (delta != new vec3(0, 0, 0))
            {
                torusModel.state.omega.x += delta.x*1f;
                torusModel.state.omega.yz += delta.y * 1f * new vec2((float)Math.Cos(-torusModel.state.fi), (float)Math.Sin(-torusModel.state.fi));
            }
        }


        // CAMERA CONTROL WITH MOUSE

        //mouse button state
        bool isMousePressed = false;
        //previous mouse position
        float prevX = 0;
        float prevY = 0;
        //view rotation angles
        private float ax = (float)-Math.PI/3;
        private float az = 0;
        //view distance from center
        private float distance = 20f;

        private void eng_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isMousePressed = true;
            prevX = (float)e.GetPosition(eng).X;
            prevY = (float)e.GetPosition(eng).Y;
        }

        private void eng_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isMousePressed = false;
        }

        private void eng_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMousePressed)
            {
                float dx = (float)e.GetPosition(eng).X - prevX;
                float dy = (float)e.GetPosition(eng).Y - prevY;

                az+=dx * 0.005f;
                ax+=dy * 0.005f;

                if(ax>0)
                    ax=0;
                if(ax<-Math.PI)
                    ax=- (float)Math.PI;

                prevX = (float)e.GetPosition(eng).X;
                prevY = (float)e.GetPosition(eng).Y;
            }
        }

        private void EngOnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            distance*= (float)Math.Pow(0.9, e.Delta / 120.0);
        }

        void updateViewTransform()
        {
            viewTransform = Transform.identity;
            if(cbCameraFollow.IsChecked==true)
                viewTransform.translation -= torusModel.state.pos;
            viewTransform.Rotate(new vec3(0, 0, 1), az);
            viewTransform.Rotate(new vec3(1, 0, 0), ax);
            viewTransform.translation += new vec3(0, 0, -distance);
        }

        //Parameters UI handlers
        private void btResetClick(object sender, RoutedEventArgs e)
        {
            if (comboPresets == null || comboPresets.SelectedIndex < 0)
                return;

            int idx = comboPresets.SelectedIndex;
            if (idx >= presets.Count)
                return;

            var preset = presets[idx];

            // Copy state
            torusModel.state = preset.Item2;
           // syncStateCallback();
        }

        private void SliderSimulationSpeed_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lbSpeed != null)
                lbSpeed.Content ="Speed: "+ sliderSpeed.Value.ToString("0.00");
        }


        //wheel state
        private void Angle1_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.fi = (float)angle1.Value/180.0f*(float)Math.PI;
            UpdateUiAngles();
        }

        private void Angle2_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.theta = (float)angle2.Value/180.0f*(float)Math.PI;
            UpdateUiAngles();
        }

        private void Angle3_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.psi = (float)angle3.Value/180.0f*(float)Math.PI;
            UpdateUiAngles();
        }


        private void Position_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (posX == sender)
            {
                torusModel.state.pos.x = (float)posX.Value;
            }

            if (posY == sender)
            {
                torusModel.state.pos.y = (float)posY.Value;
            }

            if (posZ == sender)
            {
                torusModel.state.pos.z = (float)posZ.Value;
            }
        }

        private void g_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (torusModel != null)
            {
                torusModel.parameters.g = (float)g.Value;
            }
            txtG.Content= "g: " + g.Value.ToString("0.0");
        }



        private void OmegaX_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            torusModel.state.omega.x = (float)omegaX.Value;
        }

        private void OmegaY_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.omega.y = (float)omegaY.Value;
        }

        private void OmegaZ_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.omega.z = (float)omegaZ.Value;
        }

        private void Vx_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.vel.x = (float)Vx.Value;
        }

        private void Vy_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.vel.y = (float)Vy.Value;
        }

        private void Vz_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.vel.z = (float)Vz.Value;
        }


        //Graphics settings
        private void CbLight_OnClick(object sender, RoutedEventArgs e)
        {
            eng.enableLighting = cbLight.IsChecked == true;
        }

        private void rbBoth(object sender, RoutedEventArgs e)
        {
            eng.showFace = Face.Both;
        }

        private void rbFront(object sender, RoutedEventArgs e)
        {
            eng.showFace = Face.Front;
        }

        private void rbBack(object sender, RoutedEventArgs e)
        {
            eng.showFace = Face.Back;
        }

        private void backgroundColorUpdate(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            eng.clearColor=new vec4((float)slbgR.Value/255f, (float)slbgG.Value/255f, (float)slbgB.Value/255f, 1);
        }

        private void slAmbientIntensity_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            eng.ambientLight=(float)slAmbientIntensity.Value;
        }

        private void SlLight1Intensity_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(eng.LightSources != null && eng.LightSources.Count>0)
                eng.LightSources[0].intensity = (float)slLight1Intensity.Value;
        }


        //Parameters settings

        private void SliderMass_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.parameters.m= (float)sliderMass.Value;
            torusModel.parameters_changed();
            txtMass.Content = "m: " + sliderMass.Value.ToString("0.00");
        }

        private void SliderInnerR_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.parameters.InnerRadius = (float)sliderInnerR.Value;
            if(torusModel.parameters.InnerRadius>= torusModel.parameters.OuterRadius)
            {
                torusModel.parameters.OuterRadius= torusModel.parameters.InnerRadius + 0.01f;
                setSlider(sliderOuterR, torusModel.parameters.OuterRadius);
            }
            torusModel.parameters_changed();
            txtInnerR. Content = "Inner Radius: " + sliderInnerR.Value.ToString("0.000");
        }

        private void SliderOuterR_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.parameters.OuterRadius= (float)sliderOuterR.Value;
            if (torusModel.parameters.OuterRadius <= torusModel.parameters.InnerRadius&&sliderInnerR!=null)
            {
                torusModel.parameters.InnerRadius = torusModel.parameters.OuterRadius - 0.01f;
                setSlider(sliderInnerR, torusModel.parameters.InnerRadius);
            }
            torusModel.parameters_changed();
            txtOuterR.Content = "Outer Radius: " + sliderOuterR.Value.ToString("0.000");
        }

        private void Delta_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.parameters.delta = (float)delta.Value;
            torusModel.parameters_changed();
            txtDelta.Content = "δ: " + delta.Value.ToString("0.000");
        }

         private void Mu_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
         {
             torusModel.parameters.mu = (float)mu.Value;
             torusModel.parameters_changed();
             txtMu.Content = "μ: " + mu.Value.ToString("0.000");
         }

         private void Absorption_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
         {
             torusModel.parameters.absorption = (float)absorption.Value;
             torusModel.parameters_changed();
             txtAbsorption.Content = "Absorption: " + absorption.Value.ToString("0.000");
         }

         private void ResolutionChanged(object sender, SelectionChangedEventArgs e)
        {
            string res_string= (string)((ComboBoxItem)comboResolution.SelectedItem).Content;
            string[] parts = res_string.Split('x');
            if (parts.Length == 2)
            {
                int width= int.Parse(parts[0].Trim());
                int height= int.Parse(parts[1].Trim());
                eng.SetExtent(width, height);
            }
        }

        // Apply selected preset to the wheel (parameters + state)
        private void ComboPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboPresets == null || comboPresets.SelectedIndex < 0)
                return;

            int idx = comboPresets.SelectedIndex;
            if (idx >= presets.Count)
                return;

            var preset = presets[idx];

            // Copy parameters
            torusModel.parameters = preset.Item3;
            torusModel.parameters_changed();

            // Update parameter UI controls if available
            if (sliderMass != null)
                setSlider(sliderMass, torusModel.parameters.m);
            if (sliderInnerR != null)
                setSlider(sliderInnerR, torusModel.parameters.InnerRadius);
            if (sliderOuterR != null)
                setSlider(sliderOuterR, torusModel.parameters.OuterRadius);
            if (delta != null)
                setSlider(delta, torusModel.parameters.delta);
            if (mu != null)
                setSlider(mu, torusModel.parameters.mu);
            if (g != null)
                setSlider(g, torusModel.parameters.g);
            if (absorption != null)
                setSlider(absorption, torusModel.parameters.absorption);


            // Copy state
            torusModel.state = preset.Item2;
            //syncStateCallback();
        }
    }
}
