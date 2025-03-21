using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Collections.Generic;

class Simple3DEngine : GameWindow
{
    private List<Vector3> bezierSurface;
    private List<int> indices;
    private List<Vector3> normals;
    private int vao, vbo, ebo, shaderProgram;
    private float angle = 0f;
    private bool wireframeMode = false;

    // Camera variables
    private Vector2 _lastMousePos;
    private float _yaw = -90f, _pitch = 0f;
    private Vector3 _cameraPosition = new Vector3(0, 0, 5);
    private Vector3 _cameraFront = new Vector3(0, 0, -1);
    private Vector3 _cameraUp = Vector3.UnitY;

    private float pitch = 0f; // Rotation around X-axis
    private float yaw = 0f;   // Rotation around Y-axis
    private float roll = 0f;  // Rotation around Z-axis

    public Simple3DEngine()
        : base(GameWindowSettings.Default, new NativeWindowSettings()
        { Size = new Vector2i(800, 600), Title = "Simple 3D Engine" })
    { }

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
        GL.Enable(EnableCap.DepthTest);

        bezierSurface = GenerateBezierSurface();
        indices = GenerateIndices();
        normals = CalculateNormals(bezierSurface, indices);

        // Load shaders
        shaderProgram = CreateShaderProgram();
        GL.UseProgram(shaderProgram);

        // Upload data to GPU
        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();
        ebo = GL.GenBuffer();

        GL.BindVertexArray(vao);

        // Upload vertex data
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, bezierSurface.Count * sizeof(float) * 3, bezierSurface.ToArray(), BufferUsageHint.StaticDraw);

        // Upload index data
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(int), indices.ToArray(), BufferUsageHint.StaticDraw);

        // Set up vertex attributes
        int positionLocation = GL.GetAttribLocation(shaderProgram, "aPosition");
        GL.EnableVertexAttribArray(positionLocation);
        GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, sizeof(float) * 3, 0);

        // Upload normals to GPU
        int normalVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, normalVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, normals.Count * sizeof(float) * 3, normals.ToArray(), BufferUsageHint.StaticDraw);

        int normalLocation = GL.GetAttribLocation(shaderProgram, "aNormal");
        GL.EnableVertexAttribArray(normalLocation);
        GL.VertexAttribPointer(normalLocation, 3, VertexAttribPointerType.Float, false, sizeof(float) * 3, 0);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, Size.X, Size.Y);
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        // Rotation speed
        float rotationSpeed = 100f * (float)e.Time;

        // Rotate around X-axis (pitch)
        if (KeyboardState.IsKeyDown(Keys.Up) || KeyboardState.IsKeyDown(Keys.I))
            pitch += rotationSpeed;
        if (KeyboardState.IsKeyDown(Keys.Down) || KeyboardState.IsKeyDown(Keys.K))
            pitch -= rotationSpeed;

        // Rotate around Y-axis (yaw)
        if (KeyboardState.IsKeyDown(Keys.Left) || KeyboardState.IsKeyDown(Keys.J))
            yaw += rotationSpeed;
        if (KeyboardState.IsKeyDown(Keys.Right) || KeyboardState.IsKeyDown(Keys.L))
            yaw -= rotationSpeed;

        // Rotate around Z-axis (roll)
        if (KeyboardState.IsKeyDown(Keys.U))
            roll += rotationSpeed;
        if (KeyboardState.IsKeyDown(Keys.O))
            roll -= rotationSpeed;

        // Toggle wireframe mode
        if (KeyboardState.IsKeyPressed(Keys.W))
        {
            wireframeMode = !wireframeMode;
            GL.PolygonMode(MaterialFace.FrontAndBack, wireframeMode ? PolygonMode.Line : PolygonMode.Fill);
        }

        // Camera movement (unchanged)
        float cameraSpeed = 2.5f * (float)e.Time;
        if (KeyboardState.IsKeyDown(Keys.W))
            _cameraPosition += _cameraFront * cameraSpeed;
        if (KeyboardState.IsKeyDown(Keys.S))
            _cameraPosition -= _cameraFront * cameraSpeed;
        if (KeyboardState.IsKeyDown(Keys.A))
            _cameraPosition -= Vector3.Normalize(Vector3.Cross(_cameraFront, _cameraUp)) * cameraSpeed;
        if (KeyboardState.IsKeyDown(Keys.D))
            _cameraPosition += Vector3.Normalize(Vector3.Cross(_cameraFront, _cameraUp)) * cameraSpeed;
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        GL.UseProgram(shaderProgram);

        // Create rotation matrices
        Matrix4 rotationX = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(pitch));
        Matrix4 rotationY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(yaw));
        Matrix4 rotationZ = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(roll));

        // Combine rotations
        Matrix4 model = rotationX * rotationY * rotationZ;

        // Set view and projection matrices
        Matrix4 view = Matrix4.LookAt(_cameraPosition, _cameraPosition + _cameraFront, _cameraUp);
        Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, Size.X / (float)Size.Y, 0.1f, 100f);

        // Pass matrices to the shader
        int modelLoc = GL.GetUniformLocation(shaderProgram, "uModel");
        int viewLoc = GL.GetUniformLocation(shaderProgram, "uView");
        int projLoc = GL.GetUniformLocation(shaderProgram, "uProjection");

        GL.UniformMatrix4(modelLoc, false, ref model);
        GL.UniformMatrix4(viewLoc, false, ref view);
        GL.UniformMatrix4(projLoc, false, ref projection);

        // Set light properties (unchanged)
        int lightPosLoc = GL.GetUniformLocation(shaderProgram, "lightPos");
        int lightColorLoc = GL.GetUniformLocation(shaderProgram, "lightColor");
        int objectColorLoc = GL.GetUniformLocation(shaderProgram, "objectColor");

        GL.Uniform3(lightPosLoc, new Vector3(2.0f, 2.0f, 2.0f)); // Light position
        GL.Uniform3(lightColorLoc, new Vector3(1.0f, 1.0f, 1.0f)); // Light color (white)
        GL.Uniform3(objectColorLoc, new Vector3(1.0f, 0.5f, 0.2f)); // Object color (orange)

        // Draw the surface
        GL.BindVertexArray(vao);
        GL.DrawElements(PrimitiveType.Triangles, indices.Count, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);

        SwapBuffers();
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        if (_lastMousePos == default)
        {
            _lastMousePos = new Vector2(e.X, e.Y);
        }
        else
        {
            float xOffset = e.X - _lastMousePos.X;
            float yOffset = _lastMousePos.Y - e.Y; // Reversed since y-coordinates range from bottom to top
            _lastMousePos = new Vector2(e.X, e.Y);

            float sensitivity = 0.1f;
            xOffset *= sensitivity;
            yOffset *= sensitivity;

            _yaw += xOffset;
            _pitch += yOffset;

            // Clamp pitch to avoid flipping
            if (_pitch > 89.0f) _pitch = 89.0f;
            if (_pitch < -89.0f) _pitch = -89.0f;

            // Update camera direction
            _cameraFront.X = MathF.Cos(MathHelper.DegreesToRadians(_yaw)) * MathF.Cos(MathHelper.DegreesToRadians(_pitch));
            _cameraFront.Y = MathF.Sin(MathHelper.DegreesToRadians(_pitch));
            _cameraFront.Z = MathF.Sin(MathHelper.DegreesToRadians(_yaw)) * MathF.Cos(MathHelper.DegreesToRadians(_pitch));
            _cameraFront = Vector3.Normalize(_cameraFront);
        }
    }

    private List<Vector3> GenerateBezierSurface()
    {
        List<Vector3> points = new List<Vector3>();
        for (float u = 0; u <= 1; u += 0.02f) // Reduced step size for denser mesh
        {
            for (float v = 0; v <= 1; v += 0.02f)
            {
                points.Add(ComputeBezierPoint(u, v));
            }
        }
        return points;
    }

    private List<int> GenerateIndices()
    {
        List<int> indices = new List<int>();
        int gridSize = (int)Math.Sqrt(bezierSurface.Count);

        for (int i = 0; i < gridSize - 1; i++)
        {
            for (int j = 0; j < gridSize - 1; j++)
            {
                int topLeft = i * gridSize + j;
                int topRight = topLeft + 1;
                int bottomLeft = (i + 1) * gridSize + j;
                int bottomRight = bottomLeft + 1;

                // First triangle
                indices.Add(topLeft);
                indices.Add(bottomLeft);
                indices.Add(topRight);

                // Second triangle
                indices.Add(topRight);
                indices.Add(bottomLeft);
                indices.Add(bottomRight);
            }
        }
        return indices;
    }

    private List<Vector3> CalculateNormals(List<Vector3> vertices, List<int> indices)
    {
        List<Vector3> normals = new List<Vector3>(new Vector3[vertices.Count]);

        for (int i = 0; i < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 normal = Vector3.Cross(edge1, edge2).Normalized();

            normals[i0] += normal;
            normals[i1] += normal;
            normals[i2] += normal;
        }

        for (int i = 0; i < normals.Count; i++)
        {
            normals[i] = normals[i].Normalized();
        }

        return normals;
    }

    private static Vector3 ComputeBezierPoint(float u, float v)
    {
        Vector3[,] controlPoints = new Vector3[4, 4]
        {
            { new Vector3(-1.5f, -1.5f, 0), new Vector3(-0.5f, -1.5f, 1), new Vector3(0.5f, -1.5f, 1), new Vector3(1.5f, -1.5f, 0) },
            { new Vector3(-1.5f, -0.5f, 1), new Vector3(-0.5f, -0.5f, 2), new Vector3(0.5f, -0.5f, 2), new Vector3(1.5f, -0.5f, 1) },
            { new Vector3(-1.5f,  0.5f, 1), new Vector3(-0.5f,  0.5f, 2), new Vector3(0.5f,  0.5f, 2), new Vector3(1.5f,  0.5f, 1) },
            { new Vector3(-1.5f,  1.5f, 0), new Vector3(-0.5f,  1.5f, 1), new Vector3(0.5f,  1.5f, 1), new Vector3(1.5f,  1.5f, 0) }
        };

        Vector3[] temp = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            temp[i] = CubicBezier(controlPoints[i, 0], controlPoints[i, 1], controlPoints[i, 2], controlPoints[i, 3], u);
        }

        return CubicBezier(temp[0], temp[1], temp[2], temp[3], v);
    }

    private static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1 - t;
        float uu = u * u;
        float uuu = uu * u;
        float tt = t * t;
        float ttt = tt * t;

        return (uuu * p0) + (3 * uu * t * p1) + (3 * u * tt * p2) + (ttt * p3);
    }

    private int CreateShaderProgram()
    {
        string vertexShaderSource = @"
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec3 FragPos;
        out vec3 Normal;

        void main()
        {
            FragPos = vec3(uModel * vec4(aPosition, 1.0));
            Normal = mat3(transpose(inverse(uModel))) * aNormal; // Transform normals to world space
            gl_Position = uProjection * uView * vec4(FragPos, 1.0);
        }";

        string fragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;

        in vec3 FragPos;
        in vec3 Normal;

        uniform vec3 lightPos;
        uniform vec3 lightColor;
        uniform vec3 objectColor;

        void main()
        {
            // Ambient lighting
            float ambientStrength = 0.1;
            vec3 ambient = ambientStrength * lightColor;

            // Diffuse lighting
            vec3 norm = normalize(Normal);
            vec3 lightDir = normalize(lightPos - FragPos);
            float diff = max(dot(norm, lightDir), 0.0);
            vec3 diffuse = diff * lightColor;

            // Combine results
            vec3 result = (ambient + diffuse) * objectColor;
            FragColor = vec4(result, 1.0);
        }";

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexShaderSource);
        GL.CompileShader(vertexShader);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentShaderSource);
        GL.CompileShader(fragmentShader);

        int shaderProgram = GL.CreateProgram();
        GL.AttachShader(shaderProgram, vertexShader);
        GL.AttachShader(shaderProgram, fragmentShader);
        GL.LinkProgram(shaderProgram);

        GL.DetachShader(shaderProgram, vertexShader);
        GL.DetachShader(shaderProgram, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return shaderProgram;
    }

    [STAThread]
    static void Main() => new Simple3DEngine().Run();
}