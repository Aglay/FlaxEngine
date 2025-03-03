// Copyright (c) 2012-2023 Wojciech Figat. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FlaxEditor.Content;
using FlaxEditor.Content.Import;
using FlaxEditor.CustomEditors;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.CustomEditors.GUI;
using FlaxEditor.GUI;
using FlaxEditor.Scripting;
using FlaxEditor.Viewport.Cameras;
using FlaxEditor.Viewport.Previews;
using FlaxEngine;
using FlaxEngine.GUI;
using Object = FlaxEngine.Object;

namespace FlaxEditor.Windows.Assets
{
    /// <summary>
    /// Editor window to view/modify <see cref="SkinnedModel"/> asset.
    /// </summary>
    /// <seealso cref="SkinnedModel" />
    /// <seealso cref="FlaxEditor.Windows.Assets.AssetEditorWindow" />
    public sealed class SkinnedModelWindow : ModelBaseWindow<SkinnedModel, SkinnedModelWindow>
    {
        private sealed class Preview : AnimatedModelPreview
        {
            private readonly SkinnedModelWindow _window;

            public Preview(SkinnedModelWindow window)
            : base(true)
            {
                _window = window;
            }

            /// <inheritdoc />
            public override void Draw()
            {
                base.Draw();

                var style = Style.Current;
                var asset = _window.Asset;
                if (asset == null || !asset.IsLoaded)
                {
                    Render2D.DrawText(style.FontLarge, "Loading...", new Rectangle(Float2.Zero, Size), style.ForegroundDisabled, TextAlignment.Center, TextAlignment.Center);
                }
            }
        }

        [CustomEditor(typeof(ProxyEditor))]
        private sealed class MeshesPropertiesProxy : PropertiesProxyBase
        {
            private readonly List<ComboBox> _materialSlotComboBoxes = new List<ComboBox>();
            private readonly List<CheckBox> _isolateCheckBoxes = new List<CheckBox>();
            private readonly List<CheckBox> _highlightCheckBoxes = new List<CheckBox>();

            public override void OnLoad(SkinnedModelWindow window)
            {
                base.OnLoad(window);

                Window._isolateIndex = -1;
                Window._highlightIndex = -1;
            }

            public override void OnClean()
            {
                Window._isolateIndex = -1;
                Window._highlightIndex = -1;

                base.OnClean();
            }

            /// <summary>
            /// Updates the highlight/isolate effects on UI.
            /// </summary>
            public void UpdateEffectsOnUI()
            {
                Window._skipEffectsGuiEvents = true;

                for (int i = 0; i < _isolateCheckBoxes.Count; i++)
                {
                    var checkBox = _isolateCheckBoxes[i];
                    checkBox.Checked = Window._isolateIndex == ((SkinnedMesh)checkBox.Tag).MaterialSlotIndex;
                }

                for (int i = 0; i < _highlightCheckBoxes.Count; i++)
                {
                    var checkBox = _highlightCheckBoxes[i];
                    checkBox.Checked = Window._highlightIndex == ((SkinnedMesh)checkBox.Tag).MaterialSlotIndex;
                }

                Window._skipEffectsGuiEvents = false;
            }

            /// <summary>
            /// Updates the material slots UI parts. Should be called after material slot rename.
            /// </summary>
            public void UpdateMaterialSlotsUI()
            {
                Window._skipEffectsGuiEvents = true;

                // Generate material slots labels (with index prefix)
                var slots = Asset.MaterialSlots;
                var slotsLabels = new string[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    slotsLabels[i] = string.Format("[{0}] {1}", i, slots[i].Name);
                }

                // Update comboboxes
                for (int i = 0; i < _materialSlotComboBoxes.Count; i++)
                {
                    var comboBox = _materialSlotComboBoxes[i];
                    comboBox.SetItems(slotsLabels);
                    comboBox.SelectedIndex = ((SkinnedMesh)comboBox.Tag).MaterialSlotIndex;
                }

                Window._skipEffectsGuiEvents = false;
            }

            /// <summary>
            /// Sets the material slot index to the mesh.
            /// </summary>
            /// <param name="mesh">The mesh.</param>
            /// <param name="newSlotIndex">New index of the material slot to use.</param>
            public void SetMaterialSlot(SkinnedMesh mesh, int newSlotIndex)
            {
                if (Window._skipEffectsGuiEvents)
                    return;

                mesh.MaterialSlotIndex = newSlotIndex == -1 ? 0 : newSlotIndex;
                Window.UpdateEffectsOnAsset();
                UpdateEffectsOnUI();
                Window.MarkAsEdited();
            }

            /// <summary>
            /// Sets the material slot to isolate.
            /// </summary>
            /// <param name="mesh">The mesh.</param>
            public void SetIsolate(SkinnedMesh mesh)
            {
                if (Window._skipEffectsGuiEvents)
                    return;

                Window._isolateIndex = mesh?.MaterialSlotIndex ?? -1;
                Window.UpdateEffectsOnAsset();
                UpdateEffectsOnUI();
            }

            /// <summary>
            /// Sets the material slot index to highlight.
            /// </summary>
            /// <param name="mesh">The mesh.</param>
            public void SetHighlight(SkinnedMesh mesh)
            {
                if (Window._skipEffectsGuiEvents)
                    return;

                Window._highlightIndex = mesh?.MaterialSlotIndex ?? -1;
                Window.UpdateEffectsOnAsset();
                UpdateEffectsOnUI();
            }

            private class ProxyEditor : ProxyEditorBase
            {
                public override void Initialize(LayoutElementsContainer layout)
                {
                    var proxy = (MeshesPropertiesProxy)Values[0];
                    if (proxy.Asset == null || !proxy.Asset.IsLoaded)
                    {
                        layout.Label("Loading...");
                        return;
                    }
                    proxy._materialSlotComboBoxes.Clear();
                    proxy._isolateCheckBoxes.Clear();
                    proxy._highlightCheckBoxes.Clear();
                    var lods = proxy.Asset.LODs;
                    var loadedLODs = proxy.Asset.LoadedLODs;
                    var nodes = proxy.Asset.Nodes;
                    var bones = proxy.Asset.Bones;

                    // General properties
                    {
                        var group = layout.Group("General");

                        var minScreenSize = group.FloatValue("Min Screen Size", "The minimum screen size to draw model (the bottom limit). Used to cull small models. Set to 0 to disable this feature.");
                        minScreenSize.ValueBox.MinValue = 0.0f;
                        minScreenSize.ValueBox.MaxValue = 1.0f;
                        minScreenSize.ValueBox.Value = proxy.Asset.MinScreenSize;
                        minScreenSize.ValueBox.ValueChanged += () =>
                        {
                            proxy.Asset.MinScreenSize = minScreenSize.ValueBox.Value;
                            proxy.Window.MarkAsEdited();
                        };
                    }

                    // Group per LOD
                    for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                    {
                        var group = layout.Group("LOD " + lodIndex);
                        if (lodIndex < lods.Length - loadedLODs)
                        {
                            group.Label("Loading LOD...");
                            continue;
                        }
                        var lod = lods[lodIndex];
                        var meshes = lod.Meshes;

                        int triangleCount = 0, vertexCount = 0;
                        for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
                        {
                            var mesh = meshes[meshIndex];
                            triangleCount += mesh.TriangleCount;
                            vertexCount += mesh.VertexCount;
                        }

                        group.Label(string.Format("Triangles: {0:N0}   Vertices: {1:N0}", triangleCount, vertexCount)).AddCopyContextMenu();
                        group.Label("Size: " + lod.Box.Size);
                        var screenSize = group.FloatValue("Screen Size", "The screen size to switch LODs. Bottom limit of the model screen size to render this LOD.");
                        screenSize.ValueBox.MinValue = 0.0f;
                        screenSize.ValueBox.MaxValue = 10.0f;
                        screenSize.ValueBox.Value = lod.ScreenSize;
                        screenSize.ValueBox.ValueChanged += () =>
                        {
                            lod.ScreenSize = screenSize.ValueBox.Value;
                            proxy.Window.MarkAsEdited();
                        };

                        // Every mesh properties
                        for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
                        {
                            var mesh = meshes[meshIndex];
                            group.Label($"Mesh {meshIndex} (tris: {mesh.TriangleCount:N0}, verts: {mesh.VertexCount:N0})").AddCopyContextMenu();

                            // Material Slot
                            var materialSlot = group.ComboBox("Material Slot", "Material slot used by this mesh during rendering");
                            materialSlot.ComboBox.Tag = mesh;
                            materialSlot.ComboBox.SelectedIndexChanged += comboBox => proxy.SetMaterialSlot((SkinnedMesh)comboBox.Tag, comboBox.SelectedIndex);
                            proxy._materialSlotComboBoxes.Add(materialSlot.ComboBox);

                            // Isolate
                            var isolate = group.Checkbox("Isolate", "Shows only this mesh (and meshes using the same material slot)");
                            isolate.CheckBox.Tag = mesh;
                            isolate.CheckBox.StateChanged += (box) => proxy.SetIsolate(box.Checked ? (SkinnedMesh)box.Tag : null);
                            proxy._isolateCheckBoxes.Add(isolate.CheckBox);

                            // Highlight
                            var highlight = group.Checkbox("Highlight", "Highlights this mesh with a tint color (and meshes using the same material slot)");
                            highlight.CheckBox.Tag = mesh;
                            highlight.CheckBox.StateChanged += (box) => proxy.SetHighlight(box.Checked ? (SkinnedMesh)box.Tag : null);
                            proxy._highlightCheckBoxes.Add(highlight.CheckBox);
                        }
                    }

                    // Refresh UI
                    proxy.UpdateMaterialSlotsUI();
                }

                internal override void RefreshInternal()
                {
                    // Skip updates when model is not loaded
                    var proxy = (MeshesPropertiesProxy)Values[0];
                    if (proxy.Asset == null || !proxy.Asset.IsLoaded)
                        return;

                    base.RefreshInternal();
                }
            }
        }

        [CustomEditor(typeof(ProxyEditor))]
        private sealed class SkeletonPropertiesProxy : PropertiesProxyBase
        {
            private class ProxyEditor : ProxyEditorBase
            {
                public override void Initialize(LayoutElementsContainer layout)
                {
                    var proxy = (SkeletonPropertiesProxy)Values[0];
                    if (proxy.Asset == null || !proxy.Asset.IsLoaded)
                    {
                        layout.Label("Loading...");
                        return;
                    }
                    var lods = proxy.Asset.LODs;
                    var loadedLODs = proxy.Asset.LoadedLODs;
                    var nodes = proxy.Asset.Nodes;
                    var bones = proxy.Asset.Bones;

                    // Skeleton Bones
                    {
                        var group = layout.Group("Skeleton Bones");

                        var tree = group.Tree();
                        for (int i = 0; i < bones.Length; i++)
                        {
                            if (bones[i].ParentIndex == -1)
                            {
                                var node = tree.Node(nodes[bones[i].NodeIndex].Name);
                                BuildSkeletonBonesTree(nodes, bones, node, i);
                                node.TreeNode.ExpandAll(true);
                            }
                        }
                    }

                    // Skeleton Nodes
                    {
                        var group = layout.Group("Skeleton Nodes");

                        var tree = group.Tree();
                        for (int i = 0; i < nodes.Length; i++)
                        {
                            if (nodes[i].ParentIndex == -1)
                            {
                                var node = tree.Node(nodes[i].Name);
                                BuildSkeletonNodesTree(nodes, node, i);
                                node.TreeNode.ExpandAll(true);
                            }
                        }
                    }

                    // Blend Shapes
                    var blendShapes = proxy.Asset.BlendShapes;
                    if (blendShapes.Length != 0)
                    {
                        var group = layout.Group("Blend Shapes");
                        proxy.Window._preview.PreviewActor.ClearBlendShapeWeights();

                        for (int i = 0; i < blendShapes.Length; i++)
                        {
                            var blendShape = blendShapes[i];
                            var label = new PropertyNameLabel(blendShape);
                            label.SetupContextMenu += (nameLabel, menu, linkedEditor) => { menu.AddButton("Copy name", () => Clipboard.Text = blendShape); };
                            var property = group.AddPropertyItem(label);
                            var editor = property.FloatValue();
                            editor.ValueBox.Value = 0.0f;
                            editor.ValueBox.MinValue = -1;
                            editor.ValueBox.MaxValue = 1;
                            editor.ValueBox.SlideSpeed = 0.01f;
                            editor.ValueBox.ValueChanged += () => { proxy.Window._preview.SetBlendShapeWeight(blendShape, editor.ValueBox.Value); };
                        }
                    }
                }

                private void BuildSkeletonBonesTree(SkeletonNode[] nodes, SkeletonBone[] bones, TreeNodeElement layout, int boneIndex)
                {
                    for (int i = 0; i < bones.Length; i++)
                    {
                        if (bones[i].ParentIndex == boneIndex)
                        {
                            var node = layout.Node(nodes[bones[i].NodeIndex].Name);
                            BuildSkeletonBonesTree(nodes, bones, node, i);
                        }
                    }
                }

                private void BuildSkeletonNodesTree(SkeletonNode[] nodes, TreeNodeElement layout, int nodeIndex)
                {
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        if (nodes[i].ParentIndex == nodeIndex)
                        {
                            var node = layout.Node(nodes[i].Name);
                            BuildSkeletonNodesTree(nodes, node, i);
                        }
                    }
                }

                internal override void RefreshInternal()
                {
                    // Skip updates when model is not loaded
                    var proxy = (MeshesPropertiesProxy)Values[0];
                    if (proxy.Asset == null || !proxy.Asset.IsLoaded)
                        return;

                    base.RefreshInternal();
                }
            }
        }

        [CustomEditor(typeof(ProxyEditor))]
        private sealed class MaterialsPropertiesProxy : PropertiesProxyBase
        {
            [Collection(CanReorderItems = true, NotNullItems = true, OverrideEditorTypeName = "FlaxEditor.CustomEditors.Editors.GenericEditor", Spacing = 10)]
            [EditorOrder(10), EditorDisplay("Materials", EditorDisplayAttribute.InlineStyle)]
            public MaterialSlot[] MaterialSlots
            {
                get => Asset?.MaterialSlots;
                set
                {
                    if (Asset != null)
                    {
                        if (Asset.MaterialSlots.Length != value.Length)
                        {
                            MaterialBase[] materials = new MaterialBase[value.Length];
                            string[] names = new string[value.Length];
                            ShadowsCastingMode[] shadowsModes = new ShadowsCastingMode[value.Length];
                            for (int i = 0; i < value.Length; i++)
                            {
                                if (value[i] != null)
                                {
                                    materials[i] = value[i].Material;
                                    names[i] = value[i].Name;
                                    shadowsModes[i] = value[i].ShadowsMode;
                                }
                                else
                                {
                                    materials[i] = null;
                                    names[i] = "Material " + i;
                                    shadowsModes[i] = ShadowsCastingMode.All;
                                }
                            }

                            Asset.SetupMaterialSlots(value.Length);

                            var slots = Asset.MaterialSlots;
                            for (int i = 0; i < slots.Length; i++)
                            {
                                slots[i].Material = materials[i];
                                slots[i].Name = names[i];
                                slots[i].ShadowsMode = shadowsModes[i];
                            }

                            UpdateMaterialSlotsUI();
                        }
                    }
                }
            }

            private readonly List<ComboBox> _materialSlotComboBoxes = new List<ComboBox>();

            /// <summary>
            /// Updates the material slots UI parts. Should be called after material slot rename.
            /// </summary>
            public void UpdateMaterialSlotsUI()
            {
                Window._skipEffectsGuiEvents = true;

                // Generate material slots labels (with index prefix)
                var slots = Asset.MaterialSlots;
                var slotsLabels = new string[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    slotsLabels[i] = string.Format("[{0}] {1}", i, slots[i].Name);
                }

                // Update comboboxes
                for (int i = 0; i < _materialSlotComboBoxes.Count; i++)
                {
                    var comboBox = _materialSlotComboBoxes[i];
                    comboBox.SetItems(slotsLabels);
                    comboBox.SelectedIndex = ((SkinnedMesh)comboBox.Tag).MaterialSlotIndex;
                }

                Window._skipEffectsGuiEvents = false;
            }

            private class ProxyEditor : ProxyEditorBase
            {
                public override void Initialize(LayoutElementsContainer layout)
                {
                    var proxy = (MaterialsPropertiesProxy)Values[0];
                    if (proxy.Asset == null || !proxy.Asset.IsLoaded)
                    {
                        layout.Label("Loading...");
                        return;
                    }

                    base.Initialize(layout);
                }
            }
        }

        [CustomEditor(typeof(ProxyEditor))]
        private sealed class UVsPropertiesProxy : PropertiesProxyBase
        {
            public enum UVChannel
            {
                None,
                TexCoord,
            };

            private UVChannel _uvChannel = UVChannel.None;

            [EditorOrder(0), EditorDisplay(null, "Preview UV Channel"), EnumDisplay(EnumDisplayAttribute.FormatMode.None)]
            [Tooltip("Set UV channel to preview.")]
            public UVChannel Channel
            {
                get => _uvChannel;
                set
                {
                    if (_uvChannel == value)
                        return;
                    _uvChannel = value;
                    Window.RequestMeshData();
                }
            }

            [EditorOrder(1), EditorDisplay(null, "LOD"), Limit(0, Model.MaxLODs), VisibleIf("ShowUVs")]
            [Tooltip("Level Of Detail index to preview UVs layout.")]
            public int LOD = 0;

            [EditorOrder(2), EditorDisplay(null, "Mesh"), Limit(-1, 1000000), VisibleIf("ShowUVs")]
            [Tooltip("Mesh index to preview UVs layout. Use -1 for all meshes")]
            public int Mesh = -1;

            private bool ShowUVs => _uvChannel != UVChannel.None;

            /// <inheritdoc />
            public override void OnClean()
            {
                Channel = UVChannel.None;

                base.OnClean();
            }

            private class ProxyEditor : ProxyEditorBase
            {
                private UVsLayoutPreviewControl _uvsPreview;

                public override void Initialize(LayoutElementsContainer layout)
                {
                    var proxy = (UVsPropertiesProxy)Values[0];
                    if (proxy.Asset == null || !proxy.Asset.IsLoaded)
                    {
                        layout.Label("Loading...");
                        return;
                    }

                    base.Initialize(layout);

                    _uvsPreview = layout.Custom<UVsLayoutPreviewControl>().CustomControl;
                    _uvsPreview.Proxy = proxy;
                }

                /// <inheritdoc />
                public override void Refresh()
                {
                    base.Refresh();

                    if (_uvsPreview != null)
                    {
                        _uvsPreview.Channel = _uvsPreview.Proxy._uvChannel;
                        _uvsPreview.LOD = _uvsPreview.Proxy.LOD;
                        _uvsPreview.Mesh = _uvsPreview.Proxy.Mesh;
                        _uvsPreview.HighlightIndex = _uvsPreview.Proxy.Window._highlightIndex;
                        _uvsPreview.IsolateIndex = _uvsPreview.Proxy.Window._isolateIndex;
                    }
                }

                protected override void Deinitialize()
                {
                    _uvsPreview = null;

                    base.Deinitialize();
                }
            }

            private sealed class UVsLayoutPreviewControl : RenderToTextureControl
            {
                private UVChannel _channel;
                private int _lod, _mesh = -1;
                private int _highlightIndex = -1;
                private int _isolateIndex = -1;
                public UVsPropertiesProxy Proxy;

                public UVsLayoutPreviewControl()
                {
                    Offsets = new Margin(4);
                    AutomaticInvalidate = false;
                }

                public UVChannel Channel
                {
                    set
                    {
                        if (_channel == value)
                            return;
                        _channel = value;
                        Visible = _channel != UVChannel.None;
                        if (Visible)
                            Invalidate();
                    }
                }

                public int LOD
                {
                    set
                    {
                        if (_lod != value)
                        {
                            _lod = value;
                            Invalidate();
                        }
                    }
                }

                public int Mesh
                {
                    set
                    {
                        if (_mesh != value)
                        {
                            _mesh = value;
                            Invalidate();
                        }
                    }
                }

                public int HighlightIndex
                {
                    set
                    {
                        if (_highlightIndex != value)
                        {
                            _highlightIndex = value;
                            Invalidate();
                        }
                    }
                }

                public int IsolateIndex
                {
                    set
                    {
                        if (_isolateIndex != value)
                        {
                            _isolateIndex = value;
                            Invalidate();
                        }
                    }
                }

                private void DrawMeshUVs(int meshIndex, MeshData meshData)
                {
                    var uvScale = Size;
                    var linesColor = _highlightIndex != -1 && _highlightIndex == meshIndex ? Style.Current.BackgroundSelected : Color.White;
                    switch (_channel)
                    {
                    case UVChannel.TexCoord:
                        for (int i = 0; i < meshData.IndexBuffer.Length; i += 3)
                        {
                            // Cache triangle indices
                            uint i0 = meshData.IndexBuffer[i + 0];
                            uint i1 = meshData.IndexBuffer[i + 1];
                            uint i2 = meshData.IndexBuffer[i + 2];

                            // Cache triangle uvs positions and transform positions to output target
                            Float2 uv0 = meshData.VertexBuffer[i0].TexCoord * uvScale;
                            Float2 uv1 = meshData.VertexBuffer[i1].TexCoord * uvScale;
                            Float2 uv2 = meshData.VertexBuffer[i2].TexCoord * uvScale;

                            // Don't draw too small triangles
                            float area = Float2.TriangleArea(ref uv0, ref uv1, ref uv2);
                            if (area > 3.0f)
                            {
                                // Draw triangle
                                Render2D.DrawLine(uv0, uv1, linesColor);
                                Render2D.DrawLine(uv1, uv2, linesColor);
                                Render2D.DrawLine(uv2, uv0, linesColor);
                            }
                        }
                        break;
                    }
                }

                /// <inheritdoc />
                protected override void DrawChildren()
                {
                    base.DrawChildren();

                    var size = Size;
                    if (_channel == UVChannel.None || size.MaxValue < 5.0f)
                        return;
                    if (!Proxy.Window.RequestMeshData())
                    {
                        Invalidate();
                        Render2D.DrawText(Style.Current.FontMedium, "Loading...", new Rectangle(Float2.Zero, size), Color.White, TextAlignment.Center, TextAlignment.Center);
                        return;
                    }

                    Render2D.PushClip(new Rectangle(Float2.Zero, size));

                    var meshDatas = Proxy.Window._meshDatas;
                    var lodIndex = Mathf.Clamp(_lod, 0, meshDatas.Length - 1);
                    var lod = meshDatas[lodIndex];
                    var mesh = Mathf.Clamp(_mesh, -1, lod.Length - 1);
                    if (mesh == -1)
                    {
                        for (int meshIndex = 0; meshIndex < lod.Length; meshIndex++)
                        {
                            if (_isolateIndex != -1 && _isolateIndex != meshIndex)
                                continue;
                            DrawMeshUVs(meshIndex, lod[meshIndex]);
                        }
                    }
                    else
                    {
                        DrawMeshUVs(mesh, lod[mesh]);
                    }

                    Render2D.PopClip();
                }

                protected override void OnSizeChanged()
                {
                    Height = Width;

                    base.OnSizeChanged();
                }

                protected override void OnVisibleChanged()
                {
                    base.OnVisibleChanged();

                    Parent.PerformLayout();
                    Height = Width;
                }
            }
        }

        [CustomEditor(typeof(ProxyEditor))]
        private sealed class ImportPropertiesProxy : PropertiesProxyBase
        {
            private ModelImportSettings ImportSettings = new ModelImportSettings();

            /// <inheritdoc />
            public override void OnLoad(SkinnedModelWindow window)
            {
                base.OnLoad(window);

                ModelImportSettings.TryRestore(ref ImportSettings, window.Item.Path);
            }

            public void Reimport()
            {
                Editor.Instance.ContentImporting.Reimport((BinaryAssetItem)Window.Item, ImportSettings, true);
            }

            private class ProxyEditor : ProxyEditorBase
            {
                public override void Initialize(LayoutElementsContainer layout)
                {
                    var proxy = (ImportPropertiesProxy)Values[0];
                    if (proxy.Asset == null || !proxy.Asset.IsLoaded)
                    {
                        layout.Label("Loading...");
                        return;
                    }

                    // Import Settings
                    {
                        var group = layout.Group("Import Settings");

                        var importSettingsField = typeof(ImportPropertiesProxy).GetField("ImportSettings", BindingFlags.NonPublic | BindingFlags.Instance);
                        var importSettingsValues = new ValueContainer(new ScriptMemberInfo(importSettingsField)) { proxy.ImportSettings };
                        group.Object(importSettingsValues);

                        layout.Space(5);
                        var reimportButton = group.Button("Reimport");
                        reimportButton.Button.Clicked += () => ((ImportPropertiesProxy)Values[0]).Reimport();
                    }
                }
            }
        }

        private class MeshesTab : Tab
        {
            public MeshesTab(SkinnedModelWindow window)
            : base("Meshes", window)
            {
                Proxy = new MeshesPropertiesProxy();
                Presenter.Select(Proxy);
            }
        }

        private class SkeletonTab : Tab
        {
            public SkeletonTab(SkinnedModelWindow window)
            : base("Skeleton", window)
            {
                Proxy = new SkeletonPropertiesProxy();
                Presenter.Select(Proxy);
            }
        }

        private class MaterialsTab : Tab
        {
            public MaterialsTab(SkinnedModelWindow window)
            : base("Materials", window)
            {
                Proxy = new MaterialsPropertiesProxy();
                Presenter.Select(Proxy);
            }
        }

        private class UVsTab : Tab
        {
            public UVsTab(SkinnedModelWindow window)
            : base("UVs", window, false)
            {
                Proxy = new UVsPropertiesProxy();
                Presenter.Select(Proxy);
            }
        }

        private class ImportTab : Tab
        {
            public ImportTab(SkinnedModelWindow window)
            : base("Import", window, false)
            {
                Proxy = new ImportPropertiesProxy();
                Presenter.Select(Proxy);
            }
        }

        private struct MeshData
        {
            public uint[] IndexBuffer;
            public SkinnedMesh.Vertex[] VertexBuffer;
        }

        private Preview _preview;
        private AnimatedModel _highlightActor;
        private ToolStripButton _showNodesButton;

        private MeshData[][] _meshDatas;
        private bool _meshDatasInProgress;
        private bool _meshDatasCancel;

        /// <inheritdoc />
        public SkinnedModelWindow(Editor editor, AssetItem item)
        : base(editor, item)
        {
            // Toolstrip
            _toolstrip.AddSeparator();
            _showNodesButton = (ToolStripButton)_toolstrip.AddButton(editor.Icons.Bone64, () => _preview.ShowNodes = !_preview.ShowNodes).LinkTooltip("Show animated model nodes debug view");
            _toolstrip.AddSeparator();
            _toolstrip.AddButton(editor.Icons.Docs64, () => Platform.OpenUrl(Utilities.Constants.DocsUrl + "manual/animation/skinned-model/index.html")).LinkTooltip("See documentation to learn more");

            // Model preview
            _preview = new Preview(this)
            {
                ViewportCamera = new FPSCamera(),
                ScaleToFit = false,
                Parent = _split.Panel1
            };

            // Properties tabs
            _tabs.AddTab(new MeshesTab(this));
            _tabs.AddTab(new SkeletonTab(this));
            _tabs.AddTab(new MaterialsTab(this));
            _tabs.AddTab(new UVsTab(this));
            _tabs.AddTab(new ImportTab(this));

            // Highlight actor (used to highlight selected material slot, see UpdateEffectsOnAsset)
            _highlightActor = new AnimatedModel
            {
                IsActive = false
            };
            _preview.Task.AddCustomActor(_highlightActor);
        }

        /// <summary>
        /// Updates the highlight/isolate effects on a model asset.
        /// </summary>
        private void UpdateEffectsOnAsset()
        {
            var entries = _preview.PreviewActor.Entries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    entries[i].Visible = _isolateIndex == -1 || _isolateIndex == i;
                }
                _preview.PreviewActor.Entries = entries;
            }

            if (_highlightIndex != -1)
            {
                _highlightActor.IsActive = true;

                var highlightMaterial = EditorAssets.Cache.HighlightMaterialInstance;
                entries = _highlightActor.Entries;
                if (entries != null)
                {
                    for (int i = 0; i < entries.Length; i++)
                    {
                        entries[i].Material = highlightMaterial;
                        entries[i].Visible = _highlightIndex == i;
                    }
                }
                _highlightActor.Entries = entries;
            }
            else
            {
                _highlightActor.IsActive = false;
            }
        }

        private bool RequestMeshData()
        {
            if (_meshDatasInProgress)
                return false;
            if (_meshDatas != null)
                return true;
            _meshDatasInProgress = true;
            _meshDatasCancel = false;
            Task.Run(new Action(DownloadMeshData));
            return false;
        }

        private void WaitForMeshDataRequestEnd()
        {
            if (_meshDatasInProgress)
            {
                _meshDatasCancel = true;
                for (int i = 0; i < 500 && _meshDatasInProgress; i++)
                    Thread.Sleep(10);
            }
        }

        private void DownloadMeshData()
        {
            try
            {
                if (!_asset)
                {
                    _meshDatasInProgress = false;
                    return;
                }
                var lods = _asset.LODs;
                _meshDatas = new MeshData[lods.Length][];

                for (int lodIndex = 0; lodIndex < lods.Length && !_meshDatasCancel; lodIndex++)
                {
                    var lod = lods[lodIndex];
                    var meshes = lod.Meshes;
                    _meshDatas[lodIndex] = new MeshData[meshes.Length];

                    for (int meshIndex = 0; meshIndex < meshes.Length && !_meshDatasCancel; meshIndex++)
                    {
                        var mesh = meshes[meshIndex];
                        _meshDatas[lodIndex][meshIndex] = new MeshData
                        {
                            IndexBuffer = mesh.DownloadIndexBuffer(),
                            VertexBuffer = mesh.DownloadVertexBuffer()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Editor.LogWarning("Failed to get mesh data. " + ex.Message);
                Editor.LogWarning(ex);
            }
            finally
            {
                _meshDatasInProgress = false;
            }
        }

        /// <inheritdoc />
        public override void Update(float deltaTime)
        {
            // Sync highlight actor size with actual preview model (preview scales model for better usage experience)
            if (_highlightActor && _highlightActor.IsActive)
            {
                _highlightActor.Transform = _preview.PreviewActor.Transform;
            }

            // Model is loaded but LODs data may be during streaming so refresh properties on fully loaded
            if (_refreshOnLODsLoaded && _asset && _asset.LoadedLODs == _asset.LODs.Length)
            {
                _refreshOnLODsLoaded = false;
                foreach (var child in _tabs.Children)
                {
                    if (child is Tab tab)
                    {
                        tab.Presenter.BuildLayout();
                    }
                }
            }

            _showNodesButton.Checked = _preview.ShowNodes;

            base.Update(deltaTime);
        }

        /// <inheritdoc />
        public override void Save()
        {
            if (!IsEdited)
                return;

            if (_asset.Save())
            {
                Editor.LogError("Cannot save asset.");
                return;
            }

            ClearEditedFlag();
            _item.RefreshThumbnail();
        }

        /// <inheritdoc />
        protected override void UnlinkItem()
        {
            WaitForMeshDataRequestEnd();
            _preview.SkinnedModel = null;
            _highlightActor.SkinnedModel = null;

            base.UnlinkItem();
        }

        /// <inheritdoc />
        protected override void OnAssetLinked()
        {
            _preview.SkinnedModel = _asset;
            _highlightActor.SkinnedModel = _asset;

            base.OnAssetLinked();
        }

        /// <inheritdoc />
        protected override void OnAssetLoaded()
        {
            _refreshOnLODsLoaded = true;
            _preview.ViewportCamera.SetArcBallView(Asset.GetBox());
            UpdateEffectsOnAsset();

            // TODO: disable streaming for this model

            // Reset any root motion
            _preview.PreviewActor.ResetLocalTransform();

            base.OnAssetLoaded();
        }

        /// <inheritdoc />
        public override void OnItemReimported(ContentItem item)
        {
            // Discard any old mesh data cache
            WaitForMeshDataRequestEnd();
            _meshDatas = null;
            _meshDatasInProgress = false;

            base.OnItemReimported(item);
        }

        /// <inheritdoc />
        public override void OnDestroy()
        {
            WaitForMeshDataRequestEnd();

            base.OnDestroy();

            Object.Destroy(ref _highlightActor);
            _preview = null;
            _showNodesButton = null;
        }
    }
}
