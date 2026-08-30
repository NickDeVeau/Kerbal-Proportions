using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

[assembly: AssemblyTitle("Kerbal Proportions")]
[assembly: AssemblyDescription("Additive Kerbal rig editor with viewport gumball")]
[assembly: AssemblyCompany("Nick DeVeau")]
[assembly: AssemblyProduct("Kerbal Proportions")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Nick DeVeau")]
[assembly: AssemblyVersion("2.6.0.0")]
[assembly: AssemblyFileVersion("2.6.0.0")]

namespace KerbalProportions
{
    internal enum EditMode { Move, Rotate, Scale }

    internal sealed class PortraitFraming
    {
        internal float Horizontal;
        internal float Vertical;
        internal float Zoom = 1f;
        internal float Yaw;
        internal float Pitch;

        internal PortraitFraming Clone()
        {
            return new PortraitFraming { Horizontal = Horizontal,
                Vertical = Vertical, Zoom = Zoom, Yaw = Yaw, Pitch = Pitch };
        }

        internal void CopyFrom(PortraitFraming source)
        {
            if (source == null) return;
            Horizontal = source.Horizontal;
            Vertical = source.Vertical;
            Zoom = source.Zoom;
            Yaw = source.Yaw;
            Pitch = source.Pitch;
            Clamp();
        }

        internal void Reset()
        {
            Horizontal = Vertical = Yaw = Pitch = 0f;
            Zoom = 1f;
        }

        internal void Clamp()
        {
            Horizontal = Mathf.Clamp(Horizontal, -0.25f, 0.25f);
            Vertical = Mathf.Clamp(Vertical, -0.25f, 0.25f);
            Zoom = Mathf.Clamp(Zoom, 0.5f, 2f);
            Yaw = Mathf.Clamp(Yaw, -30f, 30f);
            Pitch = Mathf.Clamp(Pitch, -30f, 30f);
        }
    }

    internal sealed class TransformEdit
    {
        internal string Key = string.Empty;
        internal string Name = string.Empty;
        internal Vector3 Position = Vector3.zero;
        internal Vector3 Rotation = Vector3.zero;
        internal Vector3 Scale = Vector3.one;
        internal Vector3 AnimationInfluence = Vector3.one;

        internal TransformEdit Clone()
        {
            return new TransformEdit { Key = Key, Name = Name,
                Position = Position, Rotation = Rotation, Scale = Scale,
                AnimationInfluence = AnimationInfluence };
        }

        internal void CopyValuesFrom(TransformEdit source)
        {
            if (source == null) return;
            Position = source.Position;
            Rotation = source.Rotation;
            Scale = source.Scale;
            AnimationInfluence = source.AnimationInfluence;
        }

        internal bool IsIdentity
        {
            get
            {
                return Position.sqrMagnitude < 0.00000001f &&
                    Rotation.sqrMagnitude < 0.00000001f &&
                    (Scale - Vector3.one).sqrMagnitude < 0.00000001f &&
                    (AnimationInfluence - Vector3.one).sqrMagnitude < 0.00000001f;
            }
        }
    }

    internal sealed class TargetGroupMemberDefinition
    {
        internal string Key = string.Empty;
        internal string Name = string.Empty;

        internal TargetGroupMemberDefinition Clone()
        {
            return new TargetGroupMemberDefinition { Key = Key, Name = Name };
        }
    }

    internal sealed class TargetGroupDefinition
    {
        internal string Id = string.Empty;
        internal string Name = string.Empty;
        internal readonly List<TargetGroupMemberDefinition> Members =
            new List<TargetGroupMemberDefinition>();

        internal TargetGroupDefinition Clone()
        {
            TargetGroupDefinition copy = new TargetGroupDefinition {
                Id = Id, Name = Name };
            foreach (TargetGroupMemberDefinition member in Members)
                copy.Members.Add(member.Clone());
            return copy;
        }
    }

    internal sealed class RuntimeTargetGroup
    {
        internal string Id = string.Empty;
        internal string Name = string.Empty;
        internal bool Automatic;
        internal readonly List<RigTarget> Members = new List<RigTarget>();
    }

    internal sealed class EditorSettings
    {
        internal bool Enabled = true;
        internal bool EnableEva = true;
        internal bool EnableIva = false;
        internal bool ShowWindow = false;
        internal bool LocalSpace = true;
        internal bool ShowBones = true;
        internal bool ShowMeshes = true;
        internal bool ShowColliders = true;
        internal bool AnimationAwareRotation = false;
        internal float GizmoSize = 1f;
        internal float HierarchyWindowX = 12f;
        internal float HierarchyWindowY = 25f;
        internal float ControlsWindowX = 668f;
        internal float ControlsWindowY = 25f;
        internal readonly PortraitFraming Portrait = new PortraitFraming();
        internal bool LoadedLegacyFormat;
        internal int Revision;
        internal readonly Dictionary<string, TransformEdit> Edits =
            new Dictionary<string, TransformEdit>(StringComparer.Ordinal);
        internal readonly List<TargetGroupDefinition> Groups =
            new List<TargetGroupDefinition>();

        internal static string SettingsPath
        {
            get { return Path.Combine(KSPUtil.ApplicationRootPath,
                "GameData/KerbalProportions/PluginData/settings.cfg"); }
        }

        internal static string LegacySettingsPath
        {
            get { return Path.Combine(KSPUtil.ApplicationRootPath,
                "GameData/KerbalProportionsV2/PluginData/settings.cfg"); }
        }

        internal static EditorSettings Load()
        {
            EditorSettings result = new EditorSettings();
            try
            {
                string loadPath = File.Exists(SettingsPath) ? SettingsPath :
                    LegacySettingsPath;
                ConfigNode root = ConfigNode.Load(loadPath);
                ConfigNode node = root == null ? null :
                    root.GetNode("KERBAL_PROPORTIONS");
                if (node == null && root != null)
                    node = root.GetNode("KERBAL_PROPORTIONS_V2");
                if (node == null && root != null &&
                    (root.name == "KERBAL_PROPORTIONS" ||
                     root.name == "KERBAL_PROPORTIONS_V2")) node = root;
                if (node == null) return result;
                result.LoadedLegacyFormat =
                    !string.Equals(loadPath, SettingsPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    node.name == "KERBAL_PROPORTIONS_V2";
                result.Enabled = ReadBool(node, "enabled", true);
                result.EnableEva = ReadBool(node, "enableEva", true);
                result.EnableIva = ReadBool(node, "enableIva", false);
                result.ShowWindow = ReadBool(node, "showWindow", false);
                result.LocalSpace = ReadBool(node, "localSpace", true);
                result.ShowBones = ReadBool(node, "showBones", true);
                result.ShowMeshes = ReadBool(node, "showMeshes", true);
                result.ShowColliders = ReadBool(node, "showColliders", true);
                result.AnimationAwareRotation = ReadBool(node,
                    "animationAwareRotation", false);
                result.GizmoSize = Mathf.Clamp(ReadFloat(node, "gizmoSize", 1f),
                    0.4f, 2.5f);
                result.HierarchyWindowX = ReadFloat(node,
                    "hierarchyWindowX", 12f);
                result.HierarchyWindowY = ReadFloat(node,
                    "hierarchyWindowY", 25f);
                result.ControlsWindowX = ReadFloat(node,
                    "controlsWindowX", 668f);
                result.ControlsWindowY = ReadFloat(node,
                    "controlsWindowY", 25f);
                result.Portrait.Horizontal = ReadFloat(node,
                    "portraitHorizontal", 0f);
                result.Portrait.Vertical = ReadFloat(node,
                    "portraitVertical", 0f);
                result.Portrait.Zoom = ReadFloat(node, "portraitZoom", 1f);
                result.Portrait.Yaw = ReadFloat(node, "portraitYaw", 0f);
                result.Portrait.Pitch = ReadFloat(node, "portraitPitch", 0f);
                result.Portrait.Clamp();
                foreach (ConfigNode target in node.GetNodes("TARGET"))
                {
                    string key = target.GetValue("key") ?? string.Empty;
                    if (key.Length == 0) continue;
                    TransformEdit edit = new TransformEdit {
                        Key = key, Name = target.GetValue("name") ?? key,
                        Position = ReadVector(target, "position", Vector3.zero),
                        Rotation = ReadVector(target, "rotation", Vector3.zero),
                        Scale = ReadVector(target, "scale", Vector3.one),
                        AnimationInfluence = ReadVector(target,
                            "animationInfluence", Vector3.one)
                    };
                    edit.Scale = ClampScale(edit.Scale);
                    edit.AnimationInfluence = ClampAnimationInfluence(
                        edit.AnimationInfluence);
                    result.Edits[key] = edit;
                }
                ReadGroups(node, result.Groups);
                result.Revision++;
            }
            catch (Exception exception)
            {
                Debug.LogError("[KerbalProportions] Settings load failed: " +
                    exception);
            }
            return result;
        }

        internal void Save()
        {
            ConfigNode root = new ConfigNode();
            ConfigNode node = root.AddNode("KERBAL_PROPORTIONS");
            node.AddValue("enabled", Enabled);
            node.AddValue("enableEva", EnableEva);
            node.AddValue("enableIva", EnableIva);
            node.AddValue("showWindow", false);
            node.AddValue("localSpace", LocalSpace);
            node.AddValue("showBones", ShowBones);
            node.AddValue("showMeshes", ShowMeshes);
            node.AddValue("showColliders", ShowColliders);
            node.AddValue("animationAwareRotation", AnimationAwareRotation);
            node.AddValue("gizmoSize", Format(GizmoSize));
            node.AddValue("hierarchyWindowX", Format(HierarchyWindowX));
            node.AddValue("hierarchyWindowY", Format(HierarchyWindowY));
            node.AddValue("controlsWindowX", Format(ControlsWindowX));
            node.AddValue("controlsWindowY", Format(ControlsWindowY));
            node.AddValue("portraitHorizontal", Format(Portrait.Horizontal));
            node.AddValue("portraitVertical", Format(Portrait.Vertical));
            node.AddValue("portraitZoom", Format(Portrait.Zoom));
            node.AddValue("portraitYaw", Format(Portrait.Yaw));
            node.AddValue("portraitPitch", Format(Portrait.Pitch));
            foreach (TransformEdit edit in Edits.Values)
            {
                if (edit.IsIdentity) continue;
                ConfigNode target = node.AddNode("TARGET");
                target.AddValue("key", edit.Key);
                target.AddValue("name", edit.Name);
                WriteVector(target, "position", edit.Position);
                WriteVector(target, "rotation", edit.Rotation);
                WriteVector(target, "scale", edit.Scale);
                WriteVector(target, "animationInfluence",
                    edit.AnimationInfluence);
            }
            WriteGroups(node, Groups);
            string directory = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            root.Save(SettingsPath);
            LoadedLegacyFormat = false;
        }

        internal static void WriteGroups(ConfigNode node,
            IEnumerable<TargetGroupDefinition> groups)
        {
            foreach (TargetGroupDefinition definition in groups)
            {
                if (definition == null || definition.Members.Count < 2) continue;
                ConfigNode group = node.AddNode("GROUP");
                group.AddValue("id", definition.Id);
                group.AddValue("name", definition.Name);
                foreach (TargetGroupMemberDefinition definitionMember in
                    definition.Members)
                {
                    ConfigNode member = group.AddNode("MEMBER");
                    member.AddValue("key", definitionMember.Key);
                    member.AddValue("name", definitionMember.Name);
                }
            }
        }

        internal static void ReadGroups(ConfigNode node,
            List<TargetGroupDefinition> destination)
        {
            destination.Clear();
            if (node == null) return;
            foreach (ConfigNode group in node.GetNodes("GROUP"))
            {
                TargetGroupDefinition definition = new TargetGroupDefinition {
                    Id = group.GetValue("id") ?? string.Empty,
                    Name = group.GetValue("name") ?? "Accessory group" };
                foreach (ConfigNode member in group.GetNodes("MEMBER"))
                {
                    string key = member.GetValue("key") ?? string.Empty;
                    string name = member.GetValue("name") ?? string.Empty;
                    if (key.Length == 0 && name.Length == 0) continue;
                    definition.Members.Add(new TargetGroupMemberDefinition {
                        Key = key, Name = name });
                }
                if (definition.Members.Count < 2) continue;
                if (definition.Id.Length == 0)
                    definition.Id = "group-" + destination.Count.ToString(
                        CultureInfo.InvariantCulture);
                destination.Add(definition);
            }
        }

        internal TransformEdit GetOrCreate(string key, string name)
        {
            TransformEdit edit;
            if (Edits.TryGetValue(key, out edit) && string.Equals(edit.Name,
                name, StringComparison.OrdinalIgnoreCase)) return edit;
            if (TryFindBestMatch(key, name, out edit)) return edit;
            edit = new TransformEdit { Key = key, Name = name };
            Edits.Add(key, edit);
            Revision++;
            return edit;
        }

        internal bool TryGetForTarget(string key, string name,
            out TransformEdit edit)
        {
            return MatchForTarget(key, name, out edit) != 0;
        }

        // 0 = no match, 1 = exact hierarchy key, 2 = semantic bone/mesh name.
        // Runtime Kerbal hierarchies vary with suit and IVA state, so full paths
        // are retained for precision but are not allowed to block a compatible
        // name match on another Kerbal instance.
        internal int MatchForTarget(string key, string name,
            out TransformEdit edit)
        {
            if (Edits.TryGetValue(key, out edit) &&
                string.Equals(edit.Name, name,
                    StringComparison.OrdinalIgnoreCase)) return 1;
            return TryFindBestMatch(key, name, out edit) ? 2 : 0;
        }

        private bool TryFindBestMatch(string key, string name,
            out TransformEdit result)
        {
            result = null;
            int bestScore = int.MinValue;
            bool ambiguous = false;
            foreach (TransformEdit candidate in Edits.Values)
            {
                if (!string.Equals(candidate.Name, name,
                    StringComparison.OrdinalIgnoreCase)) continue;
                int score = SemanticPathScore(key, candidate.Key);
                if (result == null || score > bestScore)
                {
                    result = candidate;
                    bestScore = score;
                    ambiguous = false;
                }
                else if (score == bestScore && candidate != result)
                    ambiguous = true;
            }
            if (ambiguous) result = null;
            return result != null;
        }

        private static int SemanticPathScore(string first, string second)
        {
            string[] a = (first ?? string.Empty).Split('/');
            string[] b = (second ?? string.Empty).Split('/');
            int ai = a.Length - 1, bi = b.Length - 1;
            int matchingNames = 0, matchingSegments = 0;
            while (ai >= 0 && bi >= 0)
            {
                string aName = SegmentName(a[ai]);
                string bName = SegmentName(b[bi]);
                if (!string.Equals(aName, bName,
                    StringComparison.OrdinalIgnoreCase)) break;
                matchingNames++;
                if (string.Equals(a[ai], b[bi],
                    StringComparison.OrdinalIgnoreCase)) matchingSegments++;
                ai--; bi--;
            }
            // Ancestor-name agreement is the stable part across suit/IVA rigs;
            // sibling indices only break ties between duplicate-named children.
            return matchingNames * 1000 + matchingSegments;
        }

        private static string SegmentName(string segment)
        {
            int bracket = (segment ?? string.Empty).LastIndexOf('[');
            return bracket > 0 ? segment.Substring(0, bracket) : segment;
        }

        internal Dictionary<string, TransformEdit> CloneEdits()
        {
            Dictionary<string, TransformEdit> copy =
                new Dictionary<string, TransformEdit>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, TransformEdit> pair in Edits)
                copy[pair.Key] = pair.Value.Clone();
            return copy;
        }

        internal void ReplaceEdits(Dictionary<string, TransformEdit> edits)
        {
            Edits.Clear();
            foreach (KeyValuePair<string, TransformEdit> pair in edits)
                Edits[pair.Key] = pair.Value.Clone();
            Revision++;
        }

        internal void ClearEdits()
        {
            if (Edits.Count == 0) return;
            Edits.Clear();
            Revision++;
        }

        internal static Vector3 ClampScale(Vector3 value)
        {
            return new Vector3(Mathf.Clamp(value.x, 0.05f, 5f),
                Mathf.Clamp(value.y, 0.05f, 5f),
                Mathf.Clamp(value.z, 0.05f, 5f));
        }

        internal static Vector3 ClampAnimationInfluence(Vector3 value)
        {
            return new Vector3(Mathf.Clamp01(value.x),
                Mathf.Clamp01(value.y), Mathf.Clamp01(value.z));
        }

        internal static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static bool ReadBool(ConfigNode node, string key, bool fallback)
        {
            bool value;
            return bool.TryParse(node.GetValue(key), out value) ? value : fallback;
        }

        private static float ReadFloat(ConfigNode node, string key, float fallback)
        {
            float value;
            return float.TryParse(node.GetValue(key), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static Vector3 ReadVector(ConfigNode node, string key,
            Vector3 fallback)
        {
            return new Vector3(ReadFloat(node, key + "X", fallback.x),
                ReadFloat(node, key + "Y", fallback.y),
                ReadFloat(node, key + "Z", fallback.z));
        }

        private static void WriteVector(ConfigNode node, string key, Vector3 value)
        {
            node.AddValue(key + "X", Format(value.x));
            node.AddValue(key + "Y", Format(value.y));
            node.AddValue(key + "Z", Format(value.z));
        }
    }

    internal sealed class RigTarget
    {
        internal Transform Transform;
        internal string Key;
        internal string DisplayName;
        internal string Category;
        internal RigTarget ParentTarget;
        internal readonly List<RigTarget> Children = new List<RigTarget>();
        internal int HierarchyDepth;
        internal Vector3 BasePosition;
        internal Quaternion BaseRotation;
        internal Quaternion ReferenceRotation;
        internal Quaternion FilteredBaseRotation;
        internal Vector3 BaseScale;
        internal Vector3 LastPosition;
        internal Quaternion LastRotation;
        internal Vector3 LastScale;
        internal bool HasLast;
        internal int MatchRevision = -1;
        internal bool HasMatchedEdit;
        internal TransformEdit MatchedEdit;
        internal readonly List<BoneRendererBinding> RendererBindings =
            new List<BoneRendererBinding>();
        internal readonly List<ColliderBinding> ColliderBindings =
            new List<ColliderBinding>();

        internal void Apply(TransformEdit edit, bool animationAwareRotation,
            Vector3? stableScale)
        {
            if (Transform == null) return;
            Vector3 currentPosition = Transform.localPosition;
            Quaternion currentRotation = Transform.localRotation;
            Vector3 currentScale = Transform.localScale;
            // Treat each component independently. An animator commonly refreshes
            // rotation every frame but leaves local position/scale untouched.
            // Updating all three baselines together would accumulate the untouched
            // position or scale override.
            if (!HasLast || !Same(currentPosition, LastPosition))
                BasePosition = currentPosition;
            if (!HasLast || !Same(currentRotation, LastRotation))
                BaseRotation = currentRotation;
            // Ragdoll synchronization can copy an already-scaled bone into the
            // animated hierarchy. Never accept that copy as a fresh baseline or
            // the profile multiplier is briefly applied twice during recovery.
            if (stableScale.HasValue)
                BaseScale = stableScale.Value;
            else if (!HasLast || !Same(currentScale, LastScale))
                BaseScale = currentScale;
            Transform.localPosition = BasePosition + edit.Position;
            Quaternion animatedRotation = BaseRotation;
            if ((edit.AnimationInfluence - Vector3.one).sqrMagnitude >
                0.00000001f)
            {
                // Express the animator's current local pose relative to the skin
                // bind pose, attenuate its rest-local rotation-vector components,
                // then rebuild the animated rotation. Rotation vectors avoid the
                // gimbal/cross-axis artifacts of decomposing animation into Euler
                // angles. The exact stock quaternion is retained at 100%.
                Quaternion animationDelta = Quaternion.Inverse(ReferenceRotation) *
                    BaseRotation;
                Vector3 rotationVector = Vector3.Scale(
                    QuaternionToRotationVector(animationDelta),
                    edit.AnimationInfluence);
                animatedRotation = ReferenceRotation *
                    RotationVectorToQuaternion(rotationVector);
            }
            FilteredBaseRotation = animatedRotation;
            Quaternion correction = Quaternion.Euler(edit.Rotation);
            Transform.localRotation = animationAwareRotation ?
                ReferenceRotation * correction *
                    Quaternion.Inverse(ReferenceRotation) * animatedRotation :
                animatedRotation * correction;
            Transform.localScale = Vector3.Scale(BaseScale, edit.Scale);
            LastPosition = Transform.localPosition;
            LastRotation = Transform.localRotation;
            LastScale = Transform.localScale;
            HasLast = true;
        }

        internal void Restore()
        {
            if (Transform == null || !HasLast) return;
            if (Same(Transform.localPosition, LastPosition))
                Transform.localPosition = BasePosition;
            if (Same(Transform.localRotation, LastRotation))
                Transform.localRotation = BaseRotation;
            if (Same(Transform.localScale, LastScale))
                Transform.localScale = BaseScale;
            HasLast = false;
        }

        internal bool ApplyLastPoseForRagdoll()
        {
            if (Transform == null || !HasLast || !HasMatchedEdit ||
                MatchedEdit == null || MatchedEdit.IsIdentity) return false;
            Transform.localPosition = LastPosition;
            Transform.localRotation = LastRotation;
            Transform.localScale = LastScale;
            return true;
        }

        internal void HoldLastScaleDuringRagdoll()
        {
            if (Transform == null || !HasLast || !HasMatchedEdit ||
                MatchedEdit == null ||
                (MatchedEdit.Scale - Vector3.one).sqrMagnitude < 0.00000001f)
                return;
            if (!Same(Transform.localScale, LastScale))
                Transform.localScale = LastScale;
        }

        internal void ForgetAppliedPose()
        {
            HasLast = false;
        }

        private static bool Same(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 0.0000000001f;
        }

        private static bool Same(Quaternion a, Quaternion b)
        {
            return Mathf.Abs(Quaternion.Dot(a, b)) > 0.999999f;
        }

        private static Vector3 QuaternionToRotationVector(Quaternion value)
        {
            float length = Mathf.Sqrt(value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            if (length > 0.000001f)
                value = new Quaternion(value.x / length, value.y / length,
                    value.z / length, value.w / length);
            // q and -q encode the same rotation. Choose the shortest hemisphere
            // so animation influence never jumps through a near-360-degree arc.
            if (value.w < 0f) value = new Quaternion(-value.x, -value.y,
                -value.z, -value.w);
            float vectorLength = Mathf.Sqrt(value.x * value.x +
                value.y * value.y + value.z * value.z);
            if (vectorLength < 0.000001f)
                return new Vector3(value.x, value.y, value.z) * 2f;
            float angle = 2f * Mathf.Atan2(vectorLength,
                Mathf.Clamp(value.w, -1f, 1f));
            return new Vector3(value.x, value.y, value.z) *
                (angle / vectorLength);
        }

        private static Quaternion RotationVectorToQuaternion(Vector3 value)
        {
            float angle = value.magnitude;
            if (angle < 0.000001f) return Quaternion.identity;
            float half = angle * 0.5f;
            float scale = Mathf.Sin(half) / angle;
            return new Quaternion(value.x * scale, value.y * scale,
                value.z * scale, Mathf.Cos(half));
        }
    }

    internal sealed class BoneRendererBinding
    {
        internal Renderer Renderer;
        internal int BoneIndex = -1;
    }

    internal sealed class ColliderBinding
    {
        internal Collider Collider;
        internal string Key;
        internal string Name;
        internal bool IsAlive { get { return Collider != null; } }
    }

    internal sealed class RagdollJointState
    {
        internal CharacterJoint Joint;
        internal Vector3 Anchor;
        internal Vector3 ConnectedAnchor;
        internal Vector3 Axis;
        internal Vector3 SwingAxis;
        internal bool AutoConfigureConnectedAnchor;
    }

    internal sealed class DragTargetState
    {
        internal RigTarget Target;
        internal RigTarget Source;
        internal TransformEdit Initial;
        internal Vector3 InitialWorldPosition;
        internal bool Mirrored;
    }

    internal sealed class EditableRig
    {
        internal Component Owner;
        internal int OwnerId;
        internal Transform Root;
        internal bool IsIva;
        internal readonly List<RigTarget> Targets = new List<RigTarget>();
        internal readonly List<RigTarget> RootTargets = new List<RigTarget>();
        internal readonly List<RigTarget> HierarchyPreorder =
            new List<RigTarget>();
        internal readonly Dictionary<string, RigTarget> ByKey =
            new Dictionary<string, RigTarget>(StringComparer.Ordinal);
        internal readonly List<RuntimeTargetGroup> Groups =
            new List<RuntimeTargetGroup>();
        internal readonly Dictionary<string, Vector3> EditedScaleBaselines =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);
        internal readonly List<RagdollJointState> RagdollJointStates =
            new List<RagdollJointState>();
        internal bool WasRagdoll;
        internal bool RagdollRebased;
        internal int DiscoverySignature;
        internal bool IsAlive
        {
            get
            {
                if (Owner == null || Root == null || Targets.Count == 0) return false;
                foreach (RigTarget target in Targets)
                    if (target.Transform != null) return true;
                return false;
            }
        }

        internal void Apply(EditorSettings settings, bool allow)
        {
            KerbalEVA eva = Owner as KerbalEVA;
            if (!IsIva && IsPhysicsRagdoll(eva))
            {
                foreach (RigTarget target in Targets)
                    target.HoldLastScaleDuringRagdoll();
                return;
            }
            foreach (RigTarget target in Targets)
            {
                if (target.MatchRevision != settings.Revision)
                {
                    target.MatchRevision = settings.Revision;
                    target.HasMatchedEdit = settings.TryGetForTarget(target.Key,
                        target.DisplayName, out target.MatchedEdit);
                }
                if (allow && settings.Enabled && target.HasMatchedEdit)
                {
                    Vector3? stableScale = null;
                    if ((target.MatchedEdit.Scale - Vector3.one).sqrMagnitude >
                        0.00000001f)
                    {
                        Vector3 baseline;
                        if (!EditedScaleBaselines.TryGetValue(
                            target.MatchedEdit.Key, out baseline))
                        {
                            baseline = target.BaseScale;
                            EditedScaleBaselines[target.MatchedEdit.Key] = baseline;
                        }
                        stableScale = baseline;
                    }
                    target.Apply(target.MatchedEdit,
                        settings.AnimationAwareRotation, stableScale);
                }
                else target.Restore();
            }
        }

        internal void Restore()
        {
            RestoreRagdollJoints();
            foreach (RigTarget target in Targets)
                target.Restore();
        }

        internal void UpdateRagdollPhysics(EditorSettings settings, bool allow)
        {
            if (IsIva) return;
            KerbalEVA eva = Owner as KerbalEVA;
            bool ragdoll = IsPhysicsRagdoll(eva);
            if (!ragdoll)
            {
                if (WasRagdoll)
                {
                    RestoreRagdollJoints();
                    foreach (RigTarget target in Targets)
                        target.ForgetAppliedPose();
                    Debug.Log("[KerbalProportions] Ragdoll physics restored for " +
                        (Owner == null ? "Kerbal" : Owner.name) + ".");
                }
                WasRagdoll = false;
                RagdollRebased = false;
                return;
            }

            WasRagdoll = true;
            if (!allow || settings == null || !settings.Enabled)
            {
                RestoreRagdollJoints();
                RagdollRebased = false;
                return;
            }
            if (RagdollRebased) return;
            RebaseRagdollJoints();
        }

        private static bool IsPhysicsRagdoll(KerbalEVA eva)
        {
            if (eva == null || !eva.isRagdoll) return false;
            // KSP leaves isRagdoll set during st_recover even though the
            // stand-up animator has reclaimed the skeleton. Treat recovery as
            // animation-owned so profile position/height offsets resume as
            // soon as the stand-up begins.
            return eva.fsm == null || eva.st_recover == null ||
                eva.fsm.CurrentState != eva.st_recover;
        }

        private void RebaseRagdollJoints()
        {
            RagdollJointStates.Clear();
            CharacterJoint[] joints =
                Root.GetComponentsInChildren<CharacterJoint>(true);
            foreach (CharacterJoint joint in joints)
            {
                if (joint == null || joint.connectedBody == null) continue;
                RagdollJointStates.Add(new RagdollJointState {
                    Joint = joint,
                    Anchor = joint.anchor,
                    ConnectedAnchor = joint.connectedAnchor,
                    Axis = joint.axis,
                    SwingAxis = joint.swingAxis,
                    AutoConfigureConnectedAnchor =
                        joint.autoConfigureConnectedAnchor });
            }

            int editedTargets = 0;
            foreach (RigTarget target in Targets)
                if (target.ApplyLastPoseForRagdoll()) editedTargets++;
            if (editedTargets == 0)
            {
                RagdollJointStates.Clear();
                RagdollRebased = true;
                return;
            }
            Physics.SyncTransforms();

            float maximumError = 0f;
            foreach (RagdollJointState state in RagdollJointStates)
            {
                CharacterJoint joint = state.Joint;
                if (joint == null || joint.connectedBody == null) continue;
                Vector3 pivot = joint.transform.TransformPoint(joint.anchor);
                Vector3 connectedPivot =
                    joint.connectedBody.transform.TransformPoint(
                        joint.connectedAnchor);
                maximumError = Mathf.Max(maximumError,
                    Vector3.Distance(pivot, connectedPivot));
                joint.autoConfigureConnectedAnchor = false;
                joint.connectedAnchor =
                    joint.connectedBody.transform.InverseTransformPoint(pivot);
            }
            Physics.SyncTransforms();
            RagdollRebased = true;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[KerbalProportions] Ragdoll rebaked: {0} edited targets, " +
                "{1} joints, maximum initial anchor error {2:0.0000} m.",
                editedTargets, RagdollJointStates.Count, maximumError));
        }

        private void RestoreRagdollJoints()
        {
            if (RagdollJointStates.Count == 0) return;
            foreach (RagdollJointState state in RagdollJointStates)
            {
                CharacterJoint joint = state.Joint;
                if (joint == null) continue;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = state.Anchor;
                joint.connectedAnchor = state.ConnectedAnchor;
                joint.axis = state.Axis;
                joint.swingAxis = state.SwingAxis;
                joint.autoConfigureConnectedAnchor =
                    state.AutoConfigureConnectedAnchor;
            }
            Physics.SyncTransforms();
            RagdollJointStates.Clear();
        }
    }

    internal sealed class PortraitCameraBinding
    {
        internal Camera Camera;
        internal int CameraId;
        internal Vector3 BasePosition;
        internal Quaternion BaseRotation;
        internal float BaseFieldOfView;
        internal float BaseOrthographicSize;
        internal Vector3 LastPosition;
        internal Quaternion LastRotation;
        internal float LastFieldOfView;
        internal float LastOrthographicSize;
        internal bool LastOrthographic;
        internal bool HasLast;

        internal bool IsAlive
        {
            get { return Camera != null && Camera.transform != null; }
        }

        // KSP resets seated portrait cameras immediately before invoking the
        // camera render callbacks. Capture that freshly-computed stock pose and
        // layer framing on top, rather than changing the Kerbal or seat rig.
        internal void Apply(PortraitFraming framing)
        {
            if (!IsAlive || framing == null) return;
            Transform cameraTransform = Camera.transform;
            Vector3 currentPosition = cameraTransform.localPosition;
            Quaternion currentRotation = cameraTransform.localRotation;
            float currentFieldOfView = Camera.fieldOfView;
            float currentOrthographicSize = Camera.orthographicSize;
            bool currentOrthographic = Camera.orthographic;

            if (!HasLast || !Same(currentPosition, LastPosition))
                BasePosition = currentPosition;
            if (!HasLast || !Same(currentRotation, LastRotation))
                BaseRotation = currentRotation;
            if (!HasLast || currentOrthographic != LastOrthographic ||
                !Same(currentFieldOfView, LastFieldOfView))
                BaseFieldOfView = currentFieldOfView;
            if (!HasLast || currentOrthographic != LastOrthographic ||
                !Same(currentOrthographicSize, LastOrthographicSize))
                BaseOrthographicSize = currentOrthographicSize;

            Vector3 cameraPlaneOffset = new Vector3(framing.Horizontal,
                framing.Vertical, 0f);
            cameraTransform.localPosition = BasePosition +
                BaseRotation * cameraPlaneOffset;
            cameraTransform.localRotation = BaseRotation *
                Quaternion.Euler(framing.Pitch, framing.Yaw, 0f);
            float zoom = Mathf.Clamp(framing.Zoom, 0.5f, 2f);
            if (currentOrthographic)
                Camera.orthographicSize = Mathf.Max(0.001f,
                    BaseOrthographicSize / zoom);
            else
                Camera.fieldOfView = Mathf.Clamp(BaseFieldOfView / zoom,
                    10f, 120f);

            LastPosition = cameraTransform.localPosition;
            LastRotation = cameraTransform.localRotation;
            LastFieldOfView = Camera.fieldOfView;
            LastOrthographicSize = Camera.orthographicSize;
            LastOrthographic = Camera.orthographic;
            HasLast = true;
        }

        internal void Restore()
        {
            if (!IsAlive || !HasLast) return;
            Transform cameraTransform = Camera.transform;
            if (Same(cameraTransform.localPosition, LastPosition))
                cameraTransform.localPosition = BasePosition;
            if (Same(cameraTransform.localRotation, LastRotation))
                cameraTransform.localRotation = BaseRotation;
            if (Camera.orthographic == LastOrthographic)
            {
                if (LastOrthographic &&
                    Same(Camera.orthographicSize, LastOrthographicSize))
                    Camera.orthographicSize = BaseOrthographicSize;
                else if (!LastOrthographic &&
                    Same(Camera.fieldOfView, LastFieldOfView))
                    Camera.fieldOfView = BaseFieldOfView;
            }
            HasLast = false;
        }

        private static bool Same(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 0.0000000001f;
        }

        private static bool Same(Quaternion a, Quaternion b)
        {
            return Mathf.Abs(Quaternion.Dot(a, b)) > 0.999999f;
        }

        private static bool Same(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.0001f;
        }
    }

    internal sealed class EditSnapshot
    {
        internal Dictionary<string, TransformEdit> Edits;
        internal PortraitFraming Portrait;
        internal string Label;
    }

    [DefaultExecutionOrder(10000)]
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    internal sealed class ProportionsController : MonoBehaviour
    {
        private const float TargetPanelWidth = 610f;
        private const float InspectorPanelWidth = 540f;
        private const float TargetScrollHeight = 330f;
        private const float HierarchyWindowWidth = 640f;
        private const float HierarchyWindowHeight = 760f;
        private const float ControlsWindowWidth = 570f;
        private const float ControlsWindowHeight = 690f;
        private readonly List<EditableRig> rigs = new List<EditableRig>();
        private readonly HashSet<int> knownOwners = new HashSet<int>();
        private readonly Dictionary<int, PortraitCameraBinding> portraitCameras =
            new Dictionary<int, PortraitCameraBinding>();
        private readonly List<EditSnapshot> undo = new List<EditSnapshot>();
        private readonly List<EditSnapshot> redo = new List<EditSnapshot>();
        private EditorSettings settings;
        private Rect hierarchyWindowRect = new Rect(12f, 25f,
            HierarchyWindowWidth, HierarchyWindowHeight);
        private Rect controlsWindowRect = new Rect(668f, 25f,
            ControlsWindowWidth, ControlsWindowHeight);
        private Vector2 targetScroll;
        private Vector2 groupScroll;
        private readonly HashSet<string> expandedHierarchyKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> initializedHierarchyKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<RigTarget> visibleTargetOrder =
            new List<RigTarget>();
        private int visibleHierarchyOwnerId;
        private string search = string.Empty;
        private string selectedKey = string.Empty;
        private readonly HashSet<string> selectedKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private string rangeAnchorKey = string.Empty;
        private string groupName = "Accessory group";
        private string activeGroupId = string.Empty;
        private string profileName = "My Rig";
        private readonly List<string> profileNames = new List<string>();
        private readonly Dictionary<string, string> profileSources =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private int selectedProfile = -1;
        private string posX = "0", posY = "0", posZ = "0";
        private string rotX = "0", rotY = "0", rotZ = "0";
        private string sclX = "1", sclY = "1", sclZ = "1";
        private Vector3 animationInfluence = Vector3.one;
        private bool animationSliderEditing;
        private bool portraitSliderEditing;
        private bool gizmoSliderEditing;
        private int inspectorTab;
        private static readonly string[] InspectorTabs =
            { "Pose", "Motion", "Portrait", "Profiles" };
        private static readonly string[] AxisSpaceModes =
            { "Local axes", "Surface axes" };
        private EditMode editMode = EditMode.Move;
        private bool mirrorEdit;
        private bool visible;
        private int windowId;
        private float nextScan;
        private int lastPrimaryOwnerId;
        private bool lastInternalView;
        private KSP.UI.Screens.ApplicationLauncherButton toolbarButton;
        private Texture2D toolbarIcon;
        private Material lineMaterial;
        private GUIStyle treeButtonStyle;
        private GUIStyle treeSelectedStyle;
        private RigTarget hoverTarget;
        private EditableRig hoverRig;
        private int hotAxis = -1;
        private bool dragging;
        private Vector3 dragMouse;
        private readonly List<DragTargetState> dragTargets =
            new List<DragTargetState>();
        private RigTarget dragPrimarySource;
        private Vector3[] dragAxes;
        private Vector3 dragPivot;
        private float dragSize;
        private Vector2 dragRotationTangent;
        private Vector3 dragRotationStart;
        private bool dragRotationPlaneValid;
        private bool dragVirtualGroup;
        private bool hierarchyPanelExpanded = true;

        private static string ProfilesPath
        {
            get { return Path.Combine(KSPUtil.ApplicationRootPath,
                "GameData/KerbalProportions/PluginData/profiles.cfg"); }
        }

        private static string ProfilesDirectory
        {
            get { return Path.Combine(KSPUtil.ApplicationRootPath,
                "GameData/KerbalProportions/PluginData/Profiles"); }
        }

        private static string LegacyProfilesPath
        {
            get { return Path.Combine(KSPUtil.ApplicationRootPath,
                "GameData/KerbalProportionsV2/PluginData/profiles.cfg"); }
        }

        private void Start()
        {
            MigrateLegacyPluginData();
            settings = EditorSettings.Load();
            ApplySavedWindowLayout();
            if (settings.LoadedLegacyFormat) settings.Save();
            NormalizeLegacyProfileData();
            visible = false;
            windowId = GetInstanceID() ^ 0x4B5032;
            nextScan = 0f;
            RefreshProfileNames();
            GameEvents.onGUIApplicationLauncherReady.Add(CreateToolbarButton);
            if (KSP.UI.Screens.ApplicationLauncher.Ready) CreateToolbarButton();
            Camera.onPreCull += OnCameraPreCull;
            Camera.onPreRender += OnCameraPreRender;
            Camera.onPostRender += OnCameraPostRender;
            CreateLineMaterial();
            Debug.Log("[KerbalProportions] Version 2.6.0 active.");
        }

        private static void MigrateLegacyPluginData()
        {
            try
            {
                string directory = Path.GetDirectoryName(
                    EditorSettings.SettingsPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                if (!File.Exists(EditorSettings.SettingsPath) &&
                    File.Exists(EditorSettings.LegacySettingsPath))
                    File.Copy(EditorSettings.LegacySettingsPath,
                        EditorSettings.SettingsPath);
                if (!File.Exists(ProfilesPath) &&
                    File.Exists(LegacyProfilesPath))
                    File.Copy(LegacyProfilesPath, ProfilesPath);
            }
            catch (Exception exception)
            {
                Debug.LogError("[KerbalProportions] Legacy data migration " +
                    "failed: " + exception);
            }
        }

        private static void NormalizeLegacyProfileData()
        {
            try
            {
                if (!File.Exists(ProfilesPath)) return;
                ConfigNode root = ConfigNode.Load(ProfilesPath);
                ConfigNode container = ProfileContainer(root, false);
                if (container == null || container.name !=
                    "KERBAL_PROPORTIONS_V2_PROFILES") return;
                ProfileContainer(root, true);
                root.Save(ProfilesPath);
            }
            catch (Exception exception)
            {
                Debug.LogError("[KerbalProportions] Profile-name migration " +
                    "failed: " + exception);
            }
        }

        private void Update()
        {
            if (animationSliderEditing && !Input.GetMouseButton(0))
            {
                animationSliderEditing = false;
                if (settings != null) settings.Save();
            }
            if (portraitSliderEditing && !Input.GetMouseButton(0))
            {
                portraitSliderEditing = false;
                if (settings != null) settings.Save();
            }
            if (gizmoSliderEditing && !Input.GetMouseButton(0))
            {
                gizmoSliderEditing = false;
                if (settings != null) settings.Save();
            }
            if (!visible || !settings.Enabled || !HighLogic.LoadedSceneIsFlight)
            {
                EndDrag();
                return;
            }
            HandleHotkeys();
            HandleViewportInput();
        }

        private void LateUpdate()
        {
            if (settings == null || !HighLogic.LoadedSceneIsFlight) return;
            float now = Time.realtimeSinceStartup;
            if (now >= nextScan)
            {
                nextScan = now + 0.75f;
                DiscoverRigs();
            }
            bool internalCamera = IsInternalCamera();
            RefreshRigContext(internalCamera);
            ApplyRigs(true);
        }

        private static bool IsKerbalRenderer(Transform root, Renderer renderer,
            bool checkBounds)
        {
            if (root == null || renderer == null ||
                (renderer.transform != root &&
                !renderer.transform.IsChildOf(root))) return false;
            string rendererType = renderer.GetType().Name;
            if (renderer is LineRenderer || renderer is TrailRenderer ||
                rendererType == "ParticleSystemRenderer") return false;
            string name = renderer.gameObject.name ?? string.Empty;
            string lower = name.ToLowerInvariant();
            if (lower == "quad" || lower.Contains("trajector") ||
                lower.Contains("screenspaceshadow") ||
                lower.Contains("shadowmanager")) return false;
            if (!checkBounds) return true;
            Bounds candidate = renderer.bounds;
            if (candidate.extents.magnitude > 4f) return false;
            Vector3 localCenter = root.InverseTransformPoint(candidate.center);
            return localCenter.sqrMagnitude <= 16f;
        }

        private void FixedUpdate()
        {
            if (settings == null || !HighLogic.LoadedSceneIsFlight) return;
            foreach (EditableRig rig in rigs)
            {
                if (!rig.IsAlive) continue;
                bool allow = rig.IsIva ? settings.EnableIva :
                    settings.EnableEva;
                rig.UpdateRagdollPhysics(settings, allow);
            }
        }

        private void ApplyRigs(bool removeDead)
        {
            for (int index = rigs.Count - 1; index >= 0; index--)
            {
                EditableRig rig = rigs[index];
                if (!rig.IsAlive)
                {
                    if (removeDead)
                    {
                        rig.Restore(); knownOwners.Remove(rig.OwnerId);
                        rigs.RemoveAt(index);
                    }
                    continue;
                }
                // IVA models can also be rendered by KSP's internal-space overlay
                // while CameraManager remains in an external mode. The IVA toggle
                // therefore controls the rig itself, not the current camera mode.
                bool allow = rig.IsIva ? settings.EnableIva : settings.EnableEva;
                rig.Apply(settings, allow);
            }
        }

        private void OnCameraPreCull(Camera camera)
        {
            if (settings == null || !HighLogic.LoadedSceneIsFlight) return;
            // Some seated-Kerbal animators update after ordinary LateUpdate.
            // Reapply immediately before rendering so IVA cannot overwrite the
            // additive pose for the visible frame.
            ApplyRigs(false);
            ApplyPortraitCamera(camera);
        }

        private void OnCameraPreRender(Camera camera)
        {
            if (settings == null || !HighLogic.LoadedSceneIsFlight) return;
            // PreRender runs after culling and is the final built-in camera hook.
            // Reapply here as well for active/FreeIVA animators that write between
            // LateUpdate and rendering.
            ApplyRigs(false);
            ApplyPortraitCamera(camera);
        }

        private void OnCameraPostRender(Camera camera)
        {
            if (camera == null) return;
            PortraitCameraBinding binding;
            if (portraitCameras.TryGetValue(camera.GetInstanceID(),
                out binding)) binding.Restore();
        }

        private void DiscoverRigs()
        {
            HashSet<int> seenPortraitCameras = new HashSet<int>();
            foreach (KerbalEVA eva in FindObjectsOfType<KerbalEVA>())
            {
                AddRig(eva, eva.transform, false);
                RegisterPortraitCamera(eva.kerbalPortraitCamera,
                    seenPortraitCameras);
            }
            foreach (Kerbal kerbal in FindObjectsOfType<Kerbal>())
            {
                AddRig(kerbal, kerbal.transform, true);
                Camera portraitCamera = null;
                if (kerbal.protoCrewMember != null &&
                    kerbal.protoCrewMember.seat != null)
                    portraitCamera =
                        kerbal.protoCrewMember.seat.portraitCamera;
                RegisterPortraitCamera(portraitCamera != null ?
                    portraitCamera : kerbal.kerbalCam, seenPortraitCameras);
            }
            RemoveStalePortraitCameras(seenPortraitCameras);
        }

        private void RegisterPortraitCamera(Camera camera, HashSet<int> seen)
        {
            if (camera == null) return;
            int cameraId = camera.GetInstanceID();
            seen.Add(cameraId);
            PortraitCameraBinding binding;
            if (portraitCameras.TryGetValue(cameraId, out binding) &&
                binding.Camera == camera) return;
            if (binding != null) binding.Restore();
            portraitCameras[cameraId] = new PortraitCameraBinding {
                Camera = camera, CameraId = cameraId };
            Debug.Log("[KerbalProportions] Portrait camera registered: " +
                camera.name);
        }

        private void RemoveStalePortraitCameras(HashSet<int> seen)
        {
            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, PortraitCameraBinding> pair in
                portraitCameras)
                if (!seen.Contains(pair.Key) || !pair.Value.IsAlive)
                    remove.Add(pair.Key);
            foreach (int cameraId in remove)
            {
                PortraitCameraBinding binding = portraitCameras[cameraId];
                binding.Restore();
                portraitCameras.Remove(cameraId);
            }
        }

        private void ApplyPortraitCamera(Camera camera)
        {
            if (camera == null) return;
            PortraitCameraBinding binding;
            if (!portraitCameras.TryGetValue(camera.GetInstanceID(),
                out binding)) return;
            if (settings.Enabled) binding.Apply(settings.Portrait);
            else binding.Restore();
        }

        private void RestorePortraitCameras()
        {
            foreach (PortraitCameraBinding binding in portraitCameras.Values)
                binding.Restore();
        }

        private static int RigDiscoverySignature(Transform root)
        {
            if (root == null) return 0;
            unchecked
            {
                int signature = 17;
                foreach (Renderer renderer in
                    root.GetComponentsInChildren<Renderer>(true))
                    if (renderer != null)
                        signature = signature * 31 + renderer.GetInstanceID();
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                    if (light != null)
                        signature = signature * 31 + light.GetInstanceID();
                foreach (Collider collider in
                    root.GetComponentsInChildren<Collider>(true))
                    if (collider != null)
                        signature = signature * 31 + collider.GetInstanceID();
                return signature;
            }
        }

        private void AddRig(Component owner, Transform root, bool isIva)
        {
            if (owner == null || root == null) return;
            int ownerId = owner.GetInstanceID();
            int signature = RigDiscoverySignature(root);
            if (knownOwners.Contains(ownerId))
            {
                EditableRig existing = null;
                foreach (EditableRig candidate in rigs)
                    if (candidate.OwnerId == ownerId)
                    { existing = candidate; break; }
                if (existing != null &&
                    existing.DiscoverySignature == signature) return;
                KerbalEVA eva = owner as KerbalEVA;
                if (existing != null && eva != null && eva.isRagdoll) return;
                if (existing != null)
                {
                    existing.Restore();
                    rigs.Remove(existing);
                }
                knownOwners.Remove(ownerId);
                Debug.Log("[KerbalProportions] Runtime rig hierarchy changed; " +
                    "rediscovering " + owner.name + ".");
            }
            EditableRig rig = new EditableRig { Owner = owner, Root = root,
                IsIva = isIva, OwnerId = ownerId,
                DiscoverySignature = signature };
            HashSet<Transform> bones = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer renderer in
                root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (Transform bone in renderer.bones)
                    if (bone != null && bone != root) bones.Add(bone);
            foreach (Transform bone in bones)
                AddTarget(rig, bone, "Bone");
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.transform != root && !bones.Contains(renderer.transform) &&
                    IsKerbalRenderer(root, renderer, false))
                    AddTarget(rig, renderer.transform, "Mesh");
            // Associate physics shapes with the nearest editable visual target.
            // Collider-only transforms remain diagnostic rather than editable.
            foreach (Collider collider in
                root.GetComponentsInChildren<Collider>(true))
            {
                Transform cursor = collider.transform;
                RigTarget colliderTarget = null;
                while (cursor != null && cursor != root)
                {
                    if (rig.ByKey.TryGetValue(BuildKey(root, cursor),
                        out colliderTarget)) break;
                    cursor = cursor.parent;
                }
                if (colliderTarget != null)
                    colliderTarget.ColliderBindings.Add(
                        CreateColliderBinding(root, collider));
            }
            Dictionary<Transform, Matrix4x4> restToRoot =
                new Dictionary<Transform, Matrix4x4>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsKerbalRenderer(root, renderer, false)) continue;
                RigTarget rendererTarget;
                if (rig.ByKey.TryGetValue(BuildKey(root, renderer.transform),
                    out rendererTarget) && rendererTarget.Category == "Mesh")
                    rendererTarget.RendererBindings.Add(new BoneRendererBinding {
                        Renderer = renderer, BoneIndex = -1 });
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                if (skinned == null) continue;
                Mesh sharedMesh = skinned.sharedMesh;
                Matrix4x4[] bindposes = sharedMesh == null ?
                    new Matrix4x4[0] : sharedMesh.bindposes;
                Matrix4x4 rendererToRoot = root.worldToLocalMatrix *
                    skinned.transform.localToWorldMatrix;
                for (int boneIndex = 0; boneIndex < skinned.bones.Length; boneIndex++)
                {
                    Transform bone = skinned.bones[boneIndex];
                    if (bone == null) continue;
                    if (boneIndex < bindposes.Length && !restToRoot.ContainsKey(bone))
                        restToRoot.Add(bone,
                            rendererToRoot * bindposes[boneIndex].inverse);
                    RigTarget boneTarget;
                    if (rig.ByKey.TryGetValue(BuildKey(root, bone), out boneTarget))
                        boneTarget.RendererBindings.Add(new BoneRendererBinding {
                            Renderer = skinned, BoneIndex = boneIndex });
                }
            }
            foreach (KeyValuePair<Transform, Matrix4x4> pair in restToRoot)
            {
                RigTarget target;
                if (!rig.ByKey.TryGetValue(BuildKey(root, pair.Key), out target))
                    continue;
                Matrix4x4 parentToRoot = Matrix4x4.identity;
                if (pair.Key.parent != null && pair.Key.parent != root &&
                    !restToRoot.TryGetValue(pair.Key.parent, out parentToRoot))
                    parentToRoot = root.worldToLocalMatrix *
                        pair.Key.parent.localToWorldMatrix;
                Matrix4x4 localRest = parentToRoot.inverse * pair.Value;
                target.ReferenceRotation = localRest.rotation;
            }
            BuildTargetHierarchy(rig);
            BuildTargetGroups(rig);
            if (rig.Targets.Count == 0) return;
            rigs.Add(rig);
            knownOwners.Add(ownerId);
            foreach (RigTarget rootTarget in rig.RootTargets)
                if (rootTarget.Children.Count > 0)
                {
                    string stateKey = HierarchyStateKey(rig, rootTarget);
                    if (initializedHierarchyKeys.Add(stateKey))
                        expandedHierarchyKeys.Add(stateKey);
                }
            LogRigMatches(rig);
        }

        private static ColliderBinding CreateColliderBinding(Transform root,
            Collider collider)
        {
            Collider[] components = collider.transform.GetComponents<Collider>();
            int componentIndex = Array.IndexOf(components, collider);
            string typeName = collider.GetType().Name;
            ColliderBinding binding = new ColliderBinding { Collider = collider,
                Key = BuildKey(root, collider.transform) + "/@" + typeName +
                    "[" + componentIndex + "]",
                Name = collider.transform.name + " (" + typeName + ")" };
            return binding;
        }

        private void LogRigMatches(EditableRig rig)
        {
            int configured = 0, exactTargets = 0, semanticTargets = 0;
            HashSet<TransformEdit> configuredEdits = new HashSet<TransformEdit>();
            HashSet<TransformEdit> matchedEdits = new HashSet<TransformEdit>();
            foreach (TransformEdit edit in settings.Edits.Values)
                if (!edit.IsIdentity && configuredEdits.Add(edit)) configured++;
            foreach (RigTarget target in rig.Targets)
            {
                TransformEdit edit;
                int match = settings.MatchForTarget(target.Key,
                    target.DisplayName, out edit);
                if (match == 0 || edit.IsIdentity) continue;
                matchedEdits.Add(edit);
                if (match == 1) exactTargets++; else semanticTargets++;
            }
            Debug.Log(string.Format(
                "[KerbalProportions] Rig {0} ({1}): {2} editable targets; " +
                "saved edits matched {3}/{4} ({5} exact targets, {6} semantic targets)",
                rig.Owner.name, rig.IsIva ? "IVA" : "EVA", rig.Targets.Count,
                matchedEdits.Count, configured, exactTargets, semanticTargets));
            if (configured == 0 || matchedEdits.Count == configured) return;
            List<string> missing = new List<string>();
            foreach (TransformEdit edit in configuredEdits)
                if (!matchedEdits.Contains(edit)) missing.Add(edit.Name);
            Debug.LogWarning("[KerbalProportions] Unmatched saved edits on " +
                rig.Owner.name + ": " + string.Join(", ", missing.ToArray()));
        }

        private static void AddTarget(EditableRig rig, Transform transform,
            string category)
        {
            string key = BuildKey(rig.Root, transform);
            if (rig.ByKey.ContainsKey(key)) return;
            RigTarget target = new RigTarget { Transform = transform, Key = key,
                DisplayName = transform.name, Category = category,
                BasePosition = transform.localPosition,
                BaseRotation = transform.localRotation,
                ReferenceRotation = transform.localRotation,
                FilteredBaseRotation = transform.localRotation,
                BaseScale = transform.localScale };
            rig.Targets.Add(target);
            rig.ByKey.Add(key, target);
        }

        private static void BuildTargetHierarchy(EditableRig rig)
        {
            rig.RootTargets.Clear();
            rig.HierarchyPreorder.Clear();
            Dictionary<Transform, RigTarget> byTransform =
                new Dictionary<Transform, RigTarget>();
            foreach (RigTarget target in rig.Targets)
            {
                target.ParentTarget = null;
                target.Children.Clear();
                target.HierarchyDepth = 0;
                if (target.Transform != null)
                    byTransform[target.Transform] = target;
            }

            Dictionary<Transform, int> transformOrder =
                new Dictionary<Transform, int>();
            int nextOrder = 0;
            RecordTransformOrder(rig.Root, transformOrder, ref nextOrder);
            List<RigTarget> hierarchyOrder = new List<RigTarget>(rig.Targets);
            hierarchyOrder.Sort(delegate(RigTarget first, RigTarget second) {
                int firstOrder, secondOrder;
                if (first.Transform == null ||
                    !transformOrder.TryGetValue(first.Transform, out firstOrder))
                    firstOrder = int.MaxValue;
                if (second.Transform == null ||
                    !transformOrder.TryGetValue(second.Transform,
                        out secondOrder)) secondOrder = int.MaxValue;
                int order = firstOrder.CompareTo(secondOrder);
                return order != 0 ? order : string.Compare(first.Key,
                    second.Key, StringComparison.Ordinal);
            });

            foreach (RigTarget target in hierarchyOrder)
            {
                Transform ancestor = target.Transform == null ? null :
                    target.Transform.parent;
                RigTarget parent = null;
                while (ancestor != null && ancestor != rig.Root)
                {
                    if (byTransform.TryGetValue(ancestor, out parent)) break;
                    ancestor = ancestor.parent;
                }
                target.ParentTarget = parent;
                if (parent == null) rig.RootTargets.Add(target);
                else
                {
                    parent.Children.Add(target);
                    target.HierarchyDepth = parent.HierarchyDepth + 1;
                }
                rig.HierarchyPreorder.Add(target);
            }
            // Preserve the legacy target iteration order for profile matching,
            // counterpart selection, and non-tree editing code. The tree keeps
            // its own transform-order traversal above.
            rig.Targets.Sort(delegate(RigTarget first, RigTarget second) {
                int category = string.Compare(first.Category, second.Category,
                    StringComparison.OrdinalIgnoreCase);
                return category != 0 ? category : string.Compare(
                    first.DisplayName, second.DisplayName,
                    StringComparison.OrdinalIgnoreCase);
            });
        }

        private void BuildTargetGroups(EditableRig rig)
        {
            rig.Groups.Clear();
            DiscoverWearableTargetGroups(rig);
            foreach (TargetGroupDefinition definition in settings.Groups)
            {
                RuntimeTargetGroup group = new RuntimeTargetGroup {
                    Id = "manual:" + definition.Id,
                    Name = definition.Name, Automatic = false };
                HashSet<string> added = new HashSet<string>(
                    StringComparer.Ordinal);
                foreach (TargetGroupMemberDefinition member in
                    definition.Members)
                {
                    RigTarget target = ResolveGroupMember(rig, member);
                    if (target != null && added.Add(target.Key))
                        group.Members.Add(target);
                }
                RemoveNestedGroupMembers(group);
                if (group.Members.Count >= 2) rig.Groups.Add(group);
            }
        }

        private static RigTarget ResolveGroupMember(EditableRig rig,
            TargetGroupMemberDefinition member)
        {
            RigTarget exact;
            if (!string.IsNullOrEmpty(member.Key) &&
                rig.ByKey.TryGetValue(member.Key, out exact)) return exact;
            RigTarget best = null; int bestScore = int.MinValue;
            foreach (RigTarget candidate in rig.Targets)
            {
                if (!string.Equals(candidate.DisplayName, member.Name,
                    StringComparison.OrdinalIgnoreCase)) continue;
                int score = CommonSuffixLength(candidate.Key,
                    member.Key ?? string.Empty);
                if (best == null || score > bestScore)
                { best = candidate; bestScore = score; }
            }
            return best;
        }

        private static void DiscoverWearableTargetGroups(EditableRig rig)
        {
            Dictionary<string, RuntimeTargetGroup> discovered =
                new Dictionary<string, RuntimeTargetGroup>(
                    StringComparer.OrdinalIgnoreCase);
            Component[] components = rig.Root.GetComponentsInChildren<Component>(
                true);
            foreach (Component controller in components)
            {
                if (controller == null || controller.GetType().Name.IndexOf(
                    "PropController", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                FieldInfo[] fields = controller.GetType().GetFields(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                foreach (FieldInfo field in fields)
                {
                    string fieldName = field.Name ?? string.Empty;
                    if (fieldName.IndexOf("prop", StringComparison.OrdinalIgnoreCase) < 0 &&
                        fieldName.IndexOf("attachment",
                            StringComparison.OrdinalIgnoreCase) < 0) continue;
                    object value;
                    try { value = field.GetValue(controller); }
                    catch { continue; }
                    IEnumerable collection = value as IEnumerable;
                    if (collection == null || value is string) continue;
                    foreach (object item in collection)
                        AddWearableGroupItem(rig, item, discovered);
                }
            }
            foreach (RuntimeTargetGroup group in discovered.Values)
            {
                RemoveNestedGroupMembers(group);
                if (group.Members.Count >= 2) rig.Groups.Add(group);
            }
        }

        private static void RemoveNestedGroupMembers(RuntimeTargetGroup group)
        {
            HashSet<Transform> transforms = new HashSet<Transform>();
            foreach (RigTarget member in group.Members)
                if (member.Transform != null) transforms.Add(member.Transform);
            group.Members.RemoveAll(delegate(RigTarget member) {
                Transform parent = member.Transform == null ? null :
                    member.Transform.parent;
                while (parent != null)
                {
                    if (transforms.Contains(parent)) return true;
                    parent = parent.parent;
                }
                return false;
            });
        }

        private static void AddWearableGroupItem(EditableRig rig, object item,
            Dictionary<string, RuntimeTargetGroup> discovered)
        {
            if (item == null) return;
            string name = ReadAccessoryName(item);
            if (string.IsNullOrEmpty(name)) return;
            HashSet<Transform> references = new HashSet<Transform>();
            CollectAccessoryTransforms(item, references, 0,
                new HashSet<object>());
            if (references.Count == 0) return;
            string id = "auto:" + name.ToLowerInvariant();
            RuntimeTargetGroup group;
            if (!discovered.TryGetValue(id, out group))
            {
                group = new RuntimeTargetGroup { Id = id,
                    Name = FriendlyName(name), Automatic = true };
                discovered.Add(id, group);
            }
            HashSet<string> existing = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (RigTarget member in group.Members) existing.Add(member.Key);
            foreach (RigTarget target in rig.Targets)
            {
                if (target.Transform == null || existing.Contains(target.Key))
                    continue;
                foreach (Transform reference in references)
                    if (reference != null && reference != rig.Root &&
                        (target.Transform == reference ||
                        target.Transform.IsChildOf(reference) ||
                        reference.IsChildOf(target.Transform)))
                    {
                        group.Members.Add(target); existing.Add(target.Key); break;
                    }
            }
        }

        private static string ReadAccessoryName(object item)
        {
            Type type = item.GetType();
            string[] preferred = { "moduleId", "partName", "propName", "name" };
            foreach (string preferredName in preferred)
            {
                FieldInfo field = type.GetField(preferredName,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (field != null && field.FieldType == typeof(string))
                {
                    try
                    {
                        string value = field.GetValue(item) as string;
                        if (!string.IsNullOrEmpty(value)) return value;
                    }
                    catch { }
                }
            }
            UnityEngine.Object unityObject = item as UnityEngine.Object;
            if (unityObject != null && !string.IsNullOrEmpty(unityObject.name))
                return unityObject.name;
            return string.Empty;
        }

        private static void CollectAccessoryTransforms(object value,
            HashSet<Transform> result, int depth, HashSet<object> visited)
        {
            if (value == null || depth > 2 || !visited.Add(value)) return;
            Transform transform = value as Transform;
            GameObject gameObject = value as GameObject;
            Component component = value as Component;
            if (transform != null) result.Add(transform);
            else if (gameObject != null) result.Add(gameObject.transform);
            else if (component != null) result.Add(component.transform);
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string) return;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic))
            {
                string name = field.Name ?? string.Empty;
                if (name.IndexOf("prop", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("model", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("object", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("transform", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("render", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("mesh", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                object child;
                try { child = field.GetValue(value); }
                catch { continue; }
                IEnumerable collection = child as IEnumerable;
                if (collection != null && !(child is string))
                    foreach (object entry in collection)
                        CollectAccessoryTransforms(entry, result, depth + 1,
                            visited);
                else CollectAccessoryTransforms(child, result, depth + 1,
                    visited);
            }
        }

        private static void RecordTransformOrder(Transform transform,
            Dictionary<Transform, int> result, ref int nextOrder)
        {
            if (transform == null || result.ContainsKey(transform)) return;
            result.Add(transform, nextOrder++);
            for (int childIndex = 0; childIndex < transform.childCount;
                childIndex++)
                RecordTransformOrder(transform.GetChild(childIndex), result,
                    ref nextOrder);
        }

        private static string HierarchyStateKey(EditableRig rig,
            RigTarget target)
        {
            return (rig.IsIva ? "IVA|" : "EVA|") + target.Key;
        }

        private static string BuildKey(Transform root, Transform target)
        {
            List<string> parts = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                parts.Add((current.name ?? "Transform") + "[" +
                    current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + "]");
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private EditableRig PrimaryRig()
        {
            bool internalView = IsInternalCamera();
            if (internalView && settings.EnableIva)
            {
                CameraManager manager = CameraManager.Instance;
                Kerbal activeKerbal = manager == null ? null :
                    manager.IVACameraActiveKerbal;
                if (activeKerbal != null)
                    foreach (EditableRig rig in rigs)
                        if (rig.IsIva && rig.IsAlive && rig.Owner == activeKerbal)
                            return rig;
                foreach (EditableRig rig in rigs)
                    if (rig.IsIva && rig.IsAlive) return rig;
            }

            Vessel active = FlightGlobals.ActiveVessel;
            foreach (EditableRig rig in rigs)
            {
                KerbalEVA eva = rig.Owner as KerbalEVA;
                if (!rig.IsIva && eva != null && active != null && eva.vessel == active)
                    return rig;
            }
            foreach (EditableRig rig in rigs) if (!rig.IsIva && rig.IsAlive) return rig;
            if (!HighLogic.LoadedSceneIsFlight && settings.EnableIva)
                foreach (EditableRig rig in rigs)
                if (rig.IsIva && rig.IsAlive) return rig;
            return null;
        }

        private void RefreshRigContext(bool internalView)
        {
            EditableRig primary = PrimaryRig();
            int ownerId = primary == null || primary.Owner == null ? 0 :
                primary.Owner.GetInstanceID();
            if (ownerId == lastPrimaryOwnerId && internalView == lastInternalView)
                return;
            EndDrag(); ClearSelection(); hoverTarget = null; hoverRig = null;
            targetScroll = Vector2.zero;
            visibleTargetOrder.Clear();
            visibleHierarchyOwnerId = ownerId;
            lastPrimaryOwnerId = ownerId; lastInternalView = internalView;
            CameraManager manager = CameraManager.Instance;
            string mode = manager == null ? "none" :
                manager.currentCameraMode.ToString();
            Debug.Log(string.Format(
                "[KerbalProportions] Editor context: internal={0}, cameraMode={1}, primary={2}",
                internalView, mode, primary == null ? "none" :
                (primary.IsIva ? "IVA " : "EVA ") + primary.Owner.name));
        }

        private RigTarget SelectedTarget()
        {
            EditableRig rig = PrimaryRig();
            RigTarget target;
            return rig != null && rig.ByKey.TryGetValue(selectedKey, out target) ?
                target : null;
        }

        private List<RigTarget> SelectedTargets(bool hierarchyRootsOnly)
        {
            EditableRig rig = PrimaryRig();
            List<RigTarget> result = new List<RigTarget>();
            if (rig == null) return result;
            foreach (RigTarget target in rig.Targets)
                if (selectedKeys.Contains(target.Key)) result.Add(target);
            if (!hierarchyRootsOnly || result.Count < 2) return result;
            HashSet<Transform> selectedTransforms = new HashSet<Transform>();
            foreach (RigTarget target in result)
                if (target.Transform != null) selectedTransforms.Add(target.Transform);
            result.RemoveAll(delegate(RigTarget target) {
                Transform parent = target.Transform == null ? null :
                    target.Transform.parent;
                while (parent != null && parent != rig.Root)
                {
                    if (selectedTransforms.Contains(parent)) return true;
                    parent = parent.parent;
                }
                return false;
            });
            return result;
        }

        private void OnGUI()
        {
            if (!visible || settings == null) return;
            GUI.skin = HighLogic.Skin;
            hierarchyWindowRect = GUILayout.Window(windowId,
                hierarchyWindowRect, DrawHierarchyWindow,
                "Kerbal Proportions - Hierarchy");
            controlsWindowRect = GUILayout.Window(windowId + 1,
                controlsWindowRect, DrawControlsWindow,
                "Kerbal Proportions - Controls");
            CaptureWindowPositions();
            if (hoverTarget != null)
            {
                Vector3 mouse = Input.mousePosition;
                GUI.Box(new Rect(mouse.x + 14f, Screen.height - mouse.y + 12f,
                    230f, 27f), "Select: [" + hoverTarget.Category + "] " +
                    FriendlyName(hoverTarget.DisplayName));
            }
        }

        private void ApplySavedWindowLayout()
        {
            hierarchyWindowRect = new Rect(settings.HierarchyWindowX,
                settings.HierarchyWindowY, HierarchyWindowWidth,
                HierarchyWindowHeight);
            controlsWindowRect = new Rect(settings.ControlsWindowX,
                settings.ControlsWindowY, ControlsWindowWidth,
                ControlsWindowHeight);
            ClampWindowToScreen(ref hierarchyWindowRect);
            ClampWindowToScreen(ref controlsWindowRect);
        }

        private static void ClampWindowToScreen(ref Rect rect)
        {
            rect.x = Mathf.Clamp(rect.x, 0f,
                Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f,
                Mathf.Max(0f, Screen.height - rect.height));
        }

        private void CaptureWindowPositions()
        {
            settings.HierarchyWindowX = hierarchyWindowRect.x;
            settings.HierarchyWindowY = hierarchyWindowRect.y;
            settings.ControlsWindowX = controlsWindowRect.x;
            settings.ControlsWindowY = controlsWindowRect.y;
        }

        private void DrawControlsWindow(int id)
        {
            GUILayout.BeginHorizontal();
            bool previousEnabled = settings.Enabled;
            bool previousEva = settings.EnableEva;
            bool previousIva = settings.EnableIva;
            settings.Enabled = GUILayout.Toggle(settings.Enabled, "Enabled",
                GUILayout.Width(80));
            settings.EnableEva = GUILayout.Toggle(settings.EnableEva, "EVA",
                GUILayout.Width(60));
            settings.EnableIva = GUILayout.Toggle(settings.EnableIva, "IVA",
                GUILayout.Width(60));
            if (settings.Enabled != previousEnabled ||
                settings.EnableEva != previousEva ||
                settings.EnableIva != previousIva)
            {
                if (!settings.Enabled) RestorePortraitCameras();
                CaptureWindowPositions();
                settings.Save();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save current", GUILayout.Width(95)))
            {
                CaptureWindowPositions();
                settings.Save();
                ScreenMessages.PostScreenMessage("Kerbal Proportions rig saved", 2f,
                    ScreenMessageStyle.UPPER_CENTER);
            }
            if (GUILayout.Button("Close", GUILayout.Width(60))) ToggleWindow(false);
            GUILayout.EndHorizontal();

            EditableRig primary = PrimaryRig();
            GUILayout.BeginHorizontal(HighLogic.Skin.box);
            GUILayout.Label(primary == null ?
                "No editable Kerbal is currently loaded" :
                (primary.IsIva ? "IVA  " : "EVA  ") + primary.Owner.name +
                (selectedKeys.Count > 0 ? "   |   " + selectedKeys.Count +
                    " selected" : "   |   select in world"));
            GUILayout.FlexibleSpace();
            GUILayout.Label("W move   E rotate   R scale");
            GUILayout.EndHorizontal();
            DrawInspectorPanel();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawHierarchyWindow(int id)
        {
            EditableRig primary = PrimaryRig();
            GUILayout.BeginHorizontal(HighLogic.Skin.box);
            GUILayout.Label(primary == null ? "No editable Kerbal loaded" :
                (primary.IsIva ? "IVA  " : "EVA  ") + primary.Owner.name +
                "   |   " + primary.Targets.Count + " targets");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close editor", GUILayout.Width(92)))
                ToggleWindow(false);
            GUILayout.EndHorizontal();
            DrawTargetPanel(primary);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawTargetPanel(EditableRig rig)
        {
            GUILayout.BeginVertical(GUILayout.Width(TargetPanelWidth));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hierarchy" + (selectedKeys.Count > 0 ?
                "  |  " + selectedKeys.Count + " selected" : string.Empty));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(hierarchyPanelExpanded ? "Collapse" :
                "Expand", GUILayout.Width(76)))
                hierarchyPanelExpanded = !hierarchyPanelExpanded;
            GUILayout.EndHorizontal();
            if (!hierarchyPanelExpanded)
            {
                GUILayout.Label("Hierarchy hidden; select directly in the " +
                    "world or expand this panel.");
                GUILayout.EndVertical();
                return;
            }
            GUILayout.BeginHorizontal();
            string previousSearch = search ?? string.Empty;
            search = GUILayout.TextField(previousSearch);
            if (!string.Equals(search, previousSearch,
                StringComparison.Ordinal)) targetScroll = Vector2.zero;
            bool hasSearch = !string.IsNullOrEmpty(search);
            GUI.enabled = hasSearch;
            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                search = string.Empty;
                targetScroll = Vector2.zero;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool previousBones = settings.ShowBones;
            bool previousMeshes = settings.ShowMeshes;
            bool previousColliders = settings.ShowColliders;
            settings.ShowBones = GUILayout.Toggle(settings.ShowBones,
                "Bones", GUILayout.Width(105));
            settings.ShowMeshes = GUILayout.Toggle(settings.ShowMeshes,
                "Meshes", GUILayout.Width(105));
            settings.ShowColliders = GUILayout.Toggle(settings.ShowColliders,
                "Colliders", GUILayout.Width(115));
            if (settings.ShowBones != previousBones ||
                settings.ShowMeshes != previousMeshes ||
                settings.ShowColliders != previousColliders)
            {
                EndDrag(); ClearSelection(); hoverTarget = null; hoverRig = null;
                targetScroll = Vector2.zero;
                settings.Save();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled = rig != null;
            if (GUILayout.Button("Expand all")) SetAllHierarchyExpanded(rig,
                true);
            if (GUILayout.Button("Collapse all")) SetAllHierarchyExpanded(rig,
                false);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            DrawTargetGroups(rig);
            BuildVisibleTargetOrder(rig);
            GUILayout.Label("Gold bone | cyan mesh | magenta collider");
            targetScroll.x = 0f;
            targetScroll = GUILayout.BeginScrollView(targetScroll,
                GUIStyle.none, HighLogic.Skin.verticalScrollbar,
                GUILayout.Height(TargetScrollHeight));
            if (rig != null) foreach (RigTarget target in visibleTargetOrder)
                DrawHierarchyTarget(rig, target);
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select branch")) SelectHierarchy();
            if (GUILayout.Button("Mirror")) SelectCounterparts();
            if (GUILayout.Button("Clear")) ClearSelection();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Undo" + (undo.Count > 0 ? " (" + undo.Count + ")" : "")))
                Undo();
            if (GUILayout.Button("Redo" + (redo.Count > 0 ? " (" + redo.Count + ")" : "")))
                Redo();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawTargetGroups(EditableRig rig)
        {
            GUILayout.Space(5f);
            GUILayout.Label("Virtual accessory groups");
            if (rig == null || rig.Groups.Count == 0)
                GUILayout.Label("No groups yet. Multi-select accessory pieces " +
                    "and create one below.");
            else
            {
                groupScroll = GUILayout.BeginScrollView(groupScroll,
                    GUILayout.Height(Mathf.Min(92f, rig.Groups.Count * 25f + 4f)));
                foreach (RuntimeTargetGroup group in rig.Groups)
                {
                    bool active = group.Id == activeGroupId;
                    EnsureTreeStyles();
                    GUIStyle style = active ? treeSelectedStyle :
                        treeButtonStyle;
                    string label = (group.Automatic ? "[Auto] " : string.Empty) +
                        group.Name + "  (" + group.Members.Count + ")";
                    if (GUILayout.Button(label, style, GUILayout.Height(23f)))
                        SelectRuntimeGroup(group);
                }
                GUILayout.EndScrollView();
            }
            GUILayout.BeginHorizontal();
            groupName = GUILayout.TextField(groupName ?? string.Empty,
                GUILayout.MinWidth(150f));
            GUI.enabled = rig != null && selectedKeys.Count >= 2;
            if (GUILayout.Button("Create from selection", GUILayout.Width(150f)))
                CreateGroupFromSelection(rig);
            GUI.enabled = rig != null && ActiveManualGroupDefinition() != null;
            if (GUILayout.Button("Delete group", GUILayout.Width(92f)))
                DeleteActiveGroup();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void SelectRuntimeGroup(RuntimeTargetGroup group)
        {
            if (group == null || group.Members.Count == 0) return;
            selectedKeys.Clear();
            foreach (RigTarget member in group.Members)
                selectedKeys.Add(member.Key);
            selectedKey = group.Members[0].Key;
            rangeAnchorKey = selectedKey;
            activeGroupId = group.Id;
            RigTarget primary = SelectedTarget();
            if (primary != null)
                SyncFields(settings.GetOrCreate(primary.Key,
                    primary.DisplayName));
        }

        private void CreateGroupFromSelection(EditableRig rig)
        {
            List<RigTarget> members = SelectedTargets(true);
            if (rig == null || members.Count < 2) return;
            string name = (groupName ?? string.Empty).Trim();
            if (name.Length == 0) name = "Accessory group";
            TargetGroupDefinition definition = new TargetGroupDefinition {
                Id = Guid.NewGuid().ToString("N"), Name = name };
            foreach (RigTarget target in members)
                definition.Members.Add(new TargetGroupMemberDefinition {
                    Key = target.Key, Name = target.DisplayName });
            settings.Groups.Add(definition);
            RebuildAllTargetGroups();
            RuntimeTargetGroup runtime = rig.Groups.Find(
                delegate(RuntimeTargetGroup item) {
                    return item.Id == "manual:" + definition.Id; });
            SelectRuntimeGroup(runtime);
            settings.Save();
            ScreenMessages.PostScreenMessage("Kerbal Proportions group created: " +
                name, 2f, ScreenMessageStyle.UPPER_CENTER);
        }

        private TargetGroupDefinition ActiveManualGroupDefinition()
        {
            const string prefix = "manual:";
            if (string.IsNullOrEmpty(activeGroupId) ||
                !activeGroupId.StartsWith(prefix,
                    StringComparison.Ordinal)) return null;
            string id = activeGroupId.Substring(prefix.Length);
            return settings.Groups.Find(delegate(TargetGroupDefinition item) {
                return item.Id == id; });
        }

        private void DeleteActiveGroup()
        {
            TargetGroupDefinition definition = ActiveManualGroupDefinition();
            if (definition == null) return;
            settings.Groups.Remove(definition);
            activeGroupId = string.Empty;
            RebuildAllTargetGroups();
            settings.Save();
        }

        private void RebuildAllTargetGroups()
        {
            foreach (EditableRig rig in rigs) BuildTargetGroups(rig);
        }

        private void BuildVisibleTargetOrder(EditableRig rig)
        {
            visibleTargetOrder.Clear();
            visibleHierarchyOwnerId = rig == null ? 0 : rig.OwnerId;
            if (rig == null) return;
            bool searching = !string.IsNullOrEmpty(
                (search ?? string.Empty).Trim());
            if (!searching)
            {
                foreach (RigTarget root in rig.RootTargets)
                    CollectExpandedTargets(rig, root);
                return;
            }

            Dictionary<RigTarget, bool> subtreeMatches =
                new Dictionary<RigTarget, bool>();
            foreach (RigTarget root in rig.RootTargets)
                ComputeSubtreeSearchMatch(root, subtreeMatches);
            foreach (RigTarget root in rig.RootTargets)
                CollectSearchTargets(root, subtreeMatches);
        }

        private void CollectExpandedTargets(EditableRig rig, RigTarget target)
        {
            if (target == null) return;
            bool visible = MatchesTypeFilter(target);
            if (visible) visibleTargetOrder.Add(target);
            if (visible && !expandedHierarchyKeys.Contains(HierarchyStateKey(
                rig, target))) return;
            foreach (RigTarget child in target.Children)
                CollectExpandedTargets(rig, child);
        }

        private bool ComputeSubtreeSearchMatch(RigTarget target,
            Dictionary<RigTarget, bool> result)
        {
            bool matches = MatchesSearch(target);
            foreach (RigTarget child in target.Children)
                if (ComputeSubtreeSearchMatch(child, result)) matches = true;
            result[target] = matches;
            return matches;
        }

        private void CollectSearchTargets(RigTarget target,
            Dictionary<RigTarget, bool> subtreeMatches)
        {
            bool matches;
            if (target == null || !subtreeMatches.TryGetValue(target,
                out matches) || !matches) return;
            if (MatchesTypeFilter(target)) visibleTargetOrder.Add(target);
            foreach (RigTarget child in target.Children)
                CollectSearchTargets(child, subtreeMatches);
        }

        private void DrawHierarchyTarget(EditableRig rig, RigTarget target)
        {
            bool searching = !string.IsNullOrEmpty(
                (search ?? string.Empty).Trim());
            bool hasChildren = HasFilteredDescendant(target);
            string stateKey = HierarchyStateKey(rig, target);
            bool expanded = searching ||
                expandedHierarchyKeys.Contains(stateKey);
            GUILayout.BeginHorizontal();
            int filteredDepth = FilteredDepth(target);
            float indentation = Mathf.Min(filteredDepth, 12) * 11f;
            GUILayout.Space(indentation);
            if (hasChildren)
            {
                if (searching)
                    GUILayout.Label("-", GUILayout.Width(20));
                else if (GUILayout.Button(expanded ? "-" : "+",
                    GUILayout.Width(20), GUILayout.Height(22)))
                {
                    initializedHierarchyKeys.Add(stateKey);
                    if (expanded) expandedHierarchyKeys.Remove(stateKey);
                    else expandedHierarchyKeys.Add(stateKey);
                }
            }
            else GUILayout.Space(24f);

            bool selected = selectedKeys.Contains(target.Key);
            TransformEdit currentEdit;
            string changed = settings.TryGetForTarget(target.Key,
                target.DisplayName, out currentEdit) &&
                !currentEdit.IsIdentity ? " *" : string.Empty;
            if (hasChildren && !expanded)
            {
                int hiddenSelected = CountSelectedDescendants(target);
                if (hiddenSelected > 0) changed += "  (" + hiddenSelected +
                    " selected below)";
            }
            string type = target.Category == "Bone" ? "B" : "M";
            if (target.ColliderBindings.Count > 0) type += "+C";
            string category = filteredDepth == 0 ?
                "[ROOT " + type + "] " : "[" + type + "] ";
            EnsureTreeStyles();
            GUIStyle style = selected ? treeSelectedStyle : treeButtonStyle;
            GUIContent content = new GUIContent(category +
                FriendlyName(target.DisplayName) + changed, target.Key);
            float buttonWidth = Mathf.Max(150f,
                TargetPanelWidth - 66f - indentation);
            if (GUILayout.Button(content, style, GUILayout.Width(buttonWidth),
                GUILayout.Height(23)))
                SelectTarget(target,
                    Event.current.control || Event.current.command,
                    Event.current.shift, rig, true);
            GUILayout.EndHorizontal();
        }

        private void EnsureTreeStyles()
        {
            if (treeButtonStyle != null && treeSelectedStyle != null) return;
            treeButtonStyle = new GUIStyle(HighLogic.Skin.button);
            treeSelectedStyle = new GUIStyle(HighLogic.Skin.box);
            treeButtonStyle.alignment = TextAnchor.MiddleLeft;
            treeSelectedStyle.alignment = TextAnchor.MiddleLeft;
            treeButtonStyle.clipping = TextClipping.Clip;
            treeSelectedStyle.clipping = TextClipping.Clip;
            treeButtonStyle.wordWrap = false;
            treeSelectedStyle.wordWrap = false;
        }

        private int CountSelectedDescendants(RigTarget target)
        {
            int count = 0;
            foreach (RigTarget child in target.Children)
            {
                if (selectedKeys.Contains(child.Key)) count++;
                count += CountSelectedDescendants(child);
            }
            return count;
        }

        private bool MatchesTypeFilter(RigTarget target)
        {
            if (target == null) return false;
            if (target.ColliderBindings.Count > 0)
                return settings.ShowColliders;
            return target.Category == "Bone" ? settings.ShowBones :
                settings.ShowMeshes;
        }

        private bool HasFilteredDescendant(RigTarget target)
        {
            foreach (RigTarget child in target.Children)
                if (MatchesTypeFilter(child) || HasFilteredDescendant(child))
                    return true;
            return false;
        }

        private int FilteredDepth(RigTarget target)
        {
            int depth = 0;
            RigTarget parent = target == null ? null : target.ParentTarget;
            while (parent != null)
            {
                if (MatchesTypeFilter(parent)) depth++;
                parent = parent.ParentTarget;
            }
            return depth;
        }

        private void SetAllHierarchyExpanded(EditableRig rig, bool expanded)
        {
            if (rig == null) return;
            foreach (RigTarget target in rig.HierarchyPreorder)
            {
                if (target.Children.Count == 0) continue;
                string stateKey = HierarchyStateKey(rig, target);
                initializedHierarchyKeys.Add(stateKey);
                if (expanded) expandedHierarchyKeys.Add(stateKey);
                else expandedHierarchyKeys.Remove(stateKey);
            }
        }

        private void ExpandTargetAncestors(EditableRig rig, RigTarget target)
        {
            if (rig == null || target == null) return;
            RigTarget parent = target.ParentTarget;
            while (parent != null)
            {
                expandedHierarchyKeys.Add(HierarchyStateKey(rig, parent));
                parent = parent.ParentTarget;
            }
        }

        private void DrawInspectorPanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(InspectorPanelWidth));
            RigTarget target = SelectedTarget();
            inspectorTab = GUILayout.Toolbar(inspectorTab, InspectorTabs,
                GUILayout.Height(28));
            GUILayout.Space(10);
            if (inspectorTab <= 1)
            {
                GUILayout.Label(target == null ? "Select a target" :
                    (selectedKeys.Count > 1 ? selectedKeys.Count +
                    " selected - primary: " : target.Category + ": ") +
                    target.DisplayName);
                GUILayout.Space(7);
                GUILayout.BeginHorizontal();
                if (ModeButton("Move (W)", EditMode.Move))
                    editMode = EditMode.Move;
                if (ModeButton("Rotate (E)", EditMode.Rotate))
                    editMode = EditMode.Rotate;
                if (ModeButton("Scale (R)", EditMode.Scale))
                    editMode = EditMode.Scale;
                GUILayout.EndHorizontal();
                GUILayout.Space(9);
                GUILayout.BeginHorizontal();
                bool previousLocalSpace = settings.LocalSpace;
                GUI.enabled = editMode != EditMode.Scale;
                int axisSpace = GUILayout.Toolbar(settings.LocalSpace ? 0 : 1,
                    AxisSpaceModes, GUILayout.Width(260));
                settings.LocalSpace = axisSpace == 0;
                GUI.enabled = true;
                mirrorEdit = GUILayout.Toggle(mirrorEdit, "Mirror L/R",
                    GUILayout.Width(130));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                if (settings.LocalSpace != previousLocalSpace)
                    settings.Save();
                if (editMode == EditMode.Scale)
                {
                    GUILayout.Space(5);
                    GUILayout.Label("Scale uses target-local X / Y / Z axes.");
                }
                GUILayout.Space(9);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Gizmo size", GUILayout.Width(80));
                float previousGizmoSize = settings.GizmoSize;
                settings.GizmoSize = GUILayout.HorizontalSlider(
                    settings.GizmoSize, 0.4f, 2.5f,
                    GUILayout.MinWidth(170));
                if (Mathf.Abs(settings.GizmoSize - previousGizmoSize) >
                    0.0001f) gizmoSliderEditing = true;
                GUILayout.Label(settings.GizmoSize.ToString("0.00",
                    CultureInfo.InvariantCulture) + "x", GUILayout.Width(48));
                GUILayout.EndHorizontal();
                GUILayout.Space(12);
            }
            if (inspectorTab == 0) DrawPoseTab(target);
            else if (inspectorTab == 1) DrawAnimationTab(target);
            else if (inspectorTab == 2) DrawPortraitTab();
            else DrawProfilesTab();
            GUILayout.EndVertical();
        }

        private void DrawPoseTab(RigTarget target)
        {
            GUI.enabled = target != null;
            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.Label("Pose offsets");
            GUILayout.Space(8);
            GUILayout.Label("Position (local meters)");
            DrawVectorFields(ref posX, ref posY, ref posZ);
            GUILayout.Space(9);
            GUILayout.Label("Rotation (degrees)");
            DrawVectorFields(ref rotX, ref rotY, ref rotZ);
            GUILayout.Space(9);
            GUILayout.Label("Scale multiplier");
            DrawVectorFields(ref sclX, ref sclY, ref sclZ);
            GUILayout.Space(11);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply values")) ApplyNumeric();
            if (GUILayout.Button("Reset selected")) ResetSelected();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.enabled = true;
            GUILayout.Space(10);
            GUILayout.Label("Viewport: W move, E rotate, R scale. " +
                "Ctrl-click adds targets; Shift-click selects a range.");
        }

        private void DrawAnimationTab(RigTarget target)
        {
            GUILayout.BeginVertical(HighLogic.Skin.box);
            bool previousAnimationAware = settings.AnimationAwareRotation;
            settings.AnimationAwareRotation = GUILayout.Toggle(
                settings.AnimationAwareRotation,
                "Animation-safe pose rotation");
            if (settings.AnimationAwareRotation != previousAnimationAware)
                settings.Save();
            GUI.enabled = target != null;
            GUILayout.Space(12);
            GUILayout.Label("Animation rotation strength (rest-local axes)");
            GUILayout.Space(6);
            DrawAnimationInfluenceSlider("X / red", 0);
            GUILayout.Space(5);
            DrawAnimationInfluenceSlider("Y / green", 1);
            GUILayout.Space(5);
            DrawAnimationInfluenceSlider("Z / blue", 2);
            GUILayout.Space(11);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stock motion (100%)"))
                SetAnimationInfluence(Vector3.one);
            if (GUILayout.Button("Lock rotation (0%)"))
                SetAnimationInfluence(Vector3.zero);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.enabled = true;
            GUILayout.Space(10);
            GUILayout.Label("100% keeps the stock animation; 0% removes that " +
                "axis from the selected bone. For lateral leg swing, start with " +
                "the hip/upper-leg X axis. Parent + child restrictions compound.");
        }

        private void DrawProfilesTab()
        {
            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.Label("Profiles");
            GUILayout.Space(8);
            if (profileNames.Count > 0)
            {
                int chosen = GUILayout.SelectionGrid(selectedProfile,
                    profileNames.ToArray(), 2, GUILayout.MaxHeight(74));
                if (chosen >= 0 && chosen < profileNames.Count &&
                    chosen != selectedProfile)
                {
                    selectedProfile = chosen;
                    profileName = profileNames[chosen];
                }
            }
            GUILayout.Space(8);
            profileName = GUILayout.TextField(profileName ?? string.Empty);
            GUILayout.Space(9);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save / export")) SaveProfile();
            if (GUILayout.Button("Load profile")) LoadProfile();
            if (GUILayout.Button("Delete")) DeleteProfile();
            GUILayout.EndHorizontal();
            GUILayout.Space(7);
            if (GUILayout.Button("Refresh imports")) RefreshProfileNames();
            GUILayout.Space(9);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset all edits")) ResetAll();
            if (GUILayout.Button("Save + close"))
            {
                settings.Save(); ToggleWindow(false);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(10);
            GUILayout.Label("Profiles include proportions, motion limits, " +
                "and portrait framing. Each profile is a shareable .cfg file in " +
                "PluginData/Profiles. Drop a file there, then refresh imports. " +
                "Saving also makes the profile current for scene changes.");
        }

        private void DrawPortraitTab()
        {
            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.Label("Crew portrait camera");
            GUILayout.Space(8);
            GUILayout.Label(portraitCameras.Count > 0 ?
                portraitCameras.Count + " portrait camera" +
                (portraitCameras.Count == 1 ? " detected" : "s detected") :
                "Waiting for a crew portrait camera");
            GUILayout.Space(10);
            PortraitFraming portrait = settings.Portrait;
            portrait.Horizontal = DrawPortraitSlider("Horizontal",
                portrait.Horizontal, -0.25f, 0.25f, "0.000 m");
            portrait.Vertical = DrawPortraitSlider("Vertical",
                portrait.Vertical, -0.25f, 0.25f, "0.000 m");
            portrait.Zoom = DrawPortraitSlider("Zoom", portrait.Zoom,
                0.5f, 2f, "0.00x");
            portrait.Yaw = DrawPortraitSlider("Aim yaw", portrait.Yaw,
                -30f, 30f, "0.0 deg");
            portrait.Pitch = DrawPortraitSlider("Aim pitch", portrait.Pitch,
                -30f, 30f, "0.0 deg");
            portrait.Clamp();
            GUILayout.Space(12);
            if (GUILayout.Button("Reset to stock"))
            {
                PushUndo("Reset portrait");
                RestorePortraitCameras();
                portrait.Reset();
                portraitSliderEditing = false;
                settings.Save();
            }
            GUILayout.EndVertical();
            GUILayout.Space(10);
            GUILayout.Label("Framing is relative to each pod's stock portrait " +
                "camera and applies live to every crew portrait. It never moves " +
                "the Kerbal rig or seat. Changes save automatically.");
        }

        private float DrawPortraitSlider(string label, float current,
            float minimum, float maximum, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(76));
            float next = GUILayout.HorizontalSlider(current, minimum, maximum);
            GUILayout.Label(next.ToString(format,
                CultureInfo.InvariantCulture), GUILayout.Width(66));
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            if (Mathf.Abs(next - current) < 0.0001f) return current;
            if (!portraitSliderEditing)
            {
                PushUndo("Portrait framing");
                portraitSliderEditing = true;
            }
            return next;
        }

        private void DrawAnimationInfluenceSlider(string label, int axis)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70));
            float current = animationInfluence[axis];
            float next = GUILayout.HorizontalSlider(current, 0f, 1f);
            GUILayout.Label(Mathf.RoundToInt(next * 100f) + "%",
                GUILayout.Width(45));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(next - current) < 0.0001f) return;
            if (!animationSliderEditing)
            {
                PushUndo("Animation strength");
                animationSliderEditing = true;
            }
            animationInfluence[axis] = next;
            ApplyAnimationInfluenceAxis(axis, next);
        }

        private void ApplyAnimationInfluenceAxis(int axis, float value)
        {
            EditableRig rig = PrimaryRig();
            List<RigTarget> targets = SelectedTargets(false);
            if (rig == null || targets.Count == 0) return;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (RigTarget item in targets)
            {
                SetAnimationInfluenceAxis(item, axis, value, visited);
                if (mirrorEdit)
                    SetAnimationInfluenceAxis(FindCounterpart(rig, item), axis,
                        value, visited);
            }
        }

        private void SetAnimationInfluenceAxis(RigTarget target, int axis,
            float value, HashSet<string> visited)
        {
            if (target == null || !visited.Add(target.Key)) return;
            TransformEdit edit = settings.GetOrCreate(target.Key,
                target.DisplayName);
            Vector3 influence = edit.AnimationInfluence;
            influence[axis] = Mathf.Clamp01(value);
            edit.AnimationInfluence = influence;
        }

        private void SetAnimationInfluence(Vector3 value)
        {
            EditableRig rig = PrimaryRig();
            List<RigTarget> targets = SelectedTargets(false);
            if (rig == null || targets.Count == 0) return;
            PushUndo("Animation strength");
            value = EditorSettings.ClampAnimationInfluence(value);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (RigTarget item in targets)
            {
                SetAnimationInfluence(item, value, visited);
                if (mirrorEdit)
                    SetAnimationInfluence(FindCounterpart(rig, item), value,
                        visited);
            }
            animationInfluence = value;
            settings.Save();
        }

        private void SetAnimationInfluence(RigTarget target, Vector3 value,
            HashSet<string> visited)
        {
            if (target == null || !visited.Add(target.Key)) return;
            settings.GetOrCreate(target.Key,
                target.DisplayName).AnimationInfluence = value;
        }

        private bool ModeButton(string label, EditMode mode)
        {
            GUIStyle style = editMode == mode ? HighLogic.Skin.box : HighLogic.Skin.button;
            return GUILayout.Button(label, style);
        }

        private static void DrawVectorFields(ref string x, ref string y, ref string z)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("X", GUILayout.Width(14)); x = GUILayout.TextField(x, GUILayout.Width(88));
            GUILayout.Label("Y", GUILayout.Width(14)); y = GUILayout.TextField(y, GUILayout.Width(88));
            GUILayout.Label("Z", GUILayout.Width(14)); z = GUILayout.TextField(z, GUILayout.Width(88));
            GUILayout.EndHorizontal();
        }

        private bool MatchesSearch(RigTarget target)
        {
            string query = (search ?? string.Empty).Trim();
            if (query.Length == 0) return true;
            return target.DisplayName.IndexOf(query,
                StringComparison.OrdinalIgnoreCase) >= 0 ||
                FriendlyName(target.DisplayName).IndexOf(query,
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                target.Category.IndexOf(query,
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                target.Key.IndexOf(query,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SelectTarget(RigTarget target, bool additive, bool range,
            EditableRig rig, bool visibleRangeOnly)
        {
            activeGroupId = string.Empty;
            if (range && rig != null && !string.IsNullOrEmpty(rangeAnchorKey))
            {
                bool useVisible = visibleRangeOnly &&
                    visibleHierarchyOwnerId == rig.OwnerId;
                List<RigTarget> order = useVisible ? visibleTargetOrder :
                    rig.HierarchyPreorder;
                int start = order.FindIndex(delegate(RigTarget item) {
                    return item.Key == rangeAnchorKey; });
                int end = order.IndexOf(target);
                if (!additive) selectedKeys.Clear();
                if (start >= 0 && end >= 0)
                {
                    int minimum = Mathf.Min(start, end);
                    int maximum = Mathf.Max(start, end);
                    for (int index = minimum; index <= maximum; index++)
                        selectedKeys.Add(order[index].Key);
                }
                else selectedKeys.Add(target.Key);
            }
            else if (additive)
            {
                if (!selectedKeys.Add(target.Key)) selectedKeys.Remove(target.Key);
            }
            else
            {
                selectedKeys.Clear(); selectedKeys.Add(target.Key);
            }
            if (!selectedKeys.Contains(target.Key) && selectedKeys.Count > 0)
            {
                selectedKey = string.Empty;
                if (rig != null) foreach (RigTarget candidate in
                    rig.HierarchyPreorder)
                    if (selectedKeys.Contains(candidate.Key))
                    { selectedKey = candidate.Key; break; }
            }
            else if (selectedKeys.Contains(target.Key)) selectedKey = target.Key;
            else selectedKey = string.Empty;
            rangeAnchorKey = target.Key;
            ExpandTargetAncestors(rig, target);
            if (!selectedKeys.Contains(selectedKey)) return;
            RigTarget primary = SelectedTarget();
            if (primary != null)
            {
                SyncFields(settings.GetOrCreate(primary.Key,
                    primary.DisplayName));
            }
        }

        private void ClearSelection()
        {
            selectedKeys.Clear(); selectedKey = string.Empty;
            rangeAnchorKey = string.Empty;
            activeGroupId = string.Empty;
        }

        private void SelectHierarchy()
        {
            EditableRig rig = PrimaryRig(); RigTarget primary = SelectedTarget();
            if (rig == null || primary == null || primary.Transform == null) return;
            foreach (RigTarget target in rig.Targets)
                if (target.Transform == primary.Transform ||
                    target.Transform.IsChildOf(primary.Transform))
                    selectedKeys.Add(target.Key);
        }

        private void SelectCounterparts()
        {
            EditableRig rig = PrimaryRig(); if (rig == null) return;
            List<RigTarget> selected = SelectedTargets(false);
            foreach (RigTarget target in selected)
            {
                RigTarget counterpart = FindCounterpart(rig, target);
                if (counterpart != null) selectedKeys.Add(counterpart.Key);
            }
        }

        private static RigTarget FindCounterpart(EditableRig rig, RigTarget source)
        {
            if (rig == null || source == null) return null;
            int sourceSide = DetectSide(source.DisplayName + " " + source.Key);
            if (sourceSide == 0) return null;
            string neutralName = NeutralSideName(source.DisplayName);
            RigTarget best = null; int bestScore = int.MinValue;
            foreach (RigTarget candidate in rig.Targets)
            {
                if (candidate == source || candidate.Category != source.Category ||
                    DetectSide(candidate.DisplayName + " " + candidate.Key) != -sourceSide ||
                    NeutralSideName(candidate.DisplayName) != neutralName) continue;
                int score = CommonSuffixLength(NeutralSideName(candidate.Key),
                    NeutralSideName(source.Key));
                if (score > bestScore) { best = candidate; bestScore = score; }
            }
            return best;
        }

        private static int DetectSide(string value)
        {
            string text = (value ?? string.Empty).ToLowerInvariant();
            bool left = text.Contains("left") || text.Contains("bn_l_") ||
                text.Contains("be_l_") || text.Contains("_l_") ||
                text.Contains("arm_l") || text.Contains("leg_l") ||
                text.EndsWith("_l");
            bool right = text.Contains("right") || text.Contains("bn_r_") ||
                text.Contains("be_r_") || text.Contains("_r_") ||
                text.Contains("arm_r") || text.Contains("leg_r") ||
                text.EndsWith("_r");
            return left == right ? 0 : (left ? -1 : 1);
        }

        private static string NeutralSideName(string value)
        {
            string text = (value ?? string.Empty).ToLowerInvariant();
            text = text.Replace("left", "side").Replace("right", "side")
                .Replace("bn_l_", "bn_side_").Replace("bn_r_", "bn_side_")
                .Replace("be_l_", "be_side_").Replace("be_r_", "be_side_")
                .Replace("_l_", "_side_").Replace("_r_", "_side_")
                .Replace("arm_l", "arm_side").Replace("arm_r", "arm_side")
                .Replace("leg_l", "leg_side").Replace("leg_r", "leg_side")
                .Replace("_l[", "_side[").Replace("_r[", "_side[");
            return text;
        }

        private static int CommonSuffixLength(string a, string b)
        {
            int count = 0;
            while (count < a.Length && count < b.Length &&
                a[a.Length - 1 - count] == b[b.Length - 1 - count]) count++;
            return count;
        }

        private void ApplyMirroredNumeric(RigTarget primary)
        {
            EditableRig rig = PrimaryRig(); if (rig == null) return;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            List<RigTarget> ordered = SelectedTargets(false);
            if (primary != null && ordered.Remove(primary)) ordered.Insert(0, primary);
            foreach (RigTarget source in ordered)
            {
                if (!visited.Add(source.Key)) continue;
                RigTarget counterpart = FindCounterpart(rig, source);
                if (counterpart == null) continue;
                visited.Add(counterpart.Key);
                TransformEdit sourceEdit = settings.GetOrCreate(source.Key,
                    source.DisplayName);
                settings.GetOrCreate(counterpart.Key, counterpart.DisplayName)
                    .CopyValuesFrom(MirrorEdit(rig, source, counterpart,
                        sourceEdit));
            }
        }

        private TransformEdit MirrorEdit(EditableRig rig, RigTarget source,
            RigTarget target, TransformEdit sourceEdit)
        {
            TransformEdit result = new TransformEdit { Key = target.Key,
                Name = target.DisplayName, Scale = sourceEdit.Scale,
                AnimationInfluence = sourceEdit.AnimationInfluence };
            Vector3 sourceWorldOffset = source.Transform.parent == null ?
                sourceEdit.Position :
                source.Transform.parent.TransformVector(sourceEdit.Position);
            Vector3 mirroredWorldOffset = ReflectVector(sourceWorldOffset,
                rig.Root.right);
            result.Position = target.Transform.parent == null ? mirroredWorldOffset :
                target.Transform.parent.InverseTransformVector(mirroredWorldOffset);

            Quaternion sourceRotation = Quaternion.Euler(sourceEdit.Rotation);
            float angle; Vector3 localAxis;
            sourceRotation.ToAngleAxis(out angle, out localAxis);
            if (localAxis.sqrMagnitude > 0.000001f && Mathf.Abs(angle) > 0.0001f)
            {
                Quaternion sourceBasis = settings.AnimationAwareRotation ?
                    source.ReferenceRotation : source.FilteredBaseRotation;
                Quaternion targetBasis = settings.AnimationAwareRotation ?
                    target.ReferenceRotation : target.FilteredBaseRotation;
                Quaternion sourceBaseWorld = (source.Transform.parent == null ?
                    Quaternion.identity : source.Transform.parent.rotation) *
                    sourceBasis;
                Vector3 worldAxis = sourceBaseWorld * localAxis;
                Vector3 mirroredAxis = -ReflectVector(worldAxis, rig.Root.right);
                Quaternion targetBaseWorld = (target.Transform.parent == null ?
                    Quaternion.identity : target.Transform.parent.rotation) *
                    targetBasis;
                Vector3 targetLocalAxis = Quaternion.Inverse(targetBaseWorld) *
                    mirroredAxis;
                result.Rotation = SignedEuler(Quaternion.AngleAxis(angle,
                    targetLocalAxis.normalized));
            }
            return result;
        }

        private static Vector3 ReflectVector(Vector3 value, Vector3 planeNormal)
        {
            Vector3 normal = planeNormal.normalized;
            return value - 2f * Vector3.Dot(value, normal) * normal;
        }

        private void SyncFields(TransformEdit edit)
        {
            posX = EditorSettings.Format(edit.Position.x);
            posY = EditorSettings.Format(edit.Position.y);
            posZ = EditorSettings.Format(edit.Position.z);
            rotX = EditorSettings.Format(edit.Rotation.x);
            rotY = EditorSettings.Format(edit.Rotation.y);
            rotZ = EditorSettings.Format(edit.Rotation.z);
            sclX = EditorSettings.Format(edit.Scale.x);
            sclY = EditorSettings.Format(edit.Scale.y);
            sclZ = EditorSettings.Format(edit.Scale.z);
            animationInfluence = edit.AnimationInfluence;
        }

        private void ApplyNumeric()
        {
            RigTarget target = SelectedTarget(); List<RigTarget> targets =
                SelectedTargets(false);
            if (target == null || targets.Count == 0) return;
            Vector3 position, rotation, scale;
            if (!TryVector(posX, posY, posZ, out position) ||
                !TryVector(rotX, rotY, rotZ, out rotation) ||
                !TryVector(sclX, sclY, sclZ, out scale))
            {
                ScreenMessages.PostScreenMessage(
                    "Kerbal Proportions: invalid numeric value", 2f,
                    ScreenMessageStyle.UPPER_CENTER); return;
            }
            PushUndo("Numeric edit");
            foreach (RigTarget item in targets)
            {
                TransformEdit edit = settings.GetOrCreate(item.Key, item.DisplayName);
                edit.Position = Vector3.Min(Vector3.one * 2f,
                    Vector3.Max(Vector3.one * -2f, position));
                edit.Rotation = rotation;
                edit.Scale = EditorSettings.ClampScale(scale);
            }
            if (mirrorEdit) ApplyMirroredNumeric(target);
            SyncFields(settings.GetOrCreate(target.Key, target.DisplayName));
        }

        private static bool TryVector(string x, string y, string z, out Vector3 value)
        {
            float vx = 0f, vy = 0f, vz = 0f;
            bool validX = float.TryParse(x, NumberStyles.Float,
                CultureInfo.InvariantCulture, out vx);
            bool validY = float.TryParse(y, NumberStyles.Float,
                CultureInfo.InvariantCulture, out vy);
            bool validZ = float.TryParse(z, NumberStyles.Float,
                CultureInfo.InvariantCulture, out vz);
            bool valid = validX && validY && validZ;
            value = valid ? new Vector3(vx, vy, vz) : Vector3.zero;
            return valid;
        }

        private void ResetSelected()
        {
            RigTarget target = SelectedTarget(); List<RigTarget> targets =
                SelectedTargets(false); if (target == null) return;
            PushUndo("Reset selection");
            foreach (RigTarget item in targets)
                settings.GetOrCreate(item.Key, item.DisplayName)
                    .CopyValuesFrom(new TransformEdit());
            if (mirrorEdit)
            {
                EditableRig rig = PrimaryRig();
                foreach (RigTarget item in targets)
                {
                    RigTarget counterpart = FindCounterpart(rig, item);
                    if (counterpart != null)
                        settings.GetOrCreate(counterpart.Key,
                            counterpart.DisplayName).CopyValuesFrom(
                                new TransformEdit());
                }
            }
            SyncFields(settings.GetOrCreate(target.Key, target.DisplayName));
        }

        private void ResetAll()
        {
            PushUndo("Reset all"); settings.ClearEdits();
            RigTarget target = SelectedTarget();
            if (target != null) SyncFields(settings.GetOrCreate(target.Key,
                target.DisplayName));
        }

        private void PushUndo(string label)
        {
            undo.Add(CaptureSnapshot(label));
            if (undo.Count > 40) undo.RemoveAt(0);
            redo.Clear();
        }

        private EditSnapshot CaptureSnapshot(string label)
        {
            return new EditSnapshot { Label = label,
                Edits = settings.CloneEdits(),
                Portrait = settings.Portrait.Clone() };
        }

        private void RestoreSnapshot(EditSnapshot snapshot)
        {
            if (snapshot == null) return;
            settings.ReplaceEdits(snapshot.Edits);
            if (snapshot.Portrait != null)
                settings.Portrait.CopyFrom(snapshot.Portrait);
        }

        private void Undo()
        {
            if (undo.Count == 0) return;
            redo.Add(CaptureSnapshot("Redo"));
            EditSnapshot state = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
            RestoreSnapshot(state); settings.Save();
            SyncSelectedFields();
        }

        private void Redo()
        {
            if (redo.Count == 0) return;
            undo.Add(CaptureSnapshot("Undo"));
            EditSnapshot state = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1);
            RestoreSnapshot(state); settings.Save();
            SyncSelectedFields();
        }

        private void SyncSelectedFields()
        {
            RigTarget target = SelectedTarget(); if (target == null) return;
            SyncFields(settings.GetOrCreate(target.Key, target.DisplayName));
        }

        private void SaveProfile()
        {
            string name = (profileName ?? string.Empty).Trim();
            if (name.Length == 0) return;
            string path = StandaloneProfilePath(name);
            ConfigNode root = new ConfigNode();
            ConfigNode profile = root.AddNode("KERBAL_PROPORTIONS_PROFILE");
            profile.AddValue("name", name);
            profile.AddValue("formatVersion", 2);
            WriteProfileEdits(profile, settings.Edits);
            WriteProfilePortrait(profile, settings.Portrait);
            EditorSettings.WriteGroups(profile, settings.Groups);
            if (!Directory.Exists(ProfilesDirectory))
                Directory.CreateDirectory(ProfilesDirectory);
            root.Save(path);
            // A named profile is also the user's current working state. Persist
            // both files so a quickload/scene rebuild cannot resurrect an older
            // settings snapshot while leaving the profile itself intact.
            settings.Save();
            RefreshProfileNames();
            selectedProfile = profileNames.FindIndex(delegate(string item) {
                return string.Equals(item, name, StringComparison.OrdinalIgnoreCase); });
            ScreenMessages.PostScreenMessage(
                "Kerbal Proportions profile saved: " + name, 2f,
                ScreenMessageStyle.UPPER_CENTER);
        }

        private void LoadProfile()
        {
            ConfigNode profile = FindProfile(profileName); if (profile == null) return;
            PushUndo("Load profile"); settings.ReplaceEdits(ReadProfileEdits(profile));
            settings.Groups.Clear();
            EditorSettings.ReadGroups(profile, settings.Groups);
            activeGroupId = string.Empty;
            RebuildAllTargetGroups();
            PortraitFraming portrait;
            if (TryReadProfilePortrait(profile, out portrait))
                settings.Portrait.CopyFrom(portrait);
            settings.Save(); SyncSelectedFields();
            ScreenMessages.PostScreenMessage(
                "Kerbal Proportions profile loaded: " + profileName,
                2f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void DeleteProfile()
        {
            string name = (profileName ?? string.Empty).Trim();
            if (name.Length == 0) return;
            string source;
            if (profileSources.TryGetValue(name, out source) &&
                source.Length > 0 && File.Exists(source)) File.Delete(source);
            DeleteLegacyProfile(name);
            RefreshProfileNames();
        }

        private void RefreshProfileNames()
        {
            string previous = (profileName ?? string.Empty).Trim();
            profileNames.Clear(); profileSources.Clear(); selectedProfile = -1;
            if (Directory.Exists(ProfilesDirectory))
            {
                foreach (string path in Directory.GetFiles(ProfilesDirectory,
                    "*.cfg", SearchOption.TopDirectoryOnly))
                {
                    ConfigNode profile = LoadStandaloneProfile(path);
                    if (profile == null) continue;
                    string name = (profile.GetValue("name") ?? string.Empty).Trim();
                    if (name.Length == 0 || profileSources.ContainsKey(name)) continue;
                    profileSources[name] = path;
                    profileNames.Add(name);
                }
            }
            if (File.Exists(ProfilesPath))
            {
                ConfigNode root = ConfigNode.Load(ProfilesPath);
                ConfigNode container = ProfileContainer(root, false);
                if (container != null)
                    foreach (ConfigNode profile in container.GetNodes("PROFILE"))
                    {
                        string name = (profile.GetValue("name") ??
                            string.Empty).Trim();
                        if (name.Length == 0 || profileSources.ContainsKey(name))
                            continue;
                        profileSources[name] = string.Empty;
                        profileNames.Add(name);
                    }
            }
            profileNames.Sort(StringComparer.OrdinalIgnoreCase);
            if (profileNames.Count > 0)
            {
                selectedProfile = profileNames.FindIndex(delegate(string item) {
                    return string.Equals(item, previous,
                        StringComparison.OrdinalIgnoreCase); });
                if (selectedProfile < 0) selectedProfile = 0;
                profileName = profileNames[selectedProfile];
            }
        }

        private static ConfigNode LoadStandaloneProfile(string path)
        {
            try
            {
                ConfigNode root = ConfigNode.Load(path);
                if (root == null) return null;
                if (root.name == "KERBAL_PROPORTIONS_PROFILE") return root;
                ConfigNode profile = root.GetNode("KERBAL_PROPORTIONS_PROFILE");
                if (profile != null) return profile;
                if (root.name == "PROFILE") return root;
                profile = root.GetNode("PROFILE");
                if (profile != null) return profile;
                // ConfigNode.Save can serialize a named root as flat top-level
                // values. Accept those files so profiles exported by early 2.6
                // test builds remain importable.
                if (!string.IsNullOrEmpty(root.GetValue("name")) &&
                    (root.GetNodes("TARGET").Length > 0 ||
                    root.GetNodes("GROUP").Length > 0 ||
                    root.GetNodes("COLLIDER").Length > 0 ||
                    root.GetNode("PORTRAIT") != null)) return root;
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[KerbalProportions] Could not import profile " +
                    path + ": " + exception.Message);
                return null;
            }
        }

        private string StandaloneProfilePath(string name)
        {
            string existing;
            if (profileSources.TryGetValue(name, out existing) &&
                existing.Length > 0) return existing;
            string safe = SafeFileName(name);
            string path = Path.Combine(ProfilesDirectory, safe + ".cfg");
            int suffix = 2;
            while (File.Exists(path))
            {
                ConfigNode profile = LoadStandaloneProfile(path);
                if (profile != null && string.Equals(profile.GetValue("name"),
                    name, StringComparison.OrdinalIgnoreCase)) return path;
                path = Path.Combine(ProfilesDirectory, safe + "-" +
                    suffix.ToString(CultureInfo.InvariantCulture) + ".cfg");
                suffix++;
            }
            return path;
        }

        private static string SafeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] characters = (name ?? string.Empty).Trim().ToCharArray();
            for (int index = 0; index < characters.Length; index++)
                if (Array.IndexOf(invalid, characters[index]) >= 0)
                    characters[index] = '_';
            string result = new string(characters).Trim().TrimEnd('.');
            return result.Length > 0 ? result : "Profile";
        }

        private static void DeleteLegacyProfile(string name)
        {
            if (!File.Exists(ProfilesPath)) return;
            ConfigNode root = ConfigNode.Load(ProfilesPath);
            ConfigNode container = ProfileContainer(root, false);
            if (container == null) return;
            bool changed = false;
            foreach (ConfigNode profile in container.GetNodes("PROFILE"))
                if (string.Equals(profile.GetValue("name"), name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    container.RemoveNode(profile);
                    changed = true;
                }
            if (changed) root.Save(ProfilesPath);
        }

        private static void WriteProfileEdits(ConfigNode profile,
            Dictionary<string, TransformEdit> edits)
        {
            foreach (TransformEdit edit in edits.Values)
            {
                if (edit.IsIdentity) continue;
                ConfigNode target = profile.AddNode("TARGET");
                target.AddValue("key", edit.Key); target.AddValue("name", edit.Name);
                target.AddValue("positionX", EditorSettings.Format(edit.Position.x));
                target.AddValue("positionY", EditorSettings.Format(edit.Position.y));
                target.AddValue("positionZ", EditorSettings.Format(edit.Position.z));
                target.AddValue("rotationX", EditorSettings.Format(edit.Rotation.x));
                target.AddValue("rotationY", EditorSettings.Format(edit.Rotation.y));
                target.AddValue("rotationZ", EditorSettings.Format(edit.Rotation.z));
                target.AddValue("scaleX", EditorSettings.Format(edit.Scale.x));
                target.AddValue("scaleY", EditorSettings.Format(edit.Scale.y));
                target.AddValue("scaleZ", EditorSettings.Format(edit.Scale.z));
                target.AddValue("animationInfluenceX",
                    EditorSettings.Format(edit.AnimationInfluence.x));
                target.AddValue("animationInfluenceY",
                    EditorSettings.Format(edit.AnimationInfluence.y));
                target.AddValue("animationInfluenceZ",
                    EditorSettings.Format(edit.AnimationInfluence.z));
            }
        }

        private static void WriteProfileVector(ConfigNode node, string prefix,
            Vector3 value)
        {
            node.AddValue(prefix + "X", EditorSettings.Format(value.x));
            node.AddValue(prefix + "Y", EditorSettings.Format(value.y));
            node.AddValue(prefix + "Z", EditorSettings.Format(value.z));
        }

        private static void WriteProfilePortrait(ConfigNode profile,
            PortraitFraming portrait)
        {
            ConfigNode node = profile.AddNode("PORTRAIT");
            node.AddValue("horizontal",
                EditorSettings.Format(portrait.Horizontal));
            node.AddValue("vertical", EditorSettings.Format(portrait.Vertical));
            node.AddValue("zoom", EditorSettings.Format(portrait.Zoom));
            node.AddValue("yaw", EditorSettings.Format(portrait.Yaw));
            node.AddValue("pitch", EditorSettings.Format(portrait.Pitch));
        }

        private static bool TryReadProfilePortrait(ConfigNode profile,
            out PortraitFraming portrait)
        {
            portrait = null;
            ConfigNode node = profile == null ? null :
                profile.GetNode("PORTRAIT");
            if (node == null) return false;
            PortraitFraming result = new PortraitFraming();
            result.Horizontal = ReadProfileFloat(node, "horizontal", 0f);
            result.Vertical = ReadProfileFloat(node, "vertical", 0f);
            result.Zoom = ReadProfileFloat(node, "zoom", 1f);
            result.Yaw = ReadProfileFloat(node, "yaw", 0f);
            result.Pitch = ReadProfileFloat(node, "pitch", 0f);
            result.Clamp();
            portrait = result;
            return true;
        }

        private static float ReadProfileFloat(ConfigNode node, string key,
            float fallback)
        {
            float value;
            return float.TryParse(node.GetValue(key), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private ConfigNode FindProfile(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();
            string source;
            if (profileSources.TryGetValue(trimmed, out source) &&
                source.Length > 0)
            {
                ConfigNode standalone = LoadStandaloneProfile(source);
                if (standalone != null) return standalone;
            }
            if (!File.Exists(ProfilesPath)) return null;
            ConfigNode root = ConfigNode.Load(ProfilesPath);
            ConfigNode container = ProfileContainer(root, false);
            if (container == null) return null;
            foreach (ConfigNode profile in container.GetNodes("PROFILE"))
                if (string.Equals(profile.GetValue("name"), trimmed,
                    StringComparison.OrdinalIgnoreCase)) return profile;
            ScreenMessages.PostScreenMessage(
                "Kerbal Proportions profile not found: " + name, 2f,
                ScreenMessageStyle.UPPER_CENTER); return null;
        }

        private static ConfigNode ProfileContainer(ConfigNode root, bool create)
        {
            if (root == null) return null;
            if (root.name == "KERBAL_PROPORTIONS_PROFILES") return root;
            if (root.name == "KERBAL_PROPORTIONS_V2_PROFILES")
            {
                if (create) root.name = "KERBAL_PROPORTIONS_PROFILES";
                return root;
            }
            ConfigNode container = root.GetNode(
                "KERBAL_PROPORTIONS_PROFILES");
            if (container == null)
            {
                container = root.GetNode("KERBAL_PROPORTIONS_V2_PROFILES");
                if (container != null && create)
                    container.name = "KERBAL_PROPORTIONS_PROFILES";
            }
            return container ?? (create ?
                root.AddNode("KERBAL_PROPORTIONS_PROFILES") : null);
        }

        private static Dictionary<string, TransformEdit> ReadProfileEdits(ConfigNode profile)
        {
            Dictionary<string, TransformEdit> result =
                new Dictionary<string, TransformEdit>(StringComparer.Ordinal);
            foreach (ConfigNode node in profile.GetNodes("TARGET"))
            {
                string key = node.GetValue("key") ?? string.Empty;
                if (key.Length == 0) continue;
                TransformEdit edit = new TransformEdit { Key = key,
                    Name = node.GetValue("name") ?? key };
                ParseNodeVector(node, "position", Vector3.zero, out edit.Position);
                ParseNodeVector(node, "rotation", Vector3.zero, out edit.Rotation);
                ParseNodeVector(node, "scale", Vector3.one, out edit.Scale);
                ParseNodeVector(node, "animationInfluence", Vector3.one,
                    out edit.AnimationInfluence);
                edit.Scale = EditorSettings.ClampScale(edit.Scale);
                edit.AnimationInfluence =
                    EditorSettings.ClampAnimationInfluence(
                        edit.AnimationInfluence);
                result[key] = edit;
            }
            return result;
        }

        private static void ParseNodeVector(ConfigNode node, string prefix,
            Vector3 fallback, out Vector3 value)
        {
            float x, y, z;
            if (!float.TryParse(node.GetValue(prefix + "X"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out x)) x = fallback.x;
            if (!float.TryParse(node.GetValue(prefix + "Y"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out y)) y = fallback.y;
            if (!float.TryParse(node.GetValue(prefix + "Z"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out z)) z = fallback.z;
            value = new Vector3(x, y, z);
        }

        private void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.W)) editMode = EditMode.Move;
            if (Input.GetKeyDown(KeyCode.E)) editMode = EditMode.Rotate;
            if (Input.GetKeyDown(KeyCode.R)) editMode = EditMode.Scale;
        }

        private void HandleViewportInput()
        {
            Vector3 mouse = Input.mousePosition;
            Vector2 guiMouse = new Vector2(mouse.x, Screen.height - mouse.y);
            Camera camera = ActiveCamera(); RigTarget target = SelectedTarget();
            if (dragging)
            {
                if (Input.GetMouseButton(0)) UpdateDrag(camera, mouse);
                else EndDrag();
                return;
            }
            if (hierarchyWindowRect.Contains(guiMouse) ||
                controlsWindowRect.Contains(guiMouse))
            {
                hotAxis = -1; hoverTarget = null; hoverRig = null; return;
            }
            hotAxis = target == null || camera == null ? -1 :
                HitAxis(camera, target, mouse);
            hoverRig = PrimaryRig();
            hoverTarget = hotAxis >= 0 || camera == null ? null :
                FindHoverTarget(hoverRig, camera, mouse);
            if (!Input.GetMouseButtonDown(0)) return;
            if (hotAxis >= 0) BeginDrag(camera, target, mouse);
            else if (hoverTarget != null)
                SelectTarget(hoverTarget,
                    Input.GetKey(KeyCode.LeftControl) ||
                    Input.GetKey(KeyCode.RightControl),
                    Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift), hoverRig, false);
        }

        private RigTarget FindHoverTarget(EditableRig rig, Camera camera,
            Vector3 mouse)
        {
            if (rig == null || camera == null) return null;
            Vector2 point = new Vector2(mouse.x, mouse.y);
            RigTarget best = null; float bestDistance = 16f;
            float bestDepth = float.MaxValue;
            foreach (RigTarget candidate in rig.Targets)
            {
                if (candidate.Transform == null || candidate.Category != "Bone" ||
                    !MatchesTypeFilter(candidate))
                    continue;
                Vector3 screen = camera.WorldToScreenPoint(candidate.Transform.position);
                if (screen.z <= 0f) continue;
                float distance = Vector2.Distance(new Vector2(screen.x, screen.y), point);
                foreach (RigTarget child in rig.Targets)
                {
                    if (child.Transform == null ||
                        child.Transform.parent != candidate.Transform) continue;
                    Vector3 childScreen = camera.WorldToScreenPoint(child.Transform.position);
                    if (childScreen.z <= 0f) continue;
                    distance = Mathf.Min(distance, DistanceToSegment(point,
                        new Vector2(screen.x, screen.y),
                        new Vector2(childScreen.x, childScreen.y)));
                }
                if (distance < bestDistance - 0.25f ||
                    Mathf.Abs(distance - bestDistance) <= 0.25f &&
                    screen.z < bestDepth)
                {
                    best = candidate; bestDistance = distance; bestDepth = screen.z;
                }
            }
            if (best != null) return best;

            float smallestArea = float.MaxValue;
            foreach (RigTarget candidate in rig.Targets)
            {
                if (candidate.Category != "Mesh" ||
                    !MatchesTypeFilter(candidate)) continue;
                foreach (BoneRendererBinding binding in candidate.RendererBindings)
                {
                    if (binding.Renderer == null) continue;
                    Rect bounds;
                    float depth;
                    if (!ScreenBounds(camera, binding.Renderer.bounds, out bounds,
                        out depth) || !bounds.Contains(point)) continue;
                    float area = bounds.width * bounds.height;
                    if (area < smallestArea)
                    {
                        smallestArea = area; best = candidate; bestDepth = depth;
                    }
                }
            }
            return best;
        }

        private static bool ScreenBounds(Camera camera, Bounds bounds,
            out Rect result, out float depth)
        {
            Vector3 minimum = bounds.min, maximum = bounds.max;
            Vector3[] corners = {
                new Vector3(minimum.x, minimum.y, minimum.z),
                new Vector3(maximum.x, minimum.y, minimum.z),
                new Vector3(minimum.x, maximum.y, minimum.z),
                new Vector3(maximum.x, maximum.y, minimum.z),
                new Vector3(minimum.x, minimum.y, maximum.z),
                new Vector3(maximum.x, minimum.y, maximum.z),
                new Vector3(minimum.x, maximum.y, maximum.z),
                new Vector3(maximum.x, maximum.y, maximum.z)
            };
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            depth = float.MaxValue;
            foreach (Vector3 corner in corners)
            {
                Vector3 screen = camera.WorldToScreenPoint(corner);
                if (screen.z <= 0f) { result = new Rect(); return false; }
                minX = Mathf.Min(minX, screen.x); minY = Mathf.Min(minY, screen.y);
                maxX = Mathf.Max(maxX, screen.x); maxY = Mathf.Max(maxY, screen.y);
                depth = Mathf.Min(depth, screen.z);
            }
            result = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return result.width >= 2f && result.height >= 2f;
        }

        private int HitAxis(Camera camera, RigTarget target, Vector3 mouse)
        {
            Vector3 pivot = GizmoPivot(target);
            Vector3[] axes = GizmoAxes(target);
            float size = GizmoWorldSize(camera, pivot);
            Vector3 start = camera.WorldToScreenPoint(pivot);
            if (start.z <= 0f) return -1;
            if (editMode == EditMode.Scale && Vector2.Distance(
                new Vector2(mouse.x, mouse.y), new Vector2(start.x, start.y)) < 12f)
                return 3;
            int best = -1; float bestDistance = 12f;
            for (int axis = 0; axis < 3; axis++)
            {
                float distance;
                if (editMode == EditMode.Rotate)
                    distance = DistanceToCircle(camera, new Vector2(mouse.x, mouse.y),
                        pivot, axes[axis], size * 0.72f);
                else
                {
                    Vector3 end = camera.WorldToScreenPoint(pivot + axes[axis] * size);
                    if (end.z <= 0f || Vector2.Distance(
                        new Vector2(start.x, start.y),
                        new Vector2(end.x, end.y)) < 18f) continue;
                    distance = DistanceToSegment(new Vector2(mouse.x, mouse.y),
                        new Vector2(start.x, start.y), new Vector2(end.x, end.y));
                }
                if (distance < bestDistance) { best = axis; bestDistance = distance; }
            }
            return best;
        }

        private void BeginDrag(Camera camera, RigTarget target, Vector3 mouse)
        {
            PushUndo(editMode + " " + target.DisplayName);
            dragging = true; dragMouse = mouse; dragPivot = GizmoPivot(target);
            dragAxes = GizmoAxes(target); dragSize = GizmoWorldSize(camera, dragPivot);
            dragTargets.Clear();
            EditableRig rig = PrimaryRig();
            dragVirtualGroup = !mirrorEdit && ActiveRuntimeGroup(rig) != null;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            List<RigTarget> sources = OrderedSelectionRoots(target);
            dragPrimarySource = sources.Count > 0 ? sources[0] : target;
            foreach (RigTarget source in sources)
            {
                if (!visited.Add(source.Key)) continue;
                dragTargets.Add(new DragTargetState { Target = source,
                    Source = source, Initial = settings.GetOrCreate(source.Key,
                    source.DisplayName).Clone(),
                    InitialWorldPosition = source.Transform.position,
                    Mirrored = false });
                if (!mirrorEdit || rig == null) continue;
                RigTarget counterpart = FindCounterpart(rig, source);
                if (counterpart == null || !visited.Add(counterpart.Key)) continue;
                dragTargets.Add(new DragTargetState { Target = counterpart,
                    Source = source, Initial = settings.GetOrCreate(counterpart.Key,
                    counterpart.DisplayName).Clone(),
                    InitialWorldPosition = counterpart.Transform.position,
                    Mirrored = true });
            }
            Vector3 pivotScreen = camera.WorldToScreenPoint(dragPivot);
            Vector2 radial = new Vector2(mouse.x - pivotScreen.x,
                mouse.y - pivotScreen.y).normalized;
            dragRotationTangent = radial.sqrMagnitude > 0.1f ?
                new Vector2(-radial.y, radial.x) : Vector2.right;
            if (hotAxis >= 0 && hotAxis < 3 && Vector3.Dot(
                dragAxes[hotAxis], camera.transform.forward) < 0f)
                dragRotationTangent = -dragRotationTangent;
            dragRotationPlaneValid = editMode == EditMode.Rotate &&
                TryRotationPlaneVector(camera, mouse, out dragRotationStart);
            InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS,
                "KerbalProportionsGumball");
        }

        private void UpdateDrag(Camera camera, Vector3 mouse)
        {
            RigTarget target = SelectedTarget();
            EditableRig rig = PrimaryRig();
            if (target == null || rig == null || camera == null || hotAxis < 0) return;
            Vector3 startScreen = camera.WorldToScreenPoint(dragPivot);
            Vector3 endScreen = hotAxis < 3 ? camera.WorldToScreenPoint(
                dragPivot + dragAxes[hotAxis] * dragSize) : startScreen + Vector3.right * 100f;
            Vector2 direction = new Vector2(endScreen.x - startScreen.x,
                endScreen.y - startScreen.y);
            float pixels = Mathf.Max(direction.magnitude, 1f);
            direction /= pixels;
            Vector2 deltaMouse = new Vector2(mouse.x - dragMouse.x,
                mouse.y - dragMouse.y);
            float along = Vector2.Dot(deltaMouse, direction);
            if (editMode == EditMode.Move)
            {
                float amount = along / pixels * dragSize;
                foreach (DragTargetState state in dragTargets)
                {
                    Vector3 axis = settings.LocalSpace ?
                        (state.Source == dragPrimarySource ? dragAxes[hotAxis] :
                        state.Source.Transform.TransformDirection(UnitAxis(hotAxis))) :
                        dragAxes[hotAxis];
                    Vector3 worldDelta = axis.normalized * amount;
                    if (state.Mirrored)
                        worldDelta = ReflectVector(worldDelta, rig.Root.right);
                    Vector3 localDelta = state.Target.Transform.parent == null ?
                        worldDelta : state.Target.Transform.parent
                            .InverseTransformVector(worldDelta);
                    TransformEdit edit = settings.GetOrCreate(state.Target.Key,
                        state.Target.DisplayName);
                    edit.Position = state.Initial.Position + localDelta;
                }
            }
            else if (editMode == EditMode.Rotate)
            {
                Vector3 currentRotationVector;
                float degrees = dragRotationPlaneValid &&
                    TryRotationPlaneVector(camera, mouse,
                        out currentRotationVector) ?
                    Vector3.SignedAngle(dragRotationStart,
                        currentRotationVector, dragAxes[hotAxis]) :
                    Vector2.Dot(deltaMouse, dragRotationTangent) * 0.6f;
                foreach (DragTargetState state in dragTargets)
                {
                    Vector3 worldAxis = settings.LocalSpace ?
                        (state.Source == dragPrimarySource ? dragAxes[hotAxis] :
                        state.Source.Transform.TransformDirection(UnitAxis(hotAxis))) :
                        dragAxes[hotAxis];
                    if (state.Mirrored)
                        worldAxis = -ReflectVector(worldAxis, rig.Root.right);
                    Quaternion initial = Quaternion.Euler(state.Initial.Rotation);
                    Quaternion parentWorld = state.Target.Transform.parent == null ?
                        Quaternion.identity : state.Target.Transform.parent.rotation;
                    Quaternion baseLocal = settings.AnimationAwareRotation ?
                        state.Target.ReferenceRotation :
                        state.Target.FilteredBaseRotation;
                    Quaternion worldInParent = Quaternion.Inverse(parentWorld) *
                        Quaternion.AngleAxis(degrees, worldAxis.normalized) * parentWorld;
                    Quaternion newEdit = Quaternion.Inverse(baseLocal) * worldInParent *
                        baseLocal * initial;
                    TransformEdit edit = settings.GetOrCreate(state.Target.Key,
                        state.Target.DisplayName);
                    edit.Rotation = SignedEuler(newEdit);
                    if (dragVirtualGroup && !state.Mirrored)
                    {
                        Vector3 desired = dragPivot + Quaternion.AngleAxis(
                            degrees, dragAxes[hotAxis].normalized) *
                            (state.InitialWorldPosition - dragPivot);
                        ApplyVirtualGroupPosition(state, edit, desired);
                    }
                }
            }
            else
            {
                float factor = Mathf.Clamp(1f + along / pixels, 0.05f, 5f);
                foreach (DragTargetState state in dragTargets)
                {
                    Vector3 scale = hotAxis == 3 ? state.Initial.Scale * factor :
                        state.Initial.Scale;
                    if (hotAxis < 3)
                        scale[hotAxis] = state.Initial.Scale[hotAxis] * factor;
                    TransformEdit edit = settings.GetOrCreate(state.Target.Key,
                        state.Target.DisplayName);
                    edit.Scale = EditorSettings.ClampScale(scale);
                    if (dragVirtualGroup && !state.Mirrored)
                    {
                        Vector3 relative = state.InitialWorldPosition - dragPivot;
                        Vector3 desiredRelative;
                        if (hotAxis == 3) desiredRelative = relative * factor;
                        else
                        {
                            Vector3 groupAxis = dragAxes[hotAxis].normalized;
                            desiredRelative = relative + groupAxis *
                                Vector3.Dot(relative, groupAxis) * (factor - 1f);
                        }
                        ApplyVirtualGroupPosition(state, edit,
                            dragPivot + desiredRelative);
                    }
                }
            }
            SyncSelectedFields();
        }

        private RuntimeTargetGroup ActiveRuntimeGroup(EditableRig rig)
        {
            if (rig == null || string.IsNullOrEmpty(activeGroupId)) return null;
            return rig.Groups.Find(delegate(RuntimeTargetGroup item) {
                return item.Id == activeGroupId; });
        }

        private static void ApplyVirtualGroupPosition(DragTargetState state,
            TransformEdit edit, Vector3 desiredWorldPosition)
        {
            Vector3 worldDelta = desiredWorldPosition -
                state.InitialWorldPosition;
            Vector3 localDelta = state.Target.Transform.parent == null ?
                worldDelta : state.Target.Transform.parent
                    .InverseTransformVector(worldDelta);
            edit.Position = state.Initial.Position + localDelta;
        }

        private void EndDrag()
        {
            if (!dragging) return;
            dragging = false; hotAxis = -1;
            dragTargets.Clear();
            dragPrimarySource = null;
            dragRotationPlaneValid = false;
            dragVirtualGroup = false;
            InputLockManager.RemoveControlLock("KerbalProportionsGumball");
        }

        private bool TryRotationPlaneVector(Camera camera, Vector3 mouse,
            out Vector3 direction)
        {
            direction = Vector3.zero;
            if (camera == null || hotAxis < 0 || hotAxis >= 3 ||
                dragAxes == null || dragAxes.Length < 3) return false;
            Vector3 normal = dragAxes[hotAxis].normalized;
            Ray ray = camera.ScreenPointToRay(new Vector3(mouse.x, mouse.y, 0f));
            // An edge-on ring has no stable screen-to-plane intersection; the
            // camera-facing signed tangent fallback handles that case.
            if (Mathf.Abs(Vector3.Dot(ray.direction, normal)) < 0.025f)
                return false;
            Plane plane = new Plane(normal, dragPivot);
            float distance;
            if (!plane.Raycast(ray, out distance) || distance < 0f) return false;
            direction = ray.GetPoint(distance) - dragPivot;
            if (direction.sqrMagnitude < 0.00000001f) return false;
            direction.Normalize();
            return true;
        }

        private Vector3 GizmoPivot(RigTarget fallback)
        {
            List<RigTarget> roots = SelectedTargets(true);
            if (mirrorEdit || roots.Count == 0 || roots.Count == 1)
                return fallback.Transform.position;
            Vector3 center = Vector3.zero; int count = 0;
            foreach (RigTarget target in roots)
                if (target.Transform != null) { center += target.Transform.position; count++; }
            return count > 0 ? center / count : fallback.Transform.position;
        }

        private List<RigTarget> OrderedSelectionRoots(RigTarget active)
        {
            List<RigTarget> roots = SelectedTargets(true);
            RigTarget activeRoot = null;
            foreach (RigTarget candidate in roots)
                if (candidate == active || active != null && active.Transform != null &&
                    candidate.Transform != null &&
                    active.Transform.IsChildOf(candidate.Transform))
                { activeRoot = candidate; break; }
            if (activeRoot != null && roots.Remove(activeRoot))
                roots.Insert(0, activeRoot);
            return roots;
        }

        private Vector3[] GizmoAxes(RigTarget target)
        {
            // Unity localScale has no world/surface-axis representation without
            // introducing shear. Keep the scale handles aligned to the exact
            // X/Y/Z components they modify.
            if ((settings.LocalSpace || editMode == EditMode.Scale) &&
                target != null && target.Transform != null)
                return new [] { target.Transform.right.normalized,
                    target.Transform.up.normalized, target.Transform.forward.normalized };
            return SurfaceAxes(target);
        }

        private Vector3[] SurfaceAxes(RigTarget target)
        {
            EditableRig rig = PrimaryRig();
            Transform root = rig == null ? null : rig.Root;
            KerbalEVA eva = rig == null ? null : rig.Owner as KerbalEVA;
            Vector3 pivot = target != null && target.Transform != null ?
                target.Transform.position : (root == null ? Vector3.zero :
                root.position);
            CelestialBody body = FlightGlobals.currentMainBody;
            Vector3 up = eva != null && eva.fUp.sqrMagnitude > 0.000001f ?
                eva.fUp : (body == null || body.transform == null ?
                (root == null ? Vector3.up : root.up) :
                pivot - body.transform.position);
            if (up.sqrMagnitude < 0.000001f)
                up = root == null ? Vector3.up : root.up;
            up.Normalize();

            // KerbalEVA.fFwd is the camera-relative control direction, not the
            // direction the Kerbal model is facing. The rig root carries the
            // actual character heading, including turns made while stationary.
            Vector3 facing = root == null ?
                (target == null || target.Transform == null ? Vector3.forward :
                target.Transform.forward) : root.forward;
            Vector3 forward = Vector3.ProjectOnPlane(facing, up);
            if (forward.sqrMagnitude < 0.000001f && target != null &&
                target.Transform != null)
                forward = Vector3.ProjectOnPlane(target.Transform.forward, up);
            if (forward.sqrMagnitude < 0.000001f)
                forward = Vector3.Cross(Vector3.right, up);
            if (forward.sqrMagnitude < 0.000001f)
                forward = Vector3.Cross(Vector3.forward, up);
            forward.Normalize();
            Vector3 right = Vector3.Cross(up, forward).normalized;
            forward = Vector3.Cross(right, up).normalized;
            return new [] { right, up, forward };
        }

        private float GizmoWorldSize(Camera camera, Vector3 pivot)
        {
            return Mathf.Clamp(Vector3.Distance(camera.transform.position, pivot) *
                0.08f * settings.GizmoSize, 0.04f, 3f);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start,
            Vector2 end)
        {
            Vector2 segment = end - start;
            float length = segment.sqrMagnitude;
            if (length < 0.001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / length);
            return Vector2.Distance(point, start + segment * t);
        }

        private static float DistanceToCircle(Camera camera, Vector2 point,
            Vector3 center, Vector3 normal, float radius)
        {
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.01f)
                tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize(); Vector3 bitangent = Vector3.Cross(normal, tangent);
            float best = float.MaxValue;
            Vector2 previous = Vector2.zero;
            const int segments = 40;
            for (int index = 0; index <= segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                Vector3 world = center + (tangent * Mathf.Cos(angle) +
                    bitangent * Mathf.Sin(angle)) * radius;
                Vector3 screen = camera.WorldToScreenPoint(world);
                Vector2 current = new Vector2(screen.x, screen.y);
                if (index > 0) best = Mathf.Min(best,
                    DistanceToSegment(point, previous, current));
                previous = current;
            }
            return best;
        }

        private static Vector3 UnitAxis(int axis)
        {
            return axis == 0 ? Vector3.right :
                (axis == 1 ? Vector3.up : Vector3.forward);
        }

        private static Vector3 SignedEuler(Quaternion value)
        {
            Vector3 euler = value.eulerAngles;
            if (euler.x > 180f) euler.x -= 360f;
            if (euler.y > 180f) euler.y -= 360f;
            if (euler.z > 180f) euler.z -= 360f;
            return euler;
        }

        private void OnRenderObject()
        {
            if (!visible || !settings.Enabled || lineMaterial == null) return;
            Camera camera = Camera.current;
            Camera active = ActiveCamera();
            if (active == null || camera != active) return;
            camera = active;
            RigTarget target = SelectedTarget();
            lineMaterial.SetPass(0);
            GL.PushMatrix(); GL.Begin(GL.TRIANGLES);
            if (hoverTarget != null && hoverTarget.Transform != null)
                DrawHoverHighlight(camera, hoverRig, hoverTarget);

            foreach (RigTarget selected in SelectedTargets(false))
            {
                if (selected == target || selected.Transform == null) continue;
                float markerSize = GizmoWorldSize(camera,
                    selected.Transform.position) * 0.12f;
                GL.Color(TargetHighlightColor(selected));
                DrawBillboardDiamond(camera, selected.Transform.position,
                    markerSize, 2.5f);
            }

            if (target == null || target.Transform == null)
            {
                GL.End(); GL.PopMatrix(); return;
            }
            Vector3 pivot = GizmoPivot(target);
            Vector3[] axes = GizmoAxes(target);
            float size = GizmoWorldSize(camera, pivot);
            GL.Color(TargetHighlightColor(target));
            DrawBillboardRing(camera, pivot, size * 0.12f, 3f);
            Color[] colors = { Color.red, Color.green, new Color(0.2f, 0.55f, 1f) };
            // Draw the hot axis last so its wider yellow stroke remains legible.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    bool hot = axis == hotAxis;
                    if (hot != (pass == 1)) continue;
                    GL.Color(hot ? Color.yellow : colors[axis]);
                    float pixels = hot ? 6f : 4f;
                    if (editMode == EditMode.Rotate)
                        DrawCircle(camera, pivot, axes[axis], size * 0.72f,
                            pixels);
                    else
                    {
                        Vector3 end = pivot + axes[axis] * size;
                        DrawThickSegment(camera, pivot, end, pixels);
                        DrawArrowHead(camera,
                            end - axes[axis] * size * 0.16f, end,
                            hot ? 10f : 8f);
                    }
                }
            }
            if (editMode == EditMode.Scale)
            {
                GL.Color(hotAxis == 3 ? Color.yellow : Color.white);
                float centerSize = size * 0.055f;
                float pixels = hotAxis == 3 ? 6f : 4f;
                DrawThickSegment(camera,
                    pivot - camera.transform.right * centerSize,
                    pivot + camera.transform.right * centerSize, pixels);
                DrawThickSegment(camera,
                    pivot - camera.transform.up * centerSize,
                    pivot + camera.transform.up * centerSize, pixels);
            }
            GL.End(); GL.PopMatrix();
        }

        private static void DrawHoverHighlight(Camera camera, EditableRig rig,
            RigTarget target)
        {
            Vector3 center = target.Transform.position;
            float size = Mathf.Clamp(Vector3.Distance(camera.transform.position,
                center) * 0.012f, 0.012f, 0.4f);
            GL.Color(TargetHighlightColor(target));
            DrawBillboardDiamond(camera, center, size, 3f);
            DrawBillboardRing(camera, center, size * 1.45f, 3f);
            if (target.Category == "Bone" && rig != null)
            {
                Transform parent = target.Transform.parent;
                if (parent != null && parent != rig.Root)
                    DrawThickSegment(camera, parent.position, center, 2.5f);
                foreach (RigTarget child in rig.Targets)
                    if (child.Transform != null &&
                        child.Transform.parent == target.Transform)
                        DrawThickSegment(camera, center,
                            child.Transform.position, 2.5f);
            }
            else
            {
                foreach (BoneRendererBinding binding in target.RendererBindings)
                    if (binding.Renderer != null)
                        DrawBounds(camera, binding.Renderer.bounds, 2.5f);
            }
            foreach (ColliderBinding binding in target.ColliderBindings)
            {
                Collider active = binding.Collider;
                if (active != null && active.enabled)
                    DrawBounds(camera, active.bounds, 3f);
            }
        }

        private static Color TargetHighlightColor(RigTarget target)
        {
            if (target != null && target.ColliderBindings.Count > 0)
                return new Color(1f, 0.25f, 0.82f, 1f);
            if (target != null && target.Category == "Mesh")
                return new Color(0.1f, 0.9f, 1f, 1f);
            if (target != null && target.Category == "Bone")
                return new Color(1f, 0.72f, 0.08f, 1f);
            return Color.white;
        }

        private static void DrawBillboardDiamond(Camera camera, Vector3 center,
            float size, float pixels)
        {
            Vector3 right = camera.transform.right * size;
            Vector3 up = camera.transform.up * size;
            DrawThickSegment(camera, center + up, center + right, pixels);
            DrawThickSegment(camera, center + right, center - up, pixels);
            DrawThickSegment(camera, center - up, center - right, pixels);
            DrawThickSegment(camera, center - right, center + up, pixels);
        }

        private static void DrawBillboardRing(Camera camera, Vector3 center,
            float radius, float pixels)
        {
            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;
            const int segments = 48;
            for (int index = 0; index < segments; index++)
            {
                float a = index * Mathf.PI * 2f / segments;
                float b = (index + 1) * Mathf.PI * 2f / segments;
                DrawThickSegment(camera,
                    center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius,
                    center + (right * Mathf.Cos(b) + up * Mathf.Sin(b)) * radius,
                    pixels);
            }
        }

        private static void DrawBounds(Camera camera, Bounds bounds, float pixels)
        {
            Vector3 min = bounds.min, max = bounds.max;
            Vector3[] p = {
                new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z),
                new Vector3(max.x,max.y,min.z), new Vector3(min.x,max.y,min.z),
                new Vector3(min.x,min.y,max.z), new Vector3(max.x,min.y,max.z),
                new Vector3(max.x,max.y,max.z), new Vector3(min.x,max.y,max.z)
            };
            int[] edges = { 0,1,1,2,2,3,3,0,4,5,5,6,6,7,7,4,0,4,1,5,2,6,3,7 };
            for (int index = 0; index < edges.Length; index += 2)
                DrawThickSegment(camera, p[edges[index]], p[edges[index + 1]],
                    pixels);
        }

        private static void DrawCircle(Camera camera, Vector3 center,
            Vector3 normal, float radius, float pixels)
        {
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.01f)
                tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize(); Vector3 bitangent = Vector3.Cross(normal, tangent);
            const int segments = 56;
            for (int index = 0; index < segments; index++)
            {
                float a = index * Mathf.PI * 2f / segments;
                float b = (index + 1) * Mathf.PI * 2f / segments;
                DrawThickSegment(camera,
                    center + (tangent * Mathf.Cos(a) +
                        bitangent * Mathf.Sin(a)) * radius,
                    center + (tangent * Mathf.Cos(b) +
                        bitangent * Mathf.Sin(b)) * radius, pixels);
            }
        }

        private static void DrawThickSegment(Camera camera, Vector3 start,
            Vector3 end, float pixels)
        {
            Vector3 screenStart = camera.WorldToScreenPoint(start);
            Vector3 screenEnd = camera.WorldToScreenPoint(end);
            if (screenStart.z <= 0f || screenEnd.z <= 0f) return;
            Vector2 delta = new Vector2(screenEnd.x - screenStart.x,
                screenEnd.y - screenStart.y);
            if (delta.sqrMagnitude < 0.01f) return;
            Vector2 offset = new Vector2(-delta.y, delta.x).normalized *
                (pixels * 0.5f);
            Vector3 a = camera.ScreenToWorldPoint(new Vector3(
                screenStart.x + offset.x, screenStart.y + offset.y,
                screenStart.z));
            Vector3 b = camera.ScreenToWorldPoint(new Vector3(
                screenStart.x - offset.x, screenStart.y - offset.y,
                screenStart.z));
            Vector3 c = camera.ScreenToWorldPoint(new Vector3(
                screenEnd.x - offset.x, screenEnd.y - offset.y,
                screenEnd.z));
            Vector3 d = camera.ScreenToWorldPoint(new Vector3(
                screenEnd.x + offset.x, screenEnd.y + offset.y,
                screenEnd.z));
            GL.Vertex(a); GL.Vertex(b); GL.Vertex(c);
            GL.Vertex(a); GL.Vertex(c); GL.Vertex(d);
        }

        private static void DrawArrowHead(Camera camera, Vector3 baseCenter,
            Vector3 tip, float halfWidthPixels)
        {
            Vector3 screenBase = camera.WorldToScreenPoint(baseCenter);
            Vector3 screenTip = camera.WorldToScreenPoint(tip);
            if (screenBase.z <= 0f || screenTip.z <= 0f) return;
            Vector2 direction = new Vector2(screenTip.x - screenBase.x,
                screenTip.y - screenBase.y);
            if (direction.sqrMagnitude < 0.01f) return;
            Vector2 side = new Vector2(-direction.y, direction.x).normalized *
                halfWidthPixels;
            Vector3 left = camera.ScreenToWorldPoint(new Vector3(
                screenBase.x + side.x, screenBase.y + side.y, screenBase.z));
            Vector3 right = camera.ScreenToWorldPoint(new Vector3(
                screenBase.x - side.x, screenBase.y - side.y, screenBase.z));
            GL.Vertex(tip); GL.Vertex(left); GL.Vertex(right);
        }

        private static Camera ActiveCamera()
        {
            if (IsInternalCamera() && InternalCamera.Instance != null)
            {
                Camera internalCamera = null; float internalDepth = float.MinValue;
                foreach (Camera candidate in InternalCamera.Instance
                    .GetComponentsInChildren<Camera>(true))
                    if (candidate != null && candidate.enabled &&
                        candidate.depth > internalDepth)
                    { internalCamera = candidate; internalDepth = candidate.depth; }
                if (internalCamera == null)
                    foreach (Camera candidate in Camera.allCameras)
                        if (candidate != null && candidate.enabled &&
                            candidate.name.IndexOf("internal",
                                StringComparison.OrdinalIgnoreCase) >= 0 &&
                            candidate.depth > internalDepth)
                        { internalCamera = candidate; internalDepth = candidate.depth; }
                if (internalCamera != null) return internalCamera;
            }
            Camera main = Camera.main;
            if (main != null && main.enabled) return main;
            Camera best = null; float depth = float.MinValue;
            foreach (Camera camera in Camera.allCameras)
                if (camera != null && camera.enabled && camera.depth > depth)
                { best = camera; depth = camera.depth; }
            return best;
        }

        private void CreateLineMaterial()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return;
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
            lineMaterial.SetInt("_ZTest",
                (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        private static bool IsInternalCamera()
        {
            if (!HighLogic.LoadedSceneIsFlight) return true;
            if (InternalCamera.Instance != null && InternalCamera.Instance.isActive)
                return true;
            CameraManager manager = CameraManager.Instance;
            return manager != null &&
                (manager.currentCameraMode == CameraManager.CameraMode.IVA ||
                 manager.currentCameraMode == CameraManager.CameraMode.Internal);
        }

        private static string FriendlyName(string name)
        {
            return (name ?? "Transform").Replace("bn_", "").Replace("be_", "");
        }

        private void CreateToolbarButton()
        {
            if (toolbarButton != null ||
                KSP.UI.Screens.ApplicationLauncher.Instance == null) return;
            toolbarIcon = CreateIcon();
            toolbarButton = KSP.UI.Screens.ApplicationLauncher.Instance.AddModApplication(
                delegate { ToggleWindow(true); }, delegate { ToggleWindow(false); },
                null, null, null, null,
                KSP.UI.Screens.ApplicationLauncher.AppScenes.ALWAYS, toolbarIcon);
        }

        private void ToggleWindow(bool show)
        {
            visible = show;
            if (!show)
            {
                if (settings != null)
                {
                    CaptureWindowPositions();
                    animationSliderEditing = false;
                    portraitSliderEditing = false;
                    gizmoSliderEditing = false;
                    settings.Save();
                }
                EndDrag(); hoverTarget = null; hoverRig = null;
            }
            if (toolbarButton != null && !show) toolbarButton.SetFalse(false);
        }

        private static Texture2D CreateIcon()
        {
            const int size = 38;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color32(0, 0, 0, 0);
            DrawIconLine(pixels, size, 8, 8, 30, 8, new Color32(255, 70, 70, 255));
            DrawIconLine(pixels, size, 8, 8, 8, 30, new Color32(80, 255, 100, 255));
            DrawIconLine(pixels, size, 8, 8, 27, 27, new Color32(80, 150, 255, 255));
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        private static void DrawIconLine(Color32[] pixels, int size, int x0,
            int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                {
                    int x = x0 + ox, y = y0 + oy;
                    if (x >= 0 && x < size && y >= 0 && y < size)
                        pixels[y * size + x] = color;
                }
                if (x0 == x1 && y0 == y1) break;
                int twice = error * 2;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
            }
        }

        private void OnDestroy()
        {
            if (settings != null)
            {
                CaptureWindowPositions();
                settings.Save();
            }
            EndDrag();
            foreach (EditableRig rig in rigs) rig.Restore();
            RestorePortraitCameras();
            GameEvents.onGUIApplicationLauncherReady.Remove(CreateToolbarButton);
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPostRender -= OnCameraPostRender;
            if (toolbarButton != null &&
                KSP.UI.Screens.ApplicationLauncher.Instance != null)
                KSP.UI.Screens.ApplicationLauncher.Instance.RemoveModApplication(
                    toolbarButton);
            if (toolbarIcon != null) Destroy(toolbarIcon);
            if (lineMaterial != null) Destroy(lineMaterial);
        }
    }
}
