using UnityEngine;

/// <summary>Presentation-only decisions. Never moves a controller or changes hooks.</summary>
public sealed class ScoutAnimationBrain
{
    public struct Frame
    {
        public bool grounded,gas,charging,attacking,held,dead,flipClear;
        public int hooks;
        public float speed,vertical;
    }
    public string State { get; private set; } = "Idle";
    public float ReleaseAge { get; private set; }
    int previousHooks;
    float dualAge=100,releaseLength,landing;
    bool wasGrounded=true,singleDetachFall;
    string release;
    public string Step(Frame f,float dt)
    {
        dt=Mathf.Clamp(dt,0,.1f);
        int old=previousHooks;previousHooks=f.hooks;
        dualAge=f.hooks==3?0:dualAge+dt;
        ReleaseAge+=dt;landing=Mathf.Max(0,landing-dt);
        bool landed=f.grounded&&!wasGrounded;wasGrounded=f.grounded;
        if(f.dead) { CancelRelease();return State="Dead"; }
        if(f.held) { CancelRelease();return State="Held"; }
        if(f.charging||f.attacking) { CancelRelease();return State=(f.charging?"Charge":"Attack")+(f.grounded?"Ground":"Air"); }
        if(f.grounded)
        {
            CancelRelease();if(landed)landing=.12f;
            return State=landing>0?"Land":f.speed>1?"Run":"Idle";
        }
        if(f.hooks!=0)
        {
            CancelRelease();return State=(f.gas?"Gas":"Attached")+(f.hooks==3?"Both":f.hooks==1?"Left":"Right");
        }
        if(old!=0)
        {
            CancelRelease();ReleaseAge=0;
            bool both=old==3||dualAge<.075f;
            if(both)
            {
                release=f.flipClear&&f.speed>4?"ReleaseFlip":"ReleaseBoth";
                releaseLength=release=="ReleaseFlip"?.48f:.22f;
            }
            else singleDetachFall=true; // No left/right recoil: blend straight to Fall.
        }
        if(release=="ReleaseFlip"&&!f.flipClear)CancelRelease();
        if(release!=null&&ReleaseAge<releaseLength)return State=release;
        if(singleDetachFall)return State="Fall";
        CancelRelease();return State=f.vertical>2?"Rise":f.vertical<-2?"Fall":"AirCoast";
    }
    void CancelRelease() { release=null;releaseLength=0;singleDetachFall=false; }
}
