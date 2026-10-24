using UnityEngine;

namespace JPHACKS2026
{
    public static class InvitationBeachScene
    {
        public static void Prepare(GameManager gm)
        {
            var beach = gm.GetArea("beach");
            var scene = beach.transform.Find("Invitation fishing spot");
            if (scene == null)
            {
                scene = new GameObject("Invitation fishing spot").transform;
                scene.SetParent(beach.transform, false);
                var wood = Paint(new Color(.34f, .20f, .10f));
                var cord = Paint(new Color(.62f, .66f, .64f));
                var basket = Paint(new Color(.49f, .36f, .19f));
                Segment(scene, "Fishing rod", new Vector3(1.35f, .3f, 5.9f), new Vector3(1.65f, 1.15f, 7.1f), .018f, wood);
                Segment(scene, "Fishing line", new Vector3(1.65f, 1.15f, 7.1f), new Vector3(1.65f, .04f, 9f), .003f, cord);
                Shape(scene, "Fishing basket", new Vector3(1.2f, .17f, 5.55f), new Vector3(.33f, .16f, .33f), basket);
                Shape(scene, "Basket rim", new Vector3(1.2f, .34f, 5.55f), new Vector3(.36f, .018f, .36f), wood);
                var seat = new GameObject("Fishing viewpoint").transform;
                seat.SetParent(scene, false); seat.localPosition = new Vector3(2.3f, 0, 5.7f);
                scene.gameObject.SetActive(false);
                scene.gameObject.AddComponent<Resettable>().Capture();
            }
            scene.gameObject.SetActive(true);
            gm.rig.TeleportTo(scene.Find("Fishing viewpoint"));
            var turtle = StoryAnchor.Get<SimpleCharacter>("beach.turtle");
            turtle.lookTarget = null; turtle.lookAtPlayer = false;
            turtle.transform.position = beach.transform.TransformPoint(new Vector3(2.3f, 0, 10.8f));
            turtle.transform.rotation = beach.transform.rotation * Quaternion.Euler(0, 180, 0);
            // Change only the current lighting; the Area's rescue-day settings remain intact for replay.
            gm.rig.Head.GetComponent<Camera>().backgroundColor = new Color(.91f, .76f, .59f);
            RenderSettings.ambientLight = new Color(.63f, .59f, .52f);
            RenderSettings.fogColor = new Color(.93f, .81f, .66f);
            RenderSettings.fogDensity = .009f;
            gm.sun.color = new Color(1f, .82f, .61f);
            gm.sun.intensity = 1.1f;
            gm.sun.transform.rotation = Quaternion.Euler(18, -65, 0);
        }

        static Material Paint(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color); return material;
        }
        static Transform Shape(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Object.Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = material; return go.transform;
        }
        static void Segment(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
        {
            var segment = Shape(parent, name, (from + to) * .5f, new Vector3(radius * 2, Vector3.Distance(from, to) * .5f, radius * 2), material);
            segment.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        }
    }
}
