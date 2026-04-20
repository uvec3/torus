using System;
using System.Collections.Generic;
using System.Globalization;
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

        //Object represents torus mesh, its current state and simulates its physics
        TorusModel torusModel = new TorusModel();

        //meshes
        private Triangle[] axes=null;
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
            axes = new Triangle[6];
            axes[0] = new Triangle(new vec3(0, 0.1f, 0), new vec3(0, -0.1f, 0), new vec3(1f, 0.1f, 0), new vec4(1, 0, 0, 1));//x
            axes[1] = new Triangle(new vec3(-0.1f, 0, 0), new vec3(0.1f, 0, 0), new vec3(0, 1f, 0), new vec4(0, 1, 0, 1));//y
            axes[2] = new Triangle(new vec3(-0.1f, 0, 0f), new vec3(0.1f, 0, 0f), new vec3(0, 0, 1f), new vec4(0, 0, 1, 1));//z

            axes[3] = new Triangle(new vec3(0, 0, 0.1f), new vec3(0, 0, -0.1f), new vec3(1f, 0.1f, 0), new vec4(1, 0, 0, 1));//x
            axes[4] = new Triangle(new vec3(0, 0, -0.1f), new vec3(0, 0, 0.1f), new vec3(0, 1f, 0), new vec4(0, 1, 0, 1));//y
            axes[5] = new Triangle(new vec3(0, -0.1f, 0f), new vec3(0, 0.1f, 0f), new vec3(0, 0, 1f), new vec4(0, 0, 1, 1));//z

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




        private void create_presets()
        {
            TorusModel.Parameters default_params = new TorusModel.Parameters();
            default_params.InnerRadius = 0.8f;
            default_params.OuterRadius = 1.0f;
            default_params.delta = 0.1f;
            default_params.k = 50000;
            default_params.g = 9.8f;
            default_params.m = 10;
            default_params.mu = 0.5f;
            default_params.absorption = 0.75f;

            TorusModel.State default_state = new TorusModel.State();
            default_state.pos = new dvec3(0,0,1);
            default_state.vel = new dvec3(0);
            default_state.omega=new dvec3(0);
            default_state.fi = default_state.psi = default_state.theta = 0;
            presets.Add(("Default", default_state, default_params));



            TorusModel.Parameters parameters = default_params;
            parameters.delta = 0.05f;
            parameters.InnerRadius = 0.25f;
            parameters.OuterRadius = 0.5f;
            TorusModel.State state = default_state;
            state.pos=new dvec3(0,0,3);
            state.omega.x=-30;
            state.theta = (float)Math.PI/16;
            presets.Add(("Rolling wheel", state, parameters));


            parameters = default_params;
            parameters.InnerRadius = 0.45f;
            parameters.OuterRadius = 0.5f;
            parameters.delta = 0.02f;
            parameters.mu = 0.3f;
            parameters.absorption = 0.995f;
            parameters.g = 98;
            parameters.m = 5f;
            parameters.k = 5000000;
            state = default_state;
            state.theta = (float)(Math.PI / 2.0f);
            state.psi = (float)(Math.PI / 8.0f);
            state.pos.z = 0.3f;
            state.vel.z = 70;
            state.omega.y = 100;
            state.omega.z = 30;

            presets.Add(("Coin toss", state, parameters));


            state = default_state;
            state.omega.z = 50;
            state.vel.y = 20;
            state.pos.z = 0.5f;
            parameters = default_params;
            parameters.InnerRadius = 0.45f;
            parameters.OuterRadius = 0.5f;
            parameters.delta = 0.02f;
            parameters.mu = 0.3f;
            parameters.absorption = 0.98f;
            parameters.g = 98;
            parameters.m = 10f;
            parameters.k = 5000000;
            presets.Add(("Spinning coin", state, parameters));

            parameters = default_params;
            parameters.InnerRadius = 0.5f;
            parameters.absorption = 0.85f;
            parameters.delta = 0.24f;
            parameters.k = 100000;
            parameters.m = 100;
            parameters.mu = 0.18;

            state = default_state;
            state.pos.z = 15;
            state.theta = (float)(Math.PI / 2.0f);
            state.omega.x = 3;
            state.omega.y = 0.1f;

            presets.Add(("Fall", state, parameters));


            parameters = default_params;
            parameters.absorption = 0.6f;
            state = default_state;
            state.vel.y = 5f;
            state.vel.z = 10f;
            state.omega.x = 20;
            presets.Add(("Come back", state, parameters));


            parameters = default_params;
            parameters.absorption = 0f;
            parameters.mu = 0;
            parameters.delta = 0;
            state = default_state;
            state.omega.x = 0.5f;
            state.omega.y = 0.2f;
            state.omega.z = 0.3f;
            state.pos.z = 2;
            presets.Add(("No energy loss", state, parameters));

            parameters = default_params;
            parameters.absorption = 0f;
            parameters.delta = 0;
            state = default_state;
            state.omega.x = -5;
            state.vel.x = -3;
            state.omega.z = -2;
            presets.Add(("Endless roll", state, parameters));

            parameters = default_params;
            parameters.g = 3.721;
            state = default_state;
            state.omega.x = 0.5f;
            state.omega.y = 0.2f;
            state.omega.z = 0.3f;
            state.pos.z = 2;
            presets.Add(("Mars gravity", state, parameters));

            parameters = default_params;
            parameters.g = 0;
            state = default_state;
            state.omega.x = 0.5f;
            state.omega.y = 0.2f;
            state.omega.z = 0.3f;
            state.pos.z = 2;
            presets.Add(("No gravity", state, parameters));

            parameters = default_params;
            // parameters.InnerRadius = -1;
            parameters.absorption = 0.5;
            parameters.InnerRadius = -0.3;
            parameters.OuterRadius = 0.3;
            parameters.delta = 0.001f;
            state = default_state;
            presets.Add(("Ball", state, parameters));

            parameters = default_params;
            parameters.delta = 0.02;
            state = default_state;
            state.vel.y = 5;
            state.theta = 0.4;
            state.pos.x = -11.283;
            state.omega.x = -state.vel.y / parameters.OuterRadius;
            presets.Add(("Spiral", state, parameters));


            parameters = default_params;
            parameters.delta = 0.00;
            state = default_state;
            state.vel.y = 5;
            state.theta = 0.4;
            state.pos.x = -11.283;
            state.omega.x = -state.vel.y / parameters.OuterRadius;
            presets.Add(("Circle", state, parameters));


            parameters = default_params;
            parameters.delta = 0.1;
            parameters.mu = 0.5;
            state = default_state;
            state.vel.y = 7;
            state.vel.x = 4;
            state.theta = 0.4;
            state.omega.x = -state.vel.y / parameters.OuterRadius;
            state.omega.z = -8;
            presets.Add(("Straightening", state, parameters));
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

            //handle keyboard input forrotation
            rotateWithKeyboard(simulation_dt);


            //update madel state
            torusModel.update(simulation_dt);
            //updates view transformation
            updateViewTransform();
            //set camera transform
            eng.camera.viewTransform = viewTransform;

            //draw axes
            Face face = eng.showFace;
            eng.showFace = Face.Both;
            bool lighting = eng.enableLighting;
            eng.enableLighting = false;
            eng.Render(axes, new Transform(mat3.Identity*2, new vec3(0, 0, 0.01f)));
            eng.showFace =face;
            eng.enableLighting= lighting;

            //draw floor
            var squareTransform = new Transform(mat3.Identity*50, new vec3(0, 0, 0f));
            eng.Render(square, squareTransform);

            //draw torus
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

        //Syncs state of model with UI controls
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
            UpdateEnergyLabels();
        }

        private void UpdateUiAngles()
        {
            txtAngle1.Content = "ϕ:\t"+(torusModel.state.fi/Math.PI*180).ToString("0.0");
            txtAngle2.Content = "θ:\t"+(torusModel.state.theta/Math.PI*180).ToString("0.0");
            txtAngle3.Content = "ψ:\t"+(torusModel.state.psi/Math.PI*180).ToString("0.0");
        }

        private void UpdateEnergyLabels()
        {
            if (txtEnergyTotal == null)
                return;

            double gravitational = torusModel.gravitationalPotentialEnergy();
            double spring = torusModel.springPotentialEnergy();
            double potential = gravitational + spring;

            double translational = torusModel.kineticEnergy();
            double rotational = torusModel.rotationalKineticEnergy();
            double kinetic = translational + rotational;
            double total = potential + kinetic;

            txtEnergyTotal.Content = "Total energy: " + total.ToString("0.000");
            txtEnergyPotential.Content = "Potential: " + potential.ToString("0.000");
            txtEnergyGravitational.Content = "\t\tGravitational: " + gravitational.ToString("0.000");
            txtEnergySpring.Content = "\t\tSpring: " + spring.ToString("0.000");
            txtEnergyKinetic.Content = "Kinetic: " + kinetic.ToString("0.000");
            txtEnergyLinear.Content = "\t\tTranslational: " + translational.ToString("0.000");
            txtEnergyRotational.Content = "\t\tRotational: " + rotational.ToString("0.000");
        }


        //handles rotation with keyboard input (WSADQE)
        void rotateWithKeyboard(float dt)
        {
            double speed = 10;

            dvec3 delta = new dvec3(0, 0, 0);
            if (Keyboard.IsKeyDown(Key.W))
                delta += new dvec3(-1,0,0);
            if (Keyboard.IsKeyDown(Key.S))
                delta -= new dvec3(-1,0,0);
            if (Keyboard.IsKeyDown(Key.A))
                delta += new dvec3(0,-1,0);
            if (Keyboard.IsKeyDown(Key.D))
                delta-= new dvec3(0,-1,0);
            if (Keyboard.IsKeyDown(Key.Q))
                delta += new dvec3(0,0,1);
            if (Keyboard.IsKeyDown(Key.E))
                delta-= new dvec3(0,0,1);

            if (delta != new dvec3(0, 0, 0))
            {
                delta = delta.Normalized * speed * dt;


                dmat3 camera_to_world = new dmat3(
                    Math.Cos(az), -Math.Sin(az),0,
                    Math.Sin(az),Math.Cos(az),0,
                    0,0,1);
                dmat3 world_to_model = torusModel.state.modelToWorld().Transposed;
                torusModel.state.omega+=world_to_model*camera_to_world*delta;
            }
        }

        //handles key press
        private void OnGlobalPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat)
                return;

            if (e.Key == Key.Space)
            {
                torusModel.state.vel.z += 10;
                e.Handled = true;
            }

            if (e.Key == Key.LeftShift)
            {
                torusModel.state.vel.z -= 10;
                e.Handled = true;
            }

            if (e.Key == Key.R)
            {
                btResetClick(null, null);
                e.Handled = true;
            }

            if (e.Key == Key.V)
            {
                cbCameraFollow.IsChecked = !cbCameraFollow.IsChecked;
                e.Handled = true;
            }

            //ui shouldn't react to this keys
            if (e.Key == Key.W || e.Key == Key.S || e.Key == Key.A || e.Key == Key.D || e.Key == Key.Q||e.Key == Key.E)
            {
                e.Handled = true;
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
                viewTransform.translation -= (vec3)torusModel.state.pos;
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

            if (idx == 0)//for default preset
            {
                torusModel.ResetState();
            }

            syncStateCallback();
        }

        private void SliderSimulationSpeed_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lbSpeed != null)
                lbSpeed.Content ="Speed: "+ sliderSpeed.Value.ToString("0.00");
        }

        private void Angle1_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.fi = angle1.Value/180.0*Math.PI;
            UpdateUiAngles();
        }

        private void Angle2_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.theta = angle2.Value/180.0*Math.PI;
            UpdateUiAngles();
        }

        private void Angle3_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.psi = angle3.Value/180.0*Math.PI;
            UpdateUiAngles();
        }


        private void Position_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (posX == sender)
            {
                torusModel.state.pos.x = posX.Value;
            }

            if (posY == sender)
            {
                torusModel.state.pos.y = posY.Value;
            }

            if (posZ == sender)
            {
                torusModel.state.pos.z = posZ.Value;
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static bool TryParseDouble(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private void SetTextBoxValue(TextBox box, double value, string format = "0.###")
        {
            if (box != null)
                box.Text = value.ToString(format, CultureInfo.InvariantCulture);
        }

        private void RefreshParameterLabels()
        {
            txtMass.Content = "m: " + torusModel.parameters.m.ToString("0.00");
            txtInnerR.Content = "Inner Radius: " + torusModel.parameters.InnerRadius.ToString("0.000");
            txtOuterR.Content = "Outer Radius: " + torusModel.parameters.OuterRadius.ToString("0.000");
            txtDelta.Content = "δ: " + torusModel.parameters.delta.ToString("0.000");
            txtK.Content = "k: " + torusModel.parameters.k.ToString("0");
            txtMu.Content = "μ: " + torusModel.parameters.mu.ToString("0.000");
            txtG.Content = "g: " + torusModel.parameters.g.ToString("0.0");
            txtAbsorption.Content = "Absorption: " + torusModel.parameters.absorption.ToString("0.000");
        }

        private void SyncParameterTextBoxes()
        {
            SetTextBoxValue(sliderMass, torusModel.parameters.m);
            SetTextBoxValue(sliderInnerR, torusModel.parameters.InnerRadius);
            SetTextBoxValue(sliderOuterR, torusModel.parameters.OuterRadius);
            SetTextBoxValue(delta, torusModel.parameters.delta);
            SetTextBoxValue(stiffness, torusModel.parameters.k);
            SetTextBoxValue(mu, torusModel.parameters.mu);
            SetTextBoxValue(g, torusModel.parameters.g);
            SetTextBoxValue(absorption, torusModel.parameters.absorption);
            RefreshParameterLabels();
        }

        private void ParameterTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            CommitParameterInput(sender as TextBox);
            Keyboard.ClearFocus();
            e.Handled = true;
        }

        private void ParameterTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitParameterInput(sender as TextBox);
        }

        private void CommitParameterInput(TextBox box)
        {
            if (box == null || torusModel == null)
                return;

            if (box == sliderMass)
            {
                double parsed;
                double value = TryParseDouble(box.Text, out parsed) ? Clamp(parsed, 0.01, 1000) : torusModel.parameters.m;
                torusModel.parameters.m = value;
                torusModel.parameters_changed();
                SetTextBoxValue(sliderMass, value);
                txtMass.Content = "m: " + value.ToString("0.00");
                return;
            }

            if (box == delta)
            {
                double parsed;
                double value = TryParseDouble(box.Text, out parsed) ? Clamp(parsed, 0, 0.2) : torusModel.parameters.delta;
                torusModel.parameters.delta = value;
                torusModel.parameters_changed();
                SetTextBoxValue(delta, value);
                txtDelta.Content = "δ: " + value.ToString("0.000");
                return;
            }

            if (box == stiffness)
            {
                double parsed;
                double value = TryParseDouble(box.Text, out parsed) ? Clamp(parsed, 0, 10000000) : torusModel.parameters.k;
                torusModel.parameters.k = value;
                SetTextBoxValue(stiffness, value);
                txtK.Content = "k: " + value.ToString("0");
                return;
            }

            if (box == mu)
            {
                double parsed;
                double value = TryParseDouble(box.Text, out parsed) ? Clamp(parsed, 0, 5) : torusModel.parameters.mu;
                torusModel.parameters.mu = value;
                torusModel.parameters_changed();
                SetTextBoxValue(mu, value);
                txtMu.Content = "μ: " + value.ToString("0.000");
                return;
            }

            if (box == g)
            {
                double parsed;
                double value = TryParseDouble(box.Text, out parsed) ? Clamp(parsed, 0, 1000) : torusModel.parameters.g;
                torusModel.parameters.g = value;
                SetTextBoxValue(g, value);
                txtG.Content = "g: " + value.ToString("0.0");
                return;
            }

            if (box == absorption)
            {
                double parsed;
                double value = TryParseDouble(box.Text, out parsed) ? Clamp(parsed, 0, 0.9999) : torusModel.parameters.absorption;
                torusModel.parameters.absorption = value;
                torusModel.parameters_changed();
                SetTextBoxValue(absorption, value);
                txtAbsorption.Content = "Absorption: " + value.ToString("0.000");
                return;
            }

            if (box == sliderInnerR)
            {
                double parsed;
                double inner = TryParseDouble(box.Text, out parsed) ? Clamp(parsed, -10, 10) : torusModel.parameters.InnerRadius;
                double outer = torusModel.parameters.OuterRadius;
                if (inner >= outer)
                {
                    outer = Math.Min(10, inner + 0.01);
                    if (inner >= outer)
                        inner = Math.Max(0.1, outer - 0.01);
                }

                torusModel.parameters.InnerRadius = inner;
                torusModel.parameters.OuterRadius = outer;
                torusModel.parameters_changed();
                SetTextBoxValue(sliderInnerR, inner);
                SetTextBoxValue(sliderOuterR, outer);
                txtInnerR.Content = "Inner Radius: " + inner.ToString("0.000");
                txtOuterR.Content = "Outer Radius: " + outer.ToString("0.000");
                return;
            }

            if (box == sliderOuterR)
            {
                double parsed;
                double outer = TryParseDouble(box.Text, out parsed) ? Clamp(parsed, 0.2, 10) : torusModel.parameters.OuterRadius;
                double inner = torusModel.parameters.InnerRadius;
                if (outer <= inner)
                {
                    inner = Math.Max(0.1, outer - 0.01);
                    if (outer <= inner)
                        outer = Math.Min(10, inner + 0.01);
                }

                torusModel.parameters.InnerRadius = inner;
                torusModel.parameters.OuterRadius = outer;
                torusModel.parameters_changed();
                SetTextBoxValue(sliderInnerR, inner);
                SetTextBoxValue(sliderOuterR, outer);
                txtInnerR.Content = "Inner Radius: " + inner.ToString("0.000");
                txtOuterR.Content = "Outer Radius: " + outer.ToString("0.000");
            }
        }



        private void OmegaX_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            torusModel.state.omega.x = omegaX.Value;
        }

        private void OmegaY_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.omega.y = omegaY.Value;
        }

        private void OmegaZ_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.omega.z = omegaZ.Value;
        }

        private void Vx_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.vel.x = Vx.Value;
        }

        private void Vy_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.vel.y = Vy.Value;
        }

        private void Vz_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            torusModel.state.vel.z = Vz.Value;
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

        // Apply selected preset to the model (parameters + state)
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

            // Update parameter UI controls
            SyncParameterTextBoxes();

            // Copy state
            torusModel.state = preset.Item2;

            btnParams.Focus();
            syncStateCallback();
        }
    }
}
