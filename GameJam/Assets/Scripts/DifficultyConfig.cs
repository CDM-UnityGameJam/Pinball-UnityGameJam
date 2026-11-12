using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Luck Number/Difficulty Config",fileName="DifficultyConfig")]
public sealed class DifficultyConfig : ScriptableObject
{
    public float baseSpawnInterval=2;
    public float baseDescentTime=40;
    public float spawnSpeedMultiplier=1;
    public float moveSpeedMultiplier=1;
    public int baseEnemyHealth=40;
    public DifficultyStage[] stages=DifficultyRules.Defaults();
    public List<string> Errors()=>DifficultyRules.Errors(baseSpawnInterval,baseDescentTime,spawnSpeedMultiplier,moveSpeedMultiplier,stages,baseEnemyHealth);
    public DifficultySample Sample(double time,double distance,double temporarySpawn=0,double temporaryMove=0)=>DifficultyRules.Evaluate(time,baseSpawnInterval,baseDescentTime,spawnSpeedMultiplier,moveSpeedMultiplier,stages,distance,temporarySpawn,temporaryMove,baseEnemyHealth);
}
