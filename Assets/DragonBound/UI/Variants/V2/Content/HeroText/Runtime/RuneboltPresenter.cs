using System;
using UnityEngine;
namespace Drakeforge.Runebolt {
    [DisallowMultipleComponent]
    public sealed class RuneboltPresenter : MonoBehaviour {
        public RuneboltConfig config;
        public Animator animator;
        public Transform castPoint,crystalCenter,combatOrigin,visualRoot;
        public event Action<bool> ReleaseCue;
        public event Action<string> PresentationCue;
        public string State {get;private set;}="Idle";
        public bool Busy=>State!="Idle";
        public float StateTime {get;private set;}
        public bool IsSkill=>State=="Overload";
        public bool externalClock;
        float rate=1;bool released;
        void Awake(){Initialize();}
        void Update(){if(!externalClock)Step(Time.deltaTime);}
        public void Initialize(){animator.Rebind();animator.speed=0;animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;ResetPose();}
        public void ResetPose(){State="Idle";StateTime=0;rate=1;released=false;ApplyPose();}
        public bool BeginAttack(float attacksPerSecond){if(Busy)return false;State="Attack";StateTime=0;released=false;rate=Mathf.Max(1,config.attackDuration*attacksPerSecond/.9f);ApplyPose();return true;}
        public bool BeginOverload(){if(Busy)return false;State="Overload";StateTime=0;released=false;rate=1;ApplyPose();return true;}
        public void Face(Vector3 target){float x=target.x-combatOrigin.position.x;if(Mathf.Abs(x)>.05f){var s=visualRoot.localScale;s.x=Mathf.Abs(s.x)*(x>=0?-1:1);visualRoot.localScale=s;}}
        public void Step(float dt){
            if(dt<=0)return;
            StateTime+=dt*rate;ApplyPose();
            if(Busy){bool skill=IsSkill;float cue=skill?config.skillRelease:config.attackRelease;
                if(!released&&StateTime>=cue){released=true;float current=StateTime;StateTime=cue;ApplyPose();ReleaseCue?.Invoke(skill);StateTime=current;ApplyPose();}
                if(StateTime>=(skill?config.skillDuration:config.attackDuration)){State="Idle";StateTime=0;rate=1;ApplyPose();}
            }
        }
        void ApplyPose(){if(!animator||!animator.runtimeAnimatorController)return;float len=State=="Idle"?config.idleDuration:IsSkill?config.skillDuration:config.attackDuration;
            animator.Play(State,0,State=="Idle"?Mathf.Repeat(StateTime/len,1):Mathf.Clamp01(StateTime/len));animator.Update(0);
        }
        // Clip marker is optional for a host using normal Animator playback. It never applies damage.
        public void OnAnimationCue(string label){PresentationCue?.Invoke(label);}
    }
}
