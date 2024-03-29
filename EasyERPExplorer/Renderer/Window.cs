﻿using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EasyERPExplorer.Renderer
{
    public class Window : GameWindow
    {
        ImGuiController _controller;
        public static List<ImGuiDrawWindow> DrawWindows { get; private set; } = new();
        public static Window Instance { get; private set; }

        public Window() : base(GameWindowSettings.Default, new NativeWindowSettings() { Size = new Vector2i(1600, 900), APIVersion = new Version(4, 5) })
        {
            Instance = this;

            // Init icon
            if (File.Exists("EasyERPExplorer.ico"))
            {
                using var icon = new System.Drawing.Bitmap("EasyERPExplorer.ico");
                Icon = new OpenTK.Windowing.Common.Input.WindowIcon(new OpenTK.Windowing.Common.Input.Image(icon.Width, icon.Height, Utils.ImgToBytes(icon)));
            }
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            string version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(3);
            Title = "EasyERPExplorer v" + version;

            _controller = new ImGuiController(ClientSize.X, ClientSize.Y);

            WindowState = WindowState.Maximized;
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            // Update the opengl viewport
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            // Tell ImGui of the new size
            _controller.WindowResized(ClientSize.X, ClientSize.Y);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            _controller.Update(this, (float)e.Time);

            GL.ClearColor(new Color4(10, 10, 10, 255));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            DrawWindows.Where(w => !w.IsOpen).ToList().ForEach(w => DrawWindows.Remove(w));

            ImGui.PushStyleColor(ImGuiCol.Header, 0xff202020);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0xff4d4d4d);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0xff777777);
            ImGui.PushFont(ImGuiController.Fonts[ERPLoader.Settings.Instance.ExplorerSettings.FontSize]);
            foreach (var window in DrawWindows.ToArray())
            {
                window.Draw();
            }
            ImGui.PopFont();
            ImGui.PopStyleColor(3);

            _controller.Render();

            RenderUtils.CheckGLError("End of frame");

            SwapBuffers();
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);


            _controller.PressChar((char)e.Unicode);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            _controller.MouseScroll(e.Offset);
        }
    }
}
