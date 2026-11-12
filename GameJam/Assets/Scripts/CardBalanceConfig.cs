using UnityEngine;

[CreateAssetMenu(menuName="Luck Number/Card Balance Config",fileName="CardBalanceConfig")]
public sealed class CardBalanceConfig : ScriptableObject
{
    public LuckTuning settings=new LuckTuning();
    readonly LuckTuning fallback=new LuckTuning();
    bool warned;
    public LuckTuning Effective {
        get {
            if(settings!=null&&settings.Valid()){warned=false;return settings;}
            if(!warned){warned=true;Debug.LogWarning("Card balance '"+name+"' invalid; safe defaults active. "+(settings==null?"Settings missing.":string.Join(" ",settings.Errors())),this);}
            return fallback;
        }
    }
}
