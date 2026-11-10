using UnityEngine;

public class BallEnchantmentVisuals : MonoBehaviour
{
    LineRenderer arc,flame,shock;
    TrailRenderer trail;
    GameManager gm;
    public bool ArcVisible => arc && arc.enabled;
    public bool FlameVisible => flame && flame.enabled;
    public bool ShockVisible => shock && shock.enabled;
    void Ensure()
    {
        gm=GameManager.Instance;
        if(arc || !gm || !gm.Vfx) return;
        arc=gm.Vfx.MakeLine("Ball electricity",0,.08f,transform);arc.positionCount=20;
        flame=gm.Vfx.MakeLine("Ball flames",1,.08f,transform);flame.positionCount=7;
        shock=gm.Vfx.MakeLine("Ball rings",2,.055f,transform);shock.loop=true;shock.positionCount=32;
        var fire=new GameObject("Flame trail",typeof(TrailRenderer));fire.transform.SetParent(transform,false);
        trail=fire.GetComponent<TrailRenderer>();trail.sharedMaterial=gm.Vfx.ElementMaterial(1);
        trail.time=.3f;trail.startWidth=.3f;trail.endWidth=.01f;trail.minVertexDistance=.04f;
        trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    public void Refresh()
    {
        Ensure();if(!arc) return;
        arc.enabled=gm.EnabledCount(Card.Equipment.Arc)>0;
        flame.enabled=gm.EnabledCount(Card.Equipment.Spark)>0;
        shock.enabled=gm.EnabledCount(Card.Equipment.Shockwave)>0;
        bool fire=flame.enabled;
        if(trail.emitting && !fire) trail.Clear();
        trail.enabled=fire;trail.emitting=fire;
        float phase=(float)gm.CombatTime*5;
        Vector3 center=transform.position;
        for(int i=0;i<arc.positionCount;i++) {
            float a=i*Mathf.PI*2/(arc.positionCount-1)+phase;
            float radius=i%2==0?.48f:.36f;
            arc.SetPosition(i,center+gm.board.up*.3f+(gm.board.right*Mathf.Cos(a)+gm.board.forward*Mathf.Sin(a))*radius);
        }
        gm.Vfx.DrawRing(shock,center,.55f+.04f*Mathf.Sin(phase));
        for(int i=0;i<flame.positionCount;i++) flame.SetPosition(i,center+gm.board.up*.34f+gm.board.forward*(i*.13f-.1f)+gm.board.right*Mathf.Sin(i*2+phase)*(.18f*(1-i/8f)));
    }
    void LateUpdate() => Refresh();
}
