using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JPHACKS2026
{
    /// <summary>Existing scenes receive the same decorative pass before Area captures its reset state.</summary>
    public static class SceneryDetails
    {
        static readonly Dictionary<Color, Material> palette = new();
        static readonly Color Wood = new(0.28f, .17f, .10f), Straw = new(.56f,.43f,.24f), Stone = new(.40f,.44f,.43f);
        static Material Paint(Color color)
        {
            if (palette.TryGetValue(color, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(shader) { name = "Scenery " + ColorUtility.ToHtmlStringRGB(color), enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", .18f);
            palette[color] = m;
            return m;
        }
        static Transform Group(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false); t.localPosition = pos; return t;
        }
        static Transform Shape(Transform parent, string name, Vector3 pos, Vector3 size, Color color, PrimitiveType type = PrimitiveType.Cube, Vector3 rotation = default)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            var t = go.transform; t.SetParent(parent, false); t.localPosition = pos; t.localScale = size; t.localRotation = Quaternion.Euler(rotation);
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Object.Destroy(collider);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = Paint(color); r.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }
        static void Beam(Transform t, string name, Vector3 a, Vector3 b, float width, Color color)
        {
            var p = Shape(t, name, (a+b)*.5f, new Vector3(width, Vector3.Distance(a,b)*.5f, width), color, PrimitiveType.Cylinder);
            p.localRotation = Quaternion.FromToRotation(Vector3.up, b-a);
        }
        public static void Build(Area area)
        {
            if (area.transform.Find("SceneryDetails") != null) return;
            if (area.areaId != "village" && area.areaId != "beach" && area.areaId != "sea" && area.areaId != "ryugu") return;
            var t = Group(area.transform, "SceneryDetails", Vector3.zero);
            switch (area.areaId)
            {
                case "village": Village(area.transform, t); break;
                case "beach": Beach(area.transform, t); break;
                case "sea": Seabed(t); break;
                case "ryugu": Banquet(area.transform, t); break;
            }
            CombineScenery(t);
        }
        // Keep animated fronds and waves separate; batch stationary dressing by material.
        static void CombineScenery(Transform root)
        {
            var batches = new Dictionary<Material, List<CombineInstance>>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.GetComponentInParent<SceneryMotion>() != null || filter.GetComponentInParent<Swimmer>() != null) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                var mat = renderer.sharedMaterial;
                if (!batches.TryGetValue(mat, out var list)) batches[mat] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                renderer.enabled = false;
            }
            foreach (var pair in batches)
            {
                var mesh = new Mesh { name = "Batched scenery", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                var go = Group(root, "Batched " + pair.Key.name, Vector3.zero).gameObject;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key; renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        static void Village(Transform area, Transform t)
        {
            foreach (Transform child in area)
                if (child.name == "House" || child.name == "Roof") child.gameObject.SetActive(false);
            var positions = new[] {new Vector3(-4,0,3), new Vector3(4.2f,0,5), new Vector3(-4.5f,0,9), new Vector3(4,0,12)};
            for (int n=0;n<positions.Length;n++)
            {
                var h = Group(t,"Thatched cottage " + n,positions[n]);
                h.localRotation = Quaternion.Euler(0,n%2==0?90:-90,0);
                Shape(h,"Stone foundation",new Vector3(0,.13f,0),new Vector3(3.3f,.26f,3.2f),Stone);
                Shape(h,"Plaster walls",new Vector3(0,1.2f,0),new Vector3(3,2.05f,2.8f),new Color(.72f,.64f,.46f));
                for(int x=-1;x<=1;x++) Shape(h,"Timber post",new Vector3(x*1.42f,1.2f,-1.43f),new Vector3(.12f,2.15f,.12f),Wood);
                for(int side=-1;side<=1;side+=2)
                    Shape(h,"Pitched thatch",new Vector3(side*.85f,2.55f,0),new Vector3(2.05f,.24f,3.55f),Straw,rotation:new Vector3(0,0,-side*31));
                Shape(h,"Roof ridge",new Vector3(0,3.1f,0),new Vector3(.3f,.23f,3.65f),Wood);
                Shape(h,"Sliding door",new Vector3(-.6f,.99f,-1.425f),new Vector3(.8f,1.65f,.07f),Wood);
                for(int j=0;j<6;j++) Shape(h,"Door slat",new Vector3(-.94f+j*.135f,.99f,-1.48f),new Vector3(.035f,1.6f,.04f),Straw);
                Shape(h,"Window recess",new Vector3(.75f,1.35f,-1.44f),new Vector3(.7f,.65f,.05f),Wood);
                for(int j=0;j<4;j++) Shape(h,"Window lattice",new Vector3(.48f+j*.18f,1.35f,-1.49f),new Vector3(.035f,.65f,.04f),Straw);
                Shape(h,"Doorstep",new Vector3(-.6f,.16f,-1.8f),new Vector3(1.1f,.2f,.65f),Stone);
                for(int j=0;j<3;j++) Shape(h,"Storage jars",new Vector3(1.7f,.3f,-.5f+j*.5f),new Vector3(.38f,.6f,.38f),new Color(.45f,.26f,.17f),PrimitiveType.Sphere);
            }
            var well=Group(t,"Village well",new Vector3(-2.1f,0,6.1f));
            Shape(well,"Dark water",new Vector3(0,.3f,0),new Vector3(.9f,.05f,.9f),new Color(.09f,.22f,.23f),PrimitiveType.Cylinder);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                Shape(well,"Well masonry",new Vector3(Mathf.Cos(a)*.53f,.4f,Mathf.Sin(a)*.53f),new Vector3(.29f,.65f,.27f),Stone,rotation:new Vector3(0,-i*30,0));
            }
            for(int s=-1;s<=1;s+=2) Beam(well,"Well post",new Vector3(s*.8f,0,0),new Vector3(s*.8f,1.9f,0),.1f,Wood);
            Beam(well,"Crossbar",new Vector3(-.9f,1.8f,0),new Vector3(.9f,1.8f,0),.12f,Wood);
            Beam(well,"Bucket rope",new Vector3(0,.6f,0),new Vector3(0,1.8f,0),.018f,Straw);
            for(int i=0;i<16;i++)
            {
                float z=1+i*.88f;
                Shape(t,"Worn stepping stone",new Vector3(Mathf.Sin(i*1.8f)*.2f,.025f,z),new Vector3(.55f,.06f,.45f),new Color(.58f,.56f,.48f),PrimitiveType.Sphere);
            }
            var field=Group(t,"Vegetable garden",new Vector3(6,0,9));
            Shape(field,"Tilled soil",Vector3.zero,new Vector3(2.3f,.06f,3.1f),new Color(.25f,.18f,.11f));
            for(int i=0;i<4;i++) for(int j=0;j<5;j++)
                Shape(field,"Leafy crop",new Vector3(-.8f+i*.5f,.16f,-1.2f+j*.55f),new Vector3(.35f,.3f,.35f),new Color(.28f,.43f,.16f),PrimitiveType.Sphere);
            for(int side=-1;side<=1;side+=2) for(int i=0;i<9;i++)
            {
                var p=new Vector3(side*7,0,i*1.7f);
                Beam(t,"Fence stake",p,p+Vector3.up*.8f,.07f,Wood);
                if(i<8) Beam(t,"Fence rail",p+Vector3.up*.55f,p+new Vector3(0,.55f,1.7f),.055f,Wood);
            }
            for(int i=0;i<7;i++) Tree(t,new Vector3(i%2==0?-10:10,0,-2+i*3.3f));
        }
        static void Tree(Transform t,Vector3 p)
        {
            var g=Group(t,"Coastal pine",p);
            Beam(g,"Trunk",Vector3.zero,new Vector3(.3f,3.5f,0),.25f,Wood);
            for(int j=0;j<3;j++) Shape(g,"Pine canopy",new Vector3((j-1)*.7f,2.5f+j*.55f,0),new Vector3(2.8f,1,2.2f),new Color(.19f,.32f,.22f),PrimitiveType.Sphere);
        }
        static void Beach(Transform area,Transform t)
        {
            var shore=area.Find("Shore"); if(shore!=null) shore.gameObject.SetActive(false);
            for(int row=0;row<3;row++) for(int i=0;i<30;i++)
            {
                var p=new Vector3(-29+i*2,.045f,8.25f+row*1.4f+Mathf.Sin(i*.7f+row)*.3f);
                var wave=Shape(t,"Breaking foam",p,new Vector3(2.3f,.035f,.22f+row*.06f),new Color(.85f,.95f,.93f),PrimitiveType.Sphere);
                var motion=wave.gameObject.AddComponent<SceneryMotion>(); motion.wave=true; motion.phase=i*.25f+row; motion.amplitude=.16f;
            }
            for(int i=0;i<22;i++)
            {
                float x=(i%2==0?-1:1)*(4+i%6*1.5f), z=-5+i*.55f;
                Shape(t,"Beach pebble",new Vector3(x,.06f,z),new Vector3(.22f,.13f,.17f),Stone,PrimitiveType.Sphere);
                var grass=Group(t,"Dune grass",new Vector3(x,0,z-1));
                for(int j=0;j<3;j++) Beam(grass,"Grass blade",Vector3.zero,new Vector3((j-1)*.12f,.4f+j*.07f,.05f),.025f,new Color(.46f,.53f,.25f));
            }
            for(int i=0;i<9;i++)
            {
                Shape(t,"Sand dune",new Vector3((i%2==0?-1:1)*(12+i*.5f),-.14f,-5+i*1.6f),new Vector3(5,.75f,3),new Color(.88f,.78f,.56f),PrimitiveType.Sphere);
            }
            var boat=Group(t,"Fishing boat",new Vector3(-6.3f,.3f,-1)); boat.localRotation=Quaternion.Euler(0,-25,0);
            Shape(boat,"Hull",Vector3.zero,new Vector3(1.2f,.55f,3),Wood,PrimitiveType.Sphere);
            Shape(boat,"Interior",new Vector3(0,.23f,0),new Vector3(.88f,.12f,2.2f),new Color(.18f,.12f,.08f),PrimitiveType.Sphere);
            for(int i=-1;i<=1;i++) Shape(boat,"Bench",new Vector3(0,.3f,i*.6f),new Vector3(1,.08f,.18f),Straw);
            Beam(boat,"Oar",new Vector3(-.9f,.4f,-1),new Vector3(.7f,.5f,1.2f),.06f,Straw);
            for(int i=0;i<7;i++)
            {
                Beam(t,"Fishing net warp",new Vector3(-8+i*.22f,.08f,1.6f),new Vector3(-8+i*.22f,.08f,3.3f),.012f,Straw);
                Beam(t,"Fishing net weft",new Vector3(-8,.08f,1.6f+i*.27f),new Vector3(-6.7f,.08f,1.6f+i*.27f),.012f,Straw);
            }
            for(int i=0;i<4;i++) Tree(t,new Vector3(-13+i*8,0,-8));
            for(int i=0;i<4;i++) Shape(t,"Distant island",new Vector3(-22+i*15,-.8f,39+i%2*7),new Vector3(12,3.6f,5),new Color(.34f,.52f,.52f),PrimitiveType.Sphere);
        }
        static void Seabed(Transform t)
        {
            for(int i=0;i<24;i++)
            {
                float side=i%2==0?-1:1;
                var g=Group(t,"Reef garden",new Vector3(side*(4+i%4*1.4f),-3,-4+i*2.2f));
                Shape(g,"Reef rock",new Vector3(0,.25f,0),new Vector3(2.1f,.8f,1.5f),new Color(.30f,.43f,.43f),PrimitiveType.Sphere);
                Color c=i%3==0?new Color(.95f,.38f,.39f):i%3==1?new Color(.88f,.64f,.24f):new Color(.57f,.40f,.73f);
                for(int b=0;b<4;b++)
                {
                    var start=new Vector3((b-1.5f)*.22f,.45f,0);
                    var end=start+new Vector3(Mathf.Sin(i+b)*.35f,.65f+b*.12f,.15f);
                    Beam(g,"Coral branch",start,end,.085f,c);
                    Beam(g,"Coral fork",start+Vector3.up*.25f,end+new Vector3(.28f,.13f,.1f),.055f,c);
                    Shape(g,"Coral tip",end,new Vector3(.13f,.13f,.13f),c,PrimitiveType.Sphere);
                }
                var kelp=Group(g,"Swaying kelp",new Vector3(1,0,.4f));
                var motion=kelp.gameObject.AddComponent<SceneryMotion>(); motion.phase=i; motion.amplitude=7;
                for(int b=0;b<4;b++) Shape(kelp,"Kelp frond",new Vector3(Mathf.Sin(b)*.14f,.4f+b*.43f,0),new Vector3(.23f,.8f,.045f),new Color(.12f,.43f,.30f),PrimitiveType.Sphere, new Vector3(0,0,(b%2==0?1:-1)*18));
                Shape(t,"Sand bank",new Vector3(side*10,-3.15f,i*2.4f),new Vector3(8,.6f,4),new Color(.66f,.66f,.48f),PrimitiveType.Sphere);
                if(i%3==0) for(int f=0;f<5;f++)
                {
                    var fish=Group(t,"School fish",Vector3.zero);
                    Shape(fish,"Silver body",Vector3.zero,new Vector3(.09f,.14f,.32f),new Color(.5f,.8f,.84f),PrimitiveType.Sphere);
                    Shape(fish,"Tail",new Vector3(0,0,-.19f),new Vector3(.025f,.2f,.12f),new Color(.95f,.77f,.24f),rotation:new Vector3(45,0,0));
                    var sw=fish.gameObject.AddComponent<Swimmer>(); sw.center=new Vector3(side*4,-.5f+f*.13f,i*2.2f+f*.3f); sw.radius=1.2f+f*.12f; sw.speed=.45f;
                }
            }
        }
        static void Banquet(Transform area,Transform t)
        {
            int n=0;
            foreach(var swimmer in area.GetComponentsInChildren<Swimmer>(true))
            {
                if(swimmer.name!="Tai" && swimmer.name!="Hirame") continue;
                var root=swimmer.transform;
                root.GetComponent<Renderer>().enabled=false;
                root.localScale=Vector3.one;
                bool flat=swimmer.name=="Hirame";
                Fish(root,flat,n++);
            }
            var oct=Group(area,"DancingOctopus",new Vector3(0,1.8f,3.6f));
            var swim=oct.gameObject.AddComponent<Swimmer>(); swim.center=new Vector3(0,1.7f,3.6f); swim.radius=1.15f; swim.speed=-.6f;
            oct.gameObject.AddComponent<Resettable>();
            var red=new Color(.76f,.21f,.32f);
            Shape(oct,"Octopus mantle",new Vector3(0,.16f,0),new Vector3(.52f,.65f,.46f),red,PrimitiveType.Sphere);
            Eyes(oct,new Vector3(.13f,.19f,.2f),.075f);
            Shape(oct,"Mouth",new Vector3(0,-.02f,.24f),new Vector3(.1f,.1f,.07f),new Color(.3f,.06f,.12f),PrimitiveType.Sphere);
            for(int i=0;i<8;i++)
            {
                var arm=Group(oct,"Dancing arm",new Vector3(0,-.08f,0)); arm.localRotation=Quaternion.Euler(0,i*45,0);
                var motion=arm.gameObject.AddComponent<SceneryMotion>(); motion.phase=i*.8f; motion.amplitude=14;
                for(int j=0;j<4;j++)
                {
                    var p=new Vector3(0,-.12f-Mathf.Sin(j*.8f)*.13f,.13f+j*.11f);
                    Shape(arm,"Arm segment",p,new Vector3(.13f-j*.02f,.13f-j*.02f,.18f),red,PrimitiveType.Sphere);
                    Shape(arm,"Sucker",p+Vector3.down*.06f,new Vector3(.065f,.025f,.065f),new Color(1,.65f,.66f),PrimitiveType.Sphere);
                }
            }
            for(int i=0;i<4;i++)
            {
                var food=area.Find("Food"+i); if(food==null) continue;
                var model=Resources.Load<GameObject>("RyuguFood/Food"+i);
                if(model==null) { Debug.LogWarning("Missing Blender food model Food"+i); continue; }
                // Preserve the original root, rigidbody, collider and trigger references.
                var visual=Object.Instantiate(model,food,false); visual.name="Blender cuisine";
                visual.transform.localScale=new Vector3(1/food.localScale.x,1/food.localScale.y,1/food.localScale.z);
                foreach(var r in visual.GetComponentsInChildren<Renderer>())
                {
                    var materials=r.sharedMaterials;
                    for(int j=0;j<materials.Length;j++) if(materials[j]!=null)
                    {
                        var original=materials[j]; var c=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):original.HasProperty("_Color")?original.color:Color.white;
                        materials[j]=Paint(c);
                    }
                    r.sharedMaterials=materials;
                }
                food.GetComponent<Renderer>().enabled=false;
                var plate=Shape(t,"Porcelain serving plate",new Vector3(food.localPosition.x,.408f,food.localPosition.z),new Vector3(.29f,.018f,.25f),new Color(.78f,.88f,.89f),PrimitiveType.Cylinder);
                Shape(plate,"Gold plate rim",Vector3.zero,new Vector3(1.04f,.4f,1.04f),new Color(.74f,.57f,.25f),PrimitiveType.Cylinder);
            }
        }
        static void Fish(Transform root,bool flat,int index)
        {
            Color c=flat?new Color(.57f,.44f,.27f):new Color(.96f,.30f,.34f);
            Shape(root,"Fish body",Vector3.zero,flat?new Vector3(.45f,.12f,.57f):new Vector3(.24f,.37f,.54f),c,PrimitiveType.Sphere);
            var tail=Group(root,"Tail pivot",new Vector3(0,0,-.25f));
            Shape(tail,"Fan tail",new Vector3(0,0,-.08f),new Vector3(flat?.26f:.04f,flat?.04f:.29f,.22f),c,PrimitiveType.Sphere);
            var motion=tail.gameObject.AddComponent<SceneryMotion>(); motion.phase=index; motion.amplitude=22;
            if(flat)
            {
                for(int i=0;i<8;i++) Shape(root,"Mottled markings",new Vector3(Mathf.Sin(i*2.4f)*.14f,.058f,Mathf.Cos(i*2.4f)*.2f),new Vector3(.05f,.015f,.055f),new Color(.34f,.27f,.17f),PrimitiveType.Sphere);
                for(int s=-1;s<=1;s+=2) Shape(root,"Frilled side fin",new Vector3(s*.21f,0,0),new Vector3(.13f,.035f,.48f),new Color(.68f,.54f,.33f),PrimitiveType.Sphere);
                for(int i=0;i<2;i++) Shape(root,"Upper eye",new Vector3(-.07f+i*.12f,.09f,.17f),Vector3.one*.055f,Color.black,PrimitiveType.Sphere);
            }
            else
            {
                Eyes(root,new Vector3(.115f,.07f,.18f),.055f);
                Shape(root,"Dorsal fin",new Vector3(0,.2f,-.03f),new Vector3(.035f,.16f,.32f),new Color(1,.61f,.45f),PrimitiveType.Sphere);
                for(int s=-1;s<=1;s+=2) Shape(root,"Pectoral fin",new Vector3(s*.15f,-.03f,0),new Vector3(.18f,.035f,.16f),new Color(1,.65f,.49f),PrimitiveType.Sphere);
            }
        }
        static void Eyes(Transform root,Vector3 p,float size)
        {
            for(int s=-1;s<=1;s+=2)
            {
                var eye=Shape(root,"Eye white",new Vector3(s*p.x,p.y,p.z),Vector3.one*size,Color.white,PrimitiveType.Sphere);
                Shape(eye,"Pupil",new Vector3(0,0,.43f),new Vector3(.52f,.58f,.25f),new Color(.04f,.03f,.06f),PrimitiveType.Sphere);
            }
        }
    }
}
