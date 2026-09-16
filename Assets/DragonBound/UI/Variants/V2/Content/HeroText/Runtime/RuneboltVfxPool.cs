using System.Collections.Generic;
using UnityEngine;
namespace Drakeforge.Runebolt {
    // Runs on the caller's clock. Stop, pause and time scaling never leave effects behind.
    public sealed class RuneboltVfxPool : MonoBehaviour {
        public sealed class Effect {
            public GameObject go; public SpriteRenderer renderer; public Sprite[] frames;
            public float age,duration; public Transform follow; public bool active;
        }
        readonly List<Effect> pool=new List<Effect>();
        public Material material;
        public int ActiveCount { get {int n=0; foreach(var e in pool)if(e.active)n++;return n;} }
        public Effect Spawn(Sprite[] frames,Vector3 position,float rotation,float scale,float duration,Transform follow=null) {
            if(frames==null||frames.Length==0) return null;
            Effect e=pool.Find(x=>!x.active);
            if(e==null) {e=new Effect();e.go=new GameObject("Pooled VFX");e.go.transform.SetParent(transform,false);e.renderer=e.go.AddComponent<SpriteRenderer>();e.renderer.sortingOrder=200;e.renderer.sharedMaterial=material;pool.Add(e);}
            e.frames=frames;e.age=0;e.duration=Mathf.Max(.01f,duration);e.follow=follow;e.active=true;
            e.go.SetActive(true);e.go.transform.position=position;e.go.transform.rotation=Quaternion.Euler(0,0,rotation);e.go.transform.localScale=Vector3.one*scale;
            e.renderer.sprite=frames[0];e.renderer.color=Color.white;return e;
        }
        public void Step(float dt) {
            foreach(var e in pool) {if(!e.active)continue;e.age+=dt;if(e.age>=e.duration){Release(e);continue;}
                if(e.follow)e.go.transform.position=e.follow.position;
                float p=e.age/e.duration;e.renderer.sprite=e.frames[Mathf.Min(e.frames.Length-1,Mathf.FloorToInt(p*e.frames.Length))];
                e.renderer.color=new Color(1,1,1,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,1,p)));
            }
        }
        public void Release(Effect e) {if(e==null)return;e.active=false;e.follow=null;e.go.SetActive(false);}
        public void Clear() {foreach(var e in pool)Release(e);}
    }
}
