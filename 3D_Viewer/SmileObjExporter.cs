

namespace smileUp
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Media3D;
    using System.Windows.Media.Imaging;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Serialization;
    using System.Globalization;
    using HelixToolkit.Wpf;
    using smileUp.DataModel;


    public class SmileObjExporter //: Exporter
    {
        /// <summary>
        /// Gets or sets a value indicating whether to export normals.
        /// </summary>
        public bool ExportNormals { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use "d" for transparency (default is "Tr").
        /// </summary>
        public bool UseDissolveForTransparency { get; set; }

        #region Constants and Fields

        /// <summary>
        ///   The directory.
        /// </summary>
        private readonly string directory;

        /// <summary>
        /// The exported materials.
        /// </summary>
        private readonly Dictionary<Material, string> exportedMaterials = new Dictionary<Material, string>();

        /// <summary>
        ///   The mwriter.
        /// </summary>
        private readonly StreamWriter mwriter;

        /// <summary>
        ///   The writer.
        /// </summary>
        private readonly StreamWriter writer;

        /// <summary>
        ///   The group no.
        /// </summary>
        private int groupNo = 1;

        public RawVisual3D rawVisual;
        public JawVisual3D jawVisual;
        public Patient patient;

        /// <summary>
        ///   The mat no.
        /// </summary>
        private int matNo = 1;

        /// <summary>
        ///   Normal index counter.
        /// </summary>
        private int normalIndex = 1;

        /// <summary>
        ///   The object no.
        /// </summary>
        private int objectNo = 1;

        /// <summary>
        ///   Texture index counter.
        /// </summary>
        private int textureIndex = 1;

        /// <summary>
        ///   Vertex index counter.
        /// </summary>
        private int vertexIndex = 1;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjExporter"/> class.
        /// </summary>
        /// <param name="outputFileName">
        /// Name of the output file.
        /// </param>
        public SmileObjExporter(string outputFileName)
            : this(outputFileName, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjExporter"/> class.
        /// </summary>
        /// <param name="outputFileName">
        /// Name of the output file.
        /// </param>
        /// <param name="comment">
        /// The comment.
        /// </param>
        public SmileObjExporter(string outputFileName, string comment)
        {
            this.SwitchYZ = false;
            this.ExportNormals = false;

            var fullPath = Path.GetFullPath(outputFileName);
            var mtlPath = Path.ChangeExtension(outputFileName, ".mtl");
            string mtlFilename = Path.GetFileName(mtlPath);
            this.directory = Path.GetDirectoryName(fullPath);

            this.writer = new StreamWriter(outputFileName);
            this.mwriter = new StreamWriter(mtlPath);

            if (!string.IsNullOrEmpty(comment))
            {
                this.writer.WriteLine(string.Format("# {0}", comment));
            }

            this.writer.WriteLine("mtllib ./" + mtlFilename);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets a value indicating whether to switch Y and Z coordinates.
        /// </summary>
        public bool SwitchYZ { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Closes this exporter.
        /// </summary>
        public void Close()
        {
            this.writer.Close();
            this.mwriter.Close();
            //base.Close();
        }

        /// <summary>
        /// The export mesh.
        /// </summary>
        /// <param name="m">
        /// The m.
        /// </param>
        /// <param name="t">
        /// The t.
        /// </param>
        public void ExportMesh(MeshGeometry3D m, Transform3D t)
        {
            if (m == null)
            {
                throw new ArgumentNullException("m");
            }

            if (t == null)
            {
                throw new ArgumentNullException("t");
            }

            // mapping from local indices (0-based) to the obj file indices (1-based)
            var vertexIndexMap = new Dictionary<int, int>();
            var textureIndexMap = new Dictionary<int, int>();
            var normalIndexMap = new Dictionary<int, int>();

            int index = 0;
            if (m.Positions != null)
            {
                foreach (var v in m.Positions)
                {
                    vertexIndexMap.Add(index++, this.vertexIndex++);
                    var p = t.Transform(v);
                    this.writer.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "v {0} {1} {2}",
                            p.X,
                            this.SwitchYZ ? p.Z : p.Y,
                            this.SwitchYZ ? -p.Y : p.Z));
                }

                this.writer.WriteLine(string.Format("# {0} vertices", index));
            }

            if (m.TextureCoordinates != null)
            {
                index = 0;
                foreach (var vt in m.TextureCoordinates)
                {
                    textureIndexMap.Add(index++, this.textureIndex++);
                    this.writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "vt {0} {1}", vt.X, 1 - vt.Y));
                }

                this.writer.WriteLine(string.Format("# {0} texture coordinates", index));
            }

            if (m.Normals != null && ExportNormals)
            {
                index = 0;
                foreach (var vn in m.Normals)
                {
                    normalIndexMap.Add(index++, this.normalIndex++);
                    this.writer.WriteLine(
                        string.Format(CultureInfo.InvariantCulture, "vn {0} {1} {2}", vn.X, vn.Y, vn.Z));
                }

                this.writer.WriteLine(string.Format("# {0} normals", index));
            }

            Func<int, string> formatIndices = i0 =>
                {
                    bool hasTextureIndex = textureIndexMap.ContainsKey(i0);
                    bool hasNormalIndex = normalIndexMap.ContainsKey(i0);
                    if (hasTextureIndex && hasNormalIndex)
                    {
                        return string.Format("{0}/{1}/{2}", vertexIndexMap[i0], textureIndexMap[i0], normalIndexMap[i0]);
                    }

                    if (hasTextureIndex)
                    {
                        return string.Format("{0}/{1}", vertexIndexMap[i0], textureIndexMap[i0]);
                    }

                    if (hasNormalIndex)
                    {
                        return string.Format("{0}//{1}", vertexIndexMap[i0], normalIndexMap[i0]);
                    }

                    return vertexIndexMap[i0].ToString();
                };

            if (m.TriangleIndices != null)
            {
                for (int i = 0; i < m.TriangleIndices.Count; i += 3)
                {
                    int i0 = m.TriangleIndices[i];
                    int i1 = m.TriangleIndices[i + 1];
                    int i2 = m.TriangleIndices[i + 2];

                    this.writer.WriteLine("f {0} {1} {2}", formatIndices(i0), formatIndices(i1), formatIndices(i2));
                }

                this.writer.WriteLine(string.Format("# {0} faces", m.TriangleIndices.Count / 3));
            }

            this.writer.WriteLine();
        }

        #endregion

        #region Methods

        public static void TraverseModel<T>(Model3D model, Transform3D transform, Action<T, Transform3D> action)
            where T : Model3D
        {
            var mg = model as Model3DGroup;
            if (mg != null)
            {
                var childTransform = Transform3DHelper.CombineTransform(model.Transform, transform);
                foreach (var m in mg.Children)
                {
                    TraverseModel(m, childTransform, action);
                }
            }

            var gm = model as T;
            if (gm != null)
            {
                var childTransform = Transform3DHelper.CombineTransform(model.Transform, transform);
                action(gm, childTransform);
            }
        }

        private static Model3D GetModel(Visual3D visual)
        {
            Model3D model= null;
            var mv = visual as ModelVisual3D;
            if (mv != null)
            {
                model = mv.Content;
            }
            else
            {
                //model = Visual3DModelPropertyInfo.GetValue(visual, null) as Model3D;
            }

            return model;
        }

        private static IEnumerable<Visual3D> GetChildren(Visual3D visual)
        {
            int n = VisualTreeHelper.GetChildrenCount(visual);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(visual, i) as Visual3D;
                if (child == null)
                {
                    continue;
                }

                yield return child;
            }
        }
        private void Traverse<T>(Visual3D visual, Transform3D transform, Action<T, Transform3D> action)
            where T : Model3D
        {
            var childTransform = Transform3DHelper.CombineTransform(visual.Transform, transform);
            var model = GetModel(visual);
            if (model != null)
            {
                if (jawVisual != null)
                {

                }
                else if (rawVisual != null)
                {

                }

                if (visual is TeethVisual3D)
                {
                    TeethVisual3D t = (TeethVisual3D)visual;
                    this.writer.WriteLine(string.Format("g jaw_{0}", t.Id));
                }
                else if (visual is GumVisual3D)
                {
                    GumVisual3D t = (GumVisual3D)visual;
                    this.writer.WriteLine(string.Format("g jaw_{0}", t.Id));
                }
                else if (visual is BraceVisual3D)
                {
                    BraceVisual3D t = (BraceVisual3D)visual;
                    this.writer.WriteLine(string.Format("g jaw_{0}", t.Id));
                }
                else if (visual is WireVisual3D)
                {

                }
                else
                {
                    this.writer.WriteLine(string.Format("g jaw_group{0}", this.groupNo++));
                }

                if (visual is Manipulator || visual is BoundingBoxWireFrameVisual3D)
                {
                }
                else
                {
                    TraverseModel(model, childTransform, action);
                }
            }

            foreach (var child in GetChildren(visual))
            {
                Traverse(child, childTransform, action);
            }
        }
        public  void Traverse<T>(Visual3D visual, Action<T, Transform3D> action) where T : Model3D
        {
            Traverse(visual, Transform3D.Identity, action);
        }

        public void Export(Visual3D visual, Patient p)
        {
            //this.ExportHeader();
            this.patient = p;
            Traverse<GeometryModel3D>(visual, this.ExportModel);
        }

        public static void RenderBrush(string path, Brush brush, int w, int h)
        {
            var ib = brush as ImageBrush;
            if (ib != null)
            {
                var bi = ib.ImageSource as BitmapImage;
                if (bi != null)
                {
                    w = bi.PixelWidth;
                    h = bi.PixelHeight;
                }
            }

            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var rect = new Grid
            {
                Background = brush,
                Width = 1,
                Height = 1,
                LayoutTransform = new ScaleTransform(w, h)
            };
            rect.Arrange(new Rect(0, 0, w, h));
            bmp.Render(rect);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));

            using (Stream stm = File.Create(path))
            {
                encoder.Save(stm);
            }
        }
        /// <summary>
        /// The export model.
        /// </summary>
        /// <param name="model">
        /// The model.
        /// </param>
        /// <param name="transform">
        /// The transform.
        /// </param>
        protected  void ExportModel(GeometryModel3D model, Transform3D transform)
        {
            this.writer.WriteLine(string.Format("o object{0}", this.objectNo++));
            //this.writer.WriteLine(string.Format("g group{0}", this.groupNo++));

            //IEnumerable<DependencyObject> p = model.Ancestors();
            if (this.exportedMaterials.ContainsKey(model.Material))
            {
                string matName = this.exportedMaterials[model.Material];
                this.writer.WriteLine(string.Format("usemtl {0}", matName));
            }
            else
            {
                string matName = string.Format("mat{0}", this.matNo++);
                this.writer.WriteLine(string.Format("usemtl {0}", matName));
                this.ExportMaterial(matName, model.Material, model.BackMaterial);
                this.exportedMaterials.Add(model.Material, matName);
            }

            var mesh = model.Geometry as MeshGeometry3D;
            this.ExportMesh(mesh, Transform3DHelper.CombineTransform(transform, model.Transform));
        }

        /// <summary>
        /// The export material.
        /// </summary>
        /// <param name="matName">
        /// The mat name.
        /// </param>
        /// <param name="material">
        /// The material.
        /// </param>
        /// <param name="backMaterial">
        /// The back material.
        /// </param>
        private void ExportMaterial(string matName, Material material, Material backMaterial)
        {
            this.mwriter.WriteLine(string.Format("newmtl {0}", matName));
            var dm = material as DiffuseMaterial;
            var sm = material as SpecularMaterial;
            var mg = material as MaterialGroup;
            if (mg != null)
            {
                foreach (var m in mg.Children)
                {
                    if (m is DiffuseMaterial)
                    {
                        dm = m as DiffuseMaterial;
                    }

                    if (m is SpecularMaterial)
                    {
                        sm = m as SpecularMaterial;
                    }
                }
            }

            if (dm != null)
            {
                //var adjustedAmbientColor = dm.AmbientColor.ChangeIntensity(0.2);
                var adjustedAmbientColor = dm.AmbientColor.ChangeIntensity(1);

                // this.mwriter.WriteLine(string.Format("Ka {0}", this.ToColorString(adjustedAmbientColor)));
                var scb = dm.Brush as SolidColorBrush;
                if (scb != null)
                {
                    this.mwriter.WriteLine(string.Format("Kd {0}", this.ToColorString(scb.Color)));

                    if (this.UseDissolveForTransparency)
                    {
                        // Dissolve factor
                        this.mwriter.WriteLine(
                            string.Format(CultureInfo.InvariantCulture, "d {0:F4}", scb.Color.A / 255.0));
                    }
                    else
                    {
                        // Transparency 
                        this.mwriter.WriteLine(
                            string.Format(CultureInfo.InvariantCulture, "Tr {0:F4}", scb.Color.A / 255.0));
                    }
                }
                else
                {
                    var textureFilename = matName + ".png";
                    var texturePath = Path.Combine(this.directory, textureFilename);

                    // create .png bitmap file for the brush
                    Exporter.RenderBrush(texturePath, dm.Brush, 1024, 1024);
                    this.mwriter.WriteLine(string.Format("map_Ka {0}", textureFilename));
                }
            }

/*            // Illumination model 1
            // This is a diffuse illumination model using Lambertian shading. The 
            // color includes an ambient constant term and a diffuse shading term for 
            // each light source.  The formula is
            // color = KaIa + Kd { SUM j=1..ls, (N * Lj)Ij }
            int illum = 1; // Lambertian

            if (sm != null)
            {
                var scb = sm.Brush as SolidColorBrush;
                this.mwriter.WriteLine(
                    string.Format(
                        "Ks {0}", this.ToColorString(scb != null ? scb.Color : Color.FromScRgb(1.0f, 0.2f, 0.2f, 0.2f))));

                // Illumination model 2
                // This is a diffuse and specular illumination model using Lambertian 
                // shading and Blinn's interpretation of Phong's specular illumination 
                // model (BLIN77).  The color includes an ambient constant term, and a 
                // diffuse and specular shading term for each light source.  The formula 
                // is: color = KaIa + Kd { SUM j=1..ls, (N*Lj)Ij } + Ks { SUM j=1..ls, ((H*Hj)^Ns)Ij }
                illum = 1;
                //illum = 2;

                // Specifies the specular exponent for the current material.  This defines the focus of the specular highlight.
                // "exponent" is the value for the specular exponent.  A high exponent results in a tight, concentrated highlight.  Ns values normally range from 0 to 1000.
                this.mwriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "Ns {0:F4}", sm.SpecularPower));
            }

            // roughness
            this.mwriter.WriteLine(string.Format("Ns {0}", 2));

            // Optical density (index of refraction)
            this.mwriter.WriteLine(string.Format("Ni {0}", 1));

            // Transmission filter
            this.mwriter.WriteLine(string.Format("Tf {0} {1} {2}", 1, 1, 1));

            // Illumination model
            // Illumination    Properties that are turned on in the 
            // model           Property Editor
            // 0		Color on and Ambient off
            // 1		Color on and Ambient on
            // 2		Highlight on
            // 3		Reflection on and Ray trace on
            // 4		Transparency: Glass on
            // Reflection: Ray trace on
            // 5		Reflection: Fresnel on and Ray trace on
            // 6		Transparency: Refraction on
            // Reflection: Fresnel off and Ray trace on
            // 7		Transparency: Refraction on
            // Reflection: Fresnel on and Ray trace on
            // 8		Reflection on and Ray trace off
            // 9		Transparency: Glass on
            // Reflection: Ray trace off
            // 10		Casts shadows onto invisible surfaces
            this.mwriter.WriteLine(string.Format("illum {0}", illum));
*/
        }

        /// <summary>
        /// Converts a color to a string.
        /// </summary>
        /// <param name="color">
        /// The color.
        /// </param>
        /// <returns>
        /// The string.
        /// </returns>
        private string ToColorString(Color color)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0:F4} {1:F4} {2:F4}", color.R / 255.0, color.G / 255.0, color.B / 255.0);
        }

        #endregion
 
    }
}