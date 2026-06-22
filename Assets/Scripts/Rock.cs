using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Destructible rock. Takes damage from melee attacks; when HP drops to 0
    /// it removes itself from the scene and spawns a tan "Droppable" prism.
    /// </summary>
    public class Rock : MonoBehaviour, IDamageable
    {
        [SerializeField] private int hp = 6;
        [SerializeField] private Color droppableColor = new Color(0.82f, 0.71f, 0.55f); // tan

        public void TakeDamage(int amount)
        {
            hp -= amount;
            Debug.Log($"[Rock] HP: {hp}");
            if (hp <= 0) Die();
        }

        private void Die()
        {
            SpawnDroppable();
            Destroy(gameObject);
        }

        private void SpawnDroppable()
        {
            var go = new GameObject("Droppable");
            // Place the prism at ground level (y=0) so its base sits on the grass.
            // The mesh has its base at local y=0, so setting transform.y=0 puts
            // it flush with the ground.
            go.transform.position = new Vector3(transform.position.x, 0f, transform.position.z);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = CreateTriangularPrismMesh();

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = droppableColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", droppableColor);
            mr.sharedMaterial = mat;

            // Trigger collider — the Paladin's CapsuleCollider walks THROUGH it
            // and fires OnTriggerEnter on the Droppable script. No Rigidbody is
            // needed because the Paladin already has one (required for triggers).
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.4f, 0f);
            col.size = new Vector3(1.1f, 0.85f, 0.6f);
            col.isTrigger = true;

            // Attach pickup behavior.
            go.AddComponent<Droppable>();

            AudioManager.Instance.Play(SfxId.DroppableSpawn);
        }

        /// <summary>
        /// Builds an equilateral triangular prism centered on (0, 0, 0) sitting
        /// on its base, height 0.8, depth 0.5.
        /// </summary>
        private static Mesh CreateTriangularPrismMesh()
        {
            const float h = 0.8f;
            const float halfDepth = 0.25f;
            float halfBase = h / Mathf.Sqrt(3f); // equilateral

            // 6 vertices: 0..2 are the -z triangle, 3..5 are the +z triangle.
            Vector3 v0 = new Vector3(0f,         h, -halfDepth);  // apex,        -z
            Vector3 v1 = new Vector3(-halfBase,  0, -halfDepth);  // bottom-left, -z
            Vector3 v2 = new Vector3( halfBase,  0, -halfDepth);  // bottom-right,-z
            Vector3 v3 = new Vector3(0f,         h,  halfDepth);  // apex,        +z
            Vector3 v4 = new Vector3(-halfBase,  0,  halfDepth);  // bottom-left, +z
            Vector3 v5 = new Vector3( halfBase,  0,  halfDepth);  // bottom-right,+z

            var mesh = new Mesh();
            mesh.vertices = new[] { v0, v1, v2, v3, v4, v5 };
            mesh.triangles = new[] {
                // -z face (outward normal -z)
                0, 2, 1,
                // +z face (outward normal +z)
                3, 4, 5,
                // left slant
                0, 1, 4,
                0, 4, 3,
                // right slant
                0, 3, 2,
                3, 5, 2,
                // bottom
                1, 2, 5,
                1, 5, 4,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.name = "TriangularPrism";
            return mesh;
        }
    }
}
