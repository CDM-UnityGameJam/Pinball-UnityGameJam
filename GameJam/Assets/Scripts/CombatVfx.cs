using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// World-space effects are created in the exact call that applies damage/status.
public class CombatVfx : MonoBehaviour
{
    class Pulse { public LineRenderer line; public Vector3 center; public float radius; public double start,end; public bool ring; }
    readonly List<Pulse> pulses=new List<Pulse>();
    readonly Dictionary<int,Material> materials=new Dictionary<int,Material>();
    GameManager Gm => GetComponent<GameManager>();
    public Material ElementMaterial(int element)
    {
        if(materials.TryGetValue(element,out var material)) return material;
        var source=Gm.board.GetComponentInChildren<Renderer>().sharedMaterial;
        material=new Material(source);
        Color color=element==0 ? new Color(.1f,.55f,1f) : element==1 ? new Color(1f,.25f,.015f) : new Color(.73f,.4f,1f);
        material.color=color;
        if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
        if(material.HasProperty("_EmissionColor")) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",color*3); }
        materials[element]=material;
        return material;
    }
    public LineRenderer MakeLine(string label,int element,float width,Transform parent=null)
    {
        var go=new GameObject(label,typeof(LineRenderer));
        go.transform.SetParent(parent ? parent : transform,false);
        var line=go.GetComponent<LineRenderer>();
        line.useWorldSpace=true;line.sharedMaterial=ElementMaterial(element);line.widthMultiplier=width;
        line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        line.numCapVertices=3;return line;
    }
    public void Lightning(Vector3 from,Vector3 to,bool strong)
    {
        var line=MakeLine(strong?"Thunderstorm discharge":"Contact arc",0,strong?.065f:.025f);
        line.positionCount=7;
        var side=Gm.board.right;
        for(int i=0;i<7;i++) line.SetPosition(i,Vector3.Lerp(from,to,i/6f)+Gm.board.up*.22f+side*(i==0 || i==6?0:(i%2==0?.14f:-.14f)));
        pulses.Add(new Pulse {line=line,start=Gm.CombatTime,end=Gm.CombatTime+(strong?.2:.12)});
    }
    public void Ring(Vector3 center,float radius,bool active)
    {
        var line=MakeLine(active?"Active impact":"Contact shockwave",2,active?.065f:.028f);
        line.loop=true;line.positionCount=40;
        var pulse=new Pulse {line=line,center=center,radius=radius,start=Gm.CombatTime,end=Gm.CombatTime+(active?.3:.2),ring=true};
        pulses.Add(pulse);DrawRing(line,center,radius*.25f);
    }
    public void DrawRing(LineRenderer line,Vector3 center,float radius)
    {
        for(int i=0;i<line.positionCount;i++) {
            float a=i*Mathf.PI*2/line.positionCount;
            line.SetPosition(i,center+Gm.board.up*.27f+(Gm.board.right*Mathf.Cos(a)+Gm.board.forward*Mathf.Sin(a))*radius);
        }
    }
    public void ActiveHit(Card.Equipment type,Vector3 target,CastTier tier)
    {
        if(type==Card.Equipment.Arc) Lightning(target+Gm.board.forward*(tier!=CastTier.Standard?3f:1.3f)+Gm.board.up*(tier!=CastTier.Standard?2f:.5f),target,true);
        else Ring(target,tier!=CastTier.Standard?1.6f:.9f,true);
    }
    public void Ignite(Vector3 target,bool active)
    {
        var line=MakeLine(active?"Inferno ignition":"Spark ignition",1,active?.07f:.04f);
        line.positionCount=5;
        for(int i=0;i<5;i++) line.SetPosition(i,target+Gm.board.up*(.2f+i*.12f)+Gm.board.right*(i%2==0?-.12f:.12f));
        pulses.Add(new Pulse {line=line,start=Gm.CombatTime,end=Gm.CombatTime+.22});
    }
    public void BurnTick(Vector3 target,bool active) => Ignite(target,active);
    void LateUpdate()
    {
        var gm=Gm;
        for(int i=pulses.Count-1;i>=0;i--) {
            var p=pulses[i];
            if(gm.CombatTime>=p.end) {Destroy(p.line.gameObject);pulses.RemoveAt(i);continue;}
            float t=(float)((gm.CombatTime-p.start)/(p.end-p.start));
            if(p.ring) DrawRing(p.line,p.center,p.radius*Mathf.Lerp(.25f,1f,t));
        }
    }
    void OnDestroy() {foreach(var material in materials.Values) if(material) Destroy(material);}
}
