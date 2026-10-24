using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace TheRacer
{
    /// <summary>
    /// Scene-authored props are kept here and copied into the rolling road at runtime.
    /// Put model instances under Left Side or Right Side, beyond the wall guides.
    /// </summary>
    [ExecuteAlways]
    public sealed class RoadsideDecorationLayout : MonoBehaviour
    {
        [Min(10f)] public float roadWidth = 34f;
        [Min(30f)] public float roadLength = 1800f;
        [Min(1f)] public float tileLength = 70f;
        [Min(1f)] public float repeatSegmentLength = 190f;
        [Min(1)] public int repeatCount = 10;
        [Min(1f)] public float wallHalfWidth = 0.45f;
        [Min(0f)] public float propClearance = 1.5f;

        /// <summary>Moves each placed model vertically so its lowest renderer point meets the ground plane.</summary>
        public int AlignPlacedModelsToGround()
        {
#if UNITY_EDITOR
            int aligned = 0;
            for (int groupIndex = 0; groupIndex < transform.childCount; groupIndex++)
            {
                Transform entry = transform.GetChild(groupIndex);
                if (IsSideGroup(entry))
                    aligned += AlignChildrenToGround(entry);
                else if (HasRenderer(entry) && !IsGround(entry))
                    aligned += AlignModelToGround(entry);
            }

            if (aligned > 0)
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                SceneView.RepaintAll();
            }
            return aligned;
#else
            return 0;
#endif
        }

        /// <summary>Aligns every rendered child on one roadside only; cube props are included.</summary>
        public int AlignSideToGround(Transform side)
        {
#if UNITY_EDITOR
            if (side == null) return 0;
            int aligned = IsSideGroup(side) ? AlignChildrenToGround(side) : 0;

            // Props authored directly under the layout root (for example the long
            // backing cube) are assigned to a side by their local X position.
            float sideSign = side.name == "Left Side" ? -1f : 1f;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform entry = transform.GetChild(i);
                if (IsSideGroup(entry) || !HasRenderer(entry) || IsGround(entry)) continue;
                if (Mathf.Sign(entry.localPosition.x) == sideSign)
                    aligned += AlignModelToGround(entry);
            }

            if (aligned > 0)
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                SceneView.RepaintAll();
            }
            return aligned;
#else
            return 0;
#endif
        }

        /// <summary>Replaces left-side placements with mirrored copies of the right-side placements.</summary>
        public int MirrorRightSideToLeft()
        {
#if UNITY_EDITOR
            Transform left = transform.Find("Left Side");
            Transform right = transform.Find("Right Side");
            if (left == null || right == null) return 0;

            Undo.RegisterFullObjectHierarchyUndo(left.gameObject, "Mirror Right Side to Left");
            for (int i = left.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(left.GetChild(i).gameObject);

            int copied = 0;
            for (int i = 0; i < right.childCount; i++)
            {
                Transform source = right.GetChild(i);
                GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(source.gameObject, left);
                if (copy == null) copy = Instantiate(source.gameObject, left);
                Undo.RegisterCreatedObjectUndo(copy, "Create Mirrored Roadside Prop");
                copy.name = source.name;
                Vector3 position = source.localPosition;
                position.x = -position.x;
                copy.transform.localPosition = position;
                Vector3 angles = source.localEulerAngles;
                angles.y = -angles.y;
                angles.z = -angles.z;
                copy.transform.localEulerAngles = angles;
                copy.transform.localScale = source.localScale;
                copied++;
            }

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            SceneView.RepaintAll();
            return copied;
#else
            return 0;
#endif
        }

        /// <summary>Moves authored objects in both side groups outward from the track.</summary>
        public int MoveSidePropsOutward(float distance)
        {
#if UNITY_EDITOR
            distance = Mathf.Max(0f, distance);
            int moved = 0;
            moved += MoveChildrenOutward(transform.Find("Left Side"), -1f, distance);
            moved += MoveChildrenOutward(transform.Find("Right Side"), 1f, distance);
            if (moved > 0)
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                SceneView.RepaintAll();
            }
            return moved;
#else
            return 0;
#endif
        }

        /// <summary>Creates editable ground slabs that the rolling track repeats from this layout.</summary>
        public int CreateBuildingGroundObjects()
        {
#if UNITY_EDITOR
            int created = 0;
            created += CreateGroundObject("Left Building Ground", -1f);
            created += CreateGroundObject("Right Building Ground", 1f);
            if (created > 0)
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                SceneView.RepaintAll();
            }
            return created;
#else
            return 0;
#endif
        }

        /// <summary>Places randomized streetlight model instances into both editable roadside groups.</summary>
        public int AddRandomStreetlights()
        {
#if UNITY_EDITOR
            const string modelPath = "Assets/Models/PlaceableCityProps/KayKit/streetlight.fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError("Could not find the KayKit streetlight model at " + modelPath, this);
                return 0;
            }

            int created = AddRandomStreetlightsToSide(transform.Find("Left Side"), model, -1f)
                + AddRandomStreetlightsToSide(transform.Find("Right Side"), model, 1f);
            if (created > 0)
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                SceneView.RepaintAll();
            }
            return created;
#else
            return 0;
#endif
        }

#if UNITY_EDITOR
        private int AddRandomStreetlightsToSide(Transform sideRoot, GameObject model, float sideSign)
        {
            if (sideRoot == null) return 0;

            for (int i = sideRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = sideRoot.GetChild(i);
                if (child.name.StartsWith("Streetlight - Auto", StringComparison.Ordinal))
                    Undo.DestroyObjectImmediate(child.gameObject);
            }

            int count = UnityEngine.Random.Range(2, 4);
            float sectionLength = Mathf.Max(30f, repeatSegmentLength);
            float lateral = sideSign * UnityEngine.Random.Range(19.2f, 20.8f);
            int created = 0;
            for (int i = 0; i < count; i++)
            {
                float z = sectionLength * (i + 1f) / (count + 1f)
                    + UnityEngine.Random.Range(-9f, 9f);
                GameObject light = PrefabUtility.InstantiatePrefab(model, sideRoot) as GameObject;
                if (light == null) light = Instantiate(model, sideRoot);
                Undo.RegisterCreatedObjectUndo(light, "Add Random Streetlight");
                light.name = "Streetlight - Auto " + (i + 1);
                light.transform.localPosition = new Vector3(lateral, 0f, z);
                float yaw = sideSign < 0f ? UnityEngine.Random.Range(-5f, 5f)
                    : 180f + UnityEngine.Random.Range(-5f, 5f);
                light.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                light.transform.localScale = Vector3.one;

                Renderer[] renderers = light.GetComponentsInChildren<Renderer>(true);
                float lowestY = float.PositiveInfinity;
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    if (renderers[rendererIndex] != null)
                        lowestY = Mathf.Min(lowestY, renderers[rendererIndex].bounds.min.y);
                if (!float.IsInfinity(lowestY))
                {
                    Vector3 position = light.transform.position;
                    position.y += transform.position.y - lowestY;
                    light.transform.position = position;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
                created++;
            }
            return created;
        }

        private int CreateGroundObject(string objectName, float side)
        {
            Transform existing = transform.Find(objectName);
            bool created = false;
            if (existing == null)
            {
                GameObject existingObject = GameObject.Find(objectName);
                if (existingObject != null && existingObject.scene == gameObject.scene)
                {
                    Undo.SetTransformParent(existingObject.transform, transform, "Parent Building Ground");
                    existing = existingObject.transform;
                }
            }

            if (existing == null)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = objectName;
                Undo.RegisterCreatedObjectUndo(floor, "Create Building Ground");
                floor.transform.SetParent(transform, false);
                existing = floor.transform;
                created = true;
            }

            Undo.RecordObject(existing, "Configure Building Ground");
            existing.localPosition = new Vector3(side * 32f, -0.15f, 95f);
            existing.localScale = new Vector3(30f, 0.3f, 190f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(existing);
            EditorUtility.SetDirty(existing);
            return created ? 1 : 0;
        }

        private static int MoveChildrenOutward(Transform side, float direction, float distance)
        {
            if (side == null || distance <= 0f) return 0;
            int moved = 0;
            for (int i = 0; i < side.childCount; i++)
            {
                Transform prop = side.GetChild(i);
                if (!HasRenderer(prop)) continue;
                Undo.RecordObject(prop, "Move Roadside Props Away from Track");
                Vector3 position = prop.localPosition;
                position.x += direction * distance;
                prop.localPosition = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(prop);
                EditorUtility.SetDirty(prop);
                moved++;
            }
            return moved;
        }
#endif

#if UNITY_EDITOR
        private int AlignChildrenToGround(Transform side)
        {
            int aligned = 0;
            for (int i = 0; i < side.childCount; i++)
                if (HasRenderer(side.GetChild(i)))
                    aligned += AlignModelToGround(side.GetChild(i));
            return aligned;
        }

        private int AlignModelToGround(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            float lowestY = float.PositiveInfinity;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    lowestY = Mathf.Min(lowestY, renderers[i].bounds.min.y);
            if (float.IsInfinity(lowestY)) return 0;

            Undo.RecordObject(model, "Align Roadside Model to Ground");
            Vector3 position = model.position;
            position.y += transform.position.y - lowestY;
            model.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            EditorUtility.SetDirty(model);
            return 1;
        }

        private static bool HasRenderer(Transform item) => item.GetComponentInChildren<Renderer>(true) != null;
        private static bool IsGround(Transform item) => item.name == "Left Building Ground" || item.name == "Right Building Ground";
        private static bool IsSideGroup(Transform item) => item.name == "Left Side" || item.name == "Right Side";
#endif

        private void OnDrawGizmos()
        {
            float totalLength = repeatSegmentLength;
            float roadEdge = roadWidth * 0.5f;
            float wall = roadEdge + wallHalfWidth;
            float props = wall + propClearance;

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.15f, 0.9f, 1f, 0.7f);
            Gizmos.DrawLine(new Vector3(-roadEdge, 0.02f, 0), new Vector3(-roadEdge, 0.02f, totalLength));
            Gizmos.DrawLine(new Vector3(roadEdge, 0.02f, 0), new Vector3(roadEdge, 0.02f, totalLength));
            Gizmos.color = new Color(1f, 0.15f, 0.42f, 0.9f);
            Gizmos.DrawLine(new Vector3(-wall, 0.05f, 0), new Vector3(-wall, 0.05f, totalLength));
            Gizmos.DrawLine(new Vector3(wall, 0.05f, 0), new Vector3(wall, 0.05f, totalLength));
            Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.8f);
            Gizmos.DrawLine(new Vector3(-props, 0.04f, 0), new Vector3(-props, 0.04f, totalLength));
            Gizmos.DrawLine(new Vector3(props, 0.04f, 0), new Vector3(props, 0.04f, totalLength));
            Gizmos.DrawLine(new Vector3(-props, 0.04f, totalLength), new Vector3(props, 0.04f, totalLength));
            Gizmos.matrix = previous;
        }
    }
}
