using UnityEngine;

namespace TheRacer
{
    public enum CheckpointGateKind
    {
        Start,
        Checkpoint,
        Finish
    }

    /// <summary>Trigger gate that reports crossings to its owning checkpoint system.</summary>
    public sealed class CheckpointGate : MonoBehaviour
    {
        public int Index { get; private set; }
        public CheckpointGateKind Kind { get; private set; }
        public Transform SafeTransform { get; private set; }

        private CheckpointSystem _owner;

        public void Initialize(CheckpointSystem owner, int index, CheckpointGateKind kind,
            float width, Material material, float spawnHeight)
        {
            _owner = owner;
            Index = index;
            Kind = kind;

            BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 2f, 0f);
            trigger.size = new Vector3(width, 4f, 0.8f);

            bool arch = kind != CheckpointGateKind.Checkpoint;
            float postWidth = arch ? 0.32f : 0.12f;
            float postHeight = arch ? 4f : 2.1f;
            float postY = postHeight * 0.5f;
            CreateBeam("Left gate post", material, new Vector3(-width * 0.5f + postWidth * 0.5f, postY, 0f),
                new Vector3(postWidth, postHeight, 0.18f));
            CreateBeam("Right gate post", material, new Vector3(width * 0.5f - postWidth * 0.5f, postY, 0f),
                new Vector3(postWidth, postHeight, 0.18f));
            if (arch)
                CreateBeam("Gate crossbar", material, new Vector3(0f, postHeight - postWidth * 0.5f, 0f),
                    new Vector3(width, postWidth, 0.18f));

            GameObject safePoint = new GameObject("Safe Respawn Point");
            safePoint.transform.SetParent(transform, false);
            safePoint.transform.localPosition = new Vector3(0f, spawnHeight, 4f);
            SafeTransform = safePoint.transform;
        }

        private void CreateBeam(string beamName, Material material, Vector3 localPosition, Vector3 scale)
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = beamName;
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = localPosition;
            beam.transform.localScale = scale;
            Collider beamCollider = beam.GetComponent<Collider>();
            if (beamCollider != null)
            {
                if (Application.isPlaying) Destroy(beamCollider);
                else DestroyImmediate(beamCollider);
            }
            beam.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void OnTriggerEnter(Collider other)
        {
            CarController car = other.GetComponentInParent<CarController>();
            if (car != null && _owner != null)
                _owner.Visit(this, car);
        }
    }
}
